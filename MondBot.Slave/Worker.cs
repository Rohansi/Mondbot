using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mond;
using Mond.Libraries;
using MondBot.Shared;
using Npgsql;
using MondBot.Slave.Libraries;

namespace MondBot.Slave
{
    class Worker
    {
        const int MaxVariableNameSize = 512;
        const int MaxVariableContentSize = 10 * 1024;

        const int MaxOutputChars = 5 * 1024;
        const int MaxOutputLines = 1000;

        private readonly StringBuilder _outputBuffer;
        
        private NpgsqlConnection _connection; 
        private NpgsqlTransaction _transaction;

        private MondState _state;
        private Dictionary<string, CacheEntry> _variableCache;
        private HashSet<string> _loadingVariables;

        public Worker()
        {
            _outputBuffer = new StringBuilder(MaxOutputChars);
        }

        public RunResult Run(string source)
        {
            _outputBuffer.Clear();

            var output = new LimitedTextWriter(new StringWriter(_outputBuffer), MaxOutputChars, MaxOutputLines);

            try
            {
                using (_connection = Database.CreateConnection())
                {
                    _transaction = _connection.BeginTransaction();

                    _state = new MondState
                    {
                        Options = new MondCompilerOptions
                        {
                            DebugInfo = MondDebugInfoLevel.StackTrace,
                            UseImplicitGlobals = true,
                        },
                        Libraries = new MondLibraryManager
                        {
                            new ModifiedCoreLibraries(),
                            new ConsoleOutputLibraries(),
                            new ModifiedJsonLibraries(),
                            new HttpLibraries(),
                            new ImageLibraries(),
                            new RegexLibraries(),
                            new DateTimeLibraries(),
                            new AsyncLibraries(),
                        }
                    };

                    // eagerly initialize module cache (so it doesn't try to load from DB)
                    var moduleCache = MondValue.Object(_state);
                    moduleCache.Prototype = MondValue.Null;
                    _state["__modules"] = moduleCache;

                    var searchDir = Path.Combine(Environment.CurrentDirectory, "Modules");

                    var requireWhitelist = new HashSet<string>
                    {
                        "Seq.mnd",
                        "Seq.Scalar.mnd",
                        "Seq.Sorting.mnd",
                    };

                    _state.Libraries.Configure(libs =>
                    {
                        var require = libs.Get<RequireLibrary>();
                        if (require != null)
                        {
                            require.Options = _state.Options;
                            require.SearchBesideScript = false;
                            require.Loader = (name, directories) =>
                            {
                                string foundModule = null;

                                if (requireWhitelist.Contains(name))
                                {
                                    var modulePath = Path.Combine(searchDir, name);
                                    if (File.Exists(modulePath))
                                        foundModule = modulePath;
                                }

                                if (foundModule == null)
                                    throw new MondRuntimeException("require: module could not be found: {0}", name);
                                
                                return File.ReadAllText(foundModule);
                            };
                        }

                        var consoleOut = libs.Get<ConsoleOutputLibrary>();
                        consoleOut.Out = output;
                    });

                    _state.EnsureLibrariesLoaded();

                    var global = _state.Run("return global;");
                    global.Prototype = MondValue.Null;

                    _variableCache = new Dictionary<string, CacheEntry>();
                    _loadingVariables = new HashSet<string>();

                    _state["__ops"] = MondValue.Object(_state);
                    _state["__ops"]["__get"] = MondValue.Function(VariableGetterOldOperator);

                    _state["__get"] = MondValue.Function(VariableGetter);
                    _state["__set"] = MondValue.Function(VariableSetter);

                    var program = source;

                    GC.Collect();

                    var result = _state.Run(program, "mondbox");

                    if (result != MondValue.Undefined)
                    {
                        output.WriteLine();

                        if (result["moveNext"])
                        {
                            output.WriteLine("sequence (15 max):");
                            foreach (var i in result.Enumerate(_state).Take(15))
                            {
                                output.WriteLine(i.Serialize());
                            }
                        }
                        else
                        {
                            output.WriteLine(result.Serialize());
                        }
                    }

                    SaveChanges(output);

                    _transaction.Commit();
                }
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                output.WriteLine(e.Message);
            }

            return new RunResult(_outputBuffer.ToString(), ImageModule.GetImageData());
        }

        private void SaveChanges(TextWriter output)
        {
            var comparer = new MondValueComparer(_state);
            foreach (var kv in _variableCache)
            {
                var entry = kv.Value;
                if (entry.IsMethod)
                    continue;

                var serialized = true;
                var current = entry.Current;

                try
                {
                    // if the original was serialized we need to serialize current and compare with that
                    // if current can't serialize, they can never match
                    if (entry.Serialized)
                    {
                        if (!MondUtil.TrySerialize(_state, current, out var currentSerialized))
                            serialized = false;
                        else
                            current = currentSerialized;
                    }

                    var same = serialized && comparer.Equals(entry.Original, current);
                    Console.WriteLine("Variable {0} same: {1}", kv.Key, same);
                    if (same)
                        continue;

                    StoreVariable(kv.Key, entry.Current);
                }
                catch
                {
                    output.WriteLine("Error saving '{0}': ", kv.Key);
                    throw;
                }
            }
        }

        private MondValue VariableGetter(MondState state, params MondValue[] args)
        {
            if (args.Length != 2)
                throw new MondRuntimeException("VariableObject.__get: requires 2 parameters");

            if (args[1].Type != MondValueType.String)
                throw new MondRuntimeException("VariableObject.__get: variable name must be a string");

            var name = (string)args[1];

            if (TryGetBuiltin(name, out var builtinValue))
                return builtinValue;
            
            if (_variableCache.TryGetValue(name, out var entry))
                return entry.Current;

            if (!_loadingVariables.Add(name))
                throw new MondRuntimeException($"Variable '{name}' could not finish loading due to a circular dependency");

            try
            {
                var (value, isMethod) = LoadVariable(name);
                if (value == null)
                    throw new MondRuntimeException($"Undefined variable '{name}'");

                _variableCache.Add(name, new CacheEntry(_state, isMethod, value, value));
                return value;
            }
            finally
            {
                _loadingVariables.Remove(name);
            }
        }

        private MondValue VariableSetter(MondState state, params MondValue[] args)
        {
            if (args.Length != 3)
                throw new MondRuntimeException("VariableObject.__set: requires 3 parameters");

            if (args[1].Type != MondValueType.String)
                throw new MondRuntimeException("VariableObject.__set: variable name must be a string");

            var name = (string)args[1];

            if (TryGetBuiltin(name, out var value))
                return value;
            
            value = args[2];
            
            if (_variableCache.TryGetValue(name, out var entry))
                return entry.Current = value;

            _variableCache.Add(name, new CacheEntry(_state, false, null, value));
            return value;
        }

        private bool TryGetBuiltin(string name, out MondValue value)
        {
            value = null;

            return false;
        }

        private (MondValue value, bool isMethod) LoadVariable(string name)
        {
            VariableType type;
            string data;
            int version;

            var cmd = new SqlCommand(_connection, _transaction, @"SELECT type, data, version FROM mondbot.variables WHERE name = :name FOR UPDATE;")
            {
                ["name"] = name
            };

            using (cmd)
            {
                var result = cmd.Execute().Result.SingleOrDefault();
                if (result == null)
                    return (null, false);

                type = (VariableType)(short)result.type;
                data = (string)result.data;
                version = (int)result.version;
            }

            switch (type)
            {
                case VariableType.Serialized:
                    return (JsonModule.Deserialize(_state, data), false);
                
                case VariableType.Method:
                    if (version == 1)
                    {
                        var code = "return " + data + ";";
                        return (_state.Run(code, name + ".mnd"), true);
                    }
                    else if (version == 2)
                    {
                        var code = data;

                        if (char.IsLetterOrDigit(name[0]) || name[0] == '_')
                            code += $"\n;return {name};";
                        else
                            code += $"\n;return global.__ops[\"{name}\"];";

                        return (_state.Run(code, name + ".mnd"), true);
                    }
                    else
                    {
                        throw new NotSupportedException("Method Version");
                    }

                default:
                    throw new MondRuntimeException($"Unhandled VariableType {type}");
            }
        }

        private void StoreVariable(string name, MondValue value)
        {
            if (name.Length > MaxVariableNameSize)
                throw new MondRuntimeException($"Variable name '{name}' is too long");

            var data = JsonModule.Serialize(_state, value);

            if (data.Length > MaxVariableContentSize)
                throw new MondRuntimeException($"Variable '{name}' exceeds maximum size");

            var cmd = new SqlCommand(_connection, _transaction, @"INSERT INTO mondbot.variables (name, type, data, version) VALUES (:name, :type, :data, 2)
                                                                  ON CONFLICT (name) DO UPDATE SET type = :type, data = :data, version = 2;")
            {
                ["name"] = name,
                ["type"] = (int)VariableType.Serialized,
                ["data"] = data
            };

            using (cmd)
            {
                cmd.ExecuteNonQuery().Wait();
            }
        }

        private MondValue VariableGetterOldOperator(MondState state, params MondValue[] args)
        {
            var op = (string)args[1];

            if (!Util.TryConvertOperatorName(op, out var name))
                throw new MondRuntimeException($"Invalid operator '{op}'");

            args[1] = name;
            return VariableGetter(state, args);
        }
    }
}
