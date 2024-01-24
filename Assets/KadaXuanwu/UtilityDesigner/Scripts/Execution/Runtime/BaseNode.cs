using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using UnityEngine;
using UnityEngine.UIElements;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime
{
    [Serializable]
    public abstract class BaseNode : ICloneable
    {
        /// <summary>
        /// Grants access to the SceneReferences instance attached to the same GameObject as the UtilityDesigner script.
        /// </summary>
        protected SceneReferences SceneRefs => UtilityDesigner != null ? UtilityDesigner.sceneReferences : null;

        /// <summary>
        /// Grants access to the UtilityDesigner script.
        /// </summary>
        protected global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner UtilityDesigner => _utilityDesigner;
        
        /// <summary>
        /// Grants access to the GameObject the UtilityDesigner script is attached to.
        /// </summary>
        protected GameObject ThisGameObject => _thisGameObject;
        
        /// <summary>
        /// A brief description of the node to be displayed in the node inspector.
        /// </summary>
        public virtual string Description { get; }

        /// <summary>
        /// The state of the node.
        /// </summary>
        public enum NodeState
        {
            Disabled,
            Running,
            Failure,
            Success
        }

        [NonSerialized] internal NodeState nodeState = NodeState.Disabled;
        [NonSerialized] internal bool started;
        [NonSerialized] internal bool enabled;
        [SerializeField] internal string guid;
        [SerializeField] internal Vector2 position;
        [SerializeField] [TextArea] public string notes;

        [NonSerialized] private global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner _utilityDesigner;
        [NonSerialized] private GameObject _thisGameObject;
        [NonSerialized] private VisualElement _inspectorContent;
        [NonSerialized] private string _details;


        internal void InitializeNode(global::KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner utilDesigner, GameObject thisGameObject = null)
        {
            started = false;
            enabled = false;
            _utilityDesigner = utilDesigner;
            _thisGameObject = thisGameObject;
        }

        internal NodeState Update()
        {
            if (!started)
            {
                OnAwake();
                started = true;
            }
            
            if (!enabled)
            {
                OnEnable();
                enabled = true;
                nodeState = NodeState.Running;
            }

            nodeState = OnUpdate();

            if (nodeState != NodeState.Running)
            {
                OnDisable();
                enabled = false;
            }

            return nodeState;
        }

        internal virtual void Terminate()
        {
            if (nodeState == NodeState.Running)
            {
                OnDisable();
                enabled = false;
            }
            
            nodeState = NodeState.Disabled;
        }

        /// <summary>
        /// Called when the node is started for the first time, intended for initialization logic.
        /// </summary>
        protected virtual void OnAwake() { }
        
        /// <summary>
        /// Called every time the node is enabled, intended for enabling logic.
        /// </summary>
        protected virtual void OnEnable() { }
        
        /// <summary>
        /// Called every time the node is disabled, intended for disabling logic.
        /// </summary>
        protected virtual void OnDisable() { }
        
        /// <summary>
        /// Called every execution tick, as defined in the UtilityDesigner script.
        /// </summary>
        /// <returns>The new state of the node.</returns>
        protected abstract NodeState OnUpdate();
        
        /// <summary>
        /// Retrieves a ConsiderationSet by its name.
        /// </summary>
        /// <param name="considerationSetName">The name of the ConsiderationSet to search for.</param>
        /// <returns>The ConsiderationSet with the specified name, or null if not found.</returns>
        protected ConsiderationSet GetConsiderationSet(string considerationSetName)
        {
            return UtilityDesigner != null
                ? UtilityDesigner.considerationSets.FirstOrDefault(c => c.name == considerationSetName)
                : null;
        }
        
        /// <summary>
        /// Registers custom member variables to be displayed in the node, by using the "AddVariable" method.
        /// </summary>
        protected virtual void RegisterSerializedVariables() { }
        
        /// <summary>
        /// Creates and adds a new dropdown to the node inspector based on the provided parameters.
        /// </summary>
        /// <typeparam name="T">The type of the variable.</typeparam>
        /// <param name="name">The name of the variable.</param>
        /// <param name="variable">The variable, whose value is to be serialized.</param>
        protected void AddVariable<T>(string name, T variable)
        {
            string value;

            if (variable is Component or StateSet)
            {
                value = variable switch
                {
                    Component component => component.gameObject.name,
                    StateSet decider => decider.designation,
                    _ => ""
                };
            }
            else if (IsPrimitiveDataType<T>())
                value = variable == null ? "" : variable.ToString();
            else
                value = ConvertUnityTypeToString(variable);

            string readableName = Utils.CamelCaseToReadable(name);

            if (_details != null && _details.Contains(readableName))
            {
                string pattern = $@"({readableName}:\s)([^\n]+)";
                string replacement = $"{readableName}: <b>{value}</b>";

                _details = Regex.Replace(_details, pattern, replacement);
                return;
            }

            _details = _details == null
                ? string.Concat(_details, $"{readableName}: <b>{value}</b>")
                : string.Concat(_details, "\n", $"{readableName}: <b>{value}</b>");
        }

        private string ConvertUnityTypeToString(object obj)
        {
            if (obj == null)
                return "";
            
            return obj switch
            {
                Vector2 vector2 => $"({vector2.x}, {vector2.y})",
                Vector3 vector3 => $"({vector3.x}, {vector3.y}, {vector3.z})",
                Vector4 vector4 => $"({vector4.x}, {vector4.y}, {vector4.z}, {vector4.w})",
                Quaternion quaternion => $"({quaternion.x}, {quaternion.y}, {quaternion.z}, {quaternion.w})",
                Color color => $"({color.r}, {color.g}, {color.b}, {color.a})",
                Bounds bounds => $"(Center: {bounds.center}, Size: {bounds.size})",
                Rect rect => $"(Position: {rect.position}, Size: {rect.size})",
                LayerMask layerMask => LayerMask.LayerToName(layerMask.value),
                _ => obj.ToString()
            };
        }
        
        private bool IsPrimitiveDataType<T>()
        {
            Type type = typeof(T);
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }
        
        internal string GetSerializedValue()
        {
            RegisterSerializedVariables();
            return _details;
        }
        
        /// <summary>
        /// Registers custom dropdowns to be displayed in the node inspector, by using the "AddDropdown" method.
        /// </summary>
        protected virtual void RegisterDropdowns() { }
        
        /// <summary>
        /// Creates and adds a new dropdown to the node inspector based on the provided parameters.
        /// </summary>
        /// <typeparam name="T">The type of the items in the dropdown.</typeparam>
        /// <param name="nameInInspector">The name of the dropdown in the node inspector.</param>
        /// <param name="listType">The list of items to populate the dropdown.</param>
        /// <param name="selectedIndex">The index of the initially selected item in the dropdown.</param>
        /// <param name="onSelectedIndexChanged">The action to perform when the selected item in the dropdown changes.</param>
        protected void AddDropdown<T>(string nameInInspector, List<T> listType, int selectedIndex, Action<int> onSelectedIndexChanged)
        {
            if (_inspectorContent == null)
                return;
            
            if (listType == null)
            {
                var helpBox = new HelpBox($"Add a list of type <b>{typeof(T).Name}</b> to SceneReferences", HelpBoxMessageType.Warning);
                _inspectorContent.Add(helpBox);
                return;
            }

            if (listType.Count == 0)
            {
                var helpBox = new HelpBox($"The list of type <b>{typeof(T).Name}</b> in SceneReferences is empty", HelpBoxMessageType.Info);
                _inspectorContent.Add(helpBox);
                return;
            }
            
            var dropdown = new PopupField<T>(nameInInspector, listType, selectedIndex,
                item => item is Component component ? component.gameObject.name :
                    item is StateSet decider ? decider.designation :
                    item != null ? item.ToString() : "None",
                item => item is Component component ? component.gameObject.name :
                    item is StateSet decider ? decider.designation :
                    item != null ? item.ToString() : "None");

            dropdown.RegisterValueChangedCallback(evt =>
            {
                onSelectedIndexChanged?.Invoke(listType.IndexOf(evt.newValue));
            });
            _inspectorContent.Add(dropdown);
        }

        internal void InitializedDropdowns(VisualElement inspectorContent)
        {
            _inspectorContent = inspectorContent;
            
            if (SceneRefs != null)
                RegisterDropdowns();
            else
            {
                var helpBox = new HelpBox("Scene References missing.", HelpBoxMessageType.Info);
                var spacer = new VisualElement
                {
                    style =
                    {
                        height = 5
                    }
                };
                _inspectorContent.Add(helpBox);
                _inspectorContent.Add(spacer);
            }
        }
        
        /// <summary>
        /// Creates a deep copy of the current object, including its fields, lists, and other ICloneable objects.
        /// </summary>
        /// <returns>A deep copy of the current object.</returns>
        public object Clone()
        {
            Type type = GetType();
            BaseNode newNode = (BaseNode)Activator.CreateInstance(type);

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo[] fields = type.GetFields(flags);

            foreach (FieldInfo field in fields)
            {
                object fieldValue = field.GetValue(this);
                object copiedValue = DeepCopy(fieldValue);
                field.SetValue(newNode, copiedValue);
            }

            return newNode;
        }
        
        private object DeepCopy(object original)
        {
            switch (original)
            {
                case null:
                    return null;
                case BaseNode baseNode:
                    return baseNode.Clone();
                case IList list:
                {
                    Type listType = original.GetType();
                    IList newList = (IList)Activator.CreateInstance(listType);

                    foreach (var item in list)
                    {
                        object copiedItem = DeepCopy(item);
                        newList.Add(copiedItem);
                    }

                    return newList;
                }
                case ICloneable cloneable:
                    return cloneable.Clone();
                default:
                    return original.GetType().IsValueType ? original : null;
            }
        }
    }
}
