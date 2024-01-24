using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class MoveToTransform : ActionNode
    {
        public override string Description => "Sets the destination of the Nav Mesh Agent to the desired location.\n" +
                                              "Returns success when the destination is reached, and failure when " +
                                              "references are missing or the path isn't complete";
        
        [SerializeField] private int _locationIndex;
        
        public bool setSpeed;
        public float speed;
        public bool setStoppingDistance;
        public float stoppingDistance;

        private NavMeshAgent _navMeshAgent;
        private Transform _agentTransform;
        private Transform _location;
        private bool _referenceMissing;
        private float _initialSpeed;
        private float _initialStoppingDistance;


        protected override void RegisterSerializedVariables()
        {
            if (SceneRefs != null)
                AddVariable("Location", SceneRefs.GetRef<Transform>(_locationIndex));
        }

        protected override void RegisterDropdowns()
        {
            AddDropdown("Location", SceneRefs.GetListOfType<Transform>(), _locationIndex,
                newIndex => { _locationIndex = newIndex; });
        }
        
        protected override void OnAwake()
        {
            _referenceMissing = false;
            if (SceneRefs == null || ThisGameObject == null)
            {
                _referenceMissing = true;
                return;
            }

            _navMeshAgent = ThisGameObject.GetComponent<NavMeshAgent>();
            _agentTransform = ThisGameObject.GetComponent<Transform>();
            _location = SceneRefs.GetRef<Transform>(_locationIndex);

            if (_navMeshAgent == null || _agentTransform == null || _location == null)
                _referenceMissing = true;
        }

        protected override void OnEnable()
        {
            _initialSpeed = _navMeshAgent.speed;
            _initialStoppingDistance = _navMeshAgent.stoppingDistance;

            if (_referenceMissing)
                return;

            if (setSpeed)
                _navMeshAgent.speed = speed;

            if (setStoppingDistance)
                _navMeshAgent.stoppingDistance = stoppingDistance;
        }
        
        protected override NodeState OnUpdate()
        {
            _navMeshAgent.destination = _location.position;

            if (_referenceMissing || _navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete)
                return NodeState.Failure;

            float sqrDistanceToTarget = (_agentTransform.position - _location.position).sqrMagnitude;
            float stoppingDistance = _navMeshAgent.stoppingDistance;
            float sqrStoppingDistance = stoppingDistance * stoppingDistance;

            return sqrDistanceToTarget <= sqrStoppingDistance ? NodeState.Success : NodeState.Running;
        }
        
        protected override void OnDisable()
        {
            _navMeshAgent.speed = _initialSpeed;
            _navMeshAgent.stoppingDistance = _initialStoppingDistance;
        }
    }
}
