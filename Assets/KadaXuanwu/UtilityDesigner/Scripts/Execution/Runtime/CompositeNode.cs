using System.Collections.Generic;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime
{
    public abstract class CompositeNode : BaseNode
    {
        [SerializeReference] internal List<BaseNode> children = new();


        internal override void Terminate()
        {
            foreach (var child in children)
                child.Terminate();
            base.Terminate();
        }
    }
}