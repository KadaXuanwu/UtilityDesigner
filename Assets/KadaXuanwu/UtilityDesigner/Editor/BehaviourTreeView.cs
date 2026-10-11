using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KadaXuanwu.UtilityDesigner.Core;
using KadaXuanwu.UtilityDesigner.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Node = KadaXuanwu.UtilityDesigner.Core.Node;

namespace KadaXuanwu.UtilityDesigner.Editor {
    // A state's behaviour tree as a graph. Nodes not connected to the root are kept only while the state is shown.
    [UxmlElement]
    public partial class BehaviourTreeView : GraphView {
        internal static readonly (Type kind, string category)[] Kinds = {
            (typeof(ActionNode), "Actions"), (typeof(ConditionalNode), "Conditionals"),
            (typeof(CompositeNode), "Composites"), (typeof(DecoratorNode), "Decorators"),
        };

        internal Action<NodeView> onNodeSelected;

        private State _state;
        private UtilityDesignerEditorWindow _window;
        private NodeSearchWindow _nodeSearchWindow;

        public BehaviourTreeView() {
            styleSheets.Add(Resources.Load<StyleSheet>("BehaviourTreeView"));
            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        // The node types of a kind that the editor offers, each with its sub category and whether it is the game's own.
        internal static IEnumerable<(Type type, string subCategory, bool custom)> NodeTypesOf(Type kind) {
            foreach (Type type in TypeCache.GetTypesDerivedFrom(kind)) {
                if (type.IsAbstract || !type.IsVisible || type.GetConstructor(Type.EmptyTypes) == null) {
                    continue;
                }
                bool custom = type.Assembly != typeof(Node).Assembly && type.Assembly != typeof(Nodes).Assembly;
                yield return (type, type.GetCustomAttribute<CategoryPathAttribute>()?.SubCategoryPath ?? "", custom);
            }
        }

        private void OnKeyDown(KeyDownEvent e) {
            if (_window == null) {
                return;
            }
            if (e.ctrlKey && e.keyCode == KeyCode.C) {
                _window.CopyNode();
                e.StopPropagation();
            } else if (e.ctrlKey && e.keyCode == KeyCode.V) {
                _window.PasteNode();
                e.StopPropagation();
            } else if (e.keyCode == KeyCode.Space && _nodeSearchWindow != null) {
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), _nodeSearchWindow);
                e.StopPropagation();
            }
        }

        internal void PopulateView(State state, UtilityDesignerEditorWindow window) {
            _state = state;
            _window = window;
            if (_nodeSearchWindow == null) {
                _nodeSearchWindow = ScriptableObject.CreateInstance<NodeSearchWindow>();
                _nodeSearchWindow.Initialize(this, window);
            }
            graphViewChanged -= OnGraphViewChanged;
            DeleteElements(graphElements.ToList());
            graphViewChanged += OnGraphViewChanged;
            if (state == null) {
                return;
            }
            NodeView root = new NodeView(null, state, window) { onNodeSelected = onNodeSelected };
            AddElement(root);
            AddSubtree(state.Tree);
            if (state.Tree != null) {
                AddElement(root.output.ConnectTo(View(state.Tree).input));
            }
        }

        // A copied subtree, not connected to anything, with ids of its own, where the mouse is.
        internal void AddCopy(Node tree) {
            if (_state == null || tree == null || Application.isPlaying) {
                return;
            }
            // Beside the original when there is no mouse to ask.
            Vector2 offset = Event.current == null ? new Vector2(40f, 40f)
                : this.ChangeCoordinatesTo(contentViewContainer, this.WorldToLocal(Event.current.mousePosition)) - new Vector2(tree.X, tree.Y);
            foreach (Node node in Utils.AllNodes(tree)) {
                node.Id = Utils.NewId();
                node.X += offset.x;
                node.Y += offset.y;
            }
            AddSubtree(tree);
        }

        private void AddSubtree(Node tree) {
            List<Node> all = Utils.AllNodes(tree);
            foreach (Node node in all) {
                // A design written by hand may have no ids, or the same one twice.
                if (node.Id.Length == 0 || GetNodeByGuid(node.Id) != null) {
                    node.Id = Utils.NewId();
                }
                AddElement(new NodeView(node, _state, _window) { onNodeSelected = onNodeSelected });
            }
            foreach (Node node in all) {
                foreach (Node child in node.Children) {
                    AddElement(View(node).output.ConnectTo(View(child).input));
                }
            }
        }

        private NodeView View(Node node) => GetNodeByGuid(node.Id) as NodeView;

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) {
            return ports.ToList().Where(endPort => endPort.direction != startPort.direction && endPort.node != startPort.node).ToList();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change) {
            change.elementsToRemove?.ForEach(element => {
                if (element is Edge edge && edge.output.node is NodeView parent && edge.input.node is NodeView child) {
                    parent.RemoveChild(child.node);
                }
            });
            change.edgesToCreate?.ForEach(edge => {
                if (edge.output.node is NodeView parent && edge.input.node is NodeView child) {
                    parent.AddChild(child.node);
                }
            });
            // A composite runs its children from left to right.
            nodes.ForEach(node => (node as NodeView)?.SortChildren());
            return change;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
            Vector2 position = this.ChangeCoordinatesTo(contentViewContainer, evt.localMousePosition);
            foreach ((string path, Type type) in MenuItems()) {
                evt.menu.AppendAction(path, _ => CreateNode(type, position));
            }
        }

        // The menu of the Add Node button.
        internal void ShowCreateMenu(Vector2 position) {
            GenericMenu menu = new GenericMenu();
            foreach ((string path, Type type) in MenuItems()) {
                menu.AddItem(new GUIContent(path), false, () => CreateNode(type, position));
            }
            menu.ShowAsContext();
        }

        private static IEnumerable<(string path, Type type)> MenuItems() {
            foreach ((Type kind, string category) in Kinds) {
                foreach ((Type type, string subCategory, bool custom) in NodeTypesOf(kind)) {
                    string name = Utils.Readable(type.Name);
                    yield return (custom ? $"{category} (custom)/{subCategory}/{name}".Replace("//", "/") : $"{category}/{name}", type);
                }
            }
        }

        // Not while playing: the running agent would not know the node.
        internal void CreateNode(Type type, Vector2 position) {
            if (_state == null || Application.isPlaying) {
                return;
            }
            Node node = (Node)Activator.CreateInstance(type);
            node.Id = Utils.NewId();
            node.X = position.x;
            node.Y = position.y;
            AddElement(new NodeView(node, _state, _window) { onNodeSelected = onNodeSelected });
        }

        internal void UpdateNodeStates() {
            nodes.ForEach(node => (node as NodeView)?.UpdateState());
        }
    }
}
