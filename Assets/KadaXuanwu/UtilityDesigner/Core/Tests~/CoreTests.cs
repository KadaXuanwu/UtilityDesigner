using System.Collections.Generic;
using System.Linq;
using KadaXuanwu.UtilityDesigner.Core;
using KadaXuanwu.UtilityDesigner.Samples;
using NUnit.Framework;

namespace KadaXuanwu.UtilityDesigner.Tests {
    public sealed class CurveTests {
        [Test]
        public void ShapesGiveTheirNumbersClampedToZeroToOne() {
            Assert.That(Curve.Linear().Evaluate(0.3f), Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(Curve.Linear(-1f, 1f).Evaluate(0.3f), Is.EqualTo(0.7f).Within(1e-6f));
            Assert.That(Curve.Linear(2f).Evaluate(0.8f), Is.EqualTo(1f), "clamped");
            Assert.That(Curve.Logistic(10f, 0.4f).Evaluate(0.4f), Is.EqualTo(0.5f).Within(1e-6f), "half at its middle");
            Assert.That(Curve.Polynomial(2f).Evaluate(0.5f), Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(Curve.Step(0.5f).Evaluate(0.49f), Is.EqualTo(0f));
            Assert.That(Curve.Step(0.5f).Evaluate(0.5f), Is.EqualTo(1f));
        }

        [Test]
        public void KeysPassThroughTheirPoints() {
            Curve curve = Curve.Through(new CurveKey(0f, 0f, 0f, 0f), new CurveKey(0.5f, 1f, 0f, 0f), new CurveKey(1f, 0.2f, 0f, 0f));
            Assert.That(curve.Evaluate(0.5f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(curve.Evaluate(1f), Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(curve.Evaluate(0.25f), Is.EqualTo(0.5f).Within(1e-6f), "flat tangents: a smooth step, half way at half way");
        }
    }

    public sealed class BrainTests {
        private sealed class World {
            public float A;
            public float B;
            public float[] Targets = { 0.2f, 0.9f, 0.5f };
        }

        private static Reasoner TwoActions(float momentum = 1f, float interrupt = 1f, float minCommit = 0f) {
            Reasoner design = new Reasoner { Name = "Two", Momentum = momentum, Interrupt = interrupt, MinCommit = minCommit };
            design.Actions.Add(new UtilityAction { Name = "A", Considerations = { new Consideration { Name = "a", Input = "a" } } });
            design.Actions.Add(new UtilityAction { Name = "B", Considerations = { new Consideration { Name = "b", Input = "b" } } });
            return design;
        }

        private static Bindings<World, int> Inputs() => new Bindings<World, int>()
            .Input("a", (w, _) => w.A)
            .Input("b", (w, _) => w.B)
            .Input("t", (w, t) => w.Targets[t])
            .Targets("all", (w, into) => {
                for (int i = 0; i < w.Targets.Length; i++) {
                    into.Add(i);
                }
            });

        [Test]
        public void ItChoosesTheBest() {
            Brain<World, int> brain = TwoActions().Bind(Inputs(), 1);
            Assert.That(brain.Decide(new World { A = 0.3f, B = 0.6f }, 0).Name, Is.EqualTo("B"));
        }

        [Test]
        public void AConsiderationAtZeroVetoesAndCompensationLiftsMany() {
            Reasoner design = new Reasoner();
            UtilityAction action = new UtilityAction { Name = "Many" };
            for (int i = 0; i < 4; i++) {
                action.Considerations.Add(new Consideration { Name = $"c{i}", Input = "a" });
            }
            design.Actions.Add(action);
            Brain<World, int> brain = design.Bind(Inputs(), 1);
            Assert.That(brain.Score(0, new World { A = 0f }, 0), Is.EqualTo(0f));
            float lifted = brain.Score(0, new World { A = 0.5f }, 0);
            Assert.That(lifted, Is.GreaterThan(0.5f * 0.5f * 0.5f * 0.5f), "four halves multiplied would be 0.0625");
        }

        [Test]
        public void MomentumAndInterruptKeepTheRunningChoiceThroughANearTie() {
            Brain<World, int> brain = TwoActions(1.1f, 1.5f).Bind(Inputs(), 1);
            World world = new World { A = 0.6f, B = 0.5f };
            Assert.That(brain.Decide(world, 0).Name, Is.EqualTo("A"));
            world.B = 0.8f;
            Assert.That(brain.Decide(world, 1).Name, Is.EqualTo("A"), "0.8 is not 1.5 times 0.66");
            world.B = 1f;
            world.A = 0.3f;
            Assert.That(brain.Decide(world, 2).Name, Is.EqualTo("B"), "1 is over 1.5 times 0.33");
        }

        [Test]
        public void NothingOfTheSameRankTakesOverBeforeTheMinimumCommitment() {
            Brain<World, int> brain = TwoActions(1f, 1f, 2f).Bind(Inputs(), 1);
            World world = new World { A = 0.6f, B = 0.1f };
            brain.Decide(world, 0);
            world.B = 1f;
            Assert.That(brain.Decide(world, 1.9).Name, Is.EqualTo("A"));
            Assert.That(brain.Decide(world, 2.1).Name, Is.EqualTo("B"));
        }

        [Test]
        public void AHigherRankAlwaysTakesOver() {
            Reasoner design = TwoActions(2f, 4f, 10f);
            design.Actions[1].Rank = 1;
            Brain<World, int> brain = design.Bind(Inputs(), 1);
            World world = new World { A = 1f, B = 0f };
            brain.Decide(world, 0);
            world.B = 0.1f;
            Assert.That(brain.Decide(world, 0.5).Name, Is.EqualTo("B"));
        }

        [Test]
        public void AFinishedActionWaitsOutItsCooldown() {
            Reasoner design = TwoActions();
            design.Actions[0].Cooldown = 5f;
            Brain<World, int> brain = design.Bind(Inputs(), 1);
            World world = new World { A = 1f, B = 0.2f };
            brain.Decide(world, 0);
            brain.Finish(1);
            Assert.That(brain.Decide(world, 3).Name, Is.EqualTo("B"));
            brain.Finish(3);
            Assert.That(brain.Decide(world, 6.5).Name, Is.EqualTo("A"));
        }

        [Test]
        public void ATargetedActionIsScoredForEachTarget() {
            Reasoner design = new Reasoner();
            design.Actions.Add(new UtilityAction { Name = "Go", Targets = "all", Considerations = { new Consideration { Name = "t", Input = "t" } } });
            Choice<int> choice = design.Bind(Inputs(), 1).Decide(new World(), 0);
            Assert.That(choice.Target, Is.EqualTo(1));
        }

        [Test]
        public void TheTraceKeepsEveryOption() {
            Brain<World, int> brain = TwoActions().Bind(Inputs(), 1);
            brain.Trace = new Trace(4);
            for (int i = 0; i < 6; i++) {
                brain.Decide(new World { A = 0.3f, B = 0.6f }, i);
            }
            Assert.That(brain.Trace.Count, Is.EqualTo(4));
            Assert.That(brain.Trace[0].Time, Is.EqualTo(2.0));
            Assert.That(brain.Trace[3].Options, Has.Count.EqualTo(2));
            Assert.That(brain.Trace[3].Chosen, Is.EqualTo("B"));
        }

        [Test]
        public void AMissingInputFailsAtBind() {
            Reasoner design = TwoActions();
            design.Actions[0].Considerations[0].Input = "nope";
            Assert.Throws<System.ArgumentException>(() => design.Bind(Inputs(), 1));
        }
    }

    public sealed class DesignTests {
        [Test]
        public void ADesignRoundTripsThroughJson() {
            Reasoner design = Villager.Design();
            design.Actions[0].Considerations[0].Curve = Curve.Through(new CurveKey(0f, 0f, 1f, 1f), new CurveKey(1f, 1f, 1f, 1f));
            string json = Designs.ToJson(design);
            Reasoner back = Designs.FromJson(json);
            Assert.That(Designs.ToJson(back), Is.EqualTo(json));
            Assert.That(back.Actions[3].Name, Is.EqualTo("Wander"));
            Assert.That(back.Spread, Is.EqualTo(0.15f));
        }

        [Test]
        public void ParametersRoundTripAsAVector() {
            Reasoner design = Villager.Design();
            List<Parameter> parameters = Designs.Parameters(design);
            float[] vector = Designs.Vector(parameters);
            Assert.That(parameters.Exists(p => p.Id == "actions.Eat.Hunger.m"), Is.True);
            vector[0] = 1f;
            Designs.Apply(parameters, vector);
            Assert.That(design.Momentum, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(Designs.Vector(Designs.Parameters(design))[0], Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ALayerSetsWhatItNamesAndADifferenceIsALayer() {
            Reasoner design = Villager.Design();
            Designs.Layer(design, "{\"actions.Work.weight\": 2, \"interrupt\": 3}");
            Assert.That(design.Actions[2].Weight, Is.EqualTo(2f));
            Assert.That(design.Interrupt, Is.EqualTo(3f));
            Reasoner plain = Villager.Design();
            Reasoner again = Villager.Design();
            Designs.Layer(again, Designs.Difference(plain, design));
            Assert.That(Designs.ToJson(again), Is.EqualTo(Designs.ToJson(design)));
            Assert.Throws<System.ArgumentException>(() => Designs.Layer(design, "{\"nothing\": 1}"));
        }
    }

    public sealed class TreeTests {
        private sealed class Ctx {
            public int Count;
            public bool Allowed = true;
            public bool Stopped;
        }

        [Test]
        public void ASequenceRunsInOrderAndASelectorFallsThrough() {
            Ctx ctx = new Ctx();
            Sequence<Ctx> sequence = new Sequence<Ctx>(new Do<Ctx>("one", (c, _) => {
                c.Count++;
                return Status.Success;
            }), new Wait<Ctx>(1.0), new Do<Ctx>("two", (c, _) => {
                c.Count += 10;
                return Status.Success;
            }));
            Assert.That(sequence.Tick(ctx, 0), Is.EqualTo(Status.Running));
            Assert.That(sequence.Tick(ctx, 1.5), Is.EqualTo(Status.Success));
            Assert.That(ctx.Count, Is.EqualTo(11));
            Selector<Ctx> selector = new Selector<Ctx>(new Condition<Ctx>("no", _ => false), new Condition<Ctx>("yes", _ => true));
            Assert.That(selector.Tick(ctx, 0), Is.EqualTo(Status.Success));
        }

        [Test]
        public void WhileAbortsItsChildTheMomentItsConditionFails() {
            Ctx ctx = new Ctx();
            While<Ctx> guarded = new While<Ctx>(c => c.Allowed, new Do<Ctx>("forever", (_, _) => Status.Running, c => c.Stopped = true));
            Assert.That(guarded.Tick(ctx, 0), Is.EqualTo(Status.Running));
            ctx.Allowed = false;
            Assert.That(guarded.Tick(ctx, 1), Is.EqualTo(Status.Failure));
            Assert.That(ctx.Stopped, Is.True);
        }
    }

    public sealed class PlanTests {
        private sealed class Siege {
            public float RamWorth = 1f;
            public float LadderWorth = 0.5f;
            public bool RamAlive = true;
            public readonly List<string> Done = new List<string>();
        }

        private static Primitive<Siege> Step(string name, System.Func<Siege, bool> valid = null) => new Primitive<Siege> {
            Name = name, Valid = valid ?? (_ => true),
            Operator = new Sequence<Siege>(new Wait<Siege>(1.0), new Do<Siege>(name, (s, _) => {
                s.Done.Add(name);
                return Status.Success;
            })),
        };

        private static Compound<Siege> Breach() {
            Compound<Siege> breach = new Compound<Siege> { Name = "Breach" };
            Method<Siege> ram = new Method<Siege> { Name = "Ram", Utility = s => s.RamWorth };
            ram.Steps.Add(Step("buy ram"));
            ram.Steps.Add(Step("push", s => s.RamAlive));
            ram.Steps.Add(Step("strike", s => s.RamAlive));
            Method<Siege> ladder = new Method<Siege> { Name = "Ladder", Utility = s => s.LadderWorth };
            ladder.Steps.Add(Step("buy ladder"));
            ladder.Steps.Add(Step("climb"));
            breach.Methods.Add(ram);
            breach.Methods.Add(ladder);
            return breach;
        }

        private static void Run(PlanRunner<Siege> runner, Siege siege, double from, double to) {
            for (double t = from; t < to; t += 0.25) {
                runner.Tick(siege, t);
            }
        }

        [Test]
        public void TheBestMethodThatAppliesIsPlanned() {
            Plan<Siege> plan = Planner.Decompose(Breach(), new Siege());
            Assert.That(plan.Path, Is.EqualTo("Ram"));
            Assert.That(plan.Steps, Has.Count.EqualTo(3));
        }

        [Test]
        public void ARunningPlanIsKeptThroughASmallChangeAndDroppedForABigOne() {
            Siege siege = new Siege();
            PlanRunner<Siege> runner = new PlanRunner<Siege>(Breach()) { Interrupt = 1.5f, SunkCost = 1f };
            Run(runner, siege, 0, 1.5);
            siege.LadderWorth = 1.2f;
            Run(runner, siege, 1.5, 3);
            Assert.That(runner.Current.Path, Is.EqualTo("Ram"), "1.2 is under 1.5 times the ram's 1, more with progress");
            siege.LadderWorth = 5f;
            Run(runner, siege, 3, 4);
            Assert.That(runner.Current.Path, Is.EqualTo("Ladder"));
            Assert.That(runner.Dropped, Is.EqualTo(1));
        }

        [Test]
        public void ABrokenStepDropsThePlanAndAnotherIsMade() {
            Siege siege = new Siege();
            PlanRunner<Siege> runner = new PlanRunner<Siege>(Breach());
            Run(runner, siege, 0, 1.5);
            siege.RamAlive = false;
            siege.RamWorth = 0f;
            Run(runner, siege, 1.5, 6);
            Assert.That(runner.LastDrop, Does.Contain("push"));
            Assert.That(siege.Done, Does.Contain("climb"));
        }
    }

    public sealed class TaskPoolTests {
        private sealed class Job {
            public string Name;
            public int Tier;
            public int Crew = 1;
            public Dictionary<string, float> Wants = new Dictionary<string, float>();
            public override string ToString() => Name;
        }

        private static TaskPool<string, Job> Pool() => new TaskPool<string, Job>((agent, job) => job.Wants.TryGetValue(agent, out float w) ? w : 0.1f) {
            Tier = job => job.Tier, Crew = job => job.Crew, MinCommit = 2f, Interrupt = 1.5f, SunkCost = 0f, CrewTimeout = 10f,
        };

        [Test]
        public void EachAgentTakesTheJobItWantsMostAndTheHighestTierFirst() {
            TaskPool<string, Job> pool = Pool();
            Job wall = new Job { Name = "wall", Wants = { ["a"] = 0.9f, ["b"] = 0.3f } };
            Job gun = new Job { Name = "gun", Wants = { ["a"] = 0.5f, ["b"] = 0.8f } };
            pool.Advertise(wall);
            pool.Advertise(gun);
            string[] agents = { "a", "b" };
            pool.Assign(agents, 0);
            Assert.That(pool.TaskOf("a"), Is.SameAs(wall));
            Assert.That(pool.TaskOf("b"), Is.SameAs(gun));
            Job breach = new Job { Name = "breach", Tier = 3 };
            pool.Advertise(breach);
            pool.Assign(agents, 0.5);
            Assert.That(pool.TaskOf("a") == breach || pool.TaskOf("b") == breach, Is.True, "a higher tier interrupts at once");
        }

        [Test]
        public void ABusyAgentChangesOnlyAfterTheMinimumForAJobWorthInterruptTimesMore() {
            TaskPool<string, Job> pool = Pool();
            Job first = new Job { Name = "first", Wants = { ["a"] = 0.4f } };
            pool.Advertise(first);
            string[] agents = { "a" };
            pool.Assign(agents, 0);
            Job better = new Job { Name = "better", Wants = { ["a"] = 0.9f } };
            pool.Advertise(better);
            pool.Assign(agents, 1);
            Assert.That(pool.TaskOf("a"), Is.SameAs(first), "within the minimum");
            pool.Assign(agents, 3);
            Assert.That(pool.TaskOf("a"), Is.SameAs(better));
        }

        [Test]
        public void ACrewNotWholeInTimeIsReleased() {
            TaskPool<string, Job> pool = Pool();
            Job ram = new Job { Name = "ram", Crew = 3 };
            pool.Advertise(ram);
            string[] agents = { "a", "b" };
            pool.Assign(agents, 0);
            Assert.That(pool.CrewOf(ram), Has.Count.EqualTo(2));
            Assert.That(pool.Manned(ram), Is.False);
            pool.Assign(System.Array.Empty<string>(), 11);
            Assert.That(pool.CrewOf(ram), Has.Count.EqualTo(0));
        }
    }

    public sealed class SampleTests {
        [Test]
        public void TheVillagerLivesTheSameDayForTheSameSeed() {
            List<string> once = Villager.Live(42);
            List<string> again = Villager.Live(42);
            Assert.That(once, Is.EqualTo(again));
            Assert.That(once.Count, Is.GreaterThan(10), "it does things");
            Assert.That(once.Exists(line => line.EndsWith("Eat")) && once.Exists(line => line.EndsWith("Sleep")), Is.True);
            bool anyOther = false;
            for (ulong seed = 1; seed < 10 && !anyOther; seed++) {
                anyOther = !Villager.Live(seed).SequenceEqual(once);
            }
            Assert.That(anyOther, Is.True, "near ties go other ways for other seeds");
        }
    }
}
