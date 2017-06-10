﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mond;
using Mond.Libraries;
using MondBot;
using Npgsql;
using MondHost.Libraries;

namespace MondHost
{
    class Worker
    {
        const int MaxVariableNameSize = 512;
        const int MaxVariableContentSize = 10 * 1024;

        const int MaxOutputChars = 1024;
        const int MaxOutputLines = 20;

        private readonly StringBuilder _outputBuffer;
        
        private NpgsqlConnection _connection; 
        private NpgsqlTransaction _transaction;

        private MondState _state;
        private Dictionary<string, CacheEntry> _variableCache;
        private HashSet<string> _loadingVariables;

        private MondValue _service;
        private MondValue _userid;
        private MondValue _username;

        public Worker()
        {
            _outputBuffer = new StringBuilder(MaxOutputChars);
        }

        public RunResult Run(string service, string userid, string username, string source)
        {
            _outputBuffer.Clear();

            var output = new LimitedTextWriter(new StringWriter(_outputBuffer), MaxOutputChars, MaxOutputLines);

            try
            {
                using (_connection = Database.CreateConnection())
                {
                    _transaction = _connection.BeginTransaction();

                    _service = service;
                    _userid = userid;
                    _username = username;

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
                            new RantLibraries(),
                            new RegexLibraries(),
                            new DateTimeLibraries(),
                        }
                    };

                    _state.Libraries.Configure(libs =>
                    {
                        var consoleOut = libs.Get<ConsoleOutputLibrary>();
                        consoleOut.Out = output;
                    });

                    _state.EnsureLibrariesLoaded();

                    var global = _state.Run("return global;");
                    global.Prototype = MondValue.Null;

                    _variableCache = new Dictionary<string, CacheEntry>();
                    _loadingVariables = new HashSet<string>();

                    var variableGetter = new MondValue(VariableGetter);
                    var variableSetter = new MondValue(VariableSetter);

                    _state["__ops"]["__get"] = variableGetter;
                    _state["__get"] = variableGetter;
                    _state["__set"] = variableSetter;

                    var program = source;

                    GC.Collect();

                    var result = _state.Run(program, "mondbox");

                    if (result != MondValue.Undefined)
                    {
                        output.WriteLine();

                        if (result["moveNext"])
                        {
                            output.WriteLine("sequence (20 max):");
                            foreach (var i in result.Enumerate(_state).Take(20))
                            {
                                output.WriteLine(i.Serialize());
                            }
                        }
                        else
                        {
                            output.WriteLine(result.Serialize());
                        }
                    }

                    var comparer = new MondValueComparer(_state);
                    foreach (var kv in _variableCache)
                    {
                        var entry = kv.Value;
                        if (entry.IsMethod)
                            continue;

                        var same = comparer.Equals(entry.Original, entry.Current);
                        Console.WriteLine("Variable {0} same: {1}", kv.Key, same);
                        if (same)
                            continue;

                        StoreVariable(kv.Key, entry.Current);
                    }

                    _transaction.Commit();
                }
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (MondRuntimeException e)
            {
                Console.WriteLine(e);

                if (e.InnerException != null)
                    Console.WriteLine(e.InnerException);

                output.WriteLine(e);
            }
            catch (MondCompilerException e)
            {
                Console.WriteLine(e);
                output.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                output.WriteLine(e.Message);
            }

            return new RunResult(_outputBuffer.ToString(), ImageModule.GetImageData());
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

            if (name == "service")
                value = _service;
            else if (name == "userid")
                value = _userid;
            else if (name == "username")
                value =  _username;

            return value != null;
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
    }
}
