using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class DebugLog : ActionNode
    {
        public override string Description => "Logs a message to the console and returns success.";
        
        public string message = "";


        protected override NodeState OnUpdate()
        {
            Debug.Log(message);
            return NodeState.Success;
        }
    }
}
