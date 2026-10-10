using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace KadaXuanwu.UtilityDesigner.Core {
    // The considerations of the sets that are not local, shared by every agent created with the same Globals.
    public sealed class Globals {
        private readonly Dictionary<string, ConsiderationSet> _sets = new Dictionary<string, ConsiderationSet>();

        // The shared set of that name: the first design to bring one defines it.
        internal ConsiderationSet Bind(ConsiderationSet set) {
            if (!_sets.TryGetValue(set.Name, out ConsiderationSet shared)) {
                _sets.Add(set.Name, shared = set);
            }
            return shared;
        }

        public Consideration Find(string set, string name) =>
            _sets.TryGetValue(set, out ConsiderationSet shared) ? shared.Find(name) : null;
    }

    // One agent running a design: Evaluate chooses its state, Execute ticks that state's tree. The caller owns the clock
    // (now, in seconds: game time) and the random numbers, so a run repeats for a seed.
    public sealed class Agent {
        // What Bind gives for a consideration its set does not have, and for a set the design does not have.
        public const int NoConsideration = -1;
        public const int NoSet = -2;

        public readonly Design Design;
        public readonly Rng Random;
        // Whatever the game wants its own nodes to reach: its character, its component.
        public readonly object Owner;
        // Every evaluation, if set.
        public Trace Trace;

        private readonly ConsiderationSet[] _sets;
        private readonly int[] _firstSlot;
        private readonly List<Consideration> _considerations = new List<Consideration>();
        private readonly List<State> _ranked = new List<State>();
        private readonly List<float> _rankedScores = new List<float>();
        private bool _stateLocked;

        public double Now { get; private set; }
        public StateSet RunningStateSet { get; private set; }
        public State ExecutingState { get; private set; }

        // While locked, no other state takes over. Unlocking evaluates at once.
        public bool StateLocked {
            get => _stateLocked;
            set {
                _stateLocked = value;
                if (!value) {
                    Evaluate();
                }
            }
        }

        public Agent(Design design, Globals globals, Rng random, double now = 0.0, object owner = null) {
            if (design.Owner != null) {
                throw new ArgumentException($"{design.Name} already runs an agent: give each its own (Designs.Copy).");
            }
            if (design.StateSets.Count == 0) {
                throw new ArgumentException($"{design.Name} has no state set.");
            }
            design.Owner = this;
            Design = design;
            Random = random;
            Owner = owner;
            Now = now;
            globals = globals ?? new Globals();
            _sets = new ConsiderationSet[design.ConsiderationSets.Count];
            _firstSlot = new int[_sets.Length];
            for (int i = 0; i < _sets.Length; i++) {
                ConsiderationSet set = design.ConsiderationSets[i];
                _sets[i] = set.Local ? set : globals.Bind(set);
                _firstSlot[i] = _considerations.Count;
                _considerations.AddRange(_sets[i].Considerations);
            }
            foreach (StateSet stateSet in design.StateSets) {
                foreach (State state in stateSet.States) {
                    foreach (Precondition precondition in state.Preconditions) {
                        precondition.Slot = Bind(precondition.Consideration);
                    }
                    foreach (Evaluator evaluator in state.Evaluators) {
                        evaluator.Slot = Bind(evaluator.Consideration);
                    }
                    state.LastScore = 0f;
                    Initialize(state.Root);
                }
            }
            // As v1: every agent that starts sets the shared considerations back to their initial values too.
            foreach (Consideration consideration in _considerations) {
                consideration.Initialize(now);
            }
            RunningStateSet = design.StateSets[0];
        }

        private void Initialize(Node node) {
            node.Initialize(this);
            foreach (Node child in node.Children) {
                Initialize(child);
            }
        }

        // The slot of a consideration, looked up once; NoConsideration or NoSet when it is not there.
        public int Bind(string set, string name) {
            for (int i = 0; i < _sets.Length; i++) {
                if (Design.ConsiderationSets[i].Name != set) {
                    continue;
                }
                List<Consideration> considerations = _sets[i].Considerations;
                for (int c = 0; c < considerations.Count; c++) {
                    if (considerations[c].Name == name) {
                        return _firstSlot[i] + c;
                    }
                }
                return NoConsideration;
            }
            return NoSet;
        }

        // The slot of "Set | Name".
        public int Bind(string consideration) {
            string[] parts = consideration.Split(new[] { " | " }, StringSplitOptions.None);
            return parts.Length == 2 ? Bind(parts[0], parts[1]) : NoSet;
        }

        // A consideration's value, 0 when it is not there.
        public float GetConsideration(int slot) => slot >= 0 ? _considerations[slot].Value : 0f;

        public float GetConsideration(string set, string name) => GetConsideration(Bind(set, name));

        // False when it is not there.
        public bool SetConsideration(int slot, float value) {
            if (slot < 0) {
                return false;
            }
            _considerations[slot].Value = value;
            return true;
        }

        public bool SetConsideration(string set, string name, float value) => SetConsideration(Bind(set, name), value);

        public bool ChangeConsideration(int slot, float amount) {
            if (slot < 0) {
                return false;
            }
            _considerations[slot].Value += amount;
            return true;
        }

        public bool ChangeConsideration(string set, string name, float amount) => ChangeConsideration(Bind(set, name), amount);

        public bool SetRunningStateSet(int index) {
            if (index < 0 || index >= Design.StateSets.Count) {
                return false;
            }
            RunningStateSet = Design.StateSets[index];
            return true;
        }

        // Moves the considerations on to now and chooses the state: v1's Evaluate.
        public void Evaluate(double now) {
            Now = now;
            Evaluate();
        }

        // Ticks the chosen state's tree, if it is still running: v1's Execute.
        public void Execute(double now) {
            Now = now;
            State state = ExecutingState;
            if (state != null && state.Root.State == NodeState.Running && state.Root.Child != null) {
                state.Root.Update();
            }
        }

        private void Evaluate() {
            foreach (Consideration consideration in _considerations) {
                consideration.Update(Now);
            }
            StateSet set = RunningStateSet;
            State next = Decide(set, ExecutingState);
            if (next == null) {
                ExecutingState?.Root.Terminate();
                ExecutingState = null;
            } else if (!_stateLocked && next != ExecutingState) {
                ExecutingState?.Root.Terminate();
                ExecutingState = next;
                next.Root.State = NodeState.Running;
            }
            Trace?.Record(Now, this, set);
        }

        // v1's StateSet.DecideNextState: the states that can be chosen, best first, ties in the set's order. The running
        // state is kept as soon as it is reached; any other is taken unless its fail chance passes it over. When every
        // one is passed over, the last of them is taken.
        private State Decide(StateSet set, State running) {
            _ranked.Clear();
            _rankedScores.Clear();
            foreach (State state in set.States) {
                float score = Score(state, state == running);
                if (!(score >= 0f)) {
                    continue;
                }
                int at = _ranked.Count;
                while (at > 0 && _rankedScores[at - 1] < score) {
                    at--;
                }
                _ranked.Insert(at, state);
                _rankedScores.Insert(at, score);
            }
            State lastFailed = null;
            foreach (State state in _ranked) {
                if (state == running) {
                    return running;
                }
                if (Random.NextFloat() > state.FailChance) {
                    return state;
                }
                lastFailed = state;
            }
            return lastFailed;
        }

        // v1's State.GetScore: -1 when it cannot be chosen, else
        // Weight * (BaseScore + the evaluators' scores, each by its share of their weights), times ExecutionFactor while
        // it runs, then held to MinScore and MaxScore when set. The sums run in double as v1's (LINQ's) do.
        public float Score(State state, bool running) {
            state.LastAvailable = false;
            if (!state.Active) {
                return -1f;
            }
            foreach (Precondition precondition in state.Preconditions) {
                precondition.LastConditionMet = precondition.Slot >= 0
                    && Comparing.Holds(_considerations[precondition.Slot].Value, precondition.Comparator, precondition.Value);
                if (!precondition.LastConditionMet) {
                    state.LastScore = 0f;
                    return -1f;
                }
            }
            float score = state.BaseScore;
            double weights = 0.0;
            foreach (Evaluator evaluator in state.Evaluators) {
                weights += evaluator.Weight;
            }
            float totalWeight = (float)weights;
            if (totalWeight > 0f) {
                double sum = 0.0;
                foreach (Evaluator evaluator in state.Evaluators) {
                    sum += Score(evaluator) * (evaluator.Weight / totalWeight);
                }
                score += (float)sum;
            }
            score *= state.Weight;
            if (running) {
                score *= state.ExecutionFactor;
            }
            if (state.SetMinScore && score < state.MinScore) {
                score = state.MinScore;
            }
            if (state.SetMaxScore && score > state.MaxScore) {
                score = state.MaxScore;
            }
            state.LastScore = score;
            state.LastAvailable = score >= 0f;
            return score;
        }

        // v1's Evaluator.ScoreConsideration: 0 without its consideration; a Max not above Min makes a step at Min.
        private float Score(Evaluator evaluator) {
            if (evaluator.Slot < 0) {
                return 0f;
            }
            float span = evaluator.Max - evaluator.Min;
            if (span <= 0f) {
                span = float.Epsilon;
            }
            float x = (_considerations[evaluator.Slot].Value - evaluator.Min) / span;
            evaluator.LastScore = evaluator.Curve.Evaluate(x < 0f ? 0f : x > 1f ? 1f : x);
            return evaluator.LastScore;
        }

        // v1's log entry: every state's last score, its preconditions and each evaluator's share, then the
        // considerations.
        public void WriteLog(TextWriter writer) {
            CultureInfo plain = CultureInfo.InvariantCulture;
            writer.WriteLine($"- - - Game time: {Now.ToString(plain)} - - -");
            foreach (StateSet set in Design.StateSets) {
                foreach (State state in set.States) {
                    writer.WriteLine($"STATE '{state.Name}' SCORED: {state.LastScore.ToString(plain)}"
                        + (state == ExecutingState ? " >>> EXECUTING <<<" : ""));
                    foreach (Precondition precondition in state.Preconditions) {
                        writer.WriteLine($"    Precondition '{precondition.Name}' met: {precondition.LastConditionMet}");
                    }
                    double weights = 0.0;
                    foreach (Evaluator evaluator in state.Evaluators) {
                        weights += evaluator.Weight;
                    }
                    foreach (Evaluator evaluator in state.Evaluators) {
                        float share = evaluator.LastScore * evaluator.Weight / (float)weights;
                        writer.WriteLine($"    Evaluator '{evaluator.Name}' scored: {share.ToString(plain)}");
                    }
                }
            }
            writer.WriteLine("* * * * * * * * * * * * * * * *");
            for (int i = 0; i < _sets.Length; i++) {
                foreach (Consideration consideration in _sets[i].Considerations) {
                    writer.WriteLine($"Consideration '{_sets[i].Name}' '{consideration.Name}' value: {consideration.Value.ToString(plain)}");
                }
            }
            writer.WriteLine();
            writer.WriteLine();
        }
    }

    // A seeded xorshift64*: the same seed, the same numbers, on every runtime.
    public sealed class Rng {
        private ulong _state;

        public Rng(ulong seed) {
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong Next() {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 2685821657736338717UL;
        }

        // 0 inclusive to 1 exclusive.
        public float NextFloat() => (Next() >> 40) / (float)(1UL << 24);

        // 0 inclusive to below exclusive.
        public int Next(int below) => (int)((Next() >> 33) % (ulong)below);
    }
}
