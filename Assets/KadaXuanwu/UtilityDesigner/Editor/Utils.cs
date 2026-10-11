using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KadaXuanwu.UtilityDesigner.Core;
using KadaXuanwu.UtilityDesigner.Runtime;
using UnityEditor;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("UtilityDesigner.Editor.Tests")]

namespace KadaXuanwu.UtilityDesigner.Editor {
    internal static class Utils {
        // The name asked for, or the next free "<prefix><number>" when it is empty or taken.
        internal static string VerifyItemName<T>(string prefix, string newName, IEnumerable<T> collection, Func<T, string> getName, string oldName = "") {
            T[] items = collection as T[] ?? collection.ToArray();
            newName = newName.Replace(" ", "").Equals("") ? $"{prefix}{items.Length}" : newName;
            int increase = 0;
            while (items.Any(item => getName(item).Equals(newName)) && newName != oldName && increase < 10000) {
                increase++;
                newName = $"{prefix}{items.Length + increase}";
            }
            return newName;
        }

        // "SetSpeed" as "Set Speed".
        internal static string Readable(string name) {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < name.Length; i++) {
                if (i > 0 && char.IsUpper(name[i])) {
                    result.Append(' ');
                }
                result.Append(i == 0 ? char.ToUpper(name[i]) : name[i]);
            }
            return result.ToString();
        }

        // A Keys curve as Unity draws it; any other shape sampled, to look at.
        internal static AnimationCurve ToAnimationCurve(Curve curve) {
            if (curve.Shape == CurveShape.Keys) {
                return new AnimationCurve(curve.Keys.Select(key => new Keyframe(key.Time, key.Value, key.InTangent, key.OutTangent)).ToArray());
            }
            AnimationCurve sampled = new AnimationCurve();
            for (int i = 0; i <= 32; i++) {
                sampled.AddKey(new Keyframe(i / 32f, curve.Evaluate(i / 32f)));
            }
            for (int i = 0; i <= 32; i++) {
                AnimationUtility.SetKeyLeftTangentMode(sampled, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(sampled, i, AnimationUtility.TangentMode.Linear);
            }
            return sampled;
        }

        // The core keeps a curve's keys and their tangents. Tangent weights and wrap modes other than clamp are dropped.
        internal static CurveKey[] ToKeys(AnimationCurve curve) =>
            curve.keys.Select(key => new CurveKey(key.time, key.value, key.inTangent, key.outTangent)).ToArray();

        // A copy of a tree, through its JSON.
        internal static string TreeJson(Node tree) {
            Nodes.Register();
            Design design = new Design();
            design.StateSets.Add(new StateSet());
            design.StateSets[0].States.Add(new State { Tree = tree });
            return Designs.ToJson(design);
        }

        internal static Node TreeFromJson(string json) => Nodes.Load(json).StateSets[0].States[0].Tree;

        // Every node of a tree, parents first.
        internal static List<Node> AllNodes(Node tree, List<Node> into = null) {
            into = into ?? new List<Node>();
            if (tree != null) {
                into.Add(tree);
                foreach (Node child in tree.Children) {
                    AllNodes(child, into);
                }
            }
            return into;
        }

        internal static string NewId() => Guid.NewGuid().ToString("N");
    }

    // Behaviours are JSON files, each carrying its own copy of its consideration sets; the consideration set assets are
    // where those are edited. This keeps the copies the same as the assets of their names.
    internal static class Behaviours {
        internal static ConsiderationSetAsset Asset(string name) {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(ConsiderationSetAsset)}")) {
                ConsiderationSetAsset asset = AssetDatabase.LoadAssetAtPath<ConsiderationSetAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.name == name) {
                    return asset;
                }
            }
            return null;
        }

        internal static void Sync(Design design) {
            for (int i = 0; i < design.ConsiderationSets.Count; i++) {
                ConsiderationSetAsset asset = Asset(design.ConsiderationSets[i].Name);
                if (asset != null) {
                    design.ConsiderationSets[i] = asset.ToCore();
                }
            }
        }

        internal static void Write(string path, Design design) {
            Nodes.Register();
            string json = Designs.ToJson(design);
            if (File.Exists(path) && File.ReadAllText(path) == json) {
                return;
            }
            File.WriteAllText(path, json);
            AssetDatabase.ImportAsset(path);
        }

        // After a consideration set asset changed: every behaviour of the project that has a set of its name.
        internal static void SyncAll(ConsiderationSetAsset asset) {
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset")) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json") || !path.StartsWith("Assets/")) {
                    continue;
                }
                string text = File.ReadAllText(path);
                if (!text.Contains("\"stateSets\"") || !text.Contains(Json.Quote(asset.name))) {
                    continue;
                }
                Design design;
                try {
                    design = Nodes.Load(text);
                } catch (Exception) {
                    // Some other JSON, or a design naming a node type that is gone: not ours to rewrite.
                    continue;
                }
                Sync(design);
                Write(path, design);
            }
        }
    }
}
