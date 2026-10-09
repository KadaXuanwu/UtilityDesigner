using System;
using System.Collections.Generic;
using System.Text;

namespace KadaXuanwu.UtilityDesigner.Core {
    // Hierarchical task network: a goal breaks into steps by methods, each method a way to do it, picked by utility
    // among those that apply. Primitives are what is done, each run by a behaviour tree.
    public abstract class PlanTask<T> {
        public string Name = "";
    }

    // A step: its tree does it; while Valid holds it may run, and when it stops holding the plan is broken.
    public sealed class Primitive<T> : PlanTask<T> {
        public Node<T> Operator;
        public Func<T, bool> Valid = _ => true;
    }

    // A goal, done by one of its methods.
    public sealed class Compound<T> : PlanTask<T> {
        public readonly List<Method<T>> Methods = new List<Method<T>>();
    }

    // A way to do a goal: when it applies, how good it is now (its utility, a plan's value), and its steps in order.
    public sealed class Method<T> {
        public string Name = "";
        public Func<T, bool> Applies = _ => true;
        public Func<T, float> Utility = _ => 1f;
        public readonly List<PlanTask<T>> Steps = new List<PlanTask<T>>();
    }

    // A goal broken down: the primitives to do in order, the methods chosen on the way (its path, which tells two plans
    // apart), and the value of its top method when made.
    public sealed class Plan<T> {
        public readonly List<Primitive<T>> Steps = new List<Primitive<T>>();
        public readonly List<Method<T>> Methods = new List<Method<T>>();
        public int Next;
        public float Value;

        public bool Done => Next >= Steps.Count;
        public float Progress => Steps.Count == 0 ? 1f : (float)Next / Steps.Count;
        public Primitive<T> Step => Done ? null : Steps[Next];

        public string Path {
            get {
                StringBuilder path = new StringBuilder();
                foreach (Method<T> method in Methods) {
                    path.Append(path.Length > 0 ? "/" : "").Append(method.Name);
                }
                return path.ToString();
            }
        }

        public bool SameWay(Plan<T> other) {
            if (other == null || other.Methods.Count != Methods.Count) {
                return false;
            }
            for (int i = 0; i < Methods.Count; i++) {
                if (other.Methods[i] != Methods[i]) {
                    return false;
                }
            }
            return true;
        }
    }

    public static class Planner {
        // The plan for a goal now: at every goal the best method that applies; null if some goal has none.
        public static Plan<T> Decompose<T>(Compound<T> goal, T context) {
            Plan<T> plan = new Plan<T>();
            return Add(goal, context, plan, true) ? plan : null;
        }

        private static bool Add<T>(PlanTask<T> task, T context, Plan<T> plan, bool top) {
            if (task is Primitive<T> primitive) {
                plan.Steps.Add(primitive);
                return true;
            }
            Compound<T> compound = (Compound<T>)task;
            Method<T> best = null;
            float bestUtility = 0f;
            foreach (Method<T> method in compound.Methods) {
                if (!method.Applies(context)) {
                    continue;
                }
                float utility = method.Utility(context);
                if (utility > bestUtility) {
                    best = method;
                    bestUtility = utility;
                }
            }
            if (best == null) {
                return false;
            }
            if (top) {
                plan.Value = bestUtility;
            }
            plan.Methods.Add(best);
            foreach (PlanTask<T> step in best.Steps) {
                if (!Add(step, context, plan, false)) {
                    return false;
                }
            }
            return true;
        }
    }

    // Keeps a goal's plan going, and keeps to it: a plan is dropped when a step's validity breaks (it is broken) or when
    // another way of doing the goal is worth more than finishing this one and changing over costs. The running plan's
    // value counts what is done of it (SunkCost) so a ram most of the way to the gate is not given up for a whim.
    public sealed class PlanRunner<T> {
        private readonly Compound<T> _goal;
        private double _reconsidered = double.NegativeInfinity;

        // Seconds between looks at other ways while a plan runs.
        public float Reconsider = 1f;
        // Another way takes over only worth more than Interrupt times the running plan's value plus SwitchCost.
        public float Interrupt = 1.5f;
        public float SwitchCost;
        // The running plan's value times 1 + SunkCost * its progress.
        public float SunkCost = 1f;

        public Plan<T> Current { get; private set; }
        // Plans made and given up, for a trace.
        public int Made { get; private set; }
        public int Dropped { get; private set; }
        public string LastDrop { get; private set; } = "";

        public PlanRunner(Compound<T> goal) {
            _goal = goal;
        }

        // A tick: plans if it has none, gives up a broken or beaten plan, and runs the step due. Failure when the goal
        // has no plan.
        public Status Tick(T context, double now) {
            if (Current != null && !Current.Done && !Current.Step.Valid(context)) {
                Drop(context, now, $"{Current.Step.Name} no longer valid");
            }
            if (Current != null && now - _reconsidered >= Reconsider) {
                _reconsidered = now;
                Plan<T> other = Planner.Decompose(_goal, context);
                float running = Running(context);
                if (other != null && !other.SameWay(Current) && other.Value > running * Interrupt + SwitchCost) {
                    Drop(context, now, $"{other.Path} worth {other.Value:0.##} over {Current.Path} at {running:0.##}");
                    Start(other, now);
                }
            }
            if (Current == null || Current.Done) {
                Plan<T> plan = Planner.Decompose(_goal, context);
                if (plan == null) {
                    Current = null;
                    return Status.Failure;
                }
                Start(plan, now);
            }
            Status status = Current.Step.Operator.Tick(context, now);
            if (status == Status.Success) {
                Current.Next++;
                return Current.Done ? Status.Success : Status.Running;
            }
            if (status == Status.Failure) {
                Drop(context, now, $"{Current.Step.Name} failed");
            }
            return Status.Running;
        }

        // The running plan's value now: its top method's utility re-read, with what is done of it counted.
        public float Running(T context) {
            if (Current == null || Current.Methods.Count == 0) {
                return 0f;
            }
            return Current.Methods[0].Utility(context) * (1f + SunkCost * Current.Progress);
        }

        private void Start(Plan<T> plan, double now) {
            Current = plan;
            Made++;
            _reconsidered = now;
        }

        private void Drop(T context, double now, string why) {
            Current?.Step?.Operator.Abort(context, now);
            Current = null;
            Dropped++;
            LastDrop = why;
        }
    }
}
