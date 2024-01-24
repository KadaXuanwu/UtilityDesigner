using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Conditionals
{
    public class CompareConsideration : ConditionalNode
    {
        public override string Description => "Compares a consideration's value with a set value.\n" +
                                              "Returns success if the comparison matches the selected comparator and failure otherwise.";
        
        public string considerationSetName;
        public string considerationName;
        public Comparator comparator;
        public float value;
        

        protected override void RegisterSerializedVariables()
        {
            AddVariable(nameof(considerationName), considerationName);
            AddVariable(nameof(comparator), comparator);
            AddVariable(nameof(value), value);
        }

        protected override NodeState OnUpdate()
        {
            float? considerationValue = GetConsiderationSet(considerationSetName)?.GetConsideration(considerationName, UtilityDesigner);
            if (considerationValue == null)
                return NodeState.Failure;

            bool conditionMet = false;
            switch (comparator)
            {
                case Comparator.GreaterThan:
                    conditionMet = considerationValue > value;
                    break;
                case Comparator.LessThan:
                    conditionMet = considerationValue < value;
                    break;
                case Comparator.GreaterThanOrEqualTo:
                    conditionMet = considerationValue >= value;
                    break;
                case Comparator.LessThanOrEqualTo:
                    conditionMet = considerationValue <= value;
                    break;
                case Comparator.EqualTo:
                    conditionMet = Mathf.RoundToInt(considerationValue.Value) == Mathf.RoundToInt(value);
                    break;
                case Comparator.NotEqualTo:
                    conditionMet = Mathf.RoundToInt(considerationValue.Value) != Mathf.RoundToInt(value);
                    break;
            }

            return conditionMet ? NodeState.Success : NodeState.Failure;
        }
    }
}