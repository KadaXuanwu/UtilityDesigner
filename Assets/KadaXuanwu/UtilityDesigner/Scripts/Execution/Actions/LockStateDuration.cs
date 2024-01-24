using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class LockStateDuration : ActionNode
    {
        public override string Description => "Locks the state and returns running until for the defined duration, " +
                                              "after which it unlocks the state and returns success.";
        
        public float duration;
        public bool useRealTime;

        private float _lockTimeStamp;


        protected override void RegisterSerializedVariables()
        {
            AddVariable(nameof(duration), duration);
        }

        protected override void OnEnable()
        {
            UtilityDesigner.StateLocked = true;
            _lockTimeStamp = useRealTime ? Time.realtimeSinceStartup : Time.time;
        }

        protected override NodeState OnUpdate()
        {
            float currentTime = useRealTime ? Time.realtimeSinceStartup : Time.time;
            
            if (currentTime - _lockTimeStamp < duration)
                return NodeState.Running;
            
            UtilityDesigner.StateLocked = false;
            return NodeState.Success;
        }
    }
}