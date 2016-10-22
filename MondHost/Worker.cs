using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mond;
using Mond.Libraries;
using MondBot;

namespace MondHost
{
    class Worker
    {
        const int MaxOutputChars = 1024;
        const int MaxOutputLines = 20;

        private readonly StringBuilder _outputBuffer;
        private Dictionary<string, MondValue> _methodCache;

        public Worker()
        {
            _outputBuffer = new StringBuilder(MaxOutputChars);
        }

        public string Run(string source)
        {
            _outputBuffer.Clear();

            var output = new LimitedTextWriter(new StringWriter(_outputBuffer), MaxOutputChars, MaxOutputLines);

            try
            {
                var state = new MondState
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
                        new JsonLibraries(),
                        new HttpLibraries(),
                    }
                };

                state.Libraries.Configure(libs =>
                {
                    var consoleOut = libs.Get<ConsoleOutputLibrary>();
                    consoleOut.Out = output;
                });

                _methodCache = new Dictionary<string, MondValue>();

                var methodGetter = new MondValue(MethodGetter);
                var methodSetter = new MondValue(MethodSetter);

                state["__ops"]["__get"] = methodGetter;
                state["__ops"]["__set"] = methodSetter;
                state["__get"] = methodGetter;
                state["__set"] = methodSetter;

                var program = Decode(source);

                GC.Collect();

                var result = state.Run(program, "mondbox");

                if (result != MondValue.Undefined)
                {
                    output.WriteLine();
                    output.WriteLine("=================");

                    if (result["moveNext"])
                    {
                        output.WriteLine("sequence (100 max):");
                        foreach (var i in result.Enumerate(state).Take(100))
                        {
                            output.WriteLine(i.Serialize());
                        }
                    }
                    else
                    {
                        output.WriteLine(result.Serialize());
                    }
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

            return Encode(_outputBuffer.ToString());
        }

        private MondValue MethodGetter(MondState state, params MondValue[] args)
        {
            if (args.Length != 2)
                throw new MondRuntimeException("MethodsObject.__get: requires 2 parameters");

            var name = (string)args[1];

            MondValue method;
            if (_methodCache.TryGetValue(name, out method))
                return method;

            var code = LoadMethod(name);
            if (code == null)
                throw new MondRuntimeException($"Undefined method '{name}'");

            method = state.Run(LoadMethod(name), name + ".mnd");
            _methodCache.Add(name, method);

            return method;
        }

        private MondValue MethodSetter(MondState state, params MondValue[] args)
        {
            if (args.Length != 3)
                throw new MondRuntimeException("MethodsObject.__set: requires 3 parameters");

            var name = (string)args[1];
            var method = args[2];

            _methodCache[name] = method;
            return method;
        }

        private static string LoadMethod(string name)
        {
            var cmd = new SqlCommand(@"SELECT * FROM mondbot.methods WHERE name = :name;")
            {
                ["name"] = name
            };

            using (cmd)
            {
                var result = cmd.Execute().Result.SingleOrDefault();
                if (result == null)
                    return null;

                return "return " + result.code + ";";
            }
        }

        private static string Encode(string input)
        {
            var sb = new StringBuilder(input.Length);

            foreach (var ch in input)
            {
                switch (ch)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }

        private static string Decode(string input)
        {
            var sb = new StringBuilder(4096);

            for (var i = 0; i < input.Length; i++)
            {
                var ch = input[i];

                if (ch != '\\')
                {
                    sb.Append(ch);
                    continue;
                }

                if (++i >= input.Length)
                    return sb.ToString(); // unexpected eof

                switch (input[i])
                {
                    case '\\':
                        sb.Append('\\');
                        break;

                    case 'r':
                        sb.Append('\r');
                        break;

                    case 'n':
                        sb.Append('\n');
                        break;

                    default:
                        throw new NotSupportedException("Decode: \\" + input[i]);
                }
            }

            return sb.ToString();
        }
    }
}
