using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Evaluation
{
    [CreateAssetMenu(fileName = "Set", menuName = "Utility Designer/New Consideration Set")]
    public class ConsiderationSet : ScriptableObject
    {
        [SerializeField] [HideInInspector] internal List<Consideration> considerations = new();
        [SerializeField] [HideInInspector] internal bool local;


        /// <summary>
        /// Sets the value of a Consideration.
        /// </summary>
        /// <param name="considerationName">The name of the Consideration to search for.</param>
        /// <param name="newValue">The new value of the Consideration.</param>
        /// <returns>True if the Consideration with the specified name was found, false otherwise.</returns>
        public bool SetConsideration(string considerationName, float newValue)
        {
            if (local)
                return false;
            
            foreach (var consideration in considerations.Where(c => c.designation == considerationName))
            {
                consideration.Value = newValue;
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Sets the value of a Consideration.
        /// </summary>
        /// <param name="considerationName">The name of the Consideration to search for.</param>
        /// <param name="newValue">The new value of the Consideration.</param>
        /// <param name="utilityDesigner">Only required when the ConsiderationSet is local. Used to access its ConsiderationSet.</param>
        /// <returns>True if the Consideration with the specified name was found, false otherwise.</returns>
        public bool SetConsideration(string considerationName, float newValue, global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner utilityDesigner)
        {
            if (!local)
                return SetConsideration(considerationName, newValue);

            if (utilityDesigner == null)
                return false;
            
            foreach (var consideration in utilityDesigner.localConsiderationSets[this].considerations
                         .Where(c => c.designation == considerationName))
            {
                consideration.Value = newValue;
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Sets the value of a Consideration.
        /// </summary>
        /// <param name="considerationName">The name of the Consideration to search for.</param>
        /// <param name="newValue">The new value of the Consideration.</param>
        /// <param name="gameObject">Only required when the ConsiderationSet is local. Used to access the ConsiderationSet
        /// on its UtilityDesigner component.</param>
        /// <returns>True if the Consideration with the specified name was found, false otherwise.</returns>
        public bool SetConsideration(string considerationName, float newValue, GameObject gameObject)
        {
            if (!local)
                return SetConsideration(considerationName, newValue);

            if (gameObject == null)
                return false;
            
            global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner selectedUtilityDesigner = gameObject.GetComponent<global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner>();
            if (selectedUtilityDesigner == null)
                return false;
            
            foreach (var consideration in selectedUtilityDesigner.localConsiderationSets[this].considerations
                         .Where(c => c.designation == considerationName))
            {
                consideration.Value = newValue;
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Sets the value of a Consideration.
        /// </summary>
        /// <param name="considerationName">The name of the Consideration to search for.</param>
        /// <param name="amount">The new value of the Consideration.</param>
        /// <returns>True if the Consideration with the specified name was found, false otherwise.</returns>
        public bool ChangeConsideration(string considerationName, float amount)
        {
            if (local)
                return false;
    
            foreach (var consideration in considerations.Where(c => c.designation == considerationName))
            {
                consideration.Value += amount;
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Sets the value of a Consideration.
        /// </summary>
        /// <param name="considerationName">The name of the Consideration to search for.</param>
        /// <param name="amount">The new value of the Consideration.</param>
        /// <param name="utilityDesigner">Only required when the ConsiderationSet is local. Used to access its ConsiderationSet.</param>
        /// <returns>True if the Consideration with the specified name was found, false otherwise.</returns>
        public bool ChangeConsideration(string considerationName, float amount, global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner utilityDesigner)
        {
            if (!local)
                return ChangeConsideration(considerationName, amount);

            if (utilityDesigner == null)
                return false;
    
            foreach (var consideration in utilityDesigner.localConsiderationSets[this].considerations
                         .Where(c => c.designation == considerationName))
            {
                consideration.Value += amount;
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Sets the value of a Consideration.
        /// </summary>
        /// <param name="considerationName">The name of the Consideration to search for.</param>
        /// <param name="amount">The new value of the Consideration.</param>
        /// <param name="gameObject">Only required when the ConsiderationSet is local. Used to access the ConsiderationSet
        /// on its UtilityDesigner component.</param>
        /// <returns>True if the Consideration with the specified name was found, false otherwise.</returns>
        public bool ChangeConsideration(string considerationName, float amount, GameObject gameObject)
        {
            if (!local)
                return ChangeConsideration(considerationName, amount);

            if (gameObject == null)
                return false;

            global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner selectedUtilityDesigner = gameObject.GetComponent<global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner>();
            if (selectedUtilityDesigner == null)
                return false;

            foreach (var consideration in selectedUtilityDesigner.localConsiderationSets[this].considerations
                         .Where(c => c.designation == considerationName))
            {
                consideration.Value += amount;
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Retrieves the value of a Consideration by its name.
        /// </summary>
        /// <param name="considerationName">The name of the Consideration to search for.</param>
        /// <returns>The value of the Consideration with the specified name, or 0 if not found.</returns>
        public float GetConsideration(string considerationName)
        {
            if (local)
                return 0f;

            return considerations.FirstOrDefault(c => c.designation == considerationName)?.Value ?? 0f;
        }
        
        /// <summary>
        /// Retrieves the value of a Consideration by its name.
        /// </summary>
        /// <param name="considerationName">The name of the Consideration to search for.</param>
        /// <param name="utilityDesigner">Only required when the ConsiderationSet is local. Used to access its ConsiderationSet.</param>
        /// <returns>The value of the Consideration with the specified name, or 0 if not found.</returns>
        public float GetConsideration(string considerationName, global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner utilityDesigner)
        {
            if (!local)
                return GetConsideration(considerationName);

            if (utilityDesigner == null)
                return 0f;
    
            return utilityDesigner.localConsiderationSets[this].considerations
                .FirstOrDefault(c => c.designation == considerationName)?.Value ?? 0f;
        }
        
        /// <summary>
        /// Retrieves the value of a Consideration by its name.
        /// </summary>
        /// <param name="considerationName">The name of the Consideration to search for.</param>
        /// <param name="gameObject">Only required when the ConsiderationSet is local. Used to access the ConsiderationSet
        /// on its UtilityDesigner component.</param>
        /// <returns>The value of the Consideration with the specified name, or 0 if not found.</returns>
        public float GetConsideration(string considerationName, GameObject gameObject)
        {
            if (!local)
                return GetConsideration(considerationName);

            if (gameObject == null)
                return 0f;

            global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner selectedUtilityDesigner = gameObject.GetComponent<global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner>();
            if (selectedUtilityDesigner == null)
                return 0f;

            return selectedUtilityDesigner.localConsiderationSets[this].considerations
                .FirstOrDefault(c => c.designation == considerationName)?.Value ?? 0f;
        }
    }
}
