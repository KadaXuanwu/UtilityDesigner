using System;
using System.Collections;
using System.Reflection;
using System.Text;
using KadaXuanwu.UtilityDesigner.Core;
using KadaXuanwu.UtilityDesigner.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Node = KadaXuanwu.UtilityDesigner.Core.Node;

namespace KadaXuanwu.UtilityDesigner.Editor {
    // A node in the graph. The root is the one without a core node: its child is the tree of the state.
    public class NodeView : UnityEditor.Experimental.GraphView.Node {
        private const string UxmlGuid = "67be898aceac0af41abe3b5c6f4b3b14";
        private static readonly Vector2 RootPosition = new Vector2(375, 10);

        internal Action<NodeView> onNodeSelected;
        internal readonly Node node;
        internal Port input;
        internal Port output;

        private readonly State _state;
        private readonly UtilityDesignerEditorWindow _window;
        private readonly Label _labelDetails;
        private readonly VisualElement _titleContainer;

        internal NodeView(Node coreNode, State state, UtilityDesignerEditorWindow window) : base(AssetDatabase.GUIDToAssetPath(UxmlGuid)) {
            node = coreNode;
            _state = state;
            _window = window;
            title = node == null ? "Root" : Utils.Readable(node.GetType().Name);
            viewDataKey = node == null ? "root" : node.Id;
            style.left = node == null ? RootPosition.x : node.X;
            style.top = node == null ? RootPosition.y : node.Y;

            if (node != null) {
                input = AddPort(Direction.Input, Port.Capacity.Single, inputContainer, FlexDirection.Column);
            }
            if (node == null || node is DecoratorNode) {
                output = AddPort(Direction.Output, Port.Capacity.Single, outputContainer, FlexDirection.ColumnReverse);
            } else if (node is CompositeNode) {
                output = AddPort(Direction.Output, Port.Capacity.Multi, outputContainer, FlexDirection.ColumnReverse);
            }
            AddToClassList(node == null ? "root" : node is ActionNode ? "action" : node is ConditionalNode ? "conditional"
                : node is CompositeNode ? "composite" : "decorator");
            if (node == null) {
                capabilities &= ~Capabilities.Deletable;
            }

            _labelDetails = this.Q<Label>("details-label");
            _titleContainer = this.Q<VisualElement>("title");
            UpdateDetails();
        }

        internal string Description => node == null ? "The first node to be executed." : node.Description;

        private static Port AddPort(Direction direction, Port.Capacity capacity, VisualElement container, FlexDirection flexDirection) {
            Port port = new NodePort(direction, capacity) { portName = "" };
            port.style.flexDirection = flexDirection;
            container.Add(port);
            container.style.height = 14f;
            container.style.flexDirection = FlexDirection.Row;
            return port;
        }

        // The settings of the node on its face.
        internal void UpdateDetails() {
            StringBuilder text = new StringBuilder();
            if (node != null) {
                foreach (FieldInfo field in NodeTypes.SettingsOf(node)) {
                    SceneRefAttribute sceneRef = field.GetCustomAttribute<SceneRefAttribute>();
                    text.Append(text.Length > 0 ? "\n" : "").Append(sceneRef?.Label ?? Utils.Readable(field.Name)).Append(": <b>")
                        .Append(sceneRef != null ? SceneRefName(sceneRef, (int)field.GetValue(node)) : field.GetValue(node)).Append("</b>");
                }
            }
            bool any = text.Length > 0;
            _labelDetails.parent.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            _titleContainer.style.paddingTop = any ? 0 : 4;
            _titleContainer.style.paddingBottom = any ? 0 : 4;
            _labelDetails.text = text.ToString();
        }

        private string SceneRefName(SceneRefAttribute sceneRef, int index) {
            IList list = _window.SceneReferences != null ? _window.SceneReferences.GetList(sceneRef.Type) : null;
            object item = list != null && index >= 0 && index < list.Count ? list[index] : null;
            return item is Component component && component != null ? component.gameObject.name : item != null ? item.ToString() : index.ToString();
        }

        public override void SetPosition(Rect newPos) {
            base.SetPosition(newPos);
            if (node != null) {
                node.X = newPos.xMin;
                node.Y = newPos.yMin;
            }
        }

        public override void OnSelected() {
            base.OnSelected();
            onNodeSelected?.Invoke(this);
        }

        public override void OnUnselected() {
            base.OnUnselected();
            onNodeSelected?.Invoke(null);
            UpdateDetails();
        }

        internal void AddChild(Node child) {
            if (node == null) {
                _state.Tree = child;
            } else if (node is DecoratorNode decorator) {
                decorator.Child = child;
            } else if (node is CompositeNode composite && !composite.Nodes.Contains(child)) {
                composite.Nodes.Add(child);
            }
        }

        internal void RemoveChild(Node child) {
            if (node == null) {
                if (_state.Tree == child) {
                    _state.Tree = null;
                }
            } else if (node is DecoratorNode decorator) {
                if (decorator.Child == child) {
                    decorator.Child = null;
                }
            } else if (node is CompositeNode composite) {
                composite.Nodes.Remove(child);
            }
        }

        internal void SortChildren() {
            if (node is CompositeNode composite) {
                composite.Nodes.Sort((left, right) => left.X.CompareTo(right.X));
            }
        }

        // In play: what the node is doing.
        internal void UpdateState() {
            RemoveFromClassList("running");
            RemoveFromClassList("failure");
            RemoveFromClassList("success");
            switch (node == null ? _state.TreeState : node.State) {
                case NodeState.Running:
                    AddToClassList("running");
                    break;
                case NodeState.Failure:
                    AddToClassList("failure");
                    break;
                case NodeState.Success:
                    AddToClassList("success");
                    break;
            }
        }
    }
}
