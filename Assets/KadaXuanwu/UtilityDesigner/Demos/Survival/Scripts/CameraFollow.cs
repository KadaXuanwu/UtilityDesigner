using System.Collections;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public GameObject target;
        public Vector3 cameraOffset;
        public float cameraSmoothSpeed;
        public float initialDelay;
        public float initialCameraSmoothSpeed;
        public float initializationDuration;

        private bool _enabled;
        private float _currentCameraSmoothSpeed;


        private void Start()
        {
            _enabled = false;
            StartCoroutine(WaitDelay());
        }

        private void Update()
        {
            if (!_enabled)
                return;
            
            Vector3 desiredPosition = target.transform.position + cameraOffset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, _currentCameraSmoothSpeed * Time.deltaTime);
            transform.position = smoothedPosition;
        }

        private IEnumerator WaitDelay()
        {
            yield return new WaitForSeconds(initialDelay);

            _enabled = true;
            _currentCameraSmoothSpeed = initialCameraSmoothSpeed;

            yield return new WaitForSeconds(initializationDuration);

            _currentCameraSmoothSpeed = cameraSmoothSpeed;
        }
    }
}
