using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class TakeSteak : ActionNode
    {
        private NavMeshAgent _navMeshAgentJames;
        private Transform _transformJames;
        private bool _referenceMissing;
        private GameObject _steak;
        
        
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

            if (_transformJames != null)
                _steak = _transformJames.Find("meat-steak").gameObject;
            
            if (_steak == null || _navMeshAgentJames == null)
                _referenceMissing = true;
        }

        protected override void OnEnable()
        {
            if (_referenceMissing)
                return;

            _navMeshAgentJames.enabled = false;
            _transformJames.rotation = Quaternion.Euler(0, 30, 0);
            _steak.SetActive(true);
        }

        protected override NodeState OnUpdate()
        {
            return _referenceMissing ? NodeState.Failure : NodeState.Running;
        }

        protected override void OnDisable()
        {
            if (_referenceMissing)
                return;

            _navMeshAgentJames.enabled = true;
            _steak.SetActive(false);
        }
    }
}