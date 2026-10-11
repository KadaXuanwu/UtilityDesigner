using System.Collections.Generic;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Runtime
{
    public class TransformReferences : SceneReferences
    {
        [SerializeField] private List<Transform> transforms;
        
        
        protected override void RegisterCustomLists()
        {
            AddList(transforms);
        }
    }
}
