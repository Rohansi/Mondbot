using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mond;
using Mond.Libraries;
using MondBot;
using Npgsql;

namespace MondHost
{
    class Worker
    {
        const int MaxOutputChars = 1024;
        const int MaxOutputLines = 20;

        private readonly StringBuilder _outputBuffer;
        
        private NpgsqlConnection _connection; 
        private NpgsqlTransaction _transaction;

        private MondState _state;
        private Dictionary<string, MondValue> _variableCache;
        private MondValue _username;

        public Worker()
        {
            _outputBuffer = new StringBuilder(MaxOutputChars);
        }

        public RunResult Run(string username, string source)
        {
            _outputBuffer.Clear();

            var output = new LimitedTextWriter(new StringWriter(_outputBuffer), MaxOutputChars, MaxOutputLines);

            try
            {
                using (_connection = Database.CreateConnection())
                {
                    _transaction = _connection.BeginTransaction();

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
                        }
                    };

                    _state.Libraries.Configure(libs =>
                    {
                        var consoleOut = libs.Get<ConsoleOutputLibrary>();
                        consoleOut.Out = output;
                    });

                    _state.EnsureLibrariesLoaded();

                    _variableCache = new Dictionary<string, MondValue>();

                    var variableGetter = new MondValue(VariableGetter);
                    var variableSetter = new MondValue(VariableSetter);

                    _state["__ops"]["__get"] = variableGetter;
                    _state["__ops"]["__set"] = variableSetter;
                    _state["__get"] = variableGetter;
                    _state["__set"] = variableSetter;

                    var program = source;

                    GC.Collect();

                    var result = _state.Run(program, "mondbox");

                    if (result != MondValue.Undefined)
                    {
                        output.WriteLine();
                        output.WriteLine("=================");

                        if (result["moveNext"])
                        {
                            output.WriteLine("sequence (100 max):");
                            foreach (var i in result.Enumerate(_state).Take(100))
                            {
                                output.WriteLine(i.Serialize());
                            }
                        }
                        else
                        {
                            output.WriteLine(result.Serialize());
                        }
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

            var name = (string)args[1];

            if (name == "username")
                return _username;

            MondValue value;
            if (_variableCache.TryGetValue(name, out value))
                return value;

            value = LoadVariable(name);
            if (value == null)
                throw new MondRuntimeException($"Undefined variable '{name}'");

            _variableCache.Add(name, value);
            return value;
        }

        private MondValue VariableSetter(MondState state, params MondValue[] args)
        {
            if (args.Length != 3)
                throw new MondRuntimeException("VariableObject.__set: requires 3 parameters");

            var name = (string)args[1];
            if (name == "username")
                return _username;
            
            var value = args[2];

            StoreVariable(name, value);
            _variableCache[name] = value;
            return value;
        }

        private MondValue LoadVariable(string name)
        {
            VariableType type;
            string data;

            var cmd = new SqlCommand(_connection, _transaction, @"SELECT type, data FROM mondbot.variables WHERE name = :name FOR UPDATE;")
            {
                ["name"] = name
            };

            using (cmd)
            {
                var result = cmd.Execute().Result.SingleOrDefault();
                if (result == null)
                    return null;

                type = (VariableType)(short)result.type;
                data = (string)result.data;
            }

            switch (type)
            {
                case VariableType.Serialized:
                    return _state.Call(_state["Json"]["deserialize"], data);
                
                case VariableType.Method:
                    var code = "return " + data + ";";
                    return _state.Run(code, name + ".mnd");

                default:
                    throw new MondRuntimeException($"Unhandled VariableType {type}");
            }
        }

        private void StoreVariable(string name, MondValue value)
        {
            var data = (string)_state.Call(_state["Json"]["serialize"], value);

            var cmd = new SqlCommand(_connection, _transaction, @"INSERT INTO mondbot.variables (name, type, data) VALUES (:name, :type, :data)
                                                                  ON CONFLICT (name) DO UPDATE SET type = :type, data = :data;")
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
