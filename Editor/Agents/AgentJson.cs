using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>
    /// Minimal tolerant JSON reader used to decode agent CLI stream events.
    /// </summary>
    /// <remarks>
    /// <c>JsonUtility</c> cannot represent the shapes these CLIs emit: an event's
    /// content is a heterogeneous array (text blocks mixed with tool calls) and a
    /// tool input is a free-form object with no fixed schema, so there is no set of
    /// concrete [Serializable] classes that models it. We parse into a loose tree
    /// and read only the fields we understand instead. Unknown shapes must never
    /// throw: a CLI adding a field or a new event type cannot be allowed to break
    /// the console.
    /// </remarks>
    internal static class AgentJson
    {
        /// <summary>Parses one JSON document. Returns null when the text is not valid JSON.</summary>
        public static object Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                int index = 0;
                return ReadValue(text, ref index);
            }
            catch (Exception)
            {
                // A truncated or malformed line is expected while tailing a live file.
                return null;
            }
        }

        // --- Typed accessors -------------------------------------------------

        public static object Field(object node, string key)
        {
            var map = node as Dictionary<string, object>;
            if (map == null || key == null)
                return null;
            return map.TryGetValue(key, out object value) ? value : null;
        }

        public static string String(object node, string key)
        {
            return AsString(Field(node, key));
        }

        public static string AsString(object value)
        {
            if (value == null)
                return null;
            if (value is string text)
                return text;
            if (value is bool flag)
                return flag ? "true" : "false";
            if (value is double number)
                return number.ToString("0.######", CultureInfo.InvariantCulture);
            return null;
        }

        public static bool Bool(object node, string key, bool fallback = false)
        {
            object value = Field(node, key);
            return value is bool flag ? flag : fallback;
        }

        public static double Number(object node, string key, double fallback = 0d)
        {
            object value = Field(node, key);
            return value is double number ? number : fallback;
        }

        public static List<object> List(object node, string key)
        {
            return Field(node, key) as List<object>;
        }

        // --- Reader ----------------------------------------------------------

        private static object ReadValue(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length)
                throw new FormatException("unexpected end of json");

            switch (text[index])
            {
                case '{': return ReadObject(text, ref index);
                case '[': return ReadArray(text, ref index);
                case '"': return ReadString(text, ref index);
                case 't': Expect(text, ref index, "true"); return true;
                case 'f': Expect(text, ref index, "false"); return false;
                case 'n': Expect(text, ref index, "null"); return null;
                default: return ReadNumber(text, ref index);
            }
        }

        private static Dictionary<string, object> ReadObject(string text, ref int index)
        {
            var map = new Dictionary<string, object>();
            index++;
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == '}')
            {
                index++;
                return map;
            }

            while (index < text.Length)
            {
                SkipWhitespace(text, ref index);
                string key = ReadString(text, ref index);
                SkipWhitespace(text, ref index);

                if (index >= text.Length || text[index] != ':')
                    throw new FormatException("expected colon");
                index++;

                map[key] = ReadValue(text, ref index);
                SkipWhitespace(text, ref index);

                if (index >= text.Length)
                    throw new FormatException("unterminated object");

                if (text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (text[index] == '}')
                {
                    index++;
                    return map;
                }

                throw new FormatException("expected comma or closing brace");
            }

            throw new FormatException("unterminated object");
        }

        private static List<object> ReadArray(string text, ref int index)
        {
            var list = new List<object>();
            index++;
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == ']')
            {
                index++;
                return list;
            }

            while (index < text.Length)
            {
                list.Add(ReadValue(text, ref index));
                SkipWhitespace(text, ref index);

                if (index >= text.Length)
                    throw new FormatException("unterminated array");

                if (text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (text[index] == ']')
                {
                    index++;
                    return list;
                }

                throw new FormatException("expected comma or closing bracket");
            }

            throw new FormatException("unterminated array");
        }

        private static string ReadString(string text, ref int index)
        {
            if (index >= text.Length || text[index] != '"')
                throw new FormatException("expected string");

            index++;
            var builder = new StringBuilder();

            while (index < text.Length)
            {
                char current = text[index++];

                if (current == '"')
                    return builder.ToString();

                if (current != '\\')
                {
                    builder.Append(current);
                    continue;
                }

                if (index >= text.Length)
                    throw new FormatException("unterminated escape");

                char escape = text[index++];
                switch (escape)
                {
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length)
                            throw new FormatException("bad unicode escape");
                        builder.Append((char)Convert.ToInt32(text.Substring(index, 4), 16));
                        index += 4;
                        break;
                    default:
                        // Covers the quote, backslash and slash escapes plus anything unknown.
                        builder.Append(escape);
                        break;
                }
            }

            throw new FormatException("unterminated string");
        }

        private static object ReadNumber(string text, ref int index)
        {
            int start = index;
            while (index < text.Length && "+-.eE0123456789".IndexOf(text[index]) >= 0)
                index++;

            string slice = text.Substring(start, index - start);
            if (double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return value;

            throw new FormatException("bad number");
        }

        private static void Expect(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length ||
                string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("expected literal");
            }

            index += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }
    }
}
