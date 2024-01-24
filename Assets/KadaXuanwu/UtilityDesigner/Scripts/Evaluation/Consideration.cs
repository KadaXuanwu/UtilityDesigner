using System;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Evaluation
{
    [Serializable]
    public class Consideration
    {
        [SerializeField] internal string designation;
        [SerializeField] internal Type type;
        [SerializeField] internal float initialValue;
        [SerializeField] internal bool setMinValue;
        [SerializeField] internal float minValue;
        [SerializeField] internal bool setMaxValue;
        [SerializeField] internal float maxValue;
        [SerializeField] internal float changePerSecond;
        [SerializeField] internal bool useRealTime;

        private float _value;
        internal float Value
        {
            get => _value;
            set
            {
                _value = value;
                
                if (setMinValue && value < minValue)
                    _value = minValue;
            
                if (setMaxValue && value > maxValue)
                    _value = maxValue;
            }
        }
        
        private float _lastUpdateTimeStamp;
        

        internal enum Type
        {
            Float
        }
        
        
        internal void Initialize()
        {
            Value = initialValue;
            _lastUpdateTimeStamp = useRealTime ? Time.realtimeSinceStartup : Time.time;
        }

        internal void Update()
        {
            if (changePerSecond == 0)
                return;
            
            float currentTime = useRealTime ? Time.realtimeSinceStartup : Time.time;
            Value += changePerSecond * (currentTime - _lastUpdateTimeStamp);
            
            _lastUpdateTimeStamp = currentTime;
        }
    }
}
