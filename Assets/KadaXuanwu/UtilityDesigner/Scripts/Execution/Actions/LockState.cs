using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class LockState : ActionNode
    {
        public override string Description => "Locks the state and returns success.";
        
        
        protected override void OnEnable()
        {
            UtilityDesigner.StateLocked = true;
        }

        protected override NodeState OnUpdate()
        {
            return NodeState.Success;
        }
    }
}
