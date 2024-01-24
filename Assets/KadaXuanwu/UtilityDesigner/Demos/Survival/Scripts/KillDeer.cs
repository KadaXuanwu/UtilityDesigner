using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class KillDeer : ActionNode
    {
        public int foodReward;
        
        private ConsiderationSet _jamesConsiderations;
        private ConsiderationSet _environmentConsiderations;
        private bool _referenceMissing;
        private GameObject _deer;


        protected override void RegisterSerializedVariables()
        {
            AddVariable(nameof(foodReward), foodReward);
        }

        protected override void OnAwake()
        {
            _referenceMissing = false;
            if (SceneRefs == null)
            {
                _referenceMissing = true;
                return;
            }

            _jamesConsiderations = GetConsiderationSet("UD James");
            _environmentConsiderations = GetConsiderationSet("UD Environment");
            _deer = SceneRefs.GetRef<Transform>("Deer").gameObject;

            if (_jamesConsiderations == null || _environmentConsiderations == null || _deer == null)
                _referenceMissing = true;
        }

        protected override NodeState OnUpdate()
        {
            if (_referenceMissing)
                return NodeState.Failure;

            if (Random.Range(0, 250) > _jamesConsiderations.GetConsideration("Energy", UtilityDesigner) +
                _jamesConsiderations.GetConsideration("Strength", UtilityDesigner))
            {
                _deer.SendMessage("RealizeDanger", SendMessageOptions.DontRequireReceiver);
                _environmentConsiderations.SetConsideration("Deer nearby", 0);

                return NodeState.Failure;
            }

            _jamesConsiderations.ChangeConsideration("Food", foodReward, UtilityDesigner);
            _deer.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
            _environmentConsiderations.SetConsideration("Deer nearby", 0);

            return NodeState.Success;
        }
    }
}
