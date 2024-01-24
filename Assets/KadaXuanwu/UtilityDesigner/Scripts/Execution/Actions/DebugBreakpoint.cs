using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class DebugBreakpoint : ActionNode
    {
        public override string Description => "Pauses the game and returns success.";
        
        protected override void OnEnable()
        {
            Debug.Break();
        }

        protected override NodeState OnUpdate()
        {
            return NodeState.Success;
        }
    }
}
