using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace KadaXuanwu.UtilityDesigner.Core {
    public enum BlackboardType {
        // A float.
        Number,
        // An int.
        WholeNumber,
        Boolean,
        Text,
        // Three numbers: System.Numerics.Vector3, or any struct of three floats (an engine's vector).
        Vector,
        // Anything the game gives.
        Object,
    }

    // A key a design declares: the name its nodes and the game reach a value by, and the value's type.
    public sealed class BlackboardKey {
        public string Name = "";
        public BlackboardType Type;
    }

    // An agent's named values, which its nodes and the game share: one node writes a value, a later one reads it. Only
    // the keys save with the design; every value starts as none. A key is bound once to its index (Bind), as a
    // consideration to its slot.
    public sealed class Blackboard {
        public const int NoKey = -1;
        public const string None = "none";

        private readonly BlackboardKey[] _keys;
        // Null: none.
        private readonly object[] _values;

        internal Blackboard(List<BlackboardKey> keys) {
            _keys = keys.ToArray();
            _values = new object[_keys.Length];
        }

        public int Count => _keys.Length;

        // The declared keys, in their order.
        public BlackboardKey Key(int key) => _keys[key];

        // The index of the key of that name, looked up once; NoKey when the design does not declare it.
        public int Bind(string name) {
            for (int i = 0; i < _keys.Length; i++) {
                if (_keys[i].Name == name) {
                    return i;
                }
            }
            return NoKey;
        }

        public bool Has(int key) => key >= 0 && _values[key] != null;

        // Null when the key has no value or is not there.
        public object Get(int key) => key >= 0 ? _values[key] : null;

        // The fallback when the key has no value, is not there, or holds another type.
        public T Get<T>(int key, T fallback = default) => Get(key) is T value ? value : fallback;

        public T Get<T>(string name, T fallback = default) => Get(Bind(name), fallback);

        // Null makes the value none again. False when the key is not there or the value is not of its type.
        public bool Set(int key, object value) {
            if (key < 0 || value != null && !Fits(_keys[key].Type, value)) {
                return false;
            }
            _values[key] = value;
            return true;
        }

        public bool Set(string name, object value) => Set(Bind(name), value);

        // The value as the window shows it: "none", a number, true or false, the text, (x, y, z), or the object's own
        // text.
        public string Text(int key) {
            object value = Get(key);
            CultureInfo plain = CultureInfo.InvariantCulture;
            switch (value) {
                case null:
                    return None;
                case float number:
                    return number.ToString("0.###", plain);
                case int whole:
                    return whole.ToString(plain);
                case bool flag:
                    return flag ? "true" : "false";
                case string text:
                    return text;
                default:
                    if (_keys[key].Type != BlackboardType.Vector) {
                        return value.ToString();
                    }
                    List<string> numbers = new List<string>();
                    foreach (FieldInfo field in NodeTypes.StructFields(value.GetType())) {
                        numbers.Add(((float)field.GetValue(value)).ToString("0.###", plain));
                    }
                    return $"({string.Join(", ", numbers)})";
            }
        }

        private static bool Fits(BlackboardType type, object value) {
            switch (type) {
                case BlackboardType.Number:
                    return value is float;
                case BlackboardType.WholeNumber:
                    return value is int;
                case BlackboardType.Boolean:
                    return value is bool;
                case BlackboardType.Text:
                    return value is string;
                case BlackboardType.Vector:
                    return IsThreeFloats(value.GetType());
                default:
                    return true;
            }
        }

        private static bool IsThreeFloats(Type of) {
            if (!of.IsValueType || of.IsPrimitive || of.IsEnum) {
                return false;
            }
            FieldInfo[] fields = NodeTypes.StructFields(of);
            return fields.Length == 3 && Array.TrueForAll(fields, field => field.FieldType == typeof(float));
        }
    }
}
