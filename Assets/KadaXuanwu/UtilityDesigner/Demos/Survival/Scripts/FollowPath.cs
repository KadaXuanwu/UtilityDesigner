using System.Collections.Generic;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class FollowPath : MonoBehaviour
    {
        [SerializeField] private List<Transform> waypoints;
        [SerializeField] private float speed = 3.0f;
        [SerializeField] private float rotationSpeed = 5.0f;

        private int _currentWaypointIndex;
        private bool _isMoving;

        void Update()
        {
            if (!_isMoving || waypoints.Count == 0)
                return;

            Transform targetWaypoint = waypoints[_currentWaypointIndex];
            Vector3 directionToTarget = targetWaypoint.position - transform.position;
            float distanceToTarget = directionToTarget.magnitude;

            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            Transform transformShip;
            (transformShip = transform).rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            transformShip.position += speed * Time.deltaTime * transformShip.forward;

            if (distanceToTarget < 0.5f)
            {
                _currentWaypointIndex++;

                if (_currentWaypointIndex >= waypoints.Count)
                {
                    _isMoving = false;
                    _currentWaypointIndex = 0;
                }
            }
        }

        public void StartPath()
        {
            if (_isMoving)
                return;
            
            _isMoving = true;
            _currentWaypointIndex = 0;
        }

        public bool PathFinished()
        {
            return !_isMoving;
        }
    }
}