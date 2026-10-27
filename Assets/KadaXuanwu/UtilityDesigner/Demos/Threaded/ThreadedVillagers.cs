using System.Diagnostics;
using System.Threading;
using KadaXuanwu.UtilityDesigner.Core;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Demos.Threaded {
    // Agents that no component runs, ticked on a worker thread and offered to the Utility Designer window: add this to
    // any GameObject, play, and pick a villager from the window's Offered agents menu.
    public class ThreadedVillagers : MonoBehaviour {
        [SerializeField] private int villagers = 3;

        // Held while the worker ticks; the window reads an agent only while it holds it too.
        private readonly object _gate = new object();
        private Agent[] _agents;
        private Thread _thread;
        private volatile bool _running;

        private void OnEnable() {
            _agents = new Agent[villagers];
            Globals village = new Globals();
            for (int i = 0; i < villagers; i++) {
                _agents[i] = new Agent(Design(), village, new Rng((ulong)(i + 1)));
                OfferedAgents.Add($"Villager {i + 1}", _agents[i], _gate);
            }
            _running = true;
            _thread = new Thread(Tick) { IsBackground = true, Name = "Villagers" };
            _thread.Start();
        }

        private void OnDisable() {
            _running = false;
            _thread.Join();
            foreach (Agent agent in _agents) {
                OfferedAgents.Remove(agent);
            }
        }

        // Ten ticks a second on the thread's own clock.
        private void Tick() {
            Stopwatch clock = Stopwatch.StartNew();
            while (_running) {
                lock (_gate) {
                    foreach (Agent agent in _agents) {
                        agent.Evaluate(clock.Elapsed.TotalSeconds);
                        agent.Execute(clock.Elapsed.TotalSeconds);
                    }
                }
                Thread.Sleep(100);
            }
        }

        // Hungry and tired by turns: it eats, sleeps or works.
        private static Design Design() {
            Design design = new Design { Name = "Villager" };
            design.ConsiderationSets.Add(new ConsiderationSet {
                Name = "Needs", Local = true, Considerations = {
                    new Consideration { Name = "Hunger", InitialValue = 0.3f, SetMinValue = true, SetMaxValue = true, MaxValue = 1f, ChangePerSecond = 0.1f },
                    new Consideration { Name = "Energy", InitialValue = 0.8f, SetMinValue = true, SetMaxValue = true, MaxValue = 1f, ChangePerSecond = -0.05f },
                },
            });
            StateSet day = new StateSet { Name = "Day" };
            design.StateSets.Add(day);
            day.States.Add(new State {
                Name = "Eat", ExecutionFactor = 1.2f,
                Evaluators = { Need("Hunger", "Needs | Hunger", Curve.Logistic(10f)) },
                Tree = Sequence(
                    new WaitFixed { Duration = 2f },
                    new SetConsideration { ConsiderationSetName = "Needs", ConsiderationName = "Hunger", NewValue = 0f }),
            });
            day.States.Add(new State {
                Name = "Sleep", ExecutionFactor = 1.2f,
                Evaluators = { Need("Tired", "Needs | Energy", Curve.Linear(-1f, 1f)) },
                Tree = Sequence(
                    new WaitFixed { Duration = 4f },
                    new SetConsideration { ConsiderationSetName = "Needs", ConsiderationName = "Energy", NewValue = 1f }),
            });
            day.States.Add(new State {
                Name = "Work", BaseScore = 0.4f,
                Tree = new Repeat { Child = new WaitFixed { Duration = 1f } },
            });
            foreach (State state in day.States) {
                Place(state.Tree, 375f, 160f);
            }
            return design;
        }

        // Under the window's root node, each child row below its parent, left to right.
        private static void Place(Node node, float x, float y) {
            node.X = x;
            node.Y = y;
            for (int i = 0; i < node.Children.Count; i++) {
                Place(node.Children[i], x + (i - (node.Children.Count - 1) / 2f) * 200f, y + 150f);
            }
        }

        private static Evaluator Need(string name, string consideration, Curve curve) =>
            new Evaluator { Name = name, Consideration = consideration, Weight = 1f, Curve = curve };

        private static Sequence Sequence(params Node[] nodes) {
            Sequence sequence = new Sequence();
            sequence.Nodes.AddRange(nodes);
            return sequence;
        }
    }
}
