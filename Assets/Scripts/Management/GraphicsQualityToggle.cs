using UnityEngine;
using UnityEngine.UI;

namespace RacingProject.Management
{
    // Переключатель «Низкое качество графики» в настройках меню
    [RequireComponent(typeof(Toggle))]
    public class GraphicsQualityToggle : MonoBehaviour
    {
        private Toggle toggle;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(GraphicsQuality.SetLow);
        }

        private void OnEnable()
        {
            toggle.SetIsOnWithoutNotify(GraphicsQuality.Low);
        }
    }
}
