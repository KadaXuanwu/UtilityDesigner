using System;
using System.Collections.Generic;

namespace KadaXuanwu.UtilityDesigner.Core {
    // A tunable number of a design: a stable id (its path by names), its range and scale, and how to read and write it.
    // An optimizer sees a design as the vector of these (Parameters.Vector), each mapped to 0..1.
    public sealed class Parameter {
        public string Id;
        public float Min;
        public float Max;
        // Moved on a log scale: weights, times; Min must be above 0.
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

    // Designs as versioned JSON, their tunable numbers, and override layers: a small JSON of changed ids laid over a base
    // design (a difficulty, a personality, an optimizer's result), in order.
    public static class Designs {
        public static string ToJson(Reasoner design) {
            List<object> actions = new List<object>();
            foreach (UtilityAction action in design.Actions) {
                List<object> considerations = new List<object>();
                foreach (Consideration consideration in action.Considerations) {
                    considerations.Add(new Dictionary<string, object> {
                        ["name"] = consideration.Name, ["input"] = consideration.Input, ["min"] = consideration.Min,
                        ["max"] = consideration.Max, ["curve"] = CurveToJson(consideration.Curve),
                    });
                }
                actions.Add(new Dictionary<string, object> {
                    ["name"] = action.Name, ["weight"] = action.Weight, ["rank"] = action.Rank, ["targets"] = action.Targets,
                    ["cooldown"] = action.Cooldown, ["considerations"] = considerations,
                });
            }
            return Json.Write(new Dictionary<string, object> {
                ["format"] = Reasoner.FormatVersion, ["name"] = design.Name, ["momentum"] = design.Momentum,
                ["minCommit"] = design.MinCommit, ["interrupt"] = design.Interrupt, ["sunkCost"] = design.SunkCost,
                ["spread"] = design.Spread, ["actions"] = actions,
            });
        }

        public static Reasoner FromJson(string text) {
            Dictionary<string, object> root = (Dictionary<string, object>)Json.Parse(text);
            int format = (int)Number(root, "format", 1);
            if (format > Reasoner.FormatVersion) {
                throw new FormatException($"A design of format {format}, newer than this Utility Designer ({Reasoner.FormatVersion}).");
            }
            Reasoner design = new Reasoner {
                Name = Text(root, "name"), Momentum = Number(root, "momentum", 1.1f), MinCommit = Number(root, "minCommit", 0f),
                Interrupt = Number(root, "interrupt", 1.5f), SunkCost = Number(root, "sunkCost", 0f), Spread = Number(root, "spread", 0f),
            };
            foreach (object item in List(root, "actions")) {
                Dictionary<string, object> map = (Dictionary<string, object>)item;
                UtilityAction action = new UtilityAction {
                    Name = Text(map, "name"), Weight = Number(map, "weight", 1f), Rank = (int)Number(map, "rank", 0f),
                    Targets = Text(map, "targets"), Cooldown = Number(map, "cooldown", 0f),
                };
                foreach (object entry in List(map, "considerations")) {
                    Dictionary<string, object> c = (Dictionary<string, object>)entry;
                    action.Considerations.Add(new Consideration {
                        Name = Text(c, "name"), Input = Text(c, "input"), Min = Number(c, "min", 0f), Max = Number(c, "max", 1f),
                        Curve = c.TryGetValue("curve", out object curve) ? CurveFromJson((Dictionary<string, object>)curve) : Curve.Linear(),
                    });
                }
                design.Actions.Add(action);
            }
            return design;
        }

        // Every tunable number of the design, by id.
        public static List<Parameter> Parameters(Reasoner design) {
            List<Parameter> all = new List<Parameter> {
                Number("momentum", 1f, 3f, false, () => design.Momentum, v => design.Momentum = v),
                Number("minCommit", 0f, 10f, false, () => design.MinCommit, v => design.MinCommit = v),
                Number("interrupt", 1f, 4f, false, () => design.Interrupt, v => design.Interrupt = v),
                Number("sunkCost", 0f, 2f, false, () => design.SunkCost, v => design.SunkCost = v),
                Number("spread", 0f, 1f, false, () => design.Spread, v => design.Spread = v),
            };
            foreach (UtilityAction action in design.Actions) {
                string a = $"actions.{action.Name}";
                all.Add(Number($"{a}.weight", 0.05f, 20f, true, () => action.Weight, v => action.Weight = v));
                all.Add(Number($"{a}.cooldown", 0f, 60f, false, () => action.Cooldown, v => action.Cooldown = v));
                foreach (Consideration consideration in action.Considerations) {
                    string c = $"{a}.{consideration.Name}";
                    Curve curve = consideration.Curve;
                    if (curve.Shape == CurveShape.Keys) {
                        continue;
                    }
                    all.Add(Number($"{c}.m", -20f, 20f, false, () => curve.M, v => curve.M = v));
                    all.Add(Number($"{c}.k", -10f, 10f, false, () => curve.K, v => curve.K = v));
                    all.Add(Number($"{c}.b", -1f, 1f, false, () => curve.B, v => curve.B = v));
                    all.Add(Number($"{c}.c", -1f, 2f, false, () => curve.C, v => curve.C = v));
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

        // A layer: {"id": value, ...} laid over the design; an id it does not have fails loudly.
        public static void Layer(Reasoner design, string layer) {
            Dictionary<string, Parameter> byId = new Dictionary<string, Parameter>();
            foreach (Parameter parameter in Parameters(design)) {
                byId[parameter.Id] = parameter;
            }
            foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)Json.Parse(layer)) {
                if (!byId.TryGetValue(pair.Key, out Parameter parameter)) {
                    throw new ArgumentException($"{design.Name} has no number {pair.Key} for a layer to set.");
                }
                parameter.Set((float)(double)pair.Value);
            }
        }

        // The layer that turns from into to: every number that differs.
        public static string Difference(Reasoner from, Reasoner to) {
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

        private static Parameter Number(string id, float min, float max, bool log, Func<float> get, Action<float> set) =>
            new Parameter { Id = id, Min = min, Max = max, Log = log, Get = get, Set = set };

        private static Dictionary<string, object> CurveToJson(Curve curve) {
            Dictionary<string, object> map = new Dictionary<string, object> { ["shape"] = curve.Shape.ToString() };
            if (curve.Shape == CurveShape.Keys) {
                List<object> keys = new List<object>();
                foreach (CurveKey key in curve.Keys) {
                    keys.Add(new List<object> { key.Time, key.Value, key.InTangent, key.OutTangent });
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

        private static string Text(Dictionary<string, object> map, string key) =>
            map.TryGetValue(key, out object value) && value is string text ? text : "";

        private static List<object> List(Dictionary<string, object> map, string key) =>
            map.TryGetValue(key, out object value) && value is List<object> list ? list : new List<object>();
    }
}
