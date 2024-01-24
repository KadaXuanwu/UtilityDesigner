using System.Collections;
using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class BearController : MonoBehaviour
    {
        [SerializeField] private Transform fleeLocation;
        [SerializeField] private Transform jamesTransform;
        [SerializeField] private ConsiderationSet environmentConsiderations;
        [SerializeField] private GameObject mesh;
        
        private const float MinRespawnTime = 35f;
        private const float MaxRespawnTime = 45f;
        private const float RunSpeed = 16f;

        private NavMeshAgent _navMeshAgent;
        private Vector3 _spawnPosition;
        private bool _isRunning;
        private bool _alive;


        private void Awake()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
        }

        void Start()
        {
            _spawnPosition = transform.position;
            _alive = true;
            StartCoroutine(Respawn());
        }

        void Update()
        {
            if (!_alive)
                return;
            
            if (_navMeshAgent.remainingDistance > _navMeshAgent.stoppingDistance && !_isRunning)
                _navMeshAgent.SetDestination(jamesTransform.position);
            else if (_navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance && _isRunning
                     && Vector3.Distance(_navMeshAgent.transform.position, fleeLocation.transform.position) < _navMeshAgent.stoppingDistance)
                StartCoroutine(Respawn());
        }

        public void Escape()
        {
            _navMeshAgent.speed = RunSpeed;
            _navMeshAgent.SetDestination(fleeLocation.position);
            _isRunning = true;
        }

        private IEnumerator Respawn()
        {
            _isRunning = false;
            _alive = false;
            mesh.SetActive(false);
            _navMeshAgent.SetDestination(_spawnPosition);

            yield return new WaitForSeconds(Random.Range(MinRespawnTime, MaxRespawnTime));

            mesh.SetActive(true);
            _alive = true;
            environmentConsiderations.SetConsideration("Bears nearby", 1);
            _navMeshAgent.SetDestination(jamesTransform.position);
        }
    }
}
