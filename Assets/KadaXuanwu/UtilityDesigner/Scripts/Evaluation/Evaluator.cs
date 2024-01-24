using System;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Evaluation
{
    [Serializable]
    public class Evaluator
    {
        [SerializeField] internal string designation;
        [SerializeField] internal string considerationDesignation;
        [SerializeField] internal float weight;
        [SerializeField] internal AnimationCurve curve;
        [SerializeField] internal float curveMinValue;
        [SerializeField] internal float curveMaxValue;

        internal float lastScore;

        private Consideration _consideration;
        private float _valueDifference;


        internal void Initialize(global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner utilityDesigner)
        {
            _consideration = Utils.FindConsideration(utilityDesigner.considerationSets, considerationDesignation, utilityDesigner);
            
            // Only calculate once
            _valueDifference = curveMaxValue - curveMinValue;
            if (_valueDifference <= 0)
                _valueDifference = float.Epsilon;
        }
        
        internal float ScoreConsideration()
        {
            if (_consideration == null)
                return 0;
            
            lastScore = curve.Evaluate(Mathf.Clamp01((_consideration.Value - curveMinValue) / _valueDifference));
            return lastScore;
        }
    }
}
