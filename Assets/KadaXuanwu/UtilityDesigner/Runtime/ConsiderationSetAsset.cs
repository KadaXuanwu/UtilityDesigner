using System.Collections.Generic;
using KadaXuanwu.UtilityDesigner.Core;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Runtime {
    // A consideration set as an asset: what the editor adds to behaviours under the asset's name, and the game's handle
    // on the running values. A behaviour's JSON carries its own copy, which the editor keeps the same as the asset.
    [CreateAssetMenu(fileName = "Set", menuName = "Utility Designer/New Consideration Set")]
    public class ConsiderationSetAsset : ScriptableObject {
        [HideInInspector] public List<Consideration> considerations = new List<Consideration>();
        // Each agent has its own values; otherwise every agent shares them.
        [HideInInspector] public bool local;

        // The set as a design has it.
        public ConsiderationSet ToCore() {
            ConsiderationSet set = new ConsiderationSet { Name = name, Local = local };
            foreach (Consideration c in considerations) {
                set.Considerations.Add(new Consideration {
                    Name = c.Name, InitialValue = c.InitialValue, SetMinValue = c.SetMinValue, MinValue = c.MinValue,
                    SetMaxValue = c.SetMaxValue, MaxValue = c.MaxValue, ChangePerSecond = c.ChangePerSecond,
                });
            }
            return set;
        }

        // The running value: of a local set, the agent's own (0 without one); 0 when it is not there.
        public float GetConsideration(string considerationName, UtilityDesigner designer = null) {
            if (!local) {
                return UtilityDesigner.Globals.Find(name, considerationName)?.Value ?? 0f;
            }
            return designer != null && designer.Agent != null ? designer.Agent.GetConsideration(name, considerationName) : 0f;
        }

        public float GetConsideration(string considerationName, GameObject gameObject) =>
            GetConsideration(considerationName, Designer(gameObject));

        // False when it is not there.
        public bool SetConsideration(string considerationName, float newValue, UtilityDesigner designer = null) {
            if (local) {
                return designer != null && designer.Agent != null && designer.Agent.SetConsideration(name, considerationName, newValue);
            }
            Consideration shared = UtilityDesigner.Globals.Find(name, considerationName);
            if (shared == null) {
                return false;
            }
            shared.Value = newValue;
            return true;
        }

        public bool SetConsideration(string considerationName, float newValue, GameObject gameObject) =>
            SetConsideration(considerationName, newValue, Designer(gameObject));

        public bool ChangeConsideration(string considerationName, float amount, UtilityDesigner designer = null) {
            if (local) {
                return designer != null && designer.Agent != null && designer.Agent.ChangeConsideration(name, considerationName, amount);
            }
            Consideration shared = UtilityDesigner.Globals.Find(name, considerationName);
            if (shared == null) {
                return false;
            }
            shared.Value += amount;
            return true;
        }

        public bool ChangeConsideration(string considerationName, float amount, GameObject gameObject) =>
            ChangeConsideration(considerationName, amount, Designer(gameObject));

        private static UtilityDesigner Designer(GameObject gameObject) =>
            gameObject != null ? gameObject.GetComponent<UtilityDesigner>() : null;
    }
}
