using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingProject.Hud
{
    // Круговая шкала приборов: дуга заполнения (Image типа Filled Radial 360, повёрнутая так, что дуга начинается
    // слева снизу), необязательная стрелка и текст значения. Значение сглаживается, цвет дуги — по градиенту
    public class HudArcGauge : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [Tooltip("Стрелка с осью в центре шкалы; при нулевом повороте смотрит вверх")]
        [SerializeField] private RectTransform needle;
        [SerializeField] private TMP_Text valueText;
        [Tooltip("Доля окружности, которую занимает шкала: 0,75 — 270°")]
        [SerializeField, Range(0.1f, 1f)] private float arc = 0.75f;
        [SerializeField] private bool useGradient;
        [SerializeField] private Gradient colors = new Gradient();
        [Tooltip("Скорость сглаживания, 1/с")]
        [SerializeField] private float smoothing = 8f;

        private float shown = -1f;
        private string shownText;

        public void SetValue(float value01, string text)
        {
            value01 = Mathf.Clamp01(value01);
            shown = shown < 0f ? value01 : Mathf.Lerp(shown, value01, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            if (fill != null)
            {
                fill.fillAmount = shown * arc;
                if (useGradient)
                    fill.color = colors.Evaluate(shown);
            }

            if (needle != null)
            {
                float half = arc * 180f;
                needle.localRotation = Quaternion.Euler(0f, 0f, half - arc * 360f * shown);
            }

            if (valueText != null && text != shownText)
            {
                shownText = text;
                valueText.text = text;
            }
        }
    }
}
