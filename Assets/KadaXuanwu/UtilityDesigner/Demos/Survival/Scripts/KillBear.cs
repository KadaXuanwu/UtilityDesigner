using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class KillBear : ActionNode
    {
        private ConsiderationSet _jamesConsiderations;
        private ConsiderationSet _environmentConsiderations;
        private NavMeshAgent _navMeshAgentJames;
        private Transform _transformJames;
        private bool _referenceMissing;
        private Transform _transformCamera;
        private GameObject _bear;


        protected override void OnAwake()
        {
            _referenceMissing = false;
            if (SceneRefs == null || ThisGameObject == null)
            {
                _referenceMissing = true;
                return;
            }

            _jamesConsiderations = GetConsiderationSet("UD James");
            _environmentConsiderations = GetConsiderationSet("UD Environment");
            _navMeshAgentJames = ThisGameObject.GetComponent<NavMeshAgent>();
            _transformJames = ThisGameObject.GetComponent<Transform>();
            _transformCamera = SceneRefs.GetRef<Transform>("Main Camera");
            _bear = SceneRefs.GetRef<Transform>("Bear").gameObject;

            if (_jamesConsiderations == null || _environmentConsiderations == null || _transformJames == null ||
                _navMeshAgentJames == null || _transformCamera == null || _bear == null)
                _referenceMissing = true;
        }

        protected override NodeState OnUpdate()
        {
            if (_referenceMissing)
                return NodeState.Failure;

            if (_jamesConsiderations.GetConsideration("Strength", UtilityDesigner) < 30)
            {
                Die();
                return NodeState.Failure;
            }
            
            _bear.SendMessage("Escape", SendMessageOptions.DontRequireReceiver);
            _environmentConsiderations.SetConsideration("Bears nearby", 0);
            return NodeState.Success;
        }

        private void Die()
        {
            GameManager.gameOver = true;
            Time.timeScale = 0.06f;
            _navMeshAgentJames.enabled = false;
            _transformJames.gameObject.GetComponent<MeshRenderer>().enabled = false;
            _transformJames.Find("Glasses").gameObject.GetComponent<MeshRenderer>().enabled = false;
            _transformJames.Find("ParticleDeath").gameObject.GetComponent<ParticleSystem>().Play();
            _transformCamera.gameObject.GetComponent<CameraFollow>().cameraOffset = new Vector3(4, 14, 0);
        }
    }
}
