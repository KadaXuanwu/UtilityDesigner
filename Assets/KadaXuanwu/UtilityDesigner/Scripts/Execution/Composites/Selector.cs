using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Composites
{
    public class Selector : CompositeNode
    {
        public override string Description => "Sequentially executes all children from left to right.\n" +
                                              "Returns failure if all child nodes fail to execute.\n" +
                                              "Returns success upon encountering the first successful child node.";
        
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
                    _current++;
                    break;
                case NodeState.Success:
                    return NodeState.Success;
            }

            return _current == children.Count ? NodeState.Failure : NodeState.Running;
        }
    }
}
