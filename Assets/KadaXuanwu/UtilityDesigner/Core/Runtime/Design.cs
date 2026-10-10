using System;
using System.Collections.Generic;

namespace KadaXuanwu.UtilityDesigner.Core {
    public enum Comparator {
        GreaterThan,
        LessThan,
        GreaterThanOrEqualTo,
        LessThanOrEqualTo,
        EqualTo,
        NotEqualTo,
    }

    public static class Comparing {
        // v1's comparison: equal and not equal compare both sides rounded to whole numbers (halves to even).
        public static bool Holds(float left, Comparator comparator, float right) {
            switch (comparator) {
                case Comparator.GreaterThan:
                    return left > right;
                case Comparator.LessThan:
                    return left < right;
                case Comparator.GreaterThanOrEqualTo:
                    return left >= right;
                case Comparator.LessThanOrEqualTo:
                    return left <= right;
                case Comparator.NotEqualTo:
                    return Round(left) != Round(right);
                default:
                    return Round(left) == Round(right);
            }
        }

        private static int Round(float value) => (int)Math.Round((double)value);
    }

    // A number an agent decides by: set by the game or by nodes, kept within its limits when they are set, and changing
    // by itself each second of the caller's clock.
    public sealed class Consideration {
        public string Name = "";
        public float InitialValue;
        public bool SetMinValue;
        public float MinValue;
        public bool SetMaxValue;
        public float MaxValue;
        public float ChangePerSecond;

        private float _value;
        private double _updated;

        public float Value {
            get => _value;
            set {
                _value = value;
                if (SetMinValue && value < MinValue) {
                    _value = MinValue;
                }
                if (SetMaxValue && value > MaxValue) {
                    _value = MaxValue;
                }
            }
        }

        internal void Initialize(double now) {
            Value = InitialValue;
            _updated = now;
        }

        internal void Update(double now) {
            if (ChangePerSecond == 0f) {
                return;
            }
            Value += (float)(ChangePerSecond * (now - _updated));
            _updated = now;
        }
    }

    // Considerations under a name. A local set is each agent's own; any other is shared by every agent given the same
    // Globals.
    public sealed class ConsiderationSet {
        public string Name = "";
        public bool Local;
        public List<Consideration> Considerations = new List<Consideration>();

        public Consideration Find(string name) {
            foreach (Consideration consideration in Considerations) {
                if (consideration.Name == name) {
                    return consideration;
                }
            }
            return null;
        }
    }

    // A state can only be chosen while every one of its preconditions holds. Consideration is "Set | Name".
    public sealed class Precondition {
        public string Name = "";
        public string Consideration = "";
        public Comparator Comparator;
        public float Value;

        public bool LastConditionMet { get; internal set; }
        internal int Slot = Agent.NoSet;
    }

    // One reason for a state: a consideration, normalized from Min..Max to 0..1, through a curve. Consideration is
    // "Set | Name".
    public sealed class Evaluator {
        public string Name = "";
        public string Consideration = "";
        public float Weight;
        public Curve Curve = Curve.Through();
        public float Min;
        public float Max = 1f;

        public float LastScore { get; internal set; }
        internal int Slot = Agent.NoSet;
    }

    // Something an agent can be doing: scored by its evaluators, done by its behaviour tree.
    public sealed class State {
        public string Name = "";
        public bool Active = true;
        public float Weight = 1f;
        // The score is multiplied by this while the state is the one running.
        public float ExecutionFactor = 1f;
        public float BaseScore;
        public bool SetMinScore;
        public float MinScore;
        public bool SetMaxScore;
        public float MaxScore;
        // The chance, each evaluation, that the state is passed over when it would be taken.
        public float FailChance;
        public string Notes = "";
        public List<Precondition> Preconditions = new List<Precondition>();
        public List<Evaluator> Evaluators = new List<Evaluator>();

        internal readonly Root Root = new Root();

        public Node Tree {
            get => Root.Child;
            set => Root.Child = value;
        }

        // The tree's own state: Running while it runs, its result once it ended, Disabled when the state is not chosen.
        public NodeState TreeState => Root.State;
        public float LastScore { get; internal set; }
        // Whether the last evaluation could choose it: active, its preconditions met, its score not below 0.
        public bool LastAvailable { get; internal set; }
    }

    public sealed class StateSet {
        public string Name = "";
        public List<State> States = new List<State>();
    }

    // A behaviour: consideration sets and state sets. It carries its agent's running values too, so every agent has
    // its own (Designs.Copy).
    public sealed class Design {
        public const int FormatVersion = 2;

        public string Name = "";
        public List<ConsiderationSet> ConsiderationSets = new List<ConsiderationSet>();
        public List<StateSet> StateSets = new List<StateSet>();

        internal Agent Owner;
    }
}
