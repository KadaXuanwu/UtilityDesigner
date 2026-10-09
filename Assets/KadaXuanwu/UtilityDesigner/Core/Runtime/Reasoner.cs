using System;
using System.Collections.Generic;

namespace KadaXuanwu.UtilityDesigner.Core {
    // A number an action reads off the world, for a target (default when the action has none).
    public delegate float Input<in TContext, in TTarget>(TContext context, TTarget target);

    // Fills into with the targets an action is scored for.
    public delegate void Targets<in TContext, TTarget>(TContext context, List<TTarget> into);

    // One reason for or against an action: an input, normalized from Min..Max to 0..1, through a curve.
    public sealed class Consideration {
        public string Name = "";
        public string Input = "";
        public float Min;
        public float Max = 1f;
        public Curve Curve = Curve.Linear();
    }

    // Something an agent can choose to do, scored by its considerations, once per target if it has a target provider.
    public sealed class UtilityAction {
        public string Name = "";
        public float Weight = 1f;
        // Dual utility: only the highest rank with any score above zero competes; a higher rank always interrupts.
        public int Rank;
        // The target provider's name; empty scores it once, for the default target.
        public string Targets = "";
        // Seconds after it ends before it can be chosen again.
        public float Cooldown;
        public List<Consideration> Considerations = new List<Consideration>();
    }

    // A design: the actions and how firmly a choice is kept. Plain data; Bind makes a Brain of it.
    public sealed class Reasoner {
        public const int FormatVersion = 1;

        public string Name = "";
        public List<UtilityAction> Actions = new List<UtilityAction>();
        // The running choice's score times this, so a near tie does not flip.
        public float Momentum = 1.1f;
        // Seconds a choice runs before another of its rank may take over.
        public float MinCommit;
        // A choice of the same rank takes over only scoring this many times the running one.
        public float Interrupt = 1.5f;
        // The running choice's score times 1 + SunkCost * its progress (0..1): what is nearly done is finished.
        public float SunkCost;
        // 0 always picks the best; above, a weighted random pick among the options within this share of the best.
        public float Spread;

        public Brain<TContext, TTarget> Bind<TContext, TTarget>(Bindings<TContext, TTarget> bindings, ulong seed) =>
            new Brain<TContext, TTarget>(this, bindings, seed);
    }

    // The game's side of a design: its inputs and target providers by name.
    public sealed class Bindings<TContext, TTarget> {
        internal readonly Dictionary<string, Input<TContext, TTarget>> Inputs = new Dictionary<string, Input<TContext, TTarget>>();
        internal readonly Dictionary<string, Targets<TContext, TTarget>> Providers = new Dictionary<string, Targets<TContext, TTarget>>();

        public Bindings<TContext, TTarget> Input(string name, Input<TContext, TTarget> input) {
            Inputs[name] = input;
            return this;
        }

        public Bindings<TContext, TTarget> Targets(string name, Targets<TContext, TTarget> targets) {
            Providers[name] = targets;
            return this;
        }
    }

    // What a brain chose: which action, for which target, its score and rank, since when, and how far along it is.
    public struct Choice<TTarget> {
        public int Action;
        public string Name;
        public TTarget Target;
        public float Score;
        public int Rank;
        public double Since;
        public float Progress;

        public bool Valid => Action >= 0;

        public static Choice<TTarget> None => new Choice<TTarget> { Action = -1, Name = "" };
    }

    // A design bound to a game: decides, keeps its choice by the design's commitment rules, and remembers cooldowns.
    // Deterministic for a seed; allocates nothing per decision once warm.
    public sealed class Brain<TContext, TTarget> {
        private readonly Reasoner _design;
        private readonly Input<TContext, TTarget>[][] _inputs;
        private readonly Targets<TContext, TTarget>[] _providers;
        private readonly double[] _readyAt;
        private readonly List<TTarget> _targets = new List<TTarget>();
        private readonly List<Choice<TTarget>> _options = new List<Choice<TTarget>>();
        private readonly EqualityComparer<TTarget> _same = EqualityComparer<TTarget>.Default;
        private Rng _rng;

        public Choice<TTarget> Current { get; private set; } = Choice<TTarget>.None;
        // Every option scored at the last decision, best first within its rank, if Trace is set.
        public Trace Trace { get; set; }
        public Reasoner Design => _design;

        public Brain(Reasoner design, Bindings<TContext, TTarget> bindings, ulong seed) {
            _design = design;
            _rng = new Rng(seed);
            _inputs = new Input<TContext, TTarget>[design.Actions.Count][];
            _providers = new Targets<TContext, TTarget>[design.Actions.Count];
            _readyAt = new double[design.Actions.Count];
            for (int a = 0; a < design.Actions.Count; a++) {
                UtilityAction action = design.Actions[a];
                _inputs[a] = new Input<TContext, TTarget>[action.Considerations.Count];
                for (int c = 0; c < action.Considerations.Count; c++) {
                    string name = action.Considerations[c].Input;
                    if (!bindings.Inputs.TryGetValue(name, out _inputs[a][c])) {
                        throw new ArgumentException($"{design.Name}: {action.Name} reads an input \"{name}\" the game does not bind.");
                    }
                }
                if (action.Targets.Length > 0 && !bindings.Providers.TryGetValue(action.Targets, out _providers[a])) {
                    throw new ArgumentException($"{design.Name}: {action.Name} is scored for targets \"{action.Targets}\" the game does not bind.");
                }
            }
        }

        // The action's score for a target, before momentum.
        public float Score(int action, TContext context, TTarget target) {
            UtilityAction design = _design.Actions[action];
            List<Consideration> considerations = design.Considerations;
            float compensation = considerations.Count > 0 ? 1f - 1f / considerations.Count : 0f;
            float score = design.Weight;
            for (int c = 0; c < considerations.Count && score > 0f; c++) {
                Consideration consideration = considerations[c];
                float raw = _inputs[action][c](context, target);
                float span = consideration.Max - consideration.Min;
                float utility = consideration.Curve.Evaluate(span != 0f ? (raw - consideration.Min) / span : raw >= consideration.Max ? 1f : 0f);
                // Many considerations multiplied drag every score down; each is lifted back by its share of the shortfall.
                score *= utility + (1f - utility) * compensation * utility;
            }
            return score;
        }

        // The choice for now: the running one kept unless the rules let a better one take over.
        public Choice<TTarget> Decide(TContext context, double now) {
            _options.Clear();
            Choice<TTarget> running = Current;
            bool stillThere = false;
            int topRank = int.MinValue;
            for (int a = 0; a < _design.Actions.Count; a++) {
                if (_readyAt[a] > now && !(running.Valid && running.Action == a)) {
                    continue;
                }
                _targets.Clear();
                if (_providers[a] != null) {
                    _providers[a](context, _targets);
                } else {
                    _targets.Add(default);
                }
                foreach (TTarget target in _targets) {
                    float score = Score(a, context, target);
                    bool isRunning = running.Valid && running.Action == a && _same.Equals(running.Target, target);
                    if (isRunning) {
                        stillThere = true;
                        score *= _design.Momentum * (1f + _design.SunkCost * running.Progress);
                        running.Score = score;
                    }
                    if (score <= 0f) {
                        continue;
                    }
                    int rank = _design.Actions[a].Rank;
                    topRank = Math.Max(topRank, rank);
                    _options.Add(new Choice<TTarget> { Action = a, Name = _design.Actions[a].Name, Target = target, Score = score, Rank = rank });
                }
            }
            Choice<TTarget> best = Pick(topRank);
            bool keep = running.Valid && stillThere && running.Score > 0f && best.Valid
                && best.Rank <= running.Rank
                && (now - running.Since < _design.MinCommit || best.Score <= running.Score * _design.Interrupt);
            if (keep || (running.Valid && best.Valid && best.Action == running.Action && _same.Equals(best.Target, running.Target))) {
                Current = running;
            } else {
                if (best.Valid) {
                    best.Since = now;
                }
                Current = best;
            }
            Trace?.Record(now, _design.Name, _options, Current);
            return Current;
        }

        // How far the running choice is, 0..1, for the sunk-cost rule.
        public void Report(float progress) {
            Choice<TTarget> current = Current;
            current.Progress = Math.Clamp(progress, 0f, 1f);
            Current = current;
        }

        // The running choice ended, done or failed: its cooldown starts, and the next decision starts fresh.
        public void Finish(double now) {
            if (Current.Valid) {
                _readyAt[Current.Action] = now + _design.Actions[Current.Action].Cooldown;
            }
            Current = Choice<TTarget>.None;
        }

        // Of the options of the top rank, the best, or with a spread a weighted pick among those near it.
        private Choice<TTarget> Pick(int rank) {
            Choice<TTarget> best = Choice<TTarget>.None;
            foreach (Choice<TTarget> option in _options) {
                if (option.Rank == rank && (!best.Valid || option.Score > best.Score)) {
                    best = option;
                }
            }
            if (!best.Valid || _design.Spread <= 0f) {
                return best;
            }
            float floor = best.Score * (1f - _design.Spread);
            float total = 0f;
            foreach (Choice<TTarget> option in _options) {
                if (option.Rank == rank && option.Score >= floor) {
                    total += option.Score;
                }
            }
            float roll = _rng.NextFloat() * total;
            foreach (Choice<TTarget> option in _options) {
                if (option.Rank == rank && option.Score >= floor) {
                    roll -= option.Score;
                    if (roll <= 0f) {
                        return option;
                    }
                }
            }
            return best;
        }
    }

    // A seeded xorshift64*: the same seed, the same numbers, on every runtime.
    public struct Rng {
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
    }
}
