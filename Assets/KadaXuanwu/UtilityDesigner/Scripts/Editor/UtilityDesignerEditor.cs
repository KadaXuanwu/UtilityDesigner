#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Editor
{
    [CustomEditor(typeof(UtilityDesigner))]
    public class UtilityDesignerEditor : UnityEditor.Editor
    {
        private SerializedProperty utilityBehaviour;
        private SerializedProperty sceneReferencesObjName;
        private SerializedProperty tickRateEvaluation;
        private SerializedProperty useUpdateAsTickRateForEvaluation;
        private SerializedProperty logEvaluationToFile;
        private SerializedProperty tickRateLog;
        private SerializedProperty tickRateExecution;
        private SerializedProperty useUpdateAsTickRateForExecution;

        private void OnEnable()
        {
            utilityBehaviour = serializedObject.FindProperty("utilityBehaviour");
            sceneReferencesObjName = serializedObject.FindProperty("sceneReferencesObjName");
            tickRateEvaluation = serializedObject.FindProperty("tickRateEvaluation");
            useUpdateAsTickRateForEvaluation = serializedObject.FindProperty("useUpdateAsTickRateForEvaluation");
            logEvaluationToFile = serializedObject.FindProperty("logEvaluationToFile");
            tickRateLog = serializedObject.FindProperty("tickRateLog");
            tickRateExecution = serializedObject.FindProperty("tickRateExecution");
            useUpdateAsTickRateForExecution = serializedObject.FindProperty("useUpdateAsTickRateForExecution");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(utilityBehaviour, new GUIContent("Utility Behaviour",
                    "The behaviour used for this object."));
            EditorGUILayout.PropertyField(sceneReferencesObjName, new GUIContent("Scene Refs Obj Name",
                    "Name of the GameObject with a SceneReferences component to be used for the action nodes."));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Evaluation", EditorStyles.boldLabel);

            if (!useUpdateAsTickRateForEvaluation.boolValue)
            {
                EditorGUILayout.Slider(tickRateEvaluation, 0.01f, 1f, new GUIContent("Tick Rate",
                    "The tick rate of which the evaluation is updated."));
            }

            EditorGUILayout.PropertyField(useUpdateAsTickRateForEvaluation, new GUIContent("Use Update As Tick Rate",
                "Uses Unity's Update method as tick rate instead."));

            EditorGUILayout.PropertyField(logEvaluationToFile, new GUIContent("Log To File",
                "Creates a new text file called 'UtilityDesignerLog' in the Assets folder and logs the current stats of the evaluation tab to it. Overrides if it already exists."));

            if (logEvaluationToFile.boolValue)
            {
                EditorGUILayout.Slider(tickRateLog, 0.1f, 10f, new GUIContent("Logging tick rate",
                    "The tick rate of which the stats are logged."));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Execution", EditorStyles.boldLabel);

            if (!useUpdateAsTickRateForExecution.boolValue)
            {
                EditorGUILayout.Slider(tickRateExecution, 0.01f, 1f, new GUIContent("Tick Rate",
                    "The tick rate of which the execution is updated."));
            }

            EditorGUILayout.PropertyField(useUpdateAsTickRateForExecution, new GUIContent("Use Update As Tick Rate",
                "Uses Unity's Update method as tick rate instead."));

            EditorGUILayout.Space();

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open editor", buttonStyle, GUILayout.Height(22),
                    GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.6f)))
            {
                UtilityDesignerEditorWindow.OpenWindow();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif