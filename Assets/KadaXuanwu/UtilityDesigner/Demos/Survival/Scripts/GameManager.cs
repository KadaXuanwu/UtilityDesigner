using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KadaXuanwu.UtilityDesigner.Demos.Survival.Scripts
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private Slider uiSliderTimeScale;
        [SerializeField] private TextMeshProUGUI uiTextTimeScale;

        public static bool gameOver;


        private void Start()
        {
            gameOver = false;
        }

        private void OnGUI()
        {
            if (gameOver)
                return;
            
            Time.timeScale = uiSliderTimeScale.value;
            uiTextTimeScale.text = uiSliderTimeScale.value.ToString("F2") + "x";
        }
    }
}
