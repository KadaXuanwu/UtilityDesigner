using System.Linq;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class ChangeStateSet : ActionNode
    {
        public override string Description => "Changes the state set and returns success if it was successful.";
        
        [SerializeField] private int _stateSetIndex;

        private bool _success;


        protected override void RegisterSerializedVariables()
        {
            AddVariable("StateSet", UtilityDesigner.GetStateSets().Values.ElementAt(_stateSetIndex));
        }

        protected override void RegisterDropdowns()
        {
            AddDropdown("StateSet", UtilityDesigner.GetStateSets().Values.ToList(), _stateSetIndex, newIndex => {
                _stateSetIndex = newIndex;
            });
        }
        
        protected override void OnEnable()
        {
            _success = UtilityDesigner.SetRunningStateSet(_stateSetIndex);
        }

        protected override NodeState OnUpdate()
        {
            return _success ? NodeState.Success : NodeState.Failure;
        }
    }
}
