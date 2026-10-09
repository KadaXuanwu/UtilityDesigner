using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KadaXuanwu.UtilityDesigner.Core {
    // Just enough JSON for designs and their layers, with no dependency: objects (Dictionary<string, object>, keys in
    // the order written), arrays (List<object>), strings, numbers (double), true, false and null.
    public static class Json {
        public static object Parse(string text) {
            int at = 0;
            object value = Value(text, ref at);
            Space(text, ref at);
            if (at != text.Length) {
                throw Error(text, at, "more after the value");
            }
            return value;
        }

        public static string Write(object value) {
            StringBuilder builder = new StringBuilder();
            Write(builder, value, 0);
            return builder.ToString();
        }

        public static string Quote(string text) {
            StringBuilder builder = new StringBuilder(text.Length + 2).Append('"');
            foreach (char c in text) {
                switch (c) {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < ' ') {
                            builder.Append("\\u").Append(((int)c).ToString("x4"));
                        } else {
                            builder.Append(c);
                        }
                        break;
                }
            }
            return builder.Append('"').ToString();
        }

        private static void Write(StringBuilder builder, object value, int depth) {
            switch (value) {
                case null:
                    builder.Append("null");
                    break;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    break;
                case string text:
                    builder.Append(Quote(text));
                    break;
                case Dictionary<string, object> map: {
                    builder.Append('{');
                    bool first = true;
                    foreach (KeyValuePair<string, object> pair in map) {
                        builder.Append(first ? "" : ",").Append('\n').Append(' ', 2 * (depth + 1));
                        builder.Append(Quote(pair.Key)).Append(": ");
                        Write(builder, pair.Value, depth + 1);
                        first = false;
                    }
                    if (!first) {
                        builder.Append('\n').Append(' ', 2 * depth);
                    }
                    builder.Append('}');
                    break;
                }
                case List<object> list: {
                    builder.Append('[');
                    for (int i = 0; i < list.Count; i++) {
                        builder.Append(i > 0 ? "," : "").Append('\n').Append(' ', 2 * (depth + 1));
                        Write(builder, list[i], depth + 1);
                    }
                    if (list.Count > 0) {
                        builder.Append('\n').Append(' ', 2 * depth);
                    }
                    builder.Append(']');
                    break;
                }
                default:
                    builder.Append(Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static object Value(string text, ref int at) {
            Space(text, ref at);
            if (at >= text.Length) {
                throw Error(text, at, "a value");
            }
            char c = text[at];
            if (c == '{') {
                Dictionary<string, object> map = new Dictionary<string, object>();
                at++;
                Space(text, ref at);
                if (Take(text, ref at, '}')) {
                    return map;
                }
                do {
                    Space(text, ref at);
                    string key = String(text, ref at);
                    Space(text, ref at);
                    Expect(text, ref at, ':');
                    map[key] = Value(text, ref at);
                    Space(text, ref at);
                } while (Take(text, ref at, ','));
                Expect(text, ref at, '}');
                return map;
            }
            if (c == '[') {
                List<object> list = new List<object>();
                at++;
                Space(text, ref at);
                if (Take(text, ref at, ']')) {
                    return list;
                }
                do {
                    list.Add(Value(text, ref at));
                    Space(text, ref at);
                } while (Take(text, ref at, ','));
                Expect(text, ref at, ']');
                return list;
            }
            if (c == '"') {
                return String(text, ref at);
            }
            if (Word(text, ref at, "true")) {
                return true;
            }
            if (Word(text, ref at, "false")) {
                return false;
            }
            if (Word(text, ref at, "null")) {
                return null;
            }
            int start = at;
            while (at < text.Length && "+-0123456789.eE".IndexOf(text[at]) >= 0) {
                at++;
            }
            if (start == at || !double.TryParse(text.Substring(start, at - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) {
                throw Error(text, start, "a value");
            }
            return number;
        }

        private static string String(string text, ref int at) {
            Expect(text, ref at, '"');
            StringBuilder builder = new StringBuilder();
            while (at < text.Length && text[at] != '"') {
                char c = text[at++];
                if (c != '\\') {
                    builder.Append(c);
                    continue;
                }
                char escaped = text[at++];
                switch (escaped) {
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'u':
                        builder.Append((char)Convert.ToInt32(text.Substring(at, 4), 16));
                        at += 4;
                        break;
                    default:
                        builder.Append(escaped);
                        break;
                }
            }
            Expect(text, ref at, '"');
            return builder.ToString();
        }

        private static void Space(string text, ref int at) {
            while (at < text.Length && char.IsWhiteSpace(text[at])) {
                at++;
            }
        }

        private static bool Take(string text, ref int at, char c) {
            if (at < text.Length && text[at] == c) {
                at++;
                return true;
            }
            return false;
        }

        private static void Expect(string text, ref int at, char c) {
            if (!Take(text, ref at, c)) {
                throw Error(text, at, $"'{c}'");
            }
        }

        private static bool Word(string text, ref int at, string word) {
            if (string.CompareOrdinal(text, at, word, 0, word.Length) != 0) {
                return false;
            }
            at += word.Length;
            return true;
        }

        private static FormatException Error(string text, int at, string wanted) {
            int line = 1;
            for (int i = 0; i < at && i < text.Length; i++) {
                if (text[i] == '\n') {
                    line++;
                }
            }
            return new FormatException($"JSON: {wanted} expected on line {line}.");
        }
    }
}
