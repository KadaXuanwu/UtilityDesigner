using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Conditionals
{
    public class CompareDistance : ConditionalNode
    {
        public override string Description => "Compares the position between two transforms to a set value.\n" +
                                              "Returns success if the comparison matches the selected comparator and failure otherwise.";
        
        [SerializeField] private int _positionIndex1;
        [SerializeField] private int _positionIndex2;
        
        public Comparator comparator;
        public float distance;

        
        protected override void RegisterSerializedVariables()
        {
            if (SceneRefs != null)
            {
                AddVariable("Position 1", SceneRefs.GetRef<Transform>(_positionIndex1));
                AddVariable("Position 2", SceneRefs.GetRef<Transform>(_positionIndex2));
            }
            
            AddVariable(nameof(comparator), comparator);
            AddVariable(nameof(distance), distance);
        }

        protected override void RegisterDropdowns()
        {
            AddDropdown("Position 1", SceneRefs.GetListOfType<Transform>(), _positionIndex1,
                newIndex => { _positionIndex1 = newIndex; });
            AddDropdown("Position 2", SceneRefs.GetListOfType<Transform>(), _positionIndex2,
                newIndex => { _positionIndex2 = newIndex; });
        }

        protected override NodeState OnUpdate()
        {
            if (SceneRefs == null)
                return NodeState.Failure;
            
            Transform position1 = SceneRefs.GetRef<Transform>(_positionIndex1);
            Transform position2 = SceneRefs.GetRef<Transform>(_positionIndex2);

            if(position1 == null || position2 == null)
                return NodeState.Failure;

            float actualDistance = Vector3.Distance(position1.transform.position, position2.transform.position);

            bool conditionMet = false;
            switch (comparator)
            {
                case Comparator.GreaterThan:
                    conditionMet = actualDistance > distance;
                    break;
                case Comparator.LessThan:
                    conditionMet = actualDistance < distance;
                    break;
                case Comparator.GreaterThanOrEqualTo:
                    conditionMet = actualDistance >= distance;
                    break;
                case Comparator.LessThanOrEqualTo:
                    conditionMet = actualDistance <= distance;
                    break;
                case Comparator.EqualTo:
                    conditionMet = Mathf.RoundToInt(actualDistance) == Mathf.RoundToInt(distance);
                    break;
                case Comparator.NotEqualTo:
                    conditionMet = Mathf.RoundToInt(actualDistance) != Mathf.RoundToInt(distance);
                    break;
            }

            return conditionMet ? NodeState.Success : NodeState.Failure;
        }
    }
}