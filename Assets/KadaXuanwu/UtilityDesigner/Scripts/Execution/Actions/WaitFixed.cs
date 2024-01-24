using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class WaitFixed : ActionNode
    {
        public override string Description => "Waits for a fixed duration and then returns true.";

        public float duration = 1f;
        
        private float _startTime;
        public bool useRealTime;
        
        
        protected override void RegisterSerializedVariables()
        {
            AddVariable(nameof(duration), duration);
        }

        protected override void OnEnable()
        {
            _startTime = useRealTime ? Time.realtimeSinceStartup : Time.time;
        }

        protected override NodeState OnUpdate()
        {
            float currentTime = useRealTime ? Time.realtimeSinceStartup : Time.time;
            
            return currentTime - _startTime < duration ? NodeState.Running : NodeState.Success;
        }
    }
}
