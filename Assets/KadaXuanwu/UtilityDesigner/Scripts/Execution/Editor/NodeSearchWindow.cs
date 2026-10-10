#if UNITY_EDITOR
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Editor
{
    public class NodeSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private BehaviourTreeView _graphView;
        private EditorWindow _window;
        private Texture2D _indentationIcon;

        private List<SearchTreeEntry> _tree;

        public void Initialize(BehaviourTreeView graphView, EditorWindow window)
        {
            _graphView = graphView;
            _window = window;

            _indentationIcon = new Texture2D(1, 1);
            _indentationIcon.SetPixel(0, 0, Color.clear);
            _indentationIcon.Apply();

            InitializeTree();
        }

        private void InitializeTree()
        {
            _tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node"), 0),
            };

            _tree.AddRange(AddNodeEntries<ActionNode>("Actions", 1));
            _tree.AddRange(AddNodeEntries<ConditionalNode>("Conditionals", 1));
            _tree.AddRange(AddNodeEntries<CompositeNode>("Composites", 1));
            _tree.AddRange(AddNodeEntries<DecoratorNode>("Decorators", 1));
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context) 
            => _tree;

        private List<SearchTreeEntry> AddNodeEntries<T>(string categoryName,
            int initialLevel) where T : class
        {
            List<SearchTreeEntry> entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent(categoryName), initialLevel)
            };

            var subCategories = new Dictionary<string, List<SearchTreeEntry>>();
            var types = TypeCache.GetTypesDerivedFrom<T>();

            var noSubCategoryEntries = new List<SearchTreeEntry>();

            foreach (var type in types)
            {
                //we remember to exclude abstract nodes.
                if (type.IsAbstract) continue;

                //this window sorts using this attribute
                var categoryPathAttribute = type.GetCustomAttribute<CategoryPathAttribute>();
                string subCategoryPath = categoryPathAttribute?.SubCategoryPath ?? string.Empty;

                if (string.IsNullOrEmpty(subCategoryPath) == false)
                {
                    string[] pathParts = subCategoryPath.Split('/');
                    string currentPath = string.Empty;
                    List<SearchTreeEntry> parentEntries = entries;

                    for (int i = 0; i < pathParts.Length; i++)
                    {
                        currentPath = string.IsNullOrEmpty(currentPath)
                            ? pathParts[i]
                            : $"{currentPath}/{pathParts[i]}";

                        if (subCategories.TryGetValue(currentPath, 
                            out List<SearchTreeEntry> subCategoryEntries))
                        {
                            //such path was already added -
                            //so we link parentEntries to list value by key
                            parentEntries = subCategoryEntries;
                        }
                        else
                        {
                            var subCategoryEntry = new SearchTreeGroupEntry(
                                new GUIContent(pathParts[i]), initialLevel + i + 1);
                            subCategoryEntries = new List<SearchTreeEntry>();

                            //such path was not present -
                            //so we:
                            //add to the dict new empty list
                            //link parentEntries to list value by key
                            //add header to parentEntries(which adds it to
                            //mentioned new empty list)
                            subCategories[currentPath] = subCategoryEntries;
                            parentEntries = subCategoryEntries;
                            parentEntries.Add(subCategoryEntry);

                            Debug.Log($"Created sub-category: {currentPath} " +
                                $"at level {initialLevel + i + 1}");
                        }
                    }

                    //add to parentEntries(which should hopefully add
                    //to correct subCategory)
                    parentEntries.Add(new SearchTreeEntry(
                        new GUIContent(type.Name, _indentationIcon))
                    {
                        level = initialLevel + pathParts.Length + 1,
                        userData = type
                    });

                    Debug.Log($"Added node {type.Name} under " +
                        $"{subCategoryPath} at level {initialLevel + pathParts.Length + 1}");
                }
                else
                {
                    //no subcategory so no need to
                    //work with subCategories dict.
                    noSubCategoryEntries.Add(new SearchTreeEntry(
                        new GUIContent(type.Name, _indentationIcon))
                    {
                        level = initialLevel + 1,
                        userData = type
                    });

                    Debug.Log($"Added node {type.Name} directly under " +
                        $"{categoryName} at level {initialLevel + 1}");
                }
            }

            List<string> keys = subCategories.Keys.ToList();

            //this should correctly sort folders.
            keys.Sort();

            for (int i = 0; i < keys.Count; i++)
            {
                var subCategory = subCategories[keys[i]];

                //this should sort elements within subfolders
                subCategory = SortEntries(subCategory);
                entries.AddRange(subCategory);
            }

            //this sorts elements with no subfolders
            noSubCategoryEntries = SortEntries(noSubCategoryEntries);
            entries.AddRange(noSubCategoryEntries);

            return entries;
        }

        private List<SearchTreeEntry> SortEntries(List<SearchTreeEntry> entries)
        {
            //we exclude headers. they need to always be on top of category.
            //we sort them too, but separately.

            if (entries == null || entries.Count == 0)
                return entries;

            var firstEntry = entries[0];
            entries.RemoveAt(0);

            entries = entries.OrderBy(entry => entry.name, 
                StringComparer.OrdinalIgnoreCase).ToList();

            entries.Insert(0, firstEntry);

            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            //I coudn't get the center of the node under the cursor,
            //but near-up-left corner of the node was close enough for me.

            Vector2 screenMousePos = context.screenMousePosition;
            Vector2 localMousePos = _window.rootVisualElement.WorldToLocal(screenMousePos);
            Vector2 finalMousePos = _graphView.contentViewContainer.WorldToLocal(localMousePos);

            _graphView.CreateNode(searchTreeEntry.userData as Type, finalMousePos);

            return true;
        }
    }
}
#endif