using System.Collections.Generic;
using System.Globalization;
using KadaXuanwu.UtilityDesigner.Core;

namespace KadaXuanwu.UtilityDesigner.Samples {
    // The game's own node: writes what the villager saw into its memories.
    public sealed class Remember : ActionNode {
        public string Text = "";

        protected override NodeState OnUpdate() {
            ((Villager)Agent.Owner).Memories.Add(Text);
            return NodeState.Success;
        }
    }

    // A villager in a plain C# loop, no engine: hungry, tired and bored by turns, it eats, sleeps, works or wanders. Its
    // needs are its own (a local consideration set); the food is the village's, shared by every villager given the same
    // Globals. The same seed lives the same day every time.
    public sealed class Villager {
        public readonly Agent Agent;
        public readonly List<string> Diary = new List<string>();
        public readonly List<string> Memories = new List<string>();

        private State _doing;

        public static Design Design() {
            NodeTypes.Register<Remember>();
            Design design = new Design { Name = "Villager" };
            design.ConsiderationSets.Add(new ConsiderationSet {
                Name = "Needs", Local = true, Considerations = {
                    new Consideration { Name = "Hunger", InitialValue = 0.3f, SetMinValue = true, SetMaxValue = true, MaxValue = 1f, ChangePerSecond = 0.1f },
                    new Consideration { Name = "Energy", InitialValue = 0.8f, SetMinValue = true, SetMaxValue = true, MaxValue = 1f, ChangePerSecond = -0.08f },
                    new Consideration { Name = "Boredom", InitialValue = 0.2f, SetMinValue = true, SetMaxValue = true, MaxValue = 1f, ChangePerSecond = 0.06f },
                },
            });
            design.ConsiderationSets.Add(new ConsiderationSet {
                Name = "Village", Considerations = { new Consideration { Name = "Food", InitialValue = 3f, SetMinValue = true } },
            });
            StateSet day = new StateSet { Name = "Day" };
            design.StateSets.Add(day);

            // Eats for a second, if the village has food; hunger gone, another state takes over.
            day.States.Add(new State {
                Name = "Eat", ExecutionFactor = 1.2f,
                Preconditions = { new Precondition { Name = "Food left", Consideration = "Village | Food", Comparator = Comparator.GreaterThanOrEqualTo, Value = 1f } },
                Evaluators = { Need("Hunger", "Needs | Hunger", 1f, Curve.Logistic(10f, 0.5f)) },
                Tree = Sequence(
                    new WaitFixed { Duration = 1f },
                    new ChangeConsideration { ConsiderationSetName = "Village", ConsiderationName = "Food", Amount = -1f },
                    new SetConsideration { ConsiderationSetName = "Needs", ConsiderationName = "Hunger", NewValue = 0f }),
            });
            // Sleeps, locked in it until rested: nothing wakes it.
            Repeat rest = new Repeat {
                Child = Sequence(
                    new WaitFixed { Duration = 0.5f },
                    new ChangeConsideration { ConsiderationSetName = "Needs", ConsiderationName = "Energy", Amount = 0.1f }),
            };
            day.States.Add(new State {
                Name = "Sleep",
                Evaluators = { Need("Tired", "Needs | Energy", 1f, Curve.Linear(-1f, 1f)) },
                Tree = new Parallel {
                    Nodes = { rest, new LockStateConsideration { ConsiderationSetName = "Needs", ConsiderationName = "Energy", RequiredValue = 0.95f } },
                },
            });
            // Harvests again and again while it is the best thing to do.
            day.States.Add(new State {
                Name = "Work", Weight = 0.6f, ExecutionFactor = 1.2f,
                Evaluators = {
                    Need("Rested", "Needs | Energy", 0.5f, Curve.Linear()),
                    Need("Fed", "Needs | Hunger", 0.5f, Curve.Linear(-1f, 1f)),
                },
                Tree = new Repeat {
                    Child = Sequence(
                        new WaitFixed { Duration = 3f },
                        new ChangeConsideration { ConsiderationSetName = "Village", ConsiderationName = "Food", Amount = 1f },
                        new ChangeConsideration { ConsiderationSetName = "Needs", ConsiderationName = "Boredom", Amount = -0.1f }),
                },
            });
            // Not every time it would: its fail chance passes it over three times in ten.
            day.States.Add(new State {
                Name = "Wander", Weight = 0.5f, ExecutionFactor = 1.5f, FailChance = 0.3f,
                Evaluators = { Need("Bored", "Needs | Boredom", 1f, Curve.Linear()) },
                Tree = Sequence(
                    new WaitRandom { MinDuration = 1f, MaxDuration = 3f },
                    new Remember { Text = "the hills" },
                    new SetConsideration { ConsiderationSetName = "Needs", ConsiderationName = "Boredom", NewValue = 0f }),
            });
            return design;
        }

        private static Evaluator Need(string name, string consideration, float weight, Curve curve) =>
            new Evaluator { Name = name, Consideration = consideration, Weight = weight, Curve = curve };

        private static Sequence Sequence(params Node[] nodes) {
            Sequence sequence = new Sequence();
            sequence.Nodes.AddRange(nodes);
            return sequence;
        }

        public Villager(Design design, Globals village, ulong seed) {
            Agent = new Agent(design, village, new Rng(seed), 0.0, this);
        }

        // One step of its day, a tenth of a second: evaluation and execution at v1's default rates.
        public void Tick(double now) {
            Agent.Evaluate(now);
            Agent.Execute(now);
            if (Agent.ExecutingState != _doing) {
                _doing = Agent.ExecutingState;
                Diary.Add($"{now.ToString("0.0", CultureInfo.InvariantCulture)} {_doing?.Name ?? "Nothing"}");
            }
        }

        // A day of ticks, the diary of what it chose.
        public static List<string> Live(ulong seed, int ticks = 2000) {
            Villager villager = new Villager(Design(), new Globals(), seed);
            for (int i = 0; i < ticks; i++) {
                villager.Tick(i * 0.1);
            }
            return villager.Diary;
        }
    }
}
