using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class WaitRandom : ActionNode
    {
        public override string Description => "Waits for a random duration and then returns true.";

        public float minDuration = 0f;
        public float maxDuration = 1f;
        public bool useRealTime;
        
        private float _startTime;
        private float _duration;


        protected override void RegisterSerializedVariables()
        {
            AddVariable(nameof(minDuration), minDuration);
            AddVariable(nameof(maxDuration), maxDuration);
        }

        protected override void OnEnable()
        {
            _startTime = useRealTime ? Time.realtimeSinceStartup : Time.time;
            _duration = Random.Range(minDuration, maxDuration);
        }

        protected override NodeState OnUpdate()
        {
            float currentTime = useRealTime ? Time.realtimeSinceStartup : Time.time;
            
            return currentTime - _startTime < _duration ? NodeState.Running : NodeState.Success;
        }
    }
}