using System.Collections.Generic;
using System.IO;
using System.Linq;
using KadaXuanwu.UtilityDesigner.Core;
using KadaXuanwu.UtilityDesigner.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace KadaXuanwu.UtilityDesigner.Editor {
    // Edits a consideration set asset. Closing it (it closes when it loses focus) saves the asset and brings every
    // behaviour that has the set up to it.
    public class ConsiderationSetEditorWindow : EditorWindow {
        private static readonly Vector2 WindowMinSize = new Vector2(550, 450);
        private const float ItemConsiderationHeight = 80;

        private static ConsiderationSetAsset _set;
        private static string _lastSelectedConsideration;

        private ListView _considerationsListView;
        private Consideration _selected;
        private VisualElement _inspectorContent;
        private TextField _textFieldName;
        private FloatField _floatFieldInitialValue;
        private Toggle _toggleSetMinValue;
        private FloatField _floatFieldMinValue;
        private Toggle _toggleSetMaxValue;
        private FloatField _floatFieldMaxValue;
        private FloatField _floatFieldChangePerSecond;

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceID) {
            if (!(Selection.activeObject is ConsiderationSetAsset asset)) {
                return false;
            }
            Open(asset);
            return true;
        }

        internal static void Open(ConsiderationSetAsset asset) {
            if (_set != asset) {
                _lastSelectedConsideration = null;
            }
            _set = asset;
            ConsiderationSetEditorWindow window = GetWindow<ConsiderationSetEditorWindow>();
            window.titleContent = new GUIContent("Consideration Set");
            window.minSize = WindowMinSize;
        }

        private void CreateGUI() {
            if (_set == null) {
                return;
            }
            VisualElement root = rootVisualElement;
            string directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            root.Add(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{directory}/UXML/ConsiderationSetEditor.uxml").Instantiate());
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{directory}/UXML/TemplateItemConsiderationEdit.uxml");

            _inspectorContent = root.Q<VisualElement>("InspectorContent");
            _textFieldName = root.Q<TextField>("TextFieldDesignation");
            _floatFieldInitialValue = root.Q<FloatField>("FloatFieldInitialValue");
            _toggleSetMinValue = root.Q<Toggle>("ToggleSetMinValue");
            _floatFieldMinValue = root.Q<FloatField>("FloatFieldMinValue");
            _toggleSetMaxValue = root.Q<Toggle>("ToggleSetMaxValue");
            _floatFieldMaxValue = root.Q<FloatField>("FloatFieldMaxValue");
            _floatFieldChangePerSecond = root.Q<FloatField>("FloatFieldChangePerSecond");

            root.Q<Button>("ButtonAddConsideration").clicked += AddConsideration;
            Toggle toggleLocal = root.Q<Toggle>("ToggleLocal");
            toggleLocal.value = _set.local;
            toggleLocal.RegisterValueChangedCallback(evt => _set.local = evt.newValue);
            _textFieldName.RegisterValueChangedCallback(evt => {
                _selected.Name = Utils.VerifyItemName("Consideration ", evt.newValue, _set.considerations, c => c.Name, _selected.Name);
                _lastSelectedConsideration = _selected.Name;
                _considerationsListView.RefreshItems();
            });
            _floatFieldInitialValue.RegisterValueChangedCallback(evt => _selected.InitialValue = evt.newValue);
            _toggleSetMinValue.RegisterValueChangedCallback(evt => {
                _selected.SetMinValue = evt.newValue;
                _floatFieldMinValue.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            _floatFieldMinValue.RegisterValueChangedCallback(evt => {
                _selected.MinValue = Mathf.Max(0f, evt.newValue);
                _floatFieldMinValue.SetValueWithoutNotify(_selected.MinValue);
            });
            _toggleSetMaxValue.RegisterValueChangedCallback(evt => {
                _selected.SetMaxValue = evt.newValue;
                _floatFieldMaxValue.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            _floatFieldMaxValue.RegisterValueChangedCallback(evt => {
                _selected.MaxValue = Mathf.Max(0f, evt.newValue);
                _floatFieldMaxValue.SetValueWithoutNotify(_selected.MaxValue);
            });
            _floatFieldChangePerSecond.RegisterValueChangedCallback(evt => _selected.ChangePerSecond = evt.newValue);

            _considerationsListView = new ListView(_set.considerations, ItemConsiderationHeight, () => {
                VisualElement item = template.Instantiate();
                item.Q<Button>("ButtonRemoveElement").clicked += () => {
                    _set.considerations.RemoveAt((int)item.userData);
                    _considerationsListView.ClearSelection();
                    _inspectorContent.style.visibility = Visibility.Hidden;
                    _considerationsListView.Rebuild();
                };
                return item;
            }, (item, i) => {
                item.userData = i;
                item.Q<Label>("LabelTitle").text = _set.considerations[i].Name;
            }) { selectionType = SelectionType.Single };
            _considerationsListView.selectionChanged += LoadConsideration;
            root.Q<VisualElement>("ConsiderationsContent").Add(_considerationsListView);

            int last = _set.considerations.FindIndex(c => c.Name == _lastSelectedConsideration);
            if (last >= 0) {
                _considerationsListView.SetSelection(last);
            }
        }

        private void LoadConsideration(IEnumerable<object> selectedItems) {
            _selected = (Consideration)selectedItems.FirstOrDefault();
            if (_selected == null) {
                return;
            }
            _lastSelectedConsideration = _selected.Name;
            _textFieldName.SetValueWithoutNotify(_selected.Name);
            _floatFieldInitialValue.SetValueWithoutNotify(_selected.InitialValue);
            _toggleSetMinValue.SetValueWithoutNotify(_selected.SetMinValue);
            _floatFieldMinValue.SetValueWithoutNotify(_selected.MinValue);
            _toggleSetMaxValue.SetValueWithoutNotify(_selected.SetMaxValue);
            _floatFieldMaxValue.SetValueWithoutNotify(_selected.MaxValue);
            _floatFieldChangePerSecond.SetValueWithoutNotify(_selected.ChangePerSecond);
            _floatFieldMinValue.style.display = _selected.SetMinValue ? DisplayStyle.Flex : DisplayStyle.None;
            _floatFieldMaxValue.style.display = _selected.SetMaxValue ? DisplayStyle.Flex : DisplayStyle.None;
            _inspectorContent.style.visibility = Visibility.Visible;
        }

        private void AddConsideration() {
            _set.considerations.Add(new Consideration {
                Name = Utils.VerifyItemName("Consideration ", $"Consideration {_set.considerations.Count + 1}", _set.considerations, c => c.Name),
            });
            _considerationsListView.Rebuild();
            _considerationsListView.SetSelection(_set.considerations.Count - 1);
        }

        private void OnLostFocus() {
            if (_set != null) {
                EditorUtility.SetDirty(_set);
                AssetDatabase.SaveAssetIfDirty(_set);
                Behaviours.SyncAll(_set);
                if (HasOpenInstances<UtilityDesignerEditorWindow>()) {
                    GetWindow<UtilityDesignerEditorWindow>(false, null, false).OnConsiderationSetEdited();
                }
            }
            Close();
        }
    }
}
