using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class DoPushupMovement : ActionNode
    {
        private const float PushupSpeed = 3f;
        private const float PushupRange = 60f;
    
        private float _time;
        private NavMeshAgent _navMeshAgentJames;
        private Transform _transformJames;
        private bool _referenceMissing;
        private Vector3 _pivotPoint;
        private float _angle;

        
        protected override void OnAwake()
        {
            _referenceMissing = false;
            if (SceneRefs == null || ThisGameObject == null)
            {
                _referenceMissing = true;
                return;
            }

            _navMeshAgentJames = ThisGameObject.GetComponent<NavMeshAgent>();
            _transformJames = ThisGameObject.GetComponent<Transform>();

            if (_transformJames == null || _navMeshAgentJames == null)
                _referenceMissing = true;
        }

        protected override void OnEnable()
        {
            if (_referenceMissing)
                return;
            
            _navMeshAgentJames.enabled = false;
            _pivotPoint = _transformJames.position - new Vector3(0, 0.4f, 0);
        }

        protected override NodeState OnUpdate()
        {
            if (_referenceMissing)
                return NodeState.Failure;

            _angle = (Mathf.Sin(Time.time * PushupSpeed) + 1) / 2 * PushupRange;
            _transformJames.rotation = Quaternion.Euler(_angle, _transformJames.rotation.eulerAngles.y, _transformJames.rotation.eulerAngles.z);

            return NodeState.Running;
        }

        protected override void OnDisable()
        {
            if (_referenceMissing)
                return;

            _navMeshAgentJames.enabled = true;
        }
    }
}
