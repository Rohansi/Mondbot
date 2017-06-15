using System.Collections.Generic;
using System.Text;
using Mond;
using Mond.Binding;
using Mond.Libraries;

namespace MondBot.Slave
{
    class ModifiedJsonLibraries : IMondLibraryCollection
    {
        public IEnumerable<IMondLibrary> Create(MondState state)
        {
            yield return new ModifiedJsonLibrary(state);
        }
    }

    class ModifiedJsonLibrary : IMondLibrary
    {
        private readonly MondState _state;

        public ModifiedJsonLibrary(MondState state) => _state = state;

        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions()
        {
            var jsonModule = MondModuleBinder.Bind(typeof(JsonModule), _state);
            yield return new KeyValuePair<string, MondValue>("Json", jsonModule);
        }
    }

    [MondModule("Json")]
    static partial class JsonModule
    {
        [MondFunction("serialize")]
        public static string Serialize(MondState state, MondValue value)
        {
            var sb = new StringBuilder();

            SerializeImpl(state, value, sb, 0);

            return sb.ToString();
        }

        private static void SerializeImpl(MondState state, MondValue value, StringBuilder sb, int depth)
        {
            if (depth >= 32)
                throw new MondRuntimeException("Json.serialize: maximum depth exceeded");

            if (sb.Length >= 1 * 1024 * 1024)
                throw new MondRuntimeException("Json.serialize: maxiumum size exceeded");

            var first = true;

            switch (value.Type)
            {
                case MondValueType.True:
                    sb.Append("true");
                    break;

                case MondValueType.False:
                    sb.Append("false");
                    break;

                case MondValueType.Null:
                    sb.Append("null");
                    break;

                case MondValueType.Undefined:
                    sb.Append("undefined");
                    break;

                case MondValueType.Number:
                    sb.Append((double)value);
                    break;

                case MondValueType.String:
                    SerializeString(value, sb);
                    break;

                case MondValueType.Object:
                    var serializeMethod = value["__serialize"];
                    if (serializeMethod)
                    {
                        value = state.Call(serializeMethod, value);

                        if (value.Type != MondValueType.Object)
                        {
                            SerializeImpl(state, value, sb, depth + 1);
                            return;
                        }
                    }

                    sb.Append('{');

                    foreach (var kvp in value.Object)
                    {
                        if (kvp.Value == MondValue.Undefined)
                            continue;

                        if (first)
                            first = false;
                        else
                            sb.Append(',');

                        SerializeImpl(state, kvp.Key, sb, depth + 1);

                        sb.Append(':');

                        SerializeImpl(state, kvp.Value, sb, depth + 1);
                    }

                    sb.Append('}');
                    break;

                case MondValueType.Array:
                    sb.Append('[');

                    foreach (var v in value.Array)
                    {
                        if (first)
                            first = false;
                        else
                            sb.Append(',');

                        SerializeImpl(state, v, sb, depth + 1);
                    }

                    sb.Append(']');
                    break;

                default:
                    throw new MondRuntimeException("Json.serialize: can't serialize {0}s", value.Type.GetName());
            }
        }

        private static void SerializeString(string value, StringBuilder sb)
        {
            sb.Append('"');

            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;

                    case '\"':
                        sb.Append("\\\"");
                        break;

                    case '\b':
                        sb.Append("\\b");
                        break;

                    case '\f':
                        sb.Append("\\f");
                        break;

                    case '\n':
                        sb.Append("\\n");
                        break;

                    case '\r':
                        sb.Append("\\r");
                        break;

                    case '\t':
                        sb.Append("\\t");
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }
    }
}
