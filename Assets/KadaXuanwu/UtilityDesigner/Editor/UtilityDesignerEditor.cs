using UnityEditor;
using UnityEngine;
using Designer = KadaXuanwu.UtilityDesigner.Runtime.UtilityDesigner;

namespace KadaXuanwu.UtilityDesigner.Editor {
    [CustomEditor(typeof(Designer))]
    public class UtilityDesignerEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty updateEvaluation = serializedObject.FindProperty("useUpdateAsTickRateForEvaluation");
            SerializedProperty updateExecution = serializedObject.FindProperty("useUpdateAsTickRateForExecution");
            SerializedProperty log = serializedObject.FindProperty("logEvaluationToFile");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("utilityBehaviour"), new GUIContent("Utility Behaviour",
                "The behaviour used for this object: a design's JSON file, saved from the editor window."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneReferencesObjName"), new GUIContent("Scene Refs Obj Name",
                "Name of the GameObject with a SceneReferences component to be used for the action nodes."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("seed"), new GUIContent("Seed",
                "The seed of this agent's random numbers (fail chances, the random nodes): the same seed repeats a run. 0 takes another one every run."));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Evaluation", EditorStyles.boldLabel);
            if (!updateEvaluation.boolValue) {
                EditorGUILayout.Slider(serializedObject.FindProperty("tickRateEvaluation"), 0.01f, 1f, new GUIContent("Tick Rate",
                    "The seconds of game time between two evaluations."));
            }
            EditorGUILayout.PropertyField(updateEvaluation, new GUIContent("Use Update As Tick Rate", "Evaluates every frame instead."));
            EditorGUILayout.PropertyField(log, new GUIContent("Log To File",
                $"Writes the stats of the evaluation tab to '{Designer.LogPath}' while playing in the editor. Overrides it if it already exists."));
            if (log.boolValue) {
                EditorGUILayout.Slider(serializedObject.FindProperty("tickRateLog"), 0.1f, 10f, new GUIContent("Logging tick rate",
                    "The seconds of game time between two log entries."));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Execution", EditorStyles.boldLabel);
            if (!updateExecution.boolValue) {
                EditorGUILayout.Slider(serializedObject.FindProperty("tickRateExecution"), 0.01f, 1f, new GUIContent("Tick Rate",
                    "The seconds of game time between two ticks of the behaviour tree."));
            }
            EditorGUILayout.PropertyField(updateExecution, new GUIContent("Use Update As Tick Rate", "Ticks the behaviour tree every frame instead."));

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open editor", new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold }, GUILayout.Height(22),
                    GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.6f))) {
                UtilityDesignerEditorWindow.OpenWindow();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
