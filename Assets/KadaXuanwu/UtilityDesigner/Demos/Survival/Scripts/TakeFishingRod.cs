using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class TakeFishingRod : ActionNode
    {
        private NavMeshAgent _navMeshAgentJames;
        private Transform _transformJames;
        private bool _referenceMissing;
        private GameObject _fishingPole;
        
        
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
                _fishingPole = _transformJames.Find("fishing-pole").gameObject;
            
            if (_fishingPole == null || _navMeshAgentJames == null)
                _referenceMissing = true;
        }

        protected override void OnEnable()
        {
            if (_referenceMissing)
                return;

            _navMeshAgentJames.enabled = false;
            _transformJames.rotation = Quaternion.Euler(0, 0, 0);
            _fishingPole.SetActive(true);
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
            _fishingPole.SetActive(false);
        }
    }
}
