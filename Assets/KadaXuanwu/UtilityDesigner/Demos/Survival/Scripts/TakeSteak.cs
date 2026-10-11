using KadaXuanwu.UtilityDesigner.Core;
using UnityEngine;
using UnityEngine.AI;
using Designer = KadaXuanwu.UtilityDesigner.Runtime.UtilityDesigner;

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
            Designer designer = Designer.Of(Agent);
            if (designer == null || designer.SceneReferences == null)
            {
                _referenceMissing = true;
                return;
            }

            _navMeshAgentJames = designer.GetComponent<NavMeshAgent>();
            _transformJames = designer.GetComponent<Transform>();

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