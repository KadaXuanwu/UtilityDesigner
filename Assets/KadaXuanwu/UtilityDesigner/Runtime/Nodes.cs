using System;
using System.Reflection;
using KadaXuanwu.UtilityDesigner.Core;
using UnityEngine;
using UnityEngine.AI;

namespace KadaXuanwu.UtilityDesigner.Runtime {
    // The node types a design can name in Unity: the core's, the ones below and every node class of the game, each
    // under its class name. Two node classes of the same name in different namespaces: the last one found wins.
    public static class Nodes {
        private static bool _registered;

        public static void Register() {
            if (_registered) {
                return;
            }
            _registered = true;
            string core = typeof(Node).Assembly.GetName().Name;
            foreach (Assembly assembly in UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()) {
                if (!Array.Exists(assembly.GetReferencedAssemblies(), reference => reference.Name == core)) {
                    continue;
                }
                Type[] types;
                try {
                    types = assembly.GetTypes();
                } catch (ReflectionTypeLoadException e) {
                    types = e.Types;
                }
                foreach (Type type in types) {
                    if (type != null && type.IsVisible && !type.IsAbstract && typeof(Node).IsAssignableFrom(type)
                        && type.GetConstructor(Type.EmptyTypes) != null) {
                        NodeTypes.Register(type);
                    }
                }
            }
            // Named in code, so that a build's code stripping keeps them. A game's own node classes that only a design
            // names need [Preserve] under high stripping.
            NodeTypes.Register<MoveToPosition>();
            NodeTypes.Register<MoveToTransform>();
            NodeTypes.Register<CompareDistance>();
            NodeTypes.Register<DebugLog>();
            NodeTypes.Register<DebugBreakpoint>();
        }

        // A design from its JSON, with every node type known.
        public static Design Load(string json) {
            Register();
            return Designs.FromJson(json);
        }
    }

    // Moves the agent's NavMeshAgent, with its own speed and stopping distance while it runs.
    public abstract class MoveNode : ActionNode {
        public bool SetSpeed;
        public float Speed;
        public bool SetStoppingDistance;
        public float StoppingDistance;

        protected NavMeshAgent NavMeshAgent;

        private float _initialSpeed;
        private float _initialStoppingDistance;

        protected override void OnAwake() {
            UtilityDesigner designer = UtilityDesigner.Of(Agent);
            NavMeshAgent = designer != null ? designer.GetComponent<NavMeshAgent>() : null;
        }

        protected override void OnEnable() {
            if (NavMeshAgent == null) {
                return;
            }
            _initialSpeed = NavMeshAgent.speed;
            _initialStoppingDistance = NavMeshAgent.stoppingDistance;
            if (SetSpeed) {
                NavMeshAgent.speed = Speed;
            }
            if (SetStoppingDistance) {
                NavMeshAgent.stoppingDistance = StoppingDistance;
            }
        }

        protected override void OnDisable() {
            if (NavMeshAgent == null) {
                return;
            }
            NavMeshAgent.speed = _initialSpeed;
            NavMeshAgent.stoppingDistance = _initialStoppingDistance;
        }
    }

    public sealed class MoveToPosition : MoveNode {
        public Vector3 Pos;

        public override string Description => "Sets the destination of the Nav Mesh Agent to the desired location.\n"
            + "Returns success when the destination is reached, and failure if the path isn't complete";

        protected override NodeState OnUpdate() {
            if (NavMeshAgent == null) {
                return NodeState.Failure;
            }
            NavMeshAgent.destination = Pos;
            if (NavMeshAgent.pathStatus != NavMeshPathStatus.PathComplete) {
                return NodeState.Failure;
            }
            return NavMeshAgent.remainingDistance <= NavMeshAgent.stoppingDistance ? NodeState.Success : NodeState.Running;
        }
    }

    public sealed class MoveToTransform : MoveNode {
        [SceneRef(typeof(Transform), "Location")] public int LocationIndex;

        private Transform _location;

        public override string Description => "Sets the destination of the Nav Mesh Agent to the desired location.\n"
            + "Returns success when the destination is reached, and failure when references are missing or the path isn't complete";

        protected override void OnAwake() {
            base.OnAwake();
            UtilityDesigner designer = UtilityDesigner.Of(Agent);
            _location = designer != null && designer.SceneReferences != null ? designer.SceneReferences.GetRef<Transform>(LocationIndex) : null;
        }

        protected override NodeState OnUpdate() {
            if (NavMeshAgent == null || _location == null) {
                return NodeState.Failure;
            }
            NavMeshAgent.destination = _location.position;
            if (NavMeshAgent.pathStatus != NavMeshPathStatus.PathComplete) {
                return NodeState.Failure;
            }
            float stoppingDistance = NavMeshAgent.stoppingDistance;
            return (NavMeshAgent.transform.position - _location.position).sqrMagnitude <= stoppingDistance * stoppingDistance
                ? NodeState.Success
                : NodeState.Running;
        }
    }

    public sealed class CompareDistance : ConditionalNode {
        [SceneRef(typeof(Transform), "Position 1")] public int PositionIndex1;
        [SceneRef(typeof(Transform), "Position 2")] public int PositionIndex2;
        public Comparator Comparator;
        public float Distance;

        public override string Description => "Compares the position between two transforms to a set value.\n"
            + "Returns success if the comparison matches the selected comparator and failure otherwise.";

        protected override NodeState OnUpdate() {
            UtilityDesigner designer = UtilityDesigner.Of(Agent);
            if (designer == null || designer.SceneReferences == null) {
                return NodeState.Failure;
            }
            Transform position1 = designer.SceneReferences.GetRef<Transform>(PositionIndex1);
            Transform position2 = designer.SceneReferences.GetRef<Transform>(PositionIndex2);
            if (position1 == null || position2 == null) {
                return NodeState.Failure;
            }
            return Comparing.Holds(Vector3.Distance(position1.position, position2.position), Comparator, Distance)
                ? NodeState.Success
                : NodeState.Failure;
        }
    }

    public sealed class DebugLog : ActionNode {
        public string Message = "";

        public override string Description => "Logs a message to the console and returns success.";

        protected override NodeState OnUpdate() {
            Debug.Log(Message);
            return NodeState.Success;
        }
    }

    public sealed class DebugBreakpoint : ActionNode {
        public override string Description => "Pauses the game and returns success.";

        protected override void OnEnable() => Debug.Break();

        protected override NodeState OnUpdate() => NodeState.Success;
    }
}
