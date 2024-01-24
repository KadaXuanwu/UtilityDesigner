#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Scripts.Editor
{
    [CustomEditor(typeof(UtilityDesigner))]
    public class UtilityDesignerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            UtilityDesigner utilityDesigner = (UtilityDesigner)target;

            serializedObject.Update();

            utilityDesigner.utilityBehaviour = (UtilityBehaviour)EditorGUILayout.ObjectField(new GUIContent("Utility Behaviour",
                    "The behaviour used for this object."),
                utilityDesigner.utilityBehaviour, typeof(UtilityBehaviour), true);
            utilityDesigner.sceneReferencesObjName = EditorGUILayout.TextField(new GUIContent("Scene Refs Obj Name", 
                    "Name of the GameObject with a SceneReferences component to be used for the action nodes."), 
                utilityDesigner.sceneReferencesObjName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Evaluation", EditorStyles.boldLabel);

            if (!utilityDesigner.useUpdateAsTickRateForEvaluation)
                utilityDesigner.tickRateEvaluation = EditorGUILayout.Slider(new GUIContent("Tick Rate",
                    "The tick rate of which the evaluation is updated."),
                    utilityDesigner.tickRateEvaluation, 0.01f, 1f);

            utilityDesigner.useUpdateAsTickRateForEvaluation = EditorGUILayout.Toggle(new GUIContent("Use Update As Tick Rate",
                "Uses Unity's Update method as tick rate instead."),
                utilityDesigner.useUpdateAsTickRateForEvaluation);

            utilityDesigner.logEvaluationToFile = EditorGUILayout.Toggle(new GUIContent("Log To File",
                "Creates a new text file called 'UtilityDesignerLog' in the Assets folder and logs the current stats of the evaluation tab to it. Overrides if it already exists."),
                utilityDesigner.logEvaluationToFile);

            if (utilityDesigner.logEvaluationToFile)
                utilityDesigner.tickRateLog = EditorGUILayout.Slider(new GUIContent("Logging tick rate",
                        "The tick rate of which the stats are logged."),
                        utilityDesigner.tickRateLog, 0.1f, 10f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Execution", EditorStyles.boldLabel);

            if (!utilityDesigner.useUpdateAsTickRateForExecution)
                utilityDesigner.tickRateExecution = EditorGUILayout.Slider(new GUIContent("Tick Rate",
                    "The tick rate of which the execution is updated."),
                    utilityDesigner.tickRateExecution, 0.01f, 1f);

            utilityDesigner.useUpdateAsTickRateForExecution = EditorGUILayout.Toggle(new GUIContent("Use Update As Tick Rate",
                "Uses Unity's Update method as tick rate instead."),
                utilityDesigner.useUpdateAsTickRateForExecution);

            EditorGUILayout.Space();

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open editor", buttonStyle, GUILayout.Height(22),
                    GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.6f)))
                UtilityDesignerEditorWindow.OpenWindow();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif