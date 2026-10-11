using KadaXuanwu.UtilityDesigner.Core;
using UnityEngine;
using Designer = KadaXuanwu.UtilityDesigner.Runtime.UtilityDesigner;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class KillDeer : ActionNode
    {
        public int foodReward;
        
        private bool _referenceMissing;
        private GameObject _deer;


        protected override void OnAwake()
        {
            _referenceMissing = false;
            Designer designer = Designer.Of(Agent);
            if (designer == null || designer.SceneReferences == null)
            {
                _referenceMissing = true;
                return;
            }

            _deer = designer.SceneReferences.GetRef<Transform>("Deer").gameObject;

            if (_deer == null)
                _referenceMissing = true;
        }

        protected override NodeState OnUpdate()
        {
            if (_referenceMissing)
                return NodeState.Failure;

            if (Random.Range(0, 250) > Agent.GetConsideration("UD James", "Energy") +
                Agent.GetConsideration("UD James", "Strength"))
            {
                _deer.SendMessage("RealizeDanger", SendMessageOptions.DontRequireReceiver);
                Agent.SetConsideration("UD Environment", "Deer nearby", 0);

                return NodeState.Failure;
            }

            Agent.ChangeConsideration("UD James", "Food", foodReward);
            _deer.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
            Agent.SetConsideration("UD Environment", "Deer nearby", 0);

            return NodeState.Success;
        }
    }
}
