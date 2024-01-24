using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Decorators
{
    public class Repeat : DecoratorNode
    {
        public override string Description => "Always returns running.";
        
        
        protected override NodeState OnUpdate()
        {
            child.Update();
            return NodeState.Running;
        }
    }
}
