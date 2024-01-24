using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Conditionals
{
    public class TimeCheck : ConditionalNode
    {
        public override string Description => "Compares the current time to a set value.\n" + 
                                              "Returns success if the comparison matches the selected comparator and failure otherwise.";
        
        public Comparator comparator;
        public float time;
        public bool useRealTime;

        
        protected override void RegisterSerializedVariables()
        {
            AddVariable(nameof(comparator), comparator);
            AddVariable(nameof(time), time);
        }

        protected override NodeState OnUpdate()
        {
            float currentTime = useRealTime ? Time.realtimeSinceStartup : Time.time;

            bool conditionMet = false;
            switch (comparator)
            {
                case Comparator.GreaterThan:
                    conditionMet = currentTime > time;
                    break;
                case Comparator.LessThan:
                    conditionMet = currentTime < time;
                    break;
                case Comparator.GreaterThanOrEqualTo:
                    conditionMet = currentTime >= time;
                    break;
                case Comparator.LessThanOrEqualTo:
                    conditionMet = currentTime <= time;
                    break;
                case Comparator.EqualTo:
                    conditionMet = Mathf.RoundToInt(currentTime) == Mathf.RoundToInt(time);
                    break;
                case Comparator.NotEqualTo:
                    conditionMet = Mathf.RoundToInt(currentTime) != Mathf.RoundToInt(time);
                    break;
            }

            return conditionMet ? NodeState.Success : NodeState.Failure;
        }
    }
}