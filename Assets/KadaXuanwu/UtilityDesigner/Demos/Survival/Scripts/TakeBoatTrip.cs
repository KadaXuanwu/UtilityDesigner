using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class TakeBoatTrip : ActionNode
    {
        private GameObject _james;
        private GameObject _boat;
        private FollowPath _boatPath;
        private Transform _originalParent;
        private bool _referenceMissing;
        private Vector3 _positionBeforeTrip;


        protected override void OnAwake()
        {
            _referenceMissing = false;
            if (SceneRefs == null || ThisGameObject == null)
            {
                _referenceMissing = true;
                return;
            }

            _james = ThisGameObject.GetComponent<Transform>().gameObject;
            _boat = SceneRefs.GetRef<Transform>("Boat").gameObject;
            _boatPath = _boat.GetComponent<FollowPath>();
            
            if (_james == null || _boat == null || _boatPath == null)
            {
                _referenceMissing = true;
                return;
            }
            
            _originalParent = _james.transform.parent;
        }

        protected override void OnEnable()
        {
            if (_referenceMissing)
                return;

            _positionBeforeTrip = _james.transform.position;
            _james.GetComponent<NavMeshAgent>().enabled = false;
            _james.transform.SetParent(_boat.transform);
            _james.transform.localPosition = new Vector3(0, 1.3f, -1.3f);
            _boatPath.StartPath();
        }

        protected override NodeState OnUpdate()
        {
            if (_referenceMissing)
                return NodeState.Failure;
            
            return _boatPath.PathFinished() ? NodeState.Success : NodeState.Running;
        }

        protected override void OnDisable()
        {
            if (_referenceMissing)
                return;

            _james.transform.position = _positionBeforeTrip;
            _james.transform.SetParent(_originalParent);
            _james.GetComponent<NavMeshAgent>().enabled = true;
        }
    }
}