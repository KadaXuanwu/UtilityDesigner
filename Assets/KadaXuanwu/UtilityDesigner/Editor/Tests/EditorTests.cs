using System.IO;
using KadaXuanwu.UtilityDesigner.Core;
using KadaXuanwu.UtilityDesigner.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Editor.Tests {
    public sealed class EditorTests {
        private const string Demos = "Assets/KadaXuanwu/UtilityDesigner/Demos";

        // Every node type the demos name is registered, the files are what the editor would write (so opening one
        // changes nothing), and their consideration sets are the assets'.
        [Test]
        public void TheDemoBehavioursAreWhatTheEditorWrites() {
            int found = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { Demos })) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json")) {
                    continue;
                }
                found++;
                string text = File.ReadAllText(path).Replace("\r\n", "\n");
                Design design = Nodes.Load(text);
                Assert.That(Designs.ToJson(design), Is.EqualTo(text), path);
                Behaviours.Sync(design);
                Assert.That(Designs.ToJson(design), Is.EqualTo(text), $"{path} and its Consideration Set assets");
            }
            Assert.That(found, Is.EqualTo(2));
        }

        [Test]
        public void ACurveDrawnInUnityScoresTheSameInTheCore() {
            AnimationCurve drawn = new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(0.4f, 0.9f, 0.5f, 0.5f),
                new Keyframe(0.7f, 0.3f, float.PositiveInfinity, float.PositiveInfinity), new Keyframe(1f, 0.2f, -1f, 0f));
            Curve curve = Curve.Through(Utils.ToKeys(drawn));
            AnimationCurve back = Utils.ToAnimationCurve(curve);
            for (int i = 0; i <= 40; i++) {
                float x = i / 40f;
                Assert.That(curve.Evaluate(x), Is.EqualTo(drawn.Evaluate(x)).Within(1e-5f), $"at {x}");
                Assert.That(back.Evaluate(x), Is.EqualTo(drawn.Evaluate(x)).Within(1e-5f), $"back at {x}");
            }
        }

        [Test]
        public void TheWindowListsTheDecisionsOfATrace() {
            Design design = new Design { Name = "D" };
            design.ConsiderationSets.Add(new ConsiderationSet { Name = "S", Local = true, Considerations = { new Consideration { Name = "a" } } });
            design.StateSets.Add(new StateSet {
                Name = "Main", States = {
                    new State { Name = "Rest", BaseScore = 0.5f },
                    new State { Name = "Act", Evaluators = { new Evaluator { Name = "e", Consideration = "S | a", Weight = 1f, Curve = Curve.Linear() } } },
                },
            });
            Agent agent = new Agent(design, new Globals(), new Rng(1)) { Trace = new Trace(4) };
            agent.Evaluate(0.0);
            agent.SetConsideration("S", "a", 0.9f);
            agent.Evaluate(1.0);
            agent.Evaluate(2.0);
            Assert.That(UtilityDesignerEditorWindow.Decisions(agent.Trace, 8), Is.EqualTo("1.0 s  Act 0.900  over Rest 0.500\n0.0 s  Rest 0.500  over Act 0.000"));
            Assert.That(UtilityDesignerEditorWindow.Decisions(agent.Trace, 1), Is.EqualTo("1.0 s  Act 0.900  over Rest 0.500"));
            // Full, the trace no longer knows what ran before its oldest entry.
            agent.Evaluate(3.0);
            agent.Evaluate(4.0);
            Assert.That(UtilityDesignerEditorWindow.Decisions(agent.Trace, 8), Is.Empty);
        }

        [Test]
        public void AUnityNodeKeepsItsVectorThroughJson() {
            MoveToPosition node = (MoveToPosition)Utils.TreeFromJson(Utils.TreeJson(new MoveToPosition { Pos = new Vector3(1.5f, -2f, 3f), SetSpeed = true, Speed = 4f }));
            Assert.That(node.Pos, Is.EqualTo(new Vector3(1.5f, -2f, 3f)));
            Assert.That(node.SetSpeed, Is.True);
            Assert.That(node.Speed, Is.EqualTo(4f));
        }
    }
}
