#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace KadaXuanwu.UtilityDesigner.Scripts.Editor
{
    public class ConsiderationSetEditorWindow : EditorWindow
    {
        private static readonly Vector2 WindowMinSize = new(550, 450);
        private const float ItemConsiderationHeight = 80;

        private static ConsiderationSet _selectedConsiderationSet;
        private static UtilityDesignerEditorWindow _utilityDesignerEditorWindowCaller;
        private static string _lastSelectedConsiderationDesignation;
        private static bool _sameSelected;
        private static ConsiderationSet _callerSelectedConsiderationSet;
        
        private VisualTreeAsset _templateItemConsideration;
        private VisualElement _considerationsContent;
        private ListView _considerationsListView;
        private Consideration _selectedConsideration;
        private VisualElement _inspectorContent;

        private Toggle _toggleLocal;
        private TextField _textFieldDesignation;
        private EnumField _enumFieldType;
        private FloatField _floatFieldInitialValue;
        private Toggle _toggleSetMinValue;
        private FloatField _floatFieldMinValue;
        private Toggle _toggleSetMaxValue;
        private FloatField _floatFieldMaxValue;
        private FloatField _floatFieldChangePerSecond;
        private Toggle _toggleUseRealTime;
        
        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceID)
        {
            if (Selection.activeObject is not ConsiderationSet)
                return false;

            _sameSelected = _selectedConsiderationSet == (ConsiderationSet)Selection.activeObject;
            _selectedConsiderationSet = (ConsiderationSet)Selection.activeObject;
            _callerSelectedConsiderationSet = null;
            _utilityDesignerEditorWindowCaller = null;

            OpenWindow();
            return true;
        }

        internal static void OpenAsset(ConsiderationSet considerationSet, ConsiderationSet selectedConsiderationSet, UtilityDesignerEditorWindow caller)
        {
            _sameSelected = _selectedConsiderationSet == considerationSet;
            _selectedConsiderationSet = considerationSet;
            _callerSelectedConsiderationSet = selectedConsiderationSet;
            _utilityDesignerEditorWindowCaller = caller;

            OpenWindow();
        }

        private static void OpenWindow()
        {
            ConsiderationSetEditorWindow window = GetWindow<ConsiderationSetEditorWindow>();
            window.titleContent = new GUIContent("Consideration Set");
            window.minSize = WindowMinSize;
        }

        private void CreateGUI()
        {
            // Cache information
            VisualElement root = rootVisualElement;
            string scriptDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            
            // Load UXML
            VisualElement rootFromUxml = 
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{scriptDirectory}/UXML/ConsiderationSetEditor.uxml").Instantiate();
            root.Add(rootFromUxml);

            // Store references
            _templateItemConsideration =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{scriptDirectory}/UXML/TemplateItemConsiderationEdit.uxml");
            
            _considerationsContent = root.Q<VisualElement>("ConsiderationsContent");
            _inspectorContent = root.Q<VisualElement>("InspectorContent");

            _toggleLocal = root.Q<Toggle>("ToggleLocal");
            _textFieldDesignation = root.Q<TextField>("TextFieldDesignation");
            _enumFieldType = root.Q<EnumField>("EnumFieldType");
            _floatFieldInitialValue = root.Q<FloatField>("FloatFieldInitialValue");
            _toggleSetMinValue = root.Q<Toggle>("ToggleSetMinValue");
            _floatFieldMinValue = root.Q<FloatField>("FloatFieldMinValue");
            _toggleSetMaxValue = root.Q<Toggle>("ToggleSetMaxValue");
            _floatFieldMaxValue = root.Q<FloatField>("FloatFieldMaxValue");
            _floatFieldChangePerSecond = root.Q<FloatField>("FloatFieldChangePerSecond");
            _toggleUseRealTime = root.Q<Toggle>("ToggleUseRealTime");
            
            // Add button events
            root.Q<Button>("ButtonAddConsideration").clicked += AddConsideration;
            
            // Register value changed callbacks
            _toggleLocal.RegisterValueChangedCallback(evt =>
            {
                _selectedConsiderationSet.local = evt.newValue;
            });
            _textFieldDesignation.RegisterValueChangedCallback(evt =>
            {
                _selectedConsideration.designation = Utils.VerifyItemName("Consideration ",
                    evt.newValue,
                    _selectedConsiderationSet.considerations,
                    consideration => consideration.designation,
                    _selectedConsideration.designation);
                _considerationsListView.Rebuild();
            });
            _enumFieldType.RegisterValueChangedCallback(evt =>
            {
                _selectedConsideration.type = (Consideration.Type)evt.newValue;
            });
            _floatFieldInitialValue.RegisterValueChangedCallback(evt =>
            {
                _selectedConsideration.initialValue = evt.newValue;
            });
            _toggleSetMinValue.RegisterValueChangedCallback(evt =>
            {
                _selectedConsideration.setMinValue = evt.newValue;
                _floatFieldMinValue.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                _considerationsListView.Rebuild();
            });
            _floatFieldMinValue.RegisterValueChangedCallback(evt =>
            {
                float value = evt.newValue < 0 ? 0 : evt.newValue;
                _floatFieldMinValue.value = value;
                _selectedConsideration.minValue = value;
                _considerationsListView.Rebuild();
            });
            _toggleSetMaxValue.RegisterValueChangedCallback(evt =>
            {
                _selectedConsideration.setMaxValue = evt.newValue;
                _floatFieldMaxValue.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                _considerationsListView.Rebuild();
            });
            _floatFieldMaxValue.RegisterValueChangedCallback(evt =>
            {
                float value = evt.newValue < 0 ? 0 : evt.newValue;
                _floatFieldMaxValue.value = value;
                _selectedConsideration.maxValue = value;
                _considerationsListView.Rebuild();
            });
            _floatFieldChangePerSecond.RegisterValueChangedCallback(evt =>
            {
                _selectedConsideration.changePerSecond = evt.newValue;
            });
            _toggleUseRealTime.RegisterValueChangedCallback(evt =>
            {
                _selectedConsideration.useRealTime = evt.newValue;
            });
            
            // Initialize
            GenerateConsiderationListView();

            _toggleLocal.value = _selectedConsiderationSet.local;

            if (_sameSelected)
                for (int i = 0; i < _selectedConsiderationSet.considerations.Count; i++)
                    if (_selectedConsiderationSet.considerations[i].designation == _lastSelectedConsiderationDesignation)
                        _considerationsListView.SetSelection(i);
        }

        private void GenerateConsiderationListView()
        {
            _considerationsContent.Clear();

            VisualElement MakeItem() => _templateItemConsideration.Instantiate();

            void BindItem(VisualElement e, int i)
            {
                e.Q<Label>("LabelTitle").text = _selectedConsiderationSet.considerations[i].designation;
                e.Q<Button>("ButtonRemoveElement").clickable.clicked += () =>
                {
                    _selectedConsiderationSet.considerations.RemoveAt(i);
                    if (_selectedConsiderationSet.considerations.Count <= 0)
                        _inspectorContent.style.visibility = Visibility.Hidden;
                    else
                    {
                        int index = _selectedConsiderationSet.considerations.Count > i ? i : i - 1;
                        LoadConsideration(new List<Consideration> { _selectedConsiderationSet.considerations[index] });
                        _considerationsListView.SetSelection(index);
                    }

                    _considerationsListView.Rebuild();
                };
            }

            _considerationsListView =
                new ListView(_selectedConsiderationSet.considerations, ItemConsiderationHeight, MakeItem, BindItem)
                {
                    selectionType = SelectionType.Single
                };

            _considerationsListView.selectionChanged += LoadConsideration;
            _considerationsContent.Add(_considerationsListView);
        }

        private void LoadConsideration(IEnumerable<object> selectedItems)
        {
            _selectedConsideration = (Consideration)selectedItems.First();
            _lastSelectedConsiderationDesignation = _selectedConsideration.designation;
            
            _textFieldDesignation.value = _selectedConsideration.designation;
            _enumFieldType.value = _selectedConsideration.type;
            _floatFieldInitialValue.value = _selectedConsideration.initialValue;
            _toggleSetMinValue.value = _selectedConsideration.setMinValue;
            _floatFieldMinValue.value = _selectedConsideration.minValue;
            _toggleSetMaxValue.value = _selectedConsideration.setMaxValue;
            _floatFieldMaxValue.value = _selectedConsideration.maxValue;
            _floatFieldChangePerSecond.value = _selectedConsideration.changePerSecond;
            _toggleUseRealTime.value = _selectedConsideration.useRealTime;
            
            _inspectorContent.style.visibility = Visibility.Visible;
        }

        private void AddConsideration()
        {
            _selectedConsiderationSet.considerations.Add(new Consideration
            {
                designation = Utils.VerifyItemName("Consideration ",
                    $"Consideration {_selectedConsiderationSet.considerations.Count + 1}",
                    _selectedConsiderationSet.considerations,
                    consideration => consideration.designation),
                type = Consideration.Type.Float,
                initialValue = 0,
                changePerSecond = 0
            });
            
            _considerationsListView.style.height = _selectedConsiderationSet.considerations.Count * ItemConsiderationHeight;
            _considerationsListView.SetSelection(_selectedConsiderationSet.considerations.Count - 1);
            _considerationsListView.Rebuild();
        }

        private void OnLostFocus()
        {
            if (_utilityDesignerEditorWindowCaller != null)
                _utilityDesignerEditorWindowCaller.OnConsiderationSetEdited(_callerSelectedConsiderationSet);

            EditorUtility.SetDirty(_selectedConsiderationSet);
            AssetDatabase.SaveAssets();

            Close();
        }
    }
}
#endif