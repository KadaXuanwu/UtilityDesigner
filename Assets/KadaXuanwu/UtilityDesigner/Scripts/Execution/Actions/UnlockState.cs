using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class UnlockState : ActionNode
    {
        public override string Description => "Unlocks the state and returns success.";
        
        
        protected override void OnEnable()
        {
            UtilityDesigner.StateLocked = false;
        }

        protected override NodeState OnUpdate()
        {
            return NodeState.Success;
        }
    }
}