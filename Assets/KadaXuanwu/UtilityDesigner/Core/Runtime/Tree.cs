using System;
using System.Collections.Generic;

namespace KadaXuanwu.UtilityDesigner.Core {
    public enum NodeState {
        Disabled,
        Running,
        Failure,
        Success,
    }

    // A behaviour tree node, with v1's life: OnAwake the first time it runs, OnEnable each time it starts, OnUpdate
    // every execution tick, OnDisable when it ends or is terminated. A node type's public fields (numbers, booleans,
    // text, enums) are its settings: saved with the design, and its float fields are parameters.
    public abstract class Node {
        private static readonly Node[] None = Array.Empty<Node>();

        // Stable across edits: what a layer names the node by.
        public string Id = "";
        public string Notes = "";
        // Where the editor draws it.
        public float X;
        public float Y;

        private bool _started;
        private bool _enabled;

        public NodeState State { get; internal set; } = NodeState.Disabled;
        public virtual string Description => "";
        public virtual IReadOnlyList<Node> Children => None;
        // The agent running this tree: its clock (Now), its random numbers, its considerations, its lock, its Owner.
        protected Agent Agent { get; private set; }

        internal void Initialize(Agent agent) {
            _started = false;
            _enabled = false;
            Agent = agent;
        }

        public NodeState Update() {
            if (!_started) {
                OnAwake();
                _started = true;
            }
            if (!_enabled) {
                OnEnable();
                _enabled = true;
                State = NodeState.Running;
            }
            State = OnUpdate();
            if (State != NodeState.Running) {
                OnDisable();
                _enabled = false;
            }
            return State;
        }

        public virtual void Terminate() {
            if (State == NodeState.Running) {
                OnDisable();
                _enabled = false;
            }
            State = NodeState.Disabled;
        }

        protected virtual void OnAwake() {
        }

        protected virtual void OnEnable() {
        }

        protected virtual void OnDisable() {
        }

        protected abstract NodeState OnUpdate();
    }

    // The bases of the game's own nodes, as v1's.
    public abstract class ActionNode : Node {
    }

    public abstract class ConditionalNode : Node {
    }

    public abstract class CompositeNode : Node {
        public readonly List<Node> Nodes = new List<Node>();

        public override IReadOnlyList<Node> Children => Nodes;

        public override void Terminate() {
            foreach (Node child in Nodes) {
                child.Terminate();
            }
            base.Terminate();
        }
    }

    // A decorator without a child fails (v1 throws).
    public abstract class DecoratorNode : Node {
        public Node Child;

        public override IReadOnlyList<Node> Children => Child == null ? base.Children : new[] { Child };

        public override void Terminate() {
            Child?.Terminate();
            base.Terminate();
        }
    }

    // The top of a state's tree.
    internal sealed class Root : DecoratorNode {
        protected override NodeState OnUpdate() => Child.Update();
    }

    // Sequence, Selector and their random kinds: one child a tick, in order, until a child ends with EndsOn; the other
    // result once every child has run. Without children it fails (v1 throws).
    public abstract class OrderedNode : CompositeNode {
        private readonly NodeState _endsOn;
        private readonly bool _shuffled;
        private readonly List<int> _order = new List<int>();
        private int _current;

        protected OrderedNode(NodeState endsOn, bool shuffled) {
            _endsOn = endsOn;
            _shuffled = shuffled;
        }

        protected override void OnEnable() {
            _current = 0;
            _order.Clear();
            for (int i = 0; i < Nodes.Count; i++) {
                _order.Add(i);
            }
            for (int i = _order.Count - 1; _shuffled && i > 0; i--) {
                int j = Agent.Random.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
        }

        protected override NodeState OnUpdate() {
            if (_order.Count == 0) {
                return NodeState.Failure;
            }
            NodeState state = Nodes[_order[_current]].Update();
            if (state == NodeState.Running || state == _endsOn) {
                return state;
            }
            // Success in a sequence, Failure in a selector: on to the next child, next tick.
            if (state != NodeState.Disabled) {
                _current++;
            }
            if (_current < _order.Count) {
                return NodeState.Running;
            }
            return _endsOn == NodeState.Failure ? NodeState.Success : NodeState.Failure;
        }
    }

    public sealed class Sequence : OrderedNode {
        public Sequence() : base(NodeState.Failure, false) {
        }

        public override string Description => "Sequentially executes all children from left to right.\n"
            + "Returns success if all child nodes execute successfully.\n"
            + "Returns failure upon encountering the first failed child node.";
    }

    public sealed class Selector : OrderedNode {
        public Selector() : base(NodeState.Success, false) {
        }

        public override string Description => "Sequentially executes all children from left to right.\n"
            + "Returns failure if all child nodes fail to execute.\n"
            + "Returns success upon encountering the first successful child node.";
    }

    public sealed class RandomSequence : OrderedNode {
        public RandomSequence() : base(NodeState.Failure, true) {
        }

        public override string Description => "Executes all child nodes in a random order.\n"
            + "Returns success if all child nodes execute successfully.\n"
            + "Returns failure upon encountering the first failed child node.";
    }

    public sealed class RandomSelector : OrderedNode {
        public RandomSelector() : base(NodeState.Success, true) {
        }

        public override string Description => "Executes all child nodes in a random order.\n"
            + "Returns failure if all child nodes fail to execute.\n"
            + "Returns success upon encountering the first successful child node.";
    }

    public sealed class Parallel : CompositeNode {
        private readonly List<NodeState> _states = new List<NodeState>();

        public override string Description => "Executes all child nodes concurrently in parallel.\n"
            + "Returns success if all child nodes complete successfully.\n"
            + "Returns failure if any child node fails, and aborts the remaining running child nodes.";

        protected override void OnEnable() {
            _states.Clear();
            foreach (Node _ in Nodes) {
                _states.Add(NodeState.Running);
            }
        }

        protected override NodeState OnUpdate() {
            bool running = false;
            for (int i = 0; i < _states.Count; i++) {
                if (_states[i] != NodeState.Running) {
                    continue;
                }
                NodeState state = Nodes[i].Update();
                if (state == NodeState.Failure) {
                    for (int other = 0; other < _states.Count; other++) {
                        if (_states[other] == NodeState.Running) {
                            Nodes[other].Terminate();
                        }
                    }
                    return NodeState.Failure;
                }
                running |= state == NodeState.Running;
                _states[i] = state;
            }
            return running ? NodeState.Running : NodeState.Success;
        }
    }

    public sealed class Invert : DecoratorNode {
        public override string Description => "Returns running while the child is running, and inverts the return value otherwise.";

        protected override NodeState OnUpdate() {
            switch (Child?.Update()) {
                case NodeState.Running:
                    return NodeState.Running;
                case NodeState.Failure:
                    return NodeState.Success;
                default:
                    return NodeState.Failure;
            }
        }
    }

    // Runs its child again whenever it ends.
    public sealed class Repeat : DecoratorNode {
        public override string Description => "Always returns running.";

        protected override NodeState OnUpdate() {
            if (Child == null) {
                return NodeState.Failure;
            }
            Child.Update();
            return NodeState.Running;
        }
    }

    public sealed class Success : DecoratorNode {
        public override string Description => "Returns running while the child is running, and success otherwise.";

        protected override NodeState OnUpdate() {
            NodeState state = Child?.Update() ?? NodeState.Failure;
            return Child != null && state == NodeState.Failure ? NodeState.Success : state;
        }
    }

    public sealed class Failure : DecoratorNode {
        public override string Description => "Returns running while the child is running, and failure otherwise.";

        protected override NodeState OnUpdate() {
            NodeState state = Child?.Update() ?? NodeState.Failure;
            return state == NodeState.Success ? NodeState.Failure : state;
        }
    }
}
