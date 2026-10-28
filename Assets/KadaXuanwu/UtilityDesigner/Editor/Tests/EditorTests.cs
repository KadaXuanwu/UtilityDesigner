using System.IO;
using KadaXuanwu.UtilityDesigner.Core;
using KadaXuanwu.UtilityDesigner.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Editor.Tests {
    // The threaded demo's CountMeal, which this assembly cannot reach.
    public sealed class Meal : ActionNode {
        protected override NodeState OnUpdate() {
            Agent.Blackboard.Set("Meals", Agent.Blackboard.Get("Meals", 0) + 1);
            Agent.Blackboard.Set("Last meal", (float)Agent.Now);
            return NodeState.Success;
        }
    }

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

        // As the threaded demo: ticked on a worker thread under its gate, read by the window under the same gate.
        [Test]
        public void TheWindowShowsTheBlackboardOfAnAgentTickedOffTheMainThread() {
            Design design = new Design { Name = "D" };
            design.BlackboardKeys.Add(new BlackboardKey { Name = "Stand", Type = BlackboardType.Vector });
            design.BlackboardKeys.Add(new BlackboardKey { Name = "Meals", Type = BlackboardType.WholeNumber });
            design.BlackboardKeys.Add(new BlackboardKey { Name = "Last meal", Type = BlackboardType.Number });
            design.StateSets.Add(new StateSet {
                Name = "Main", States = { new State { Name = "Eat", BaseScore = 1f, Tree = new Repeat { Child = new Meal() } } },
            });
            Agent agent = new Agent(design, new Globals(), new Rng(1));
            object gate = new object();
            Assert.That(UtilityDesignerEditorWindow.BlackboardValues(agent), Is.EqualTo(new[] { "none", "none", "none" }));
            System.Threading.Thread worker = new System.Threading.Thread(() => {
                for (int i = 1; i <= 3; i++) {
                    lock (gate) {
                        agent.Evaluate(i * 0.5);
                        agent.Execute(i * 0.5);
                        agent.Blackboard.Set("Stand", new Vector3(1f, 2f, 3f));
                    }
                }
            });
            worker.Start();
            worker.Join();
            lock (gate) {
                Assert.That(UtilityDesignerEditorWindow.BlackboardValues(agent), Is.EqualTo(new[] { "(1, 2, 3)", "3", "1.5" }));
            }
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
