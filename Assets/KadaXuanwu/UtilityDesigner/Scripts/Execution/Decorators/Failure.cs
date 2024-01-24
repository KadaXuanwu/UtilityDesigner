using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Decorators
{
    public class Failure : DecoratorNode
    {
        public override string Description => "Returns running while the child is running, and failure otherwise.";
        
        
        protected override NodeState OnUpdate()
        {
            NodeState status = child.Update();
            return status == NodeState.Success ? NodeState.Failure : status;
        }
    }
}
