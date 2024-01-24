using System.Collections;
using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using TMPro;
using UnityEngine;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class JamesStats : MonoBehaviour
    {
        [SerializeField] private ConsiderationSet jamesConsiderations;
        [SerializeField] private GameObject uiCanvas;
        [SerializeField] private TextMeshProUGUI textHunger;
        [SerializeField] private TextMeshProUGUI textEnergy;
        [SerializeField] private TextMeshProUGUI textMotivation;
        [SerializeField] private TextMeshProUGUI textStrength;
        [SerializeField] private TextMeshProUGUI textFood;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private ParticleSystem particleStarve;
        
        private Camera _mainCamera;
        private bool _starved;


        private void Update()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_mainCamera != null)
            {
                uiCanvas.transform.LookAt(uiCanvas.transform.position + _mainCamera.transform.rotation * Vector3.forward);
                uiCanvas.transform.localRotation *= Quaternion.Euler(0, 90, 0);
            }

            textHunger.text = $"Hunger: <b>{jamesConsiderations.GetConsideration("Hunger", gameObject):F0}</b>";
            textEnergy.text = $"Energy: <b>{jamesConsiderations.GetConsideration("Energy", gameObject):F0}</b>";
            textMotivation.text = $"Motivation: <b>{jamesConsiderations.GetConsideration("Motivation", gameObject):F0}</b>";
            textStrength.text = $"Strength: <b>{jamesConsiderations.GetConsideration("Strength", gameObject):F0}</b>";
            textFood.text = $"Food: <b>{jamesConsiderations.GetConsideration("Food", gameObject):F0}</b>";

            if (jamesConsiderations.GetConsideration("Hunger", gameObject) > 99 && !_starved)
            {
                _starved = true;
                Starve();
            }
        }

        private void Starve()
        {
            GameManager.gameOver = true;
            Time.timeScale = 0;
            particleStarve.Play();
            StartCoroutine(StarvingEffect());
        }

        private IEnumerator StarvingEffect()
        {
            while (true)
            {
                Vector3 desiredPosition = transform.position + new Vector3(4, 14, 0);
                Vector3 smoothedPosition = Vector3.Lerp(mainCamera.transform.position, desiredPosition, 0.01f);
                mainCamera.transform.position = smoothedPosition;
                
                yield return new WaitForSecondsRealtime(0.01f);
            }
        }
    }
}
