using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KadaXuanwu.UtilityDesigner.Core;
using KadaXuanwu.UtilityDesigner.Samples;
using NUnit.Framework;

namespace KadaXuanwu.UtilityDesigner.Tests {
    // A design built in a few lines: one local consideration set "S", one global "G", state sets of scored states.
    internal sealed class Bench {
        public readonly Design Design = new Design { Name = "Bench" };
        public readonly ConsiderationSet Local = new ConsiderationSet { Name = "S", Local = true };
        public readonly ConsiderationSet Shared = new ConsiderationSet { Name = "G" };
        public readonly StateSet Set = new StateSet { Name = "Main" };

        public Bench() {
            Design.ConsiderationSets.Add(Local);
            Design.ConsiderationSets.Add(Shared);
            Design.StateSets.Add(Set);
        }

        // v1's default curve drawn as a straight line from (0, 0) to (1, 1).
        public static Curve Line => Curve.Through(new CurveKey(0f, 0f, 1f, 1f), new CurveKey(1f, 1f, 1f, 1f));

        public Consideration Value(string name, float initial) {
            Consideration consideration = new Consideration { Name = name, InitialValue = initial };
            Local.Considerations.Add(consideration);
            return consideration;
        }

        // A state scoring its consideration's value: one evaluator of weight 1 over the line.
        public State State(string name, string consideration = null, StateSet set = null) {
            State state = new State { Name = name };
            if (consideration != null) {
                state.Evaluators.Add(new Evaluator { Name = "e", Consideration = $"S | {consideration}", Weight = 1f, Curve = Line });
            }
            (set ?? Set).States.Add(state);
            return state;
        }

        public Agent Agent(ulong seed = 1, Globals globals = null, double now = 0.0) => new Agent(Design, globals, new Rng(seed), now);
    }

    // A node that returns what it is told, tick by tick (the last for ever), and counts its life.
    internal sealed class Script : ActionNode {
        private readonly NodeState[] _results;
        private int _at;
        public int Awakes;
        public int Enables;
        public int Disables;
        public int Updates;

        public Script(params NodeState[] results) {
            _results = results;
        }

        protected override void OnAwake() => Awakes++;

        protected override void OnEnable() => Enables++;

        protected override void OnDisable() => Disables++;

        protected override NodeState OnUpdate() {
            Updates++;
            NodeState result = _results[Math.Min(_at, _results.Length - 1)];
            _at++;
            return result;
        }
    }

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

        [Test]
        public void KeysAreAnAnimationCurve() {
            Assert.That(Curve.Through().Evaluate(0.5f), Is.EqualTo(0f), "no keys");
            Assert.That(Curve.Through(new CurveKey(0.3f, 0.7f)).Evaluate(0.9f), Is.EqualTo(0.7f), "one key");
            Curve tall = Curve.Through(new CurveKey(0f, -1f, 4f, 4f), new CurveKey(1f, 3f, 4f, 4f));
            Assert.That(tall.Evaluate(0f), Is.EqualTo(-1f), "not clamped to 0..1");
            Assert.That(tall.Evaluate(0.75f), Is.EqualTo(2f).Within(1e-6f));
            Curve inner = Curve.Through(new CurveKey(0.2f, 0.4f), new CurveKey(0.8f, 0.6f));
            Assert.That(inner.Evaluate(0f), Is.EqualTo(0.4f), "before the first key, its value");
            Assert.That(inner.Evaluate(1f), Is.EqualTo(0.6f), "after the last key, its value");
            Curve stepped = Curve.Through(new CurveKey(0f, 0.2f, 0f, float.PositiveInfinity), new CurveKey(0.5f, 0.9f, float.PositiveInfinity, 0f));
            Assert.That(stepped.Evaluate(0.49f), Is.EqualTo(0.2f), "an infinite tangent holds the left key");
            Assert.That(stepped.Evaluate(0.5f), Is.EqualTo(0.9f));
        }
    }

    // Each case worked by hand from v1's State.GetScore, Evaluator.ScoreConsideration and Precondition.ConditionMet.
    public sealed class ScoringTests {
        private static Evaluator Over(string consideration, float weight, float min = 0f, float max = 1f) =>
            new Evaluator { Name = consideration, Consideration = $"S | {consideration}", Weight = weight, Curve = Bench.Line, Min = min, Max = max };

        // Weight 2, base 0.1, evaluators 0.5 at weight 0.75 and 0.2 at weight 0.25.
        private static State Worked(Bench bench, float firstWeight = 0.75f, float secondWeight = 0.25f) {
            bench.Value("a", 0.5f);
            bench.Value("b", 0.2f);
            State state = bench.State("Worked");
            state.Weight = 2f;
            state.BaseScore = 0.1f;
            state.ExecutionFactor = 1.5f;
            state.Evaluators.Add(Over("a", firstWeight));
            state.Evaluators.Add(Over("b", secondWeight));
            return state;
        }

        [Test]
        public void TheFormula() {
            Bench bench = new Bench();
            State state = Worked(bench);
            // 2 * (0.1 + 0.5 * 0.75 / 1 + 0.2 * 0.25 / 1) = 2 * 0.525
            Assert.That(bench.Agent().Score(state, false), Is.EqualTo(1.05f).Within(1e-6f));
            Assert.That(state.LastScore, Is.EqualTo(1.05f).Within(1e-6f));
            Assert.That(state.Evaluators[0].LastScore, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(state.Evaluators[1].LastScore, Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void EvaluatorWeightsCountByTheirShare() {
            Bench bench = new Bench();
            State state = Worked(bench, 3f, 1f);
            // 3 / 4 and 1 / 4: the same shares as 0.75 and 0.25.
            Assert.That(bench.Agent().Score(state, false), Is.EqualTo(1.05f).Within(1e-6f));
        }

        [Test]
        public void TheRunningStateIsMultipliedByItsExecutionFactor() {
            Bench bench = new Bench();
            State state = Worked(bench);
            // 1.05 * 1.5
            Assert.That(bench.Agent().Score(state, true), Is.EqualTo(1.575f).Within(1e-6f));
        }

        [Test]
        public void TheScoreIsHeldToItsMinAndMaxWhenSet() {
            Bench bench = new Bench();
            State state = Worked(bench);
            Agent agent = bench.Agent();
            state.MinScore = 2f;
            state.MaxScore = 1f;
            Assert.That(agent.Score(state, false), Is.EqualTo(1.05f).Within(1e-6f), "neither is set");
            state.SetMinScore = true;
            Assert.That(agent.Score(state, false), Is.EqualTo(2f), "1.05 lifted to the min");
            state.SetMaxScore = true;
            Assert.That(agent.Score(state, false), Is.EqualTo(1f), "the min first, then the max: 2 cut to 1");
            state.SetMinScore = false;
            Assert.That(agent.Score(state, false), Is.EqualTo(1f), "1.05 cut to the max");
            Assert.That(agent.Score(state, true), Is.EqualTo(1f), "the execution factor comes before the clamp: 1.575 cut to 1");
        }

        [Test]
        public void WithoutEvaluatorWeightOnlyTheBaseScoreCounts() {
            Bench bench = new Bench();
            State state = Worked(bench, 0f, 0f);
            // 2 * 0.1; the evaluators are not even scored.
            Assert.That(bench.Agent().Score(state, false), Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(state.Evaluators[0].LastScore, Is.EqualTo(0f));
            Bench bare = new Bench();
            State none = bare.State("None");
            none.BaseScore = 0.4f;
            none.Weight = 0.5f;
            Assert.That(bare.Agent().Score(none, false), Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void AnEvaluatorNormalizesBetweenItsMinAndMax() {
            Bench bench = new Bench();
            Consideration health = bench.Value("health", 25f);
            State state = bench.State("State");
            state.Evaluators.Add(Over("health", 1f, 10f, 30f));
            Agent agent = bench.Agent();
            // (25 - 10) / (30 - 10)
            Assert.That(agent.Score(state, false), Is.EqualTo(0.75f).Within(1e-6f));
            health.Value = 40f;
            Assert.That(agent.Score(state, false), Is.EqualTo(1f), "above the max");
            health.Value = 5f;
            Assert.That(agent.Score(state, false), Is.EqualTo(0f), "below the min");
        }

        [Test]
        public void AMaxNotAboveTheMinIsAStepAtTheMin() {
            Bench bench = new Bench();
            Consideration value = bench.Value("v", 10f);
            State state = bench.State("State");
            state.Evaluators.Add(Over("v", 1f, 10f, 10f));
            Agent agent = bench.Agent();
            // v1 divides by the smallest float instead of by 0: (10 - 10) / epsilon = 0.
            Assert.That(agent.Score(state, false), Is.EqualTo(0f));
            value.Value = 10.5f;
            Assert.That(agent.Score(state, false), Is.EqualTo(1f));
            state.Evaluators[0].Max = 2f;
            Assert.That(agent.Score(state, false), Is.EqualTo(1f), "a max below the min, the same");
        }

        [Test]
        public void AnEvaluatorWithoutItsConsiderationScoresZero() {
            Bench bench = new Bench();
            bench.Value("a", 1f);
            State state = bench.State("State");
            state.Evaluators.Add(Over("a", 1f));
            state.Evaluators.Add(Over("gone", 1f));
            state.Evaluators.Add(new Evaluator { Name = "bad", Consideration = "no separator", Weight = 2f, Curve = Bench.Line });
            // (1 * 1 + 0 * 1 + 0 * 2) / 4
            Assert.That(bench.Agent().Score(state, false), Is.EqualTo(0.25f).Within(1e-6f));
        }

        [Test]
        public void AnInactiveStateCannotBeChosenAndKeepsItsLastScore() {
            Bench bench = new Bench();
            State state = Worked(bench);
            Agent agent = bench.Agent();
            agent.Score(state, false);
            state.Active = false;
            Assert.That(agent.Score(state, false), Is.EqualTo(-1f));
            Assert.That(state.LastScore, Is.EqualTo(1.05f).Within(1e-6f));
            Assert.That(state.LastAvailable, Is.False);
        }

        [Test]
        public void AnUnmetPreconditionGatesTheStateAndZeroesItsLastScore() {
            Bench bench = new Bench();
            State state = Worked(bench);
            Precondition met = new Precondition { Name = "met", Consideration = "S | a", Comparator = Comparator.LessThan, Value = 1f };
            Precondition gate = new Precondition { Name = "gate", Consideration = "S | a", Comparator = Comparator.GreaterThan, Value = 0.5f };
            state.Preconditions.Add(met);
            state.Preconditions.Add(gate);
            Agent agent = bench.Agent();
            Assert.That(agent.Score(state, false), Is.EqualTo(-1f), "0.5 is not above 0.5");
            Assert.That(state.LastScore, Is.EqualTo(0f));
            Assert.That(met.LastConditionMet, Is.True);
            Assert.That(gate.LastConditionMet, Is.False);
            gate.Comparator = Comparator.GreaterThanOrEqualTo;
            Assert.That(agent.Score(state, false), Is.EqualTo(1.05f).Within(1e-6f));
            gate.Consideration = "S | gone";
            Assert.That(agent.Score(state, false), Is.EqualTo(1.05f).Within(1e-6f), "bound once: a later rename is not looked up");
        }

        [Test]
        public void APreconditionWithoutItsConsiderationIsNotMet() {
            Bench bench = new Bench();
            State state = bench.State("State");
            state.BaseScore = 1f;
            state.Preconditions.Add(new Precondition { Name = "gone", Consideration = "S | gone", Comparator = Comparator.LessThan, Value = 1f });
            Assert.That(bench.Agent().Score(state, false), Is.EqualTo(-1f));
        }

        [TestCase(Comparator.GreaterThan, 2f, 1f, true)]
        [TestCase(Comparator.GreaterThan, 1f, 1f, false)]
        [TestCase(Comparator.LessThan, 1f, 2f, true)]
        [TestCase(Comparator.LessThan, 1f, 1f, false)]
        [TestCase(Comparator.GreaterThanOrEqualTo, 1f, 1f, true)]
        [TestCase(Comparator.GreaterThanOrEqualTo, 0.9f, 1f, false)]
        [TestCase(Comparator.LessThanOrEqualTo, 1f, 1f, true)]
        [TestCase(Comparator.LessThanOrEqualTo, 1.1f, 1f, false)]
        // Equality is of the whole numbers both sides round to, halves to the even one.
        [TestCase(Comparator.EqualTo, 2.4f, 1.6f, true)]
        [TestCase(Comparator.EqualTo, 2.5f, 2f, true)]
        [TestCase(Comparator.EqualTo, 3.5f, 4f, true)]
        [TestCase(Comparator.EqualTo, 2.6f, 2f, false)]
        [TestCase(Comparator.NotEqualTo, 2.6f, 2f, true)]
        [TestCase(Comparator.NotEqualTo, 2.4f, 2f, false)]
        public void Comparators(Comparator comparator, float left, float right, bool holds) {
            Assert.That(Comparing.Holds(left, comparator, right), Is.EqualTo(holds));
        }
    }

    // v1's StateSet.DecideNextState and UtilityDesigner.Evaluate.
    public sealed class DecisionTests {
        private static Bench Two(float a, float b, out State first, out State second) {
            Bench bench = new Bench();
            bench.Value("a", a);
            bench.Value("b", b);
            first = bench.State("A", "a");
            second = bench.State("B", "b");
            return bench;
        }

        [Test]
        public void TheBestStateIsChosen() {
            Agent agent = Two(0.3f, 0.6f, out _, out State b).Agent();
            agent.Evaluate(0.0);
            Assert.That(agent.ExecutingState, Is.SameAs(b));
            Assert.That(b.TreeState, Is.EqualTo(NodeState.Running));
        }

        [Test]
        public void ATieGoesToTheFirstInTheSet() {
            Agent agent = Two(0.5f, 0.5f, out State a, out _).Agent();
            agent.Evaluate(0.0);
            Assert.That(agent.ExecutingState, Is.SameAs(a));
        }

        [Test]
        public void AFailChancePassesOverTheBestAndTheNextIsChecked() {
            Agent agent = Two(0.9f, 0.4f, out State a, out State b).Agent();
            a.FailChance = 1f;
            agent.Evaluate(0.0);
            Assert.That(agent.ExecutingState, Is.SameAs(b));
        }

        [Test]
        public void WhenEveryStateIsPassedOverTheLastOfThemIsTaken() {
            Agent agent = Two(0.9f, 0.4f, out State a, out State b).Agent();
            a.FailChance = 1f;
            b.FailChance = 1f;
            agent.Evaluate(0.0);
            Assert.That(agent.ExecutingState, Is.SameAs(b), "the lowest scoring, as v1");
        }

        [Test]
        public void TheRunningStateIsKeptWhenTheBetterOnesArePassedOver() {
            Bench bench = Two(0.2f, 0.4f, out State a, out State b);
            Agent agent = bench.Agent();
            agent.Evaluate(0.0);
            Assert.That(agent.ExecutingState, Is.SameAs(b));
            bench.Local.Find("a").Value = 0.9f;
            a.FailChance = 1f;
            agent.Evaluate(0.1);
            Assert.That(agent.ExecutingState, Is.SameAs(b));
            a.FailChance = 0f;
            agent.Evaluate(0.2);
            Assert.That(agent.ExecutingState, Is.SameAs(a));
            Assert.That(b.TreeState, Is.EqualTo(NodeState.Disabled), "its tree is terminated");
        }

        [Test]
        public void TheExecutionFactorKeepsTheRunningStateAhead() {
            Bench bench = Two(0.4f, 0.5f, out _, out State b);
            b.ExecutionFactor = 2f;
            Agent agent = bench.Agent();
            agent.Evaluate(0.0);
            bench.Local.Find("a").Value = 0.9f;
            agent.Evaluate(0.1);
            Assert.That(agent.ExecutingState, Is.SameAs(b), "0.5 * 2 against 0.9");
        }

        [Test]
        public void OneRandomNumberIsDrawnForEachStateCheckedBeforeTheRunningOne() {
            Rng twin = new Rng(7);
            Bench bench = Two(0.9f, 0.4f, out State a, out _);
            Agent agent = new Agent(bench.Design, null, new Rng(7));
            agent.Evaluate(0.0);
            agent.Evaluate(0.1);
            agent.Evaluate(0.2);
            Assert.That(agent.ExecutingState, Is.SameAs(a));
            twin.NextFloat();
            Assert.That(agent.Random.Next(), Is.EqualTo(twin.Next()), "one for choosing A, none while A runs on top");
        }

        [Test]
        public void AScoreBelowZeroCannotBeChosenUnlessTheMinLiftsIt() {
            Bench bench = Two(0.5f, 0.5f, out State a, out State b);
            a.Weight = -1f;
            b.Active = false;
            Agent agent = bench.Agent();
            agent.Evaluate(0.0);
            Assert.That(agent.ExecutingState, Is.Null);
            Assert.That(a.LastScore, Is.EqualTo(-0.5f).Within(1e-6f));
            a.SetMinScore = true;
            agent.Evaluate(0.1);
            Assert.That(agent.ExecutingState, Is.SameAs(a), "a score of 0 can be chosen");
        }

        [Test]
        public void ALockKeepsTheStateAndUnlockingEvaluatesAtOnce() {
            Bench bench = Two(0.2f, 0.4f, out State a, out State b);
            Agent agent = bench.Agent();
            agent.Evaluate(0.0);
            agent.StateLocked = true;
            bench.Local.Find("a").Value = 0.9f;
            agent.Evaluate(0.1);
            Assert.That(agent.ExecutingState, Is.SameAs(b));
            agent.StateLocked = false;
            Assert.That(agent.ExecutingState, Is.SameAs(a));
        }

        [Test]
        public void ALockedStateThatCanNoLongerBeChosenStaysWhileAnotherCanBe() {
            Bench bench = Two(0.2f, 0.4f, out _, out State b);
            Agent agent = bench.Agent();
            agent.Evaluate(0.0);
            agent.StateLocked = true;
            b.Active = false;
            agent.Evaluate(0.1);
            Assert.That(agent.ExecutingState, Is.SameAs(b));
        }

        [Test]
        public void WhenNothingCanBeChosenTheStateEndsEvenLocked() {
            Bench bench = Two(0.2f, 0.4f, out State a, out State b);
            Agent agent = bench.Agent();
            agent.Evaluate(0.0);
            agent.StateLocked = true;
            a.Active = false;
            b.Active = false;
            agent.Evaluate(0.1);
            Assert.That(agent.ExecutingState, Is.Null);
            Assert.That(b.TreeState, Is.EqualTo(NodeState.Disabled));
        }

        [Test]
        public void AFinishedTreeLeavesTheAgentIdleUntilAnotherStateHasRun() {
            Bench bench = Two(0.9f, 0.4f, out State a, out State b);
            Script once = new Script(NodeState.Success);
            a.Tree = once;
            b.Tree = new Script(NodeState.Running);
            Agent agent = bench.Agent();
            for (int i = 0; i < 5; i++) {
                agent.Evaluate(i);
                agent.Execute(i);
            }
            Assert.That(agent.ExecutingState, Is.SameAs(a));
            Assert.That(a.TreeState, Is.EqualTo(NodeState.Success));
            Assert.That(once.Updates, Is.EqualTo(1), "the state stays chosen, its tree is not started again");
            bench.Local.Find("a").Value = 0.1f;
            agent.Evaluate(5.0);
            bench.Local.Find("a").Value = 0.9f;
            agent.Evaluate(6.0);
            agent.Execute(6.0);
            Assert.That(once.Updates, Is.EqualTo(2), "chosen anew, it runs again");
            Assert.That(once.Awakes, Is.EqualTo(1));
        }

        [Test]
        public void SwitchingTerminatesTheRunningNodes() {
            Bench bench = Two(0.9f, 0.4f, out State a, out _);
            Script busy = new Script(NodeState.Running);
            a.Tree = new Sequence { Nodes = { busy } };
            Agent agent = bench.Agent();
            agent.Evaluate(0.0);
            agent.Execute(0.0);
            bench.Local.Find("a").Value = 0.1f;
            agent.Evaluate(0.1);
            Assert.That(busy.Disables, Is.EqualTo(1));
            Assert.That(busy.State, Is.EqualTo(NodeState.Disabled));
            agent.Execute(0.1);
            Assert.That(busy.Updates, Is.EqualTo(1));
        }

        [Test]
        public void AnAgentNeedsItsOwnDesignWithAStateSet() {
            Bench bench = Two(0.9f, 0.4f, out _, out _);
            bench.Agent();
            Assert.Throws<ArgumentException>(() => bench.Agent());
            Assert.Throws<ArgumentException>(() => new Agent(new Design(), null, new Rng(1)));
        }
    }

    public sealed class ConsiderationTests {
        [Test]
        public void AValueIsHeldToItsLimitsWhenSet() {
            Consideration consideration = new Consideration { MinValue = 1f, MaxValue = 3f };
            consideration.Value = -5f;
            Assert.That(consideration.Value, Is.EqualTo(-5f));
            consideration.SetMinValue = true;
            consideration.SetMaxValue = true;
            consideration.Value = -5f;
            Assert.That(consideration.Value, Is.EqualTo(1f));
            consideration.Value = 9f;
            Assert.That(consideration.Value, Is.EqualTo(3f));
        }

        [Test]
        public void ItChangesEachSecondOfTheCallersClock() {
            Bench bench = new Bench();
            Consideration hunger = bench.Value("hunger", 1f);
            hunger.ChangePerSecond = 0.5f;
            hunger.SetMaxValue = true;
            hunger.MaxValue = 2.5f;
            bench.State("State");
            Agent agent = bench.Agent(now: 10.0);
            Assert.That(hunger.Value, Is.EqualTo(1f));
            agent.Evaluate(12.0);
            Assert.That(hunger.Value, Is.EqualTo(2f).Within(1e-6f));
            agent.Evaluate(12.0);
            Assert.That(hunger.Value, Is.EqualTo(2f).Within(1e-6f), "no time, no change");
            agent.Evaluate(20.0);
            Assert.That(hunger.Value, Is.EqualTo(2.5f));
        }

        [Test]
        public void GlobalSetsAreSharedAndLocalOnesAreEachAgentsOwn() {
            Bench first = new Bench();
            Bench second = new Bench();
            foreach (Bench bench in new[] { first, second }) {
                bench.Value("own", 1f);
                bench.Shared.Considerations.Add(new Consideration { Name = "shared", InitialValue = 5f, ChangePerSecond = 1f });
                bench.State("State");
            }
            Globals globals = new Globals();
            Agent a = first.Agent(globals: globals);
            Agent b = second.Agent(globals: globals);
            a.SetConsideration("S", "own", 2f);
            a.SetConsideration("G", "shared", 7f);
            Assert.That(b.GetConsideration("S", "own"), Is.EqualTo(1f));
            Assert.That(b.GetConsideration("G", "shared"), Is.EqualTo(7f));
            Assert.That(globals.Find("G", "shared").Value, Is.EqualTo(7f));
            a.Evaluate(1.0);
            b.Evaluate(1.0);
            Assert.That(b.GetConsideration("G", "shared"), Is.EqualTo(8f).Within(1e-6f), "a second, counted once");
        }

        [Test]
        public void AStartingAgentSetsTheSharedConsiderationsBackAsV1() {
            Globals globals = new Globals();
            Bench first = new Bench();
            first.Shared.Considerations.Add(new Consideration { Name = "shared", InitialValue = 5f });
            first.State("State");
            Agent a = first.Agent(globals: globals);
            a.SetConsideration("G", "shared", 9f);
            Bench second = new Bench();
            second.Shared.Considerations.Add(new Consideration { Name = "shared", InitialValue = 5f });
            second.State("State");
            second.Agent(globals: globals);
            Assert.That(a.GetConsideration("G", "shared"), Is.EqualTo(5f));
        }

        [Test]
        public void AConsiderationThatIsNotThereReadsZeroAndCannotBeSet() {
            Bench bench = new Bench();
            bench.Value("a", 1f);
            bench.State("State");
            Agent agent = bench.Agent();
            Assert.That(agent.Bind("S", "gone"), Is.EqualTo(Agent.NoConsideration));
            Assert.That(agent.Bind("Gone", "a"), Is.EqualTo(Agent.NoSet));
            Assert.That(agent.GetConsideration("S", "gone"), Is.EqualTo(0f));
            Assert.That(agent.SetConsideration("S", "gone", 1f), Is.False);
            Assert.That(agent.ChangeConsideration("Gone", "a", 1f), Is.False);
            Assert.That(agent.ChangeConsideration("S", "a", 1f), Is.True);
            Assert.That(agent.GetConsideration("S", "a"), Is.EqualTo(2f));
        }
    }

    // Every engine-free node of v1, its success and its failure.
    public sealed class NodeTests {
        private const NodeState R = NodeState.Running;
        private const NodeState S = NodeState.Success;
        private const NodeState F = NodeState.Failure;

        // An agent whose one state runs the tree; "v" is a consideration at 2.
        private static Agent Run(Node tree, out State state, ulong seed = 1) {
            Bench bench = new Bench();
            bench.Value("v", 2f);
            state = bench.State("State");
            state.BaseScore = 1f;
            state.Tree = tree;
            bench.Design.StateSets.Add(new StateSet { Name = "Other", States = { new State { Name = "Elsewhere", BaseScore = 1f } } });
            Agent agent = bench.Agent(seed);
            agent.Evaluate(0.0);
            return agent;
        }

        // The tree's state after each of the ticks, a second apart from 0.
        private static NodeState[] Ticks(Node tree, int ticks, ulong seed = 1) {
            Agent agent = Run(tree, out State state, seed);
            NodeState[] states = new NodeState[ticks];
            for (int i = 0; i < ticks; i++) {
                agent.Execute(i);
                states[i] = state.TreeState;
            }
            return states;
        }

        private static T With<T>(T composite, params Node[] children) where T : CompositeNode {
            composite.Nodes.AddRange(children);
            return composite;
        }

        [Test]
        public void SequenceRunsOneChildATickUntilOneFails() {
            Assert.That(Ticks(With(new Sequence(), new Script(S), new Script(R, S), new Script(S)), 4), Is.EqualTo(new[] { R, R, R, S }));
            Script never = new Script(S);
            Assert.That(Ticks(With(new Sequence(), new Script(S), new Script(F), never), 3), Is.EqualTo(new[] { R, F, F }));
            Assert.That(never.Updates, Is.EqualTo(0));
            Assert.That(Ticks(new Sequence(), 1), Is.EqualTo(new[] { F }), "no children (v1 throws)");
        }

        [Test]
        public void SelectorRunsOneChildATickUntilOneSucceeds() {
            Script never = new Script(S);
            Assert.That(Ticks(With(new Selector(), new Script(F), new Script(R, S), never), 3), Is.EqualTo(new[] { R, R, S }));
            Assert.That(never.Updates, Is.EqualTo(0));
            Assert.That(Ticks(With(new Selector(), new Script(F), new Script(F)), 2), Is.EqualTo(new[] { R, F }));
        }

        private static string Order(CompositeNode composite, NodeState each, ulong seed) {
            Script[] children = { new Script(each), new Script(each), new Script(each), new Script(each) };
            With(composite, children);
            Agent agent = Run(composite, out _, seed);
            string order = "";
            for (int tick = 0; tick < 4; tick++) {
                agent.Execute(tick);
                order += Array.FindIndex(children, child => child.Updates == 1 && !order.Contains(Array.IndexOf(children, child).ToString()));
            }
            return order;
        }

        [Test]
        public void RandomSequenceRunsEveryChildOnceInASeededOrder() {
            string order = Order(new RandomSequence(), S, 3);
            Assert.That(order.OrderBy(c => c), Is.EqualTo("0123"));
            Assert.That(Order(new RandomSequence(), S, 3), Is.EqualTo(order), "the same seed, the same order");
            Assert.That(Enumerable.Range(1, 20).Select(seed => Order(new RandomSequence(), S, (ulong)seed)).Distinct().Count(), Is.GreaterThan(1));
            Assert.That(Ticks(With(new RandomSequence(), new Script(S), new Script(S)), 2), Is.EqualTo(new[] { R, S }));
            Assert.That(Ticks(With(new RandomSequence(), new Script(F), new Script(F)), 1), Is.EqualTo(new[] { F }));
        }

        [Test]
        public void RandomSelectorTriesEveryChildInASeededOrder() {
            string order = Order(new RandomSelector(), F, 5);
            Assert.That(order.OrderBy(c => c), Is.EqualTo("0123"));
            Assert.That(Order(new RandomSelector(), F, 5), Is.EqualTo(order));
            Assert.That(Ticks(With(new RandomSelector(), new Script(F), new Script(F)), 2), Is.EqualTo(new[] { R, F }));
            Assert.That(Ticks(With(new RandomSelector(), new Script(S), new Script(S)), 1), Is.EqualTo(new[] { S }));
        }

        [Test]
        public void ParallelRunsEveryChildEachTickAndFailsWithTheFirstFailure() {
            Script slow = new Script(R, R, S);
            Script quick = new Script(S);
            Assert.That(Ticks(With(new Parallel(), slow, quick), 3), Is.EqualTo(new[] { R, R, S }));
            Assert.That(quick.Updates, Is.EqualTo(1), "a child that ended is not run again");

            Script running = new Script(R);
            Script late = new Script(S);
            Assert.That(Ticks(With(new Parallel(), running, new Script(R, F), late), 2), Is.EqualTo(new[] { R, F }));
            Assert.That(running.Disables, Is.EqualTo(1), "the running child is terminated");
            Assert.That(running.State, Is.EqualTo(NodeState.Disabled));
            Assert.That(late.Updates, Is.EqualTo(1), "a child after the failed one is not run that tick");
            Assert.That(Ticks(new Parallel(), 1), Is.EqualTo(new[] { S }), "no children");
        }

        [Test]
        public void TheDecorators() {
            Assert.That(Ticks(new Invert { Child = new Script(R, S) }, 2), Is.EqualTo(new[] { R, F }));
            Assert.That(Ticks(new Invert { Child = new Script(F) }, 1), Is.EqualTo(new[] { S }));
            Assert.That(Ticks(new Success { Child = new Script(R, F) }, 2), Is.EqualTo(new[] { R, S }));
            Assert.That(Ticks(new Success { Child = new Script(S) }, 1), Is.EqualTo(new[] { S }));
            Assert.That(Ticks(new Failure { Child = new Script(R, S) }, 2), Is.EqualTo(new[] { R, F }));
            Assert.That(Ticks(new Failure { Child = new Script(F) }, 1), Is.EqualTo(new[] { F }));
            Assert.That(Ticks(new Invert(), 1), Is.EqualTo(new[] { F }), "no child (v1 throws)");
        }

        [Test]
        public void RepeatRunsItsChildAgainWhateverItReturns() {
            Script child = new Script(S, F, R, S);
            Assert.That(Ticks(new Repeat { Child = child }, 4), Is.EqualTo(new[] { R, R, R, R }));
            Assert.That(child.Enables, Is.EqualTo(3), "started again after the success and after the failure");
            Assert.That(child.Awakes, Is.EqualTo(1));
        }

        [Test]
        public void WaitFixedSucceedsOnceItsDurationHasPassed() {
            Agent agent = Run(new WaitFixed { Duration = 2f }, out State state);
            agent.Execute(5.0);
            Assert.That(state.TreeState, Is.EqualTo(R));
            agent.Execute(6.9);
            Assert.That(state.TreeState, Is.EqualTo(R));
            agent.Execute(7.0);
            Assert.That(state.TreeState, Is.EqualTo(S));
        }

        [Test]
        public void WaitRandomWaitsASeededDurationBetweenItsMinAndMax() {
            Rng twin = new Rng(9);
            twin.NextFloat();
            float duration = 2f + 2f * twin.NextFloat();
            Agent agent = Run(new WaitRandom { MinDuration = 2f, MaxDuration = 4f }, out State state, 9);
            agent.Execute(1.0);
            agent.Execute(1.0 + duration - 0.01);
            Assert.That(state.TreeState, Is.EqualTo(R));
            agent.Execute(1.0 + duration + 0.01);
            Assert.That(state.TreeState, Is.EqualTo(S));
        }

        [Test]
        public void LockStateAndUnlockState() {
            Agent agent = Run(With(new Sequence(), new LockState(), new Script(R)), out State state);
            agent.Execute(0.0);
            Assert.That(agent.StateLocked, Is.True);
            Assert.That(state.Tree.Children[0].State, Is.EqualTo(S));
            agent = Run(new UnlockState(), out state);
            agent.StateLocked = true;
            agent.Execute(0.0);
            Assert.That(agent.StateLocked, Is.False);
            Assert.That(state.TreeState, Is.EqualTo(S));
        }

        [Test]
        public void UnlockingInsideATreeChoosesTheBetterStateAtOnce() {
            Bench bench = new Bench();
            bench.Value("a", 0.9f);
            bench.Value("b", 0.4f);
            State a = bench.State("A", "a");
            State b = bench.State("B", "b");
            a.Tree = With(new Sequence(), new LockState(), new UnlockState(), new Script(R));
            Agent agent = bench.Agent();
            agent.Evaluate(0.0);
            agent.Execute(0.0);
            bench.Local.Find("a").Value = 0.1f;
            agent.Evaluate(0.1);
            Assert.That(agent.ExecutingState, Is.SameAs(a), "locked");
            agent.Execute(0.1);
            Assert.That(agent.ExecutingState, Is.SameAs(b));
        }

        [Test]
        public void LockStateDurationLocksForItsDuration() {
            Agent agent = Run(new LockStateDuration { Duration = 2f }, out State state);
            agent.Execute(1.0);
            Assert.That(agent.StateLocked, Is.True);
            agent.Execute(2.9);
            Assert.That(state.TreeState, Is.EqualTo(R));
            agent.Execute(3.0);
            Assert.That(state.TreeState, Is.EqualTo(S));
            Assert.That(agent.StateLocked, Is.False);
        }

        [Test]
        public void LockStateConsiderationLocksUntilTheValueIsReached() {
            Agent agent = Run(new LockStateConsideration { ConsiderationSetName = "S", ConsiderationName = "v", RequiredValue = 5f }, out State state);
            agent.Execute(0.0);
            Assert.That(agent.StateLocked, Is.True);
            Assert.That(state.TreeState, Is.EqualTo(R), "2, rising to 5");
            agent.SetConsideration("S", "v", 5f);
            agent.Execute(1.0);
            Assert.That(state.TreeState, Is.EqualTo(S));
            Assert.That(agent.StateLocked, Is.False);

            agent = Run(new LockStateConsideration { ConsiderationSetName = "S", ConsiderationName = "v", RequiredValue = 1f }, out state);
            agent.Execute(0.0);
            Assert.That(state.TreeState, Is.EqualTo(R), "2, falling to 1");
            agent.SetConsideration("S", "v", 0.5f);
            agent.Execute(1.0);
            Assert.That(state.TreeState, Is.EqualTo(S));
        }

        [Test]
        public void LockStateConsiderationCanAddTheValueItStartsFrom() {
            LockStateConsideration node = new LockStateConsideration { ConsiderationSetName = "S", ConsiderationName = "v", RequiredValue = 3f, AddConsiderationValue = true };
            Agent agent = Run(new Repeat { Child = node }, out _);
            agent.Execute(0.0);
            Assert.That(node.RequiredValue, Is.EqualTo(5f), "3 more than the 2 it starts from");
            agent.SetConsideration("S", "v", 5f);
            agent.Execute(1.0);
            Assert.That(node.State, Is.EqualTo(S));
            agent.Execute(2.0);
            Assert.That(node.RequiredValue, Is.EqualTo(10f), "as v1: the second run adds to the first run's target, not to 3");
        }

        [Test]
        public void LockStateConsiderationFailsWithoutItsSet() {
            Agent agent = Run(new LockStateConsideration { ConsiderationSetName = "Gone", ConsiderationName = "v" }, out State state);
            agent.Execute(0.0);
            Assert.That(state.TreeState, Is.EqualTo(F), "v1 throws");
            Assert.That(agent.StateLocked, Is.False);
        }

        [Test]
        public void ChangeStateSetSwitchesTheSetAtTheNextEvaluation() {
            Agent agent = Run(new ChangeStateSet { StateSetIndex = 1 }, out State state);
            agent.Execute(0.0);
            Assert.That(state.TreeState, Is.EqualTo(S));
            Assert.That(agent.RunningStateSet.Name, Is.EqualTo("Other"));
            Assert.That(agent.ExecutingState, Is.SameAs(state), "until the next evaluation");
            agent.Evaluate(0.1);
            Assert.That(agent.ExecutingState.Name, Is.EqualTo("Elsewhere"));

            agent = Run(new ChangeStateSet { StateSetIndex = 2 }, out state);
            agent.Execute(0.0);
            Assert.That(state.TreeState, Is.EqualTo(F));
            Assert.That(agent.RunningStateSet.Name, Is.EqualTo("Main"));
        }

        [Test]
        public void SetConsiderationAndChangeConsideration() {
            Agent agent = Run(new SetConsideration { ConsiderationSetName = "S", ConsiderationName = "v", NewValue = 7f }, out State state);
            agent.Execute(0.0);
            Assert.That(state.TreeState, Is.EqualTo(S));
            Assert.That(agent.GetConsideration("S", "v"), Is.EqualTo(7f));
            Assert.That(Ticks(new SetConsideration { ConsiderationSetName = "S", ConsiderationName = "gone" }, 1), Is.EqualTo(new[] { F }));

            agent = Run(new ChangeConsideration { ConsiderationSetName = "S", ConsiderationName = "v", Amount = -0.5f }, out state);
            agent.Execute(0.0);
            Assert.That(state.TreeState, Is.EqualTo(S));
            Assert.That(agent.GetConsideration("S", "v"), Is.EqualTo(1.5f));
            Assert.That(Ticks(new ChangeConsideration { ConsiderationSetName = "Gone", ConsiderationName = "v" }, 1), Is.EqualTo(new[] { F }));
        }

        [TestCase(Comparator.GreaterThan, 1f, true)]
        [TestCase(Comparator.GreaterThan, 2f, false)]
        [TestCase(Comparator.LessThan, 3f, true)]
        [TestCase(Comparator.LessThan, 2f, false)]
        [TestCase(Comparator.GreaterThanOrEqualTo, 2f, true)]
        [TestCase(Comparator.GreaterThanOrEqualTo, 2.1f, false)]
        [TestCase(Comparator.LessThanOrEqualTo, 2f, true)]
        [TestCase(Comparator.LessThanOrEqualTo, 1.9f, false)]
        [TestCase(Comparator.EqualTo, 2.4f, true)]
        [TestCase(Comparator.EqualTo, 3f, false)]
        [TestCase(Comparator.NotEqualTo, 3f, true)]
        [TestCase(Comparator.NotEqualTo, 1.6f, false)]
        public void CompareConsiderationComparesTheValueTwo(Comparator comparator, float value, bool holds) {
            CompareConsideration node = new CompareConsideration { ConsiderationSetName = "S", ConsiderationName = "v", Comparator = comparator, Value = value };
            Assert.That(Ticks(node, 1), Is.EqualTo(new[] { holds ? S : F }));
        }

        [Test]
        public void CompareConsiderationWithoutItsConsideration() {
            CompareConsideration noSet = new CompareConsideration { ConsiderationSetName = "Gone", ConsiderationName = "v", Comparator = Comparator.LessThan, Value = 1f };
            Assert.That(Ticks(noSet, 1), Is.EqualTo(new[] { F }), "no such set: failure");
            CompareConsideration noValue = new CompareConsideration { ConsiderationSetName = "S", ConsiderationName = "gone", Comparator = Comparator.LessThan, Value = 1f };
            Assert.That(Ticks(noValue, 1), Is.EqualTo(new[] { S }), "as v1: a consideration the set does not have counts as 0");
        }

        [Test]
        public void TimeCheckComparesTheCallersClock() {
            Agent agent = Run(new Repeat { Child = new TimeCheck { Comparator = Comparator.GreaterThan, Time = 3f } }, out State state);
            agent.Execute(3.0);
            Assert.That(state.Tree.Children[0].State, Is.EqualTo(F));
            agent.Execute(3.5);
            Assert.That(state.Tree.Children[0].State, Is.EqualTo(S));
            agent = Run(new TimeCheck { Comparator = Comparator.EqualTo, Time = 2f }, out state);
            agent.Execute(2.5);
            Assert.That(state.TreeState, Is.EqualTo(S), "2.5 rounds to 2");
        }

        [Test]
        public void ANodeAwakesOnceAndIsEnabledEachRun() {
            Script child = new Script(R, S, R, S);
            Agent agent = Run(new Repeat { Child = child }, out _);
            for (int i = 0; i < 4; i++) {
                agent.Execute(i);
            }
            Assert.That(child.Awakes, Is.EqualTo(1));
            Assert.That(child.Enables, Is.EqualTo(2));
            Assert.That(child.Disables, Is.EqualTo(2));
        }
    }

    public sealed class DesignTests {
        [Test]
        public void ADesignSurvivesJson() {
            string json = Designs.ToJson(Villager.Design());
            Design back = Designs.FromJson(json);
            Assert.That(Designs.ToJson(back), Is.EqualTo(json));
            Assert.That(back.StateSets[0].States.Select(state => state.Name), Is.EqualTo(new[] { "Eat", "Sleep", "Work", "Wander" }));
            Assert.That(back.ConsiderationSets[0].Local, Is.True);
            Assert.That(back.ConsiderationSets[1].Considerations[0].SetMaxValue, Is.False);
        }

        [Test]
        public void ACopyDecidesAsItsOriginal() {
            List<string> Day(Design design) {
                Villager villager = new Villager(design, new Globals(), 11);
                for (int i = 0; i < 2000; i++) {
                    villager.Tick(i * 0.1);
                }
                return villager.Diary;
            }
            Design design = Villager.Design();
            Assert.That(Day(Designs.Copy(design)), Is.EqualTo(Day(design)));
        }

        [Test]
        public void NodeSettingsAndSteppedKeysSurviveJson() {
            Bench bench = new Bench();
            State state = bench.State("State");
            state.Evaluators.Add(new Evaluator {
                Name = "e", Consideration = "S | v", Weight = 1f,
                Curve = Curve.Through(new CurveKey(0f, 0.2f, 0f, float.PositiveInfinity), new CurveKey(1f, 0.9f, float.NegativeInfinity, 0f)),
            });
            state.Tree = new Invert {
                Id = "top", Notes = "why", X = 10f, Y = -20f,
                Child = new Sequence {
                    Nodes = {
                        new CompareConsideration { ConsiderationSetName = "S", ConsiderationName = "v", Comparator = Comparator.LessThanOrEqualTo, Value = 0.25f },
                        new ChangeStateSet { StateSetIndex = 3 },
                        new LockStateConsideration { AddConsiderationValue = true },
                    },
                },
            };
            Design back = Designs.Copy(bench.Design);
            Invert top = (Invert)back.StateSets[0].States[0].Tree;
            Assert.That((top.Id, top.Notes, top.X, top.Y), Is.EqualTo(("top", "why", 10f, -20f)));
            CompareConsideration compare = (CompareConsideration)top.Child.Children[0];
            Assert.That((compare.ConsiderationSetName, compare.ConsiderationName, compare.Comparator, compare.Value),
                Is.EqualTo(("S", "v", Comparator.LessThanOrEqualTo, 0.25f)));
            Assert.That(((ChangeStateSet)top.Child.Children[1]).StateSetIndex, Is.EqualTo(3));
            Assert.That(((LockStateConsideration)top.Child.Children[2]).AddConsiderationValue, Is.True);
            CurveKey[] keys = back.StateSets[0].States[0].Evaluators[0].Curve.Keys;
            Assert.That(keys[0].OutTangent, Is.EqualTo(float.PositiveInfinity));
            Assert.That(keys[1].InTangent, Is.EqualTo(float.NegativeInfinity));
        }

        [Test]
        public void AnotherFormatOrAnUnknownNodeFailsLoudly() {
            Assert.Throws<FormatException>(() => Designs.FromJson("{\"format\": 1, \"name\": \"The first core's\"}"));
            Assert.Throws<FormatException>(() => Designs.FromJson("{\"format\": 99}"));
            Assert.Throws<FormatException>(() => Designs.FromJson(
                "{\"format\": 2, \"stateSets\": [{\"name\": \"Main\", \"states\": [{\"name\": \"State\", \"tree\": {\"type\": \"Teleport\"}}]}]}"));
        }

        [Test]
        public void EveryNumberIsAParameter() {
            Design design = Villager.Design();
            List<Parameter> parameters = Designs.Parameters(design);
            string[] ids = parameters.Select(parameter => parameter.Id).ToArray();
            Assert.That(ids, Is.Unique);
            Assert.That(ids, Is.SupersetOf(new[] {
                "considerations.Needs.Hunger.initial", "considerations.Needs.Hunger.max", "considerations.Needs.Hunger.changePerSecond",
                "states.Day.Eat.weight", "states.Day.Eat.executionFactor", "states.Day.Eat.baseScore", "states.Day.Wander.failChance",
                "states.Day.Eat.Food left.value", "states.Day.Eat.Hunger.weight", "states.Day.Eat.Hunger.min", "states.Day.Eat.Hunger.max",
                "states.Day.Eat.Hunger.m", "states.Day.Eat.tree.0.0.duration", "states.Day.Wander.tree.0.0.maxDuration",
            }));
            Assert.That(ids, Has.None.EqualTo("considerations.Village.Food.max"), "not set, not a parameter");

            float[] vector = Designs.Vector(parameters);
            Assert.That(vector, Is.All.InRange(0f, 1f));
            vector[Array.IndexOf(ids, "states.Day.Eat.weight")] = 0.5f;
            Designs.Apply(parameters, vector);
            Assert.That(design.StateSets[0].States[0].Weight, Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void ALayerSetsNumbersByIdAndDifferenceWritesOne() {
            Design design = Villager.Design();
            Designs.Layer(design, "{\"states.Day.Wander.failChance\": 0.75, \"states.Day.Eat.tree.0.0.duration\": 2.5}");
            Assert.That(design.StateSets[0].States[3].FailChance, Is.EqualTo(0.75f));
            Assert.That(((WaitFixed)design.StateSets[0].States[0].Tree.Children[0]).Duration, Is.EqualTo(2.5f));
            Assert.Throws<ArgumentException>(() => Designs.Layer(design, "{\"states.Day.Fly.weight\": 1}"));

            Design plain = Villager.Design();
            string layer = Designs.Difference(plain, design);
            Assert.That(((Dictionary<string, object>)Json.Parse(layer)).Keys, Is.EquivalentTo(new[] { "states.Day.Wander.failChance", "states.Day.Eat.tree.0.0.duration" }));
            Designs.Layer(plain, layer);
            Assert.That(Designs.ToJson(plain), Is.EqualTo(Designs.ToJson(design)));
        }

        [Test]
        public void ANodeWithAnIdIsNamedByItInALayer() {
            Bench bench = new Bench();
            bench.State("State").Tree = new WaitFixed { Id = "pause" };
            Designs.Layer(bench.Design, "{\"states.Main.State.tree.pause.duration\": 4}");
            Assert.That(((WaitFixed)bench.Set.States[0].Tree).Duration, Is.EqualTo(4f));
        }
    }

    public sealed class RunTests {
        [Test]
        public void TheSameSeedLivesTheSameDay() {
            List<string> day = Villager.Live(42);
            Assert.That(Villager.Live(42), Is.EqualTo(day));
            Assert.That(day.Select(line => line.Split(' ')[1]).Distinct(), Is.SupersetOf(new[] { "Eat", "Sleep", "Work", "Wander" }));
            Assert.That(Enumerable.Range(1, 10).Select(seed => string.Join(",", Villager.Live((ulong)seed))).Distinct().Count(), Is.GreaterThan(1));
        }

        [Test]
        public void TheVillagersNodeReachesItsOwner() {
            Villager villager = new Villager(Villager.Design(), new Globals(), 42);
            for (int i = 0; i < 2000; i++) {
                villager.Tick(i * 0.1);
            }
            Assert.That(villager.Memories, Has.Some.EqualTo("the hills"));
        }

        [Test]
        public void TheTraceKeepsEveryEvaluation() {
            Bench bench = new Bench();
            bench.Value("a", 0.9f);
            bench.Value("b", 0.4f);
            bench.State("A", "a");
            bench.State("B", "b").Active = false;
            Agent agent = bench.Agent();
            agent.Trace = new Trace(2);
            agent.Evaluate(0.0);
            agent.Evaluate(0.5);
            agent.Evaluate(1.0);
            Assert.That(agent.Trace.Count, Is.EqualTo(2));
            Trace.Entry last = agent.Trace[1];
            Assert.That((last.Time, last.Agent, last.StateSet, last.Executing, last.Locked), Is.EqualTo((1.0, "Bench", "Main", "A", false)));
            Assert.That(last.Options.Select(option => (option.State, option.Available)), Is.EqualTo(new[] { ("A", true), ("B", false) }));
            Assert.That(last.Options[0].Score, Is.EqualTo(0.9f).Within(1e-6f));

            StringWriter writer = new StringWriter();
            agent.Trace.Write(writer);
            string[] lines = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines.Length, Is.EqualTo(2));
            Dictionary<string, object> parsed = (Dictionary<string, object>)Json.Parse(lines[1]);
            Assert.That(parsed["executing"], Is.EqualTo("A"));
        }

        [Test]
        public void TheLogIsV1s() {
            Bench bench = new Bench();
            bench.Value("a", 0.5f);
            State state = bench.State("A", "a");
            state.Preconditions.Add(new Precondition { Name = "p", Consideration = "S | a", Comparator = Comparator.LessThan, Value = 1f });
            Agent agent = bench.Agent();
            agent.Evaluate(2.5);
            StringWriter writer = new StringWriter();
            agent.WriteLog(writer);
            string[] lines = writer.ToString().Replace("\r", "").Split('\n');
            Assert.That(lines, Is.EqualTo(new[] {
                "- - - Game time: 2.5 - - -",
                "STATE 'A' SCORED: 0.5 >>> EXECUTING <<<",
                "    Precondition 'p' met: True",
                "    Evaluator 'e' scored: 0.5",
                "* * * * * * * * * * * * * * * *",
                "Consideration 'S' 'a' value: 0.5",
                "", "", "",
            }));
        }
    }
}
