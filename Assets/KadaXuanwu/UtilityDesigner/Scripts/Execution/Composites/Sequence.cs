using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Composites
{
    public class Sequence : CompositeNode
    {
        public override string Description => "Sequentially executes all children from left to right.\n" +
                                              "Returns success if all child nodes execute successfully.\n" +
                                              "Returns failure upon encountering the first failed child node.";
        
        private int _current;
        
        
        protected override void OnEnable()
        {
            _current = 0;
        }

        protected override NodeState OnUpdate()
        {
            var child = children[_current];

            switch (child.Update())
            {
                case NodeState.Running:
                    return NodeState.Running;
                case NodeState.Failure:
                    return NodeState.Failure;
                case NodeState.Success:
                    _current++;
                    break;
            }

            return _current == children.Count ? NodeState.Success : NodeState.Running;
        }
    }
}
