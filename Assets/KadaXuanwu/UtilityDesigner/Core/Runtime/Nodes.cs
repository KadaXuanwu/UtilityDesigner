namespace KadaXuanwu.UtilityDesigner.Core {
    public sealed class WaitFixed : ActionNode {
        public float Duration = 1f;

        private double _start;

        public override string Description => "Waits for a fixed duration and then returns true.";

        protected override void OnEnable() => _start = Agent.Now;

        protected override NodeState OnUpdate() => Agent.Now - _start < Duration ? NodeState.Running : NodeState.Success;
    }

    public sealed class WaitRandom : ActionNode {
        public float MinDuration;
        public float MaxDuration = 1f;

        private double _start;
        private float _duration;

        public override string Description => "Waits for a random duration and then returns true.";

        protected override void OnEnable() {
            _start = Agent.Now;
            _duration = MinDuration + (MaxDuration - MinDuration) * Agent.Random.NextFloat();
        }

        protected override NodeState OnUpdate() => Agent.Now - _start < _duration ? NodeState.Running : NodeState.Success;
    }

    public sealed class LockState : ActionNode {
        public override string Description => "Locks the state and returns success.";

        protected override void OnEnable() => Agent.StateLocked = true;

        protected override NodeState OnUpdate() => NodeState.Success;
    }

    public sealed class UnlockState : ActionNode {
        public override string Description => "Unlocks the state and returns success.";

        protected override void OnEnable() => Agent.StateLocked = false;

        protected override NodeState OnUpdate() => NodeState.Success;
    }

    public sealed class LockStateDuration : ActionNode {
        public float Duration;

        private double _locked;

        public override string Description => "Locks the state and returns running until for the defined duration, "
            + "after which it unlocks the state and returns success.";

        protected override void OnEnable() {
            Agent.StateLocked = true;
            _locked = Agent.Now;
        }

        protected override NodeState OnUpdate() {
            if (Agent.Now - _locked < Duration) {
                return NodeState.Running;
            }
            Agent.StateLocked = false;
            return NodeState.Success;
        }
    }

    public sealed class LockStateConsideration : ActionNode {
        public string ConsiderationSetName = "";
        public string ConsiderationName = "";
        public float RequiredValue;
        public bool AddConsiderationValue;

        private int _slot;
        private bool _changePositive;

        public override string Description => "Locks the state and returns running until the consideration reaches the required value, "
            + "after which it unlocks the state and returns success.";

        protected override void OnEnable() {
            _slot = Agent.Bind(ConsiderationSetName, ConsiderationName);
            if (_slot == Agent.NoSet) {
                return;
            }
            Agent.StateLocked = true;
            // As v1: added to the setting itself, so every run of the node adds again.
            if (AddConsiderationValue) {
                RequiredValue += Agent.GetConsideration(_slot);
            }
            _changePositive = RequiredValue >= Agent.GetConsideration(_slot);
        }

        protected override NodeState OnUpdate() {
            // v1 throws without its consideration set.
            if (_slot == Agent.NoSet) {
                return NodeState.Failure;
            }
            float value = Agent.GetConsideration(_slot);
            if (_changePositive ? value < RequiredValue : value > RequiredValue) {
                return NodeState.Running;
            }
            Agent.StateLocked = false;
            return NodeState.Success;
        }
    }

    public sealed class ChangeStateSet : ActionNode {
        public int StateSetIndex;

        private bool _success;

        public override string Description => "Changes the state set and returns success if it was successful.";

        protected override void OnEnable() => _success = Agent.SetRunningStateSet(StateSetIndex);

        protected override NodeState OnUpdate() => _success ? NodeState.Success : NodeState.Failure;
    }

    public sealed class SetConsideration : ActionNode {
        public string ConsiderationSetName = "";
        public string ConsiderationName = "";
        public float NewValue;

        private int _slot;
        private bool _success;

        public override string Description => "Sets the value of a consideration and returns success if the consideration exists, otherwise failure.";

        protected override void OnAwake() => _slot = Agent.Bind(ConsiderationSetName, ConsiderationName);

        protected override void OnEnable() => _success = Agent.SetConsideration(_slot, NewValue);

        protected override NodeState OnUpdate() => _success ? NodeState.Success : NodeState.Failure;
    }

    public sealed class ChangeConsideration : ActionNode {
        public string ConsiderationSetName = "";
        public string ConsiderationName = "";
        public float Amount;

        private int _slot;
        private bool _success;

        public override string Description => "Changes the value of a consideration and returns success if the consideration exists, otherwise failure.";

        protected override void OnAwake() => _slot = Agent.Bind(ConsiderationSetName, ConsiderationName);

        protected override void OnEnable() => _success = Agent.ChangeConsideration(_slot, Amount);

        protected override NodeState OnUpdate() => _success ? NodeState.Success : NodeState.Failure;
    }

    // As v1: fails without its consideration set; a consideration the set does not have counts as 0.
    public sealed class CompareConsideration : ConditionalNode {
        public string ConsiderationSetName = "";
        public string ConsiderationName = "";
        public Comparator Comparator;
        public float Value;

        private int _slot;

        public override string Description => "Compares a consideration's value with a set value.\n"
            + "Returns success if the comparison matches the selected comparator and failure otherwise.";

        protected override void OnAwake() => _slot = Agent.Bind(ConsiderationSetName, ConsiderationName);

        protected override NodeState OnUpdate() =>
            _slot != Agent.NoSet && Comparing.Holds(Agent.GetConsideration(_slot), Comparator, Value) ? NodeState.Success : NodeState.Failure;
    }

    // Compares the caller's clock.
    public sealed class TimeCheck : ConditionalNode {
        public Comparator Comparator;
        public float Time;

        public override string Description => "Compares the current time to a set value.\n"
            + "Returns success if the comparison matches the selected comparator and failure otherwise.";

        protected override NodeState OnUpdate() =>
            Comparing.Holds((float)Agent.Now, Comparator, Time) ? NodeState.Success : NodeState.Failure;
    }
}
