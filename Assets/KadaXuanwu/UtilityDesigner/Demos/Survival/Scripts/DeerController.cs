using System.Collections;
using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class DeerController : MonoBehaviour
    {
        [SerializeField] private Transform fleeLocation;
        [SerializeField] private ConsiderationSet environmentConsiderations;
        [SerializeField] private GameObject particleDeath;
        [SerializeField] private GameObject mesh;
        
        private const float WalkSpeed = 1.5f;
        private const float WalkRangePerStep = 5.0f;
        private const float RunSpeed = 14f;
        private const float MaxDistanceFromSpawn = 12f;
        private const float MinRespawnTime = 20f;
        private const float MaxRespawnTime = 30f;

        private NavMeshAgent _navMeshAgent;
        private Vector3 _target;
        private bool _isRunning;
        private bool _alive;
        private Vector3 _spawnPosition;


        private void Awake()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
        }

        void Start()
        {
            _spawnPosition = transform.position;
            _alive = true;
            SetRandomTarget();
        }

        void Update()
        {
            if (_navMeshAgent.remainingDistance > _navMeshAgent.stoppingDistance || !_alive)
                return;

            if (!_isRunning)
                SetRandomTarget();
            else
                StartCoroutine(Respawn());
        }

        public void RealizeDanger()
        {
            _navMeshAgent.speed = RunSpeed;
            _navMeshAgent.SetDestination(fleeLocation.position);
            _isRunning = true;
        }

        public void Die()
        {
            Instantiate(particleDeath, transform.position + new Vector3(0, 2, 0), Quaternion.Euler(270, 0, 0));
            StartCoroutine(Respawn());
        }

        private void SetRandomTarget()
        {
            Vector3 randomDirection = Random.insideUnitSphere * WalkRangePerStep;
            randomDirection += _spawnPosition;

            Vector3 clampedDirection = Vector3.ClampMagnitude(randomDirection - _spawnPosition, MaxDistanceFromSpawn);
            clampedDirection += _spawnPosition;
            NavMesh.SamplePosition(clampedDirection, out NavMeshHit navHit, WalkRangePerStep, NavMesh.AllAreas);

            _target = navHit.position;
            _navMeshAgent.speed = WalkSpeed;
            _navMeshAgent.SetDestination(_target);
            _isRunning = false;
        }

        private IEnumerator Respawn()
        {
            _alive = false;
            mesh.SetActive(false);
            _navMeshAgent.SetDestination(_target);

            yield return new WaitForSeconds(Random.Range(MinRespawnTime, MaxRespawnTime));

            environmentConsiderations.SetConsideration("Deer nearby", 1);
            mesh.SetActive(true);
            _isRunning = false;
            _alive = true;

            SetRandomTarget();
        }
    }
}