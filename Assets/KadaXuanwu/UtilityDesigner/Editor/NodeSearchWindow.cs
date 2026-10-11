using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace KadaXuanwu.UtilityDesigner.Editor {
    // The node search (Space in the graph): every node type under its kind, a custom one under its [CategoryPath].
    public class NodeSearchWindow : ScriptableObject, ISearchWindowProvider {
        private BehaviourTreeView _graphView;
        private EditorWindow _window;
        private Texture2D _indentationIcon;

        public void Initialize(BehaviourTreeView graphView, EditorWindow window) {
            _graphView = graphView;
            _window = window;
            _indentationIcon = new Texture2D(1, 1);
            _indentationIcon.SetPixel(0, 0, Color.clear);
            _indentationIcon.Apply();
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context) {
            List<SearchTreeEntry> tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent("Create Node"), 0) };
            foreach ((Type kind, string category) in BehaviourTreeView.Kinds) {
                tree.Add(new SearchTreeGroupEntry(new GUIContent(category), 1));
                // Sub categories first, each path once, then the nodes without one; all by name.
                string shown = "";
                foreach ((Type type, string subCategory, bool _) in BehaviourTreeView.NodeTypesOf(kind)
                             .OrderBy(entry => entry.subCategory.Length == 0).ThenBy(entry => entry.subCategory, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(entry => entry.type.Name, StringComparer.OrdinalIgnoreCase)) {
                    string[] parts = subCategory.Length == 0 ? Array.Empty<string>() : subCategory.Split('/');
                    string[] before = shown.Length == 0 ? Array.Empty<string>() : shown.Split('/');
                    int same = 0;
                    while (same < parts.Length && same < before.Length && parts[same] == before[same]) {
                        same++;
                    }
                    for (int i = same; i < parts.Length; i++) {
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(parts[i]), 2 + i));
                    }
                    shown = subCategory;
                    tree.Add(new SearchTreeEntry(new GUIContent(type.Name, _indentationIcon)) { level = 2 + parts.Length, userData = type });
                }
            }
            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context) {
            // Near the mouse: the window's own corner stands in for the panel's.
            Vector2 inWindow = context.screenMousePosition - _window.position.position;
            _graphView.CreateNode((Type)entry.userData, _graphView.contentViewContainer.WorldToLocal(inWindow));
            return true;
        }
    }
}
