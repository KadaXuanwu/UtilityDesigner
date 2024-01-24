using System;
using System.Collections.Generic;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Composites
{
    public class RandomSequence : CompositeNode
    {
        public override string Description => "Executes all child nodes in a random order.\n" +
                                              "Returns success if all child nodes execute successfully.\n" +
                                              "Returns failure upon encountering the first failed child node.";

        private int _current;
        private Random _random;
        private List<int> _shuffledIndices;
        
        
        protected override void OnEnable()
        {
            _current = 0;
            _random = new Random();
            _shuffledIndices = new List<int>();

            for (int i = 0; i < children.Count; i++)
                _shuffledIndices.Add(i);

            for (int i = _shuffledIndices.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
            }
        }

        protected override NodeState OnUpdate()
        {
            var child = children[_shuffledIndices[_current]];

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

            return _current == _shuffledIndices.Count ? NodeState.Success : NodeState.Running;
        }
    }
}