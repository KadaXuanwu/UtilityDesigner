using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime
{
    public abstract class DecoratorNode : BaseNode
    {
        [SerializeReference] internal BaseNode child;


        internal override void Terminate()
        {
            child.Terminate();
            base.Terminate();
        }
    }
}