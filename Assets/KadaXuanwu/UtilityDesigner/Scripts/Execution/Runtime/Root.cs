using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime
{
    public class Root : BaseNode
    {
        public override string Description => "The first node to be executed.";
        
        [SerializeReference] internal BaseNode child;
        
        
        protected override void OnEnable() { }

        protected override void OnDisable() { }

        protected override NodeState OnUpdate()
        {
            return child.Update();
        }

        internal override void Terminate()
        {
            child.Terminate();
            base.Terminate();
        }
    }
}
