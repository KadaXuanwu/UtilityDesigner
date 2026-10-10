using System;
using System.Collections.Generic;
using System.Threading;

namespace KadaXuanwu.UtilityDesigner.Core {
    public enum Status { Running, Success, Failure }

    // A behaviour tree node over a context the game passes in, ticked by the caller with the time. Start runs on the first
    // tick of a run, Stop when it ends or is aborted. State is the last result, for a live view.
    public abstract class Node<T> {
        private static readonly Node<T>[] None = Array.Empty<Node<T>>();

        public string Name = "";
        public Status State { get; private set; } = Status.Failure;
        public bool Running { get; private set; }

        public virtual IReadOnlyList<Node<T>> Children => None;

        public Status Tick(T context, double now) {
            if (!Running) {
                Running = true;
                Start(context, now);
            }
            State = Update(context, now);
            if (State != Status.Running) {
                Running = false;
                Stop(context, now);
            }
            return State;
        }

        // Ends a run early, its running children first.
        public void Abort(T context, double now) {
            foreach (Node<T> child in Children) {
                child.Abort(context, now);
            }
            if (Running) {
                Running = false;
                State = Status.Failure;
                Stop(context, now);
            }
        }

        protected virtual void Start(T context, double now) {
        }

        protected abstract Status Update(T context, double now);

        protected virtual void Stop(T context, double now) {
        }
    }

    public abstract class Composite<T> : Node<T> {
        protected readonly Node<T>[] Nodes;
        protected int At;

        protected Composite(Node<T>[] nodes) {
            Nodes = nodes;
        }

        public override IReadOnlyList<Node<T>> Children => Nodes;

        protected override void Start(T context, double now) {
            At = 0;
        }
    }

    // Each child in turn until one fails.
    public sealed class Sequence<T> : Composite<T> {
        public Sequence(params Node<T>[] nodes) : base(nodes) {
        }

        protected override Status Update(T context, double now) {
            while (At < Nodes.Length) {
                Status status = Nodes[At].Tick(context, now);
                if (status != Status.Success) {
                    return status;
                }
                At++;
            }
            return Status.Success;
        }
    }

    // Each child in turn until one succeeds.
    public sealed class Selector<T> : Composite<T> {
        public Selector(params Node<T>[] nodes) : base(nodes) {
        }

        protected override Status Update(T context, double now) {
            while (At < Nodes.Length) {
                Status status = Nodes[At].Tick(context, now);
                if (status != Status.Failure) {
                    return status;
                }
                At++;
            }
            return Status.Failure;
        }
    }

    // Every child each tick: fails as soon as one fails, succeeds when all have (or, with any, when one has).
    public sealed class Parallel<T> : Composite<T> {
        private readonly bool _any;
        private readonly Status[] _done;

        public Parallel(bool any, params Node<T>[] nodes) : base(nodes) {
            _any = any;
            _done = new Status[nodes.Length];
        }

        protected override void Start(T context, double now) {
            for (int i = 0; i < _done.Length; i++) {
                _done[i] = Status.Running;
            }
        }

        protected override Status Update(T context, double now) {
            int succeeded = 0;
            for (int i = 0; i < Nodes.Length; i++) {
                if (_done[i] == Status.Running) {
                    _done[i] = Nodes[i].Tick(context, now);
                }
                if (_done[i] == Status.Failure) {
                    foreach (Node<T> node in Nodes) {
                        node.Abort(context, now);
                    }
                    return Status.Failure;
                }
                if (_done[i] == Status.Success) {
                    succeeded++;
                }
            }
            if (succeeded == Nodes.Length || (_any && succeeded > 0)) {
                foreach (Node<T> node in Nodes) {
                    node.Abort(context, now);
                }
                return Status.Success;
            }
            return Status.Running;
        }
    }

    public abstract class Decorator<T> : Node<T> {
        protected readonly Node<T> Child;
        private readonly Node<T>[] _children;

        protected Decorator(Node<T> child) {
            Child = child;
            _children = new[] { child };
        }

        public override IReadOnlyList<Node<T>> Children => _children;
    }

    public sealed class Invert<T> : Decorator<T> {
        public Invert(Node<T> child) : base(child) {
        }

        protected override Status Update(T context, double now) {
            Status status = Child.Tick(context, now);
            return status == Status.Running ? status : status == Status.Success ? Status.Failure : Status.Success;
        }
    }

    // Runs its child again each time it succeeds, count times (0 for ever); a failure ends it.
    public sealed class Repeat<T> : Decorator<T> {
        private readonly int _count;
        private int _done;

        public Repeat(Node<T> child, int count = 0) : base(child) {
            _count = count;
        }

        protected override void Start(T context, double now) {
            _done = 0;
        }

        protected override Status Update(T context, double now) {
            Status status = Child.Tick(context, now);
            if (status != Status.Success) {
                return status;
            }
            _done++;
            return _count > 0 && _done >= _count ? Status.Success : Status.Running;
        }
    }

    // Its child for as long as the condition holds; the moment it does not, the child is aborted and this fails (a
    // conditional abort).
    public sealed class While<T> : Decorator<T> {
        private readonly Func<T, bool> _condition;

        public While(Func<T, bool> condition, Node<T> child) : base(child) {
            _condition = condition;
        }

        protected override Status Update(T context, double now) {
            if (!_condition(context)) {
                Child.Abort(context, now);
                return Status.Failure;
            }
            return Child.Tick(context, now);
        }
    }

    public sealed class Condition<T> : Node<T> {
        private readonly Func<T, bool> _test;

        public Condition(string name, Func<T, bool> test) {
            Name = name;
            _test = test;
        }

        protected override Status Update(T context, double now) => _test(context) ? Status.Success : Status.Failure;
    }

    // A leaf that does something: the game's callback, ticked until it says it is done.
    public sealed class Do<T> : Node<T> {
        private readonly Func<T, double, Status> _act;
        private readonly Action<T> _stop;

        public Do(string name, Func<T, double, Status> act, Action<T> stop = null) {
            Name = name;
            _act = act;
            _stop = stop;
        }

        protected override Status Update(T context, double now) => _act(context, now);

        protected override void Stop(T context, double now) => _stop?.Invoke(context);
    }

    public sealed class Wait<T> : Node<T> {
        private readonly double _seconds;
        private double _until;

        public Wait(double seconds) {
            _seconds = seconds;
            Name = $"Wait {seconds}s";
        }

        protected override void Start(T context, double now) => _until = now + _seconds;

        protected override Status Update(T context, double now) => now >= _until ? Status.Success : Status.Running;
    }

    // Runs the child its brain chooses, one per action of the brain's design, by index; a new choice aborts the running
    // child. A child ending, done or failed, tells the brain it finished, and the brain chooses again: it runs until
    // nothing can be chosen.
    public sealed class Choose<T> : Composite<T> {
        private readonly Brain<T, int> _brain;
        private int _running = -1;

        public Choose(Brain<T, int> brain, params Node<T>[] byAction) : base(byAction) {
            if (byAction.Length != brain.Design.Actions.Count) {
                throw new ArgumentException($"{brain.Design.Name} has {brain.Design.Actions.Count} actions, {byAction.Length} children given.");
            }
            _brain = brain;
        }

        protected override Status Update(T context, double now) {
            Choice<int> choice = _brain.Decide(context, now);
            if (!choice.Valid) {
                return Status.Failure;
            }
            if (_running >= 0 && _running != choice.Action) {
                Nodes[_running].Abort(context, now);
            }
            _running = choice.Action;
            Status status = Nodes[_running].Tick(context, now);
            if (status != Status.Running) {
                _brain.Finish(now);
                _running = -1;
            }
            return Status.Running;
        }

        protected override void Stop(T context, double now) {
            _running = -1;
        }
    }

    // A blackboard key: its name and its slot among its type's.
    public sealed class Key<TValue> {
        private static int _count;

        public readonly string Name;
        internal readonly int Index;

        public Key(string name) {
            Name = name;
            Index = Interlocked.Increment(ref _count) - 1;
        }
    }

    // What an agent's trees share: typed values by key, no boxing.
    public sealed class Blackboard {
        private readonly Dictionary<Type, object> _stores = new Dictionary<Type, object>();

        private sealed class Store<TValue> {
            public TValue[] Values = new TValue[8];
            public bool[] Has = new bool[8];
        }

        public void Set<TValue>(Key<TValue> key, TValue value) {
            Store<TValue> store = StoreOf<TValue>();
            if (key.Index >= store.Values.Length) {
                int size = Math.Max(key.Index + 1, store.Values.Length * 2);
                Array.Resize(ref store.Values, size);
                Array.Resize(ref store.Has, size);
            }
            store.Values[key.Index] = value;
            store.Has[key.Index] = true;
        }

        public TValue Get<TValue>(Key<TValue> key, TValue fallback = default) {
            Store<TValue> store = StoreOf<TValue>();
            return key.Index < store.Has.Length && store.Has[key.Index] ? store.Values[key.Index] : fallback;
        }

        public bool Has<TValue>(Key<TValue> key) {
            Store<TValue> store = StoreOf<TValue>();
            return key.Index < store.Has.Length && store.Has[key.Index];
        }

        public void Clear<TValue>(Key<TValue> key) {
            Store<TValue> store = StoreOf<TValue>();
            if (key.Index < store.Has.Length) {
                store.Has[key.Index] = false;
                store.Values[key.Index] = default;
            }
        }

        private Store<TValue> StoreOf<TValue>() {
            if (!_stores.TryGetValue(typeof(TValue), out object store)) {
                store = new Store<TValue>();
                _stores.Add(typeof(TValue), store);
            }
            return (Store<TValue>)store;
        }
    }
}
