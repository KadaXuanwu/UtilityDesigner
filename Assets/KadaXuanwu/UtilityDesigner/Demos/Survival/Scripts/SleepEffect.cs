using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class SleepEffect : ActionNode
    {
        private NavMeshAgent _navMeshAgentJames;
        private Transform _transformJames;
        private Transform _transformHome;
        private bool _referenceMissing;
        private Vector3 _originalPosition;
        
        
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
            _transformHome = SceneRefs.GetRef<Transform>("Home");

            if (_transformJames == null || _navMeshAgentJames == null || _transformHome == null)
                _referenceMissing = true;
        }

        protected override void OnEnable()
        {
            if (_referenceMissing)
                return;
            
            _navMeshAgentJames.enabled = false;
            _originalPosition = _transformJames.position;
            _transformJames.position = _transformHome.position + new Vector3(2.5f, 0, 0);
        }

        protected override NodeState OnUpdate()
        {
            return _referenceMissing ? NodeState.Failure : NodeState.Running;
        }

        protected override void OnDisable()
        {
            if (_referenceMissing)
                return;

            _transformJames.position = _originalPosition;
            _navMeshAgentJames.enabled = true;
        }
    }
}
