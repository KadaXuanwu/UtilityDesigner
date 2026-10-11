using System.IO;
using KadaXuanwu.UtilityDesigner.Core;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Runtime {
    // An agent on a GameObject: it runs its own copy of a behaviour (a design's JSON) on game time, so a paused game
    // stops deciding.
    [DisallowMultipleComponent]
    public class UtilityDesigner : MonoBehaviour {
        public const string LogPath = "Assets/UtilityDesignerLog.txt";

        // The considerations of the sets that are not local, shared by every agent.
        public static Globals Globals { get; private set; } = new Globals();

        [SerializeField] private TextAsset utilityBehaviour;
        [SerializeField] private string sceneReferencesObjName;
        [SerializeField] private float tickRateEvaluation = 0.1f;
        [SerializeField] private bool useUpdateAsTickRateForEvaluation;
        [SerializeField] private float tickRateExecution = 0.1f;
        [SerializeField] private bool useUpdateAsTickRateForExecution;
        [SerializeField] private bool logEvaluationToFile;
        [SerializeField] private float tickRateLog = 1f;
        // 0: another one every run.
        [SerializeField] private int seed;

        private double _evaluated = double.NegativeInfinity;
        private double _executed = double.NegativeInfinity;
        private double _logged = double.NegativeInfinity;

        // The design's JSON. Set before Awake (an inactive GameObject, or AddComponent then this) to give one from code.
        public TextAsset Behaviour {
            get => utilityBehaviour;
            set => utilityBehaviour = value;
        }

        // Null without a behaviour, or with one that has no state set.
        public Agent Agent { get; private set; }
        public SceneReferences SceneReferences { get; private set; }

        // The component running a node's agent, for the node's GameObject and SceneReferences; null outside Unity.
        public static UtilityDesigner Of(Agent agent) => agent?.Owner as UtilityDesigner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobals() => Globals = new Globals();

        private void Awake() {
            SceneReferences = FindSceneReferences();
            if (utilityBehaviour == null) {
                return;
            }
            Design design = Nodes.Load(utilityBehaviour.text);
            if (design.StateSets.Count == 0) {
                return;
            }
            ulong runSeed = seed != 0 ? (ulong)seed : (ulong)Random.Range(1, int.MaxValue);
            Agent = new Agent(design, Globals, new Rng(runSeed), Time.timeAsDouble, this);
#if UNITY_EDITOR
            if (logEvaluationToFile) {
                File.WriteAllText(LogPath, "");
            }
#endif
        }

        private void Update() {
            if (Agent == null) {
                return;
            }
            double now = Time.timeAsDouble;
            if (Due(now, ref _evaluated, useUpdateAsTickRateForEvaluation ? 0f : Mathf.Clamp(tickRateEvaluation, 0.01f, 1f))) {
                Agent.Evaluate(now);
            }
            if (Due(now, ref _executed, useUpdateAsTickRateForExecution ? 0f : Mathf.Clamp(tickRateExecution, 0.01f, 1f))) {
                Agent.Execute(now);
            }
#if UNITY_EDITOR
            if (logEvaluationToFile && Due(now, ref _logged, Mathf.Clamp(tickRateLog, 0.1f, 10f))) {
                using StreamWriter writer = new StreamWriter(LogPath, true);
                Agent.WriteLog(writer);
            }
#endif
        }

        // The SceneReferences on the GameObject named in the inspector; the editor asks too, outside play.
        public SceneReferences FindSceneReferences() {
            GameObject found = string.IsNullOrEmpty(sceneReferencesObjName) ? null : GameObject.Find(sceneReferencesObjName);
            return found != null ? found.GetComponent<SceneReferences>() : null;
        }

        // Whether a tick is due: the first time, then whenever its rate of game time has passed; with a rate of 0, every
        // frame the clock moved.
        private static bool Due(double now, ref double last, float rate) {
            if (now <= last || now < last + rate) {
                return false;
            }
            last = now;
            return true;
        }
    }
}
