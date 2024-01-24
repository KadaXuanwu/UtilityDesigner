using System.Collections.Generic;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Composites
{
    public class Parallel : CompositeNode
    {
        public override string Description => "Executes all child nodes concurrently in parallel.\n" +
                                              "Returns success if all child nodes complete successfully.\n" +
                                              "Returns failure if any child node fails, and aborts the remaining running child nodes.";
        
        private List<NodeState> _childrenStates = new();
        
        
        protected override void OnEnable()
        {
            _childrenStates.Clear();
            children.ForEach(a => {
                _childrenStates.Add(NodeState.Running);
            });
        }

        protected override NodeState OnUpdate()
        {
            bool running = false;
            for (int i = 0; i < _childrenStates.Count; ++i)
            {
                if (_childrenStates[i] != NodeState.Running)
                    continue;
                
                NodeState status = children[i].Update();
                switch (status)
                {
                    case NodeState.Failure:
                        AbortRunningChildren();
                        return NodeState.Failure;
                    case NodeState.Running:
                        running = true;
                        break;
                }

                _childrenStates[i] = status;
            }

            return running ? NodeState.Running : NodeState.Success;
        }

        void AbortRunningChildren()
        {
            for (int i = 0; i < _childrenStates.Count; ++i)
                if (_childrenStates[i] == NodeState.Running)
                    children[i].Terminate();
        }
    }
}
