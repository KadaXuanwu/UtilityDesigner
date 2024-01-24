using System;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Evaluation
{
    [Serializable]
    public class Precondition
    {
        [SerializeField] internal string designation;
        [SerializeField] internal string considerationDesignation;
        [SerializeField] internal Comparator comparator;
        [SerializeField] internal float value;

        internal bool lastConditionMet;
        
        private Consideration _consideration;


        internal void Initialize(global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner utilityDesigner)
        {
            _consideration = Utils.FindConsideration(utilityDesigner.considerationSets, considerationDesignation, utilityDesigner);
        }
        
        internal bool ConditionMet()
        {
            if (_consideration == null)
            {
                lastConditionMet = false;
                return false;
            }
            
            switch (comparator)
            {
                case Comparator.GreaterThan:
                    lastConditionMet = _consideration.Value > value;
                    break;
                case Comparator.LessThan:
                    lastConditionMet = _consideration.Value < value;
                    break;
                case Comparator.GreaterThanOrEqualTo:
                    lastConditionMet = _consideration.Value >= value;
                    break;
                case Comparator.LessThanOrEqualTo:
                    lastConditionMet = _consideration.Value <= value;
                    break;
                case Comparator.EqualTo:
                    lastConditionMet = Mathf.RoundToInt(_consideration.Value) == Mathf.RoundToInt(value);
                    break;
                case Comparator.NotEqualTo:
                    lastConditionMet = Mathf.RoundToInt(_consideration.Value) != Mathf.RoundToInt(value);
                    break;
                default:
                    lastConditionMet = Mathf.RoundToInt(_consideration.Value) == Mathf.RoundToInt(value);
                    break;
            }
            
            return lastConditionMet;
        }
    }
}
