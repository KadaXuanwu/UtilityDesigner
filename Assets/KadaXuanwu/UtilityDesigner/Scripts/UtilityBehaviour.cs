using System.Collections.Generic;
using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts
{
    public class UtilityBehaviour : ScriptableObject
    {
        [SerializeField] [HideInInspector] internal List<StateSet> stateSets;
        [SerializeField] [HideInInspector] internal List<ConsiderationSet> considerationSets;
    }
}
