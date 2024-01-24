using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Decorators
{
    public class Success : DecoratorNode
    {
        public override string Description => "Returns running while the child is running, and success otherwise.";
        
        
        protected override NodeState OnUpdate()
        {
            var status = child.Update();
            return status == NodeState.Failure ? NodeState.Success : status;
        }
    }
}
