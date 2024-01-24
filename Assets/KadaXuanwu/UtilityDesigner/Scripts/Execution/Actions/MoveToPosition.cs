using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class MoveToPosition : ActionNode
    {
        public override string Description => "Sets the destination of the Nav Mesh Agent to the desired location.\n" +
                                              "Returns success when the destination is reached, and failure if " +
                                              "the path isn't complete";

        public Vector3 pos;
        public bool setSpeed;
        public float speed;
        public bool setStoppingDistance;
        public float stoppingDistance;
        
        private NavMeshAgent _navMeshAgent;
        private Transform _agentTransform;
        private bool _referenceMissing;
        private float _initialSpeed;
        private float _initialStoppingDistance;


        protected override void RegisterSerializedVariables()
        {
            AddVariable(nameof(pos), pos);
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

            if (_navMeshAgent == null || _agentTransform == null)
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
            _navMeshAgent.destination = pos;
    
            if (_referenceMissing || _navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete)
                return NodeState.Failure;
    
            return _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance
                ? NodeState.Success
                : NodeState.Running;
        }

        protected override void OnDisable()
        {
            _navMeshAgent.speed = _initialSpeed;
            _navMeshAgent.stoppingDistance = _initialStoppingDistance;
        }
    }
}
