using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace KadaXuanwu.UtilityDesigner.Core {
    // A tunable number of a design: a stable id (its path by names), its range and scale, and how to read and write it.
    // An optimizer sees a design as the vector of these (Designs.Vector), each mapped to 0..1. The ranges are starting
    // brackets: narrow Min and Max to the design before optimizing.
    public sealed class Parameter {
        public string Id;
        public float Min;
        public float Max;
        // Moved on a log scale; Min must be above 0.
        public bool Log;
        public Func<float> Get;
        public Action<float> Set;

        public float Normalized {
            get {
                float value = Math.Clamp(Get(), Min, Max);
                return Log ? MathF.Log(value / Min) / MathF.Log(Max / Min) : (value - Min) / (Max - Min);
            }
            set {
                float t = Math.Clamp(value, 0f, 1f);
                Set(Log ? Min * MathF.Pow(Max / Min, t) : Min + (Max - Min) * t);
            }
        }
    }

    // The node types a design can name. v1's engine-free nodes are known; the game registers its own before loading.
    public static class NodeTypes {
        private static readonly Dictionary<string, Func<Node>> Makers = new Dictionary<string, Func<Node>>();
        private static readonly Dictionary<Type, string> Names = new Dictionary<Type, string>();
        // Filled while designs load, which several threads may do at once (Makers and Names change only at registration).
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> Settings = new ConcurrentDictionary<Type, FieldInfo[]>();

        static NodeTypes() {
            Register<Sequence>();
            Register<Selector>();
            Register<Parallel>();
            Register<RandomSelector>();
            Register<RandomSequence>();
            Register<Invert>();
            Register<Repeat>();
            Register<Success>();
            Register<Failure>();
            Register<WaitFixed>();
            Register<WaitRandom>();
            Register<LockState>();
            Register<LockStateDuration>();
            Register<LockStateConsideration>();
            Register<UnlockState>();
            Register<ChangeStateSet>();
            Register<SetConsideration>();
            Register<ChangeConsideration>();
            Register<CompareConsideration>();
            Register<TimeCheck>();
        }

        public static void Register<T>(string name = null) where T : Node, new() {
            name = name ?? typeof(T).Name;
            Makers[name] = () => new T();
            Names[typeof(T)] = name;
        }

        // For the types found by reflection (the Unity layer registers every node type it finds).
        public static void Register(Type type, string name = null) {
            name = name ?? type.Name;
            Makers[name] = () => (Node)Activator.CreateInstance(type);
            Names[type] = name;
        }

        public static Node Create(string name) {
            if (!Makers.TryGetValue(name, out Func<Node> make)) {
                throw new FormatException($"A node of type \"{name}\", which is not registered (NodeTypes.Register).");
            }
            return make();
        }

        public static string NameOf(Node node) {
            if (!Names.TryGetValue(node.GetType(), out string name)) {
                throw new ArgumentException($"{node.GetType().Name} is not registered (NodeTypes.Register).");
            }
            return name;
        }

        // The node type's settings: its own public fields of a number, a boolean, text, an enum, or a struct of numbers
        // (an engine's vector or colour).
        public static FieldInfo[] SettingsOf(Node node) => Settings.GetOrAdd(node.GetType(), FindSettings);

        // Empties the cache, so a test sees threads fill it at once.
        internal static void ForgetSettings() => Settings.Clear();

        private static FieldInfo[] FindSettings(Type type) {
            List<FieldInfo> found = new List<FieldInfo>();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
                Type declared = field.DeclaringType;
                Type of = field.FieldType;
                bool saved = IsNumber(of) || of == typeof(string) || of.IsEnum || IsNumbers(of);
                if (saved && !field.IsInitOnly && declared != typeof(Node) && declared != typeof(CompositeNode) && declared != typeof(DecoratorNode)) {
                    found.Add(field);
                }
            }
            return found.ToArray();
        }

        private static bool IsNumber(Type of) => of == typeof(float) || of == typeof(double) || of == typeof(int) || of == typeof(bool);

        // A struct whose fields, private ones too, are all numbers or such structs.
        internal static bool IsNumbers(Type of) {
            if (!of.IsValueType || of.IsPrimitive || of.IsEnum) {
                return false;
            }
            FieldInfo[] fields = StructFields(of);
            foreach (FieldInfo field in fields) {
                if (!IsNumber(field.FieldType) && !IsNumbers(field.FieldType)) {
                    return false;
                }
            }
            return fields.Length > 0;
        }

        internal static FieldInfo[] StructFields(Type of) => of.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    // Designs as versioned JSON, their tunable numbers, and override layers: a small JSON of changed ids laid over a base
    // design (a difficulty, a personality, an optimizer's result), in order.
    public static class Designs {
        public static string ToJson(Design design) {
            List<object> considerationSets = new List<object>();
            foreach (ConsiderationSet set in design.ConsiderationSets) {
                List<object> considerations = new List<object>();
                foreach (Consideration consideration in set.Considerations) {
                    Dictionary<string, object> c = new Dictionary<string, object> { ["name"] = consideration.Name, ["initial"] = consideration.InitialValue };
                    if (consideration.SetMinValue) {
                        c["min"] = consideration.MinValue;
                    }
                    if (consideration.SetMaxValue) {
                        c["max"] = consideration.MaxValue;
                    }
                    c["changePerSecond"] = consideration.ChangePerSecond;
                    considerations.Add(c);
                }
                considerationSets.Add(new Dictionary<string, object> { ["name"] = set.Name, ["local"] = set.Local, ["considerations"] = considerations });
            }
            List<object> stateSets = new List<object>();
            foreach (StateSet set in design.StateSets) {
                List<object> states = new List<object>();
                foreach (State state in set.States) {
                    states.Add(StateToJson(state));
                }
                stateSets.Add(new Dictionary<string, object> { ["name"] = set.Name, ["states"] = states });
            }
            Dictionary<string, object> root = new Dictionary<string, object> { ["format"] = Design.FormatVersion, ["name"] = design.Name };
            // Written only when on, so a design without them saves as before.
            if (design.ChooseAgainWhenTreeEnds) {
                root["chooseAgainWhenTreeEnds"] = true;
            }
            if (design.InstantChildren) {
                root["instantChildren"] = true;
            }
            root["considerationSets"] = considerationSets;
            root["stateSets"] = stateSets;
            return Json.Write(root);
        }

        public static Design FromJson(string text) {
            Dictionary<string, object> root = (Dictionary<string, object>)Json.Parse(text);
            int format = (int)Number(root, "format", 1);
            if (format != Design.FormatVersion) {
                throw new FormatException($"A design of format {format}; this Utility Designer reads format {Design.FormatVersion}.");
            }
            Design design = new Design {
                Name = Text(root, "name"), ChooseAgainWhenTreeEnds = Flag(root, "chooseAgainWhenTreeEnds", false),
                InstantChildren = Flag(root, "instantChildren", false),
            };
            foreach (object item in List(root, "considerationSets")) {
                Dictionary<string, object> map = (Dictionary<string, object>)item;
                ConsiderationSet set = new ConsiderationSet { Name = Text(map, "name"), Local = Flag(map, "local", false) };
                foreach (object entry in List(map, "considerations")) {
                    Dictionary<string, object> c = (Dictionary<string, object>)entry;
                    set.Considerations.Add(new Consideration {
                        Name = Text(c, "name"), InitialValue = Number(c, "initial", 0f), SetMinValue = c.ContainsKey("min"), MinValue = Number(c, "min", 0f),
                        SetMaxValue = c.ContainsKey("max"), MaxValue = Number(c, "max", 0f), ChangePerSecond = Number(c, "changePerSecond", 0f),
                    });
                }
                design.ConsiderationSets.Add(set);
            }
            foreach (object item in List(root, "stateSets")) {
                Dictionary<string, object> map = (Dictionary<string, object>)item;
                StateSet set = new StateSet { Name = Text(map, "name") };
                foreach (object entry in List(map, "states")) {
                    set.States.Add(StateFromJson((Dictionary<string, object>)entry));
                }
                design.StateSets.Add(set);
            }
            return design;
        }

        // The same design again, for another agent.
        public static Design Copy(Design design) => FromJson(ToJson(design));

        // Every tunable number of the design, by id.
        public static List<Parameter> Parameters(Design design) {
            List<Parameter> all = new List<Parameter>();
            foreach (ConsiderationSet set in design.ConsiderationSets) {
                foreach (Consideration consideration in set.Considerations) {
                    string c = $"considerations.{set.Name}.{consideration.Name}";
                    all.Add(Number($"{c}.initial", -100f, 100f, () => consideration.InitialValue, v => consideration.InitialValue = v));
                    if (consideration.SetMinValue) {
                        all.Add(Number($"{c}.min", -100f, 100f, () => consideration.MinValue, v => consideration.MinValue = v));
                    }
                    if (consideration.SetMaxValue) {
                        all.Add(Number($"{c}.max", -100f, 100f, () => consideration.MaxValue, v => consideration.MaxValue = v));
                    }
                    all.Add(Number($"{c}.changePerSecond", -10f, 10f, () => consideration.ChangePerSecond, v => consideration.ChangePerSecond = v));
                }
            }
            foreach (StateSet set in design.StateSets) {
                foreach (State state in set.States) {
                    string s = $"states.{set.Name}.{state.Name}";
                    all.Add(Number($"{s}.weight", 0f, 10f, () => state.Weight, v => state.Weight = v));
                    all.Add(Number($"{s}.executionFactor", 0f, 5f, () => state.ExecutionFactor, v => state.ExecutionFactor = v));
                    all.Add(Number($"{s}.baseScore", 0f, 1f, () => state.BaseScore, v => state.BaseScore = v));
                    if (state.SetMinScore) {
                        all.Add(Number($"{s}.minScore", 0f, 10f, () => state.MinScore, v => state.MinScore = v));
                    }
                    if (state.SetMaxScore) {
                        all.Add(Number($"{s}.maxScore", 0f, 10f, () => state.MaxScore, v => state.MaxScore = v));
                    }
                    all.Add(Number($"{s}.failChance", 0f, 1f, () => state.FailChance, v => state.FailChance = v));
                    foreach (Precondition precondition in state.Preconditions) {
                        all.Add(Number($"{s}.{precondition.Name}.value", -100f, 100f, () => precondition.Value, v => precondition.Value = v));
                    }
                    foreach (Evaluator evaluator in state.Evaluators) {
                        string e = $"{s}.{evaluator.Name}";
                        all.Add(Number($"{e}.weight", 0f, 1f, () => evaluator.Weight, v => evaluator.Weight = v));
                        all.Add(Number($"{e}.min", -100f, 100f, () => evaluator.Min, v => evaluator.Min = v));
                        all.Add(Number($"{e}.max", -100f, 100f, () => evaluator.Max, v => evaluator.Max = v));
                        Curve curve = evaluator.Curve;
                        if (curve.Shape == CurveShape.Keys) {
                            continue;
                        }
                        all.Add(Number($"{e}.m", -20f, 20f, () => curve.M, v => curve.M = v));
                        all.Add(Number($"{e}.k", -10f, 10f, () => curve.K, v => curve.K = v));
                        all.Add(Number($"{e}.b", -1f, 1f, () => curve.B, v => curve.B = v));
                        all.Add(Number($"{e}.c", -1f, 2f, () => curve.C, v => curve.C = v));
                    }
                    if (state.Tree != null) {
                        NodeParameters(state.Tree, $"{s}.tree", "0", all);
                    }
                }
            }
            return all;
        }

        // The tunable numbers as 0..1 each, in Parameters' order, and back.
        public static float[] Vector(List<Parameter> parameters) {
            float[] vector = new float[parameters.Count];
            for (int i = 0; i < vector.Length; i++) {
                vector[i] = parameters[i].Normalized;
            }
            return vector;
        }

        public static void Apply(List<Parameter> parameters, float[] vector) {
            for (int i = 0; i < vector.Length; i++) {
                parameters[i].Normalized = vector[i];
            }
        }

        // A layer: {"id": value, ...} laid over the design; an id it does not have fails loudly, and so does a design
        // with two numbers under one id (two evaluators of one name in a state): a layer could only reach the last.
        public static void Layer(Design design, string layer) {
            Dictionary<string, Parameter> byId = new Dictionary<string, Parameter>();
            foreach (Parameter parameter in Parameters(design)) {
                if (!byId.TryAdd(parameter.Id, parameter)) {
                    throw new ArgumentException($"{design.Name} has two numbers named {parameter.Id}; a layer needs every name once.");
                }
            }
            foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)Json.Parse(layer)) {
                if (!byId.TryGetValue(pair.Key, out Parameter parameter)) {
                    throw new ArgumentException($"{design.Name} has no number {pair.Key} for a layer to set.");
                }
                parameter.Set((float)(double)pair.Value);
            }
        }

        // The layer that turns from into to: every number that differs.
        public static string Difference(Design from, Design to) {
            List<Parameter> before = Parameters(from);
            List<Parameter> after = Parameters(to);
            Dictionary<string, object> layer = new Dictionary<string, object>();
            for (int i = 0; i < before.Count && i < after.Count; i++) {
                if (before[i].Id == after[i].Id && before[i].Get() != after[i].Get()) {
                    layer[after[i].Id] = (double)after[i].Get();
                }
            }
            return Json.Write(layer);
        }

        // A node's float settings, under its Id, or its place in the tree when it has none.
        private static void NodeParameters(Node node, string tree, string place, List<Parameter> all) {
            string n = $"{tree}.{(node.Id.Length > 0 ? node.Id : place)}";
            foreach (FieldInfo field in NodeTypes.SettingsOf(node)) {
                if (field.FieldType == typeof(float)) {
                    all.Add(Number($"{n}.{Key(field)}", -100f, 100f, () => (float)field.GetValue(node), v => field.SetValue(node, v)));
                }
            }
            for (int i = 0; i < node.Children.Count; i++) {
                NodeParameters(node.Children[i], tree, $"{place}.{i}", all);
            }
        }

        private static Parameter Number(string id, float min, float max, Func<float> get, Action<float> set) =>
            new Parameter { Id = id, Min = min, Max = max, Get = get, Set = set };

        private static Dictionary<string, object> StateToJson(State state) {
            List<object> preconditions = new List<object>();
            foreach (Precondition precondition in state.Preconditions) {
                preconditions.Add(new Dictionary<string, object> {
                    ["name"] = precondition.Name, ["consideration"] = precondition.Consideration,
                    ["comparator"] = precondition.Comparator.ToString(), ["value"] = precondition.Value,
                });
            }
            List<object> evaluators = new List<object>();
            foreach (Evaluator evaluator in state.Evaluators) {
                evaluators.Add(new Dictionary<string, object> {
                    ["name"] = evaluator.Name, ["consideration"] = evaluator.Consideration, ["weight"] = evaluator.Weight,
                    ["min"] = evaluator.Min, ["max"] = evaluator.Max, ["curve"] = CurveToJson(evaluator.Curve),
                });
            }
            Dictionary<string, object> map = new Dictionary<string, object> {
                ["name"] = state.Name, ["active"] = state.Active, ["weight"] = state.Weight,
                ["executionFactor"] = state.ExecutionFactor, ["baseScore"] = state.BaseScore,
            };
            if (state.SetMinScore) {
                map["minScore"] = state.MinScore;
            }
            if (state.SetMaxScore) {
                map["maxScore"] = state.MaxScore;
            }
            map["failChance"] = state.FailChance;
            map["notes"] = state.Notes;
            map["preconditions"] = preconditions;
            map["evaluators"] = evaluators;
            if (state.Tree != null) {
                map["tree"] = NodeToJson(state.Tree);
            }
            return map;
        }

        private static State StateFromJson(Dictionary<string, object> map) {
            State state = new State {
                Name = Text(map, "name"), Active = Flag(map, "active", true), Weight = Number(map, "weight", 1f),
                ExecutionFactor = Number(map, "executionFactor", 1f), BaseScore = Number(map, "baseScore", 0f),
                SetMinScore = map.ContainsKey("minScore"), MinScore = Number(map, "minScore", 0f),
                SetMaxScore = map.ContainsKey("maxScore"), MaxScore = Number(map, "maxScore", 0f),
                FailChance = Number(map, "failChance", 0f), Notes = Text(map, "notes"),
            };
            foreach (object item in List(map, "preconditions")) {
                Dictionary<string, object> p = (Dictionary<string, object>)item;
                state.Preconditions.Add(new Precondition {
                    Name = Text(p, "name"), Consideration = Text(p, "consideration"),
                    Comparator = (Comparator)Enum.Parse(typeof(Comparator), Text(p, "comparator")), Value = Number(p, "value", 0f),
                });
            }
            foreach (object item in List(map, "evaluators")) {
                Dictionary<string, object> e = (Dictionary<string, object>)item;
                state.Evaluators.Add(new Evaluator {
                    Name = Text(e, "name"), Consideration = Text(e, "consideration"), Weight = Number(e, "weight", 0f),
                    Min = Number(e, "min", 0f), Max = Number(e, "max", 1f),
                    Curve = e.TryGetValue("curve", out object curve) ? CurveFromJson((Dictionary<string, object>)curve) : Curve.Through(),
                });
            }
            if (map.TryGetValue("tree", out object tree) && tree != null) {
                state.Tree = NodeFromJson((Dictionary<string, object>)tree);
            }
            return state;
        }

        private static Dictionary<string, object> NodeToJson(Node node) {
            Dictionary<string, object> map = new Dictionary<string, object> { ["type"] = NodeTypes.NameOf(node) };
            if (node.Id.Length > 0) {
                map["id"] = node.Id;
            }
            if (node.Notes.Length > 0) {
                map["notes"] = node.Notes;
            }
            if (node.X != 0f || node.Y != 0f) {
                map["x"] = node.X;
                map["y"] = node.Y;
            }
            foreach (FieldInfo field in NodeTypes.SettingsOf(node)) {
                map[Key(field)] = Saved(field.GetValue(node), field.FieldType);
            }
            if (node is DecoratorNode decorator) {
                if (decorator.Child != null) {
                    map["child"] = NodeToJson(decorator.Child);
                }
            } else if (node is CompositeNode composite) {
                List<object> children = new List<object>();
                foreach (Node child in composite.Nodes) {
                    children.Add(NodeToJson(child));
                }
                map["children"] = children;
            }
            return map;
        }

        private static Node NodeFromJson(Dictionary<string, object> map) {
            Node node = NodeTypes.Create(Text(map, "type"));
            node.Id = Text(map, "id");
            node.Notes = Text(map, "notes");
            node.X = Number(map, "x", 0f);
            node.Y = Number(map, "y", 0f);
            foreach (FieldInfo field in NodeTypes.SettingsOf(node)) {
                if (!map.TryGetValue(Key(field), out object value) || value == null) {
                    continue;
                }
                field.SetValue(node, Loaded(value, field.FieldType, field.GetValue(node)));
            }
            if (node is DecoratorNode decorator) {
                if (map.TryGetValue("child", out object child) && child != null) {
                    decorator.Child = NodeFromJson((Dictionary<string, object>)child);
                }
            } else if (node is CompositeNode composite) {
                foreach (object child in List(map, "children")) {
                    composite.Nodes.Add(NodeFromJson((Dictionary<string, object>)child));
                }
            }
            return node;
        }

        // A setting as JSON has it: an enum by name, a struct of numbers as an object of its fields.
        private static object Saved(object value, Type of) {
            if (of.IsEnum) {
                return value.ToString();
            }
            if (!NodeTypes.IsNumbers(of)) {
                return value;
            }
            Dictionary<string, object> map = new Dictionary<string, object>();
            foreach (FieldInfo field in NodeTypes.StructFields(of)) {
                map[field.Name] = Saved(field.GetValue(value), field.FieldType);
            }
            return map;
        }

        private static object Loaded(object json, Type of, object current) {
            if (of.IsEnum) {
                return Enum.Parse(of, (string)json);
            }
            if (json is double number) {
                return Convert.ChangeType(number, of);
            }
            if (json is Dictionary<string, object> map && NodeTypes.IsNumbers(of)) {
                foreach (FieldInfo field in NodeTypes.StructFields(of)) {
                    if (map.TryGetValue(field.Name, out object value) && value != null) {
                        field.SetValue(current, Loaded(value, field.FieldType, field.GetValue(current)));
                    }
                }
                return current;
            }
            return json;
        }

        // A setting's key: its field's name, the first letter small.
        private static string Key(FieldInfo field) => char.ToLowerInvariant(field.Name[0]) + field.Name.Substring(1);

        private static Dictionary<string, object> CurveToJson(Curve curve) {
            Dictionary<string, object> map = new Dictionary<string, object> { ["shape"] = curve.Shape.ToString() };
            if (curve.Shape == CurveShape.Keys) {
                List<object> keys = new List<object>();
                foreach (CurveKey key in curve.Keys) {
                    keys.Add(new List<object> { key.Time, key.Value, Tangent(key.InTangent), Tangent(key.OutTangent) });
                }
                map["keys"] = keys;
            } else {
                map["m"] = curve.M;
                map["k"] = curve.K;
                map["b"] = curve.B;
                map["c"] = curve.C;
            }
            return map;
        }

        // JSON has no infinity: a stepped key's tangent is written as a number too large for a float, which reads back
        // as infinity.
        private static object Tangent(float tangent) => float.IsInfinity(tangent) ? (tangent > 0f ? 1e39 : -1e39) : (object)tangent;

        private static Curve CurveFromJson(Dictionary<string, object> map) {
            Curve curve = new Curve {
                Shape = (CurveShape)Enum.Parse(typeof(CurveShape), Text(map, "shape")), M = Number(map, "m", 1f), K = Number(map, "k", 1f),
                B = Number(map, "b", 0f), C = Number(map, "c", 0f),
            };
            if (curve.Shape == CurveShape.Keys) {
                List<object> keys = List(map, "keys");
                curve.Keys = new CurveKey[keys.Count];
                for (int i = 0; i < keys.Count; i++) {
                    List<object> key = (List<object>)keys[i];
                    curve.Keys[i] = new CurveKey((float)(double)key[0], (float)(double)key[1], (float)(double)key[2], (float)(double)key[3]);
                }
            }
            return curve;
        }

        private static float Number(Dictionary<string, object> map, string key, float fallback) =>
            map.TryGetValue(key, out object value) && value is double number ? (float)number : fallback;

        private static bool Flag(Dictionary<string, object> map, string key, bool fallback) =>
            map.TryGetValue(key, out object value) && value is bool flag ? flag : fallback;

        private static string Text(Dictionary<string, object> map, string key) =>
            map.TryGetValue(key, out object value) && value is string text ? text : "";

        private static List<object> List(Dictionary<string, object> map, string key) =>
            map.TryGetValue(key, out object value) && value is List<object> list ? list : new List<object>();
    }
}
