using KadaXuanwu.UtilityDesigner.Core;
using UnityEngine;
using UnityEngine.AI;
using Designer = KadaXuanwu.UtilityDesigner.Runtime.UtilityDesigner;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class KillBear : ActionNode
    {
        private NavMeshAgent _navMeshAgentJames;
        private Transform _transformJames;
        private bool _referenceMissing;
        private Transform _transformCamera;
        private GameObject _bear;


        protected override void OnAwake()
        {
            _referenceMissing = false;
            Designer designer = Designer.Of(Agent);
            if (designer == null || designer.SceneReferences == null)
            {
                _referenceMissing = true;
                return;
            }

            _navMeshAgentJames = designer.GetComponent<NavMeshAgent>();
            _transformJames = designer.GetComponent<Transform>();
            _transformCamera = designer.SceneReferences.GetRef<Transform>("Main Camera");
            _bear = designer.SceneReferences.GetRef<Transform>("Bear").gameObject;

            if (_transformJames == null ||
                _navMeshAgentJames == null || _transformCamera == null || _bear == null)
                _referenceMissing = true;
        }

        protected override NodeState OnUpdate()
        {
            if (_referenceMissing)
                return NodeState.Failure;

            if (Agent.GetConsideration("UD James", "Strength") < 30)
            {
                Die();
                return NodeState.Failure;
            }
            
            _bear.SendMessage("Escape", SendMessageOptions.DontRequireReceiver);
            Agent.SetConsideration("UD Environment", "Bears nearby", 0);
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
