using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Decorators
{
    public class Invert : DecoratorNode
    {
        public override string Description => "Returns running while the child is running, and inverts the return value otherwise.";
        
        
        protected override NodeState OnUpdate()
        {
            switch (child.Update())
            {
                case NodeState.Running:
                    return NodeState.Running;
                case NodeState.Failure:
                    return NodeState.Success;
                case NodeState.Success:
                    return NodeState.Failure;
            }
            
            return NodeState.Failure;
        }
    }
}
