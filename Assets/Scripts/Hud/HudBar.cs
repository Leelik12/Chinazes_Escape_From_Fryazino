using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingProject.Hud
{
    // Полоса приборов: Image типа Filled Horizontal и необязательный текст. Значение сглаживается,
    // цвет — по градиенту; в режиме предупреждения полоса пульсирует
    public class HudBar : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private bool useGradient;
        [SerializeField] private Gradient colors = new Gradient();
        [Tooltip("Скорость сглаживания, 1/с")]
        [SerializeField] private float smoothing = 10f;

        private float shown = -1f;
        private string shownText;

        public void SetValue(float value01, string text, bool warning = false)
        {
            value01 = Mathf.Clamp01(value01);
            shown = shown < 0f ? value01 : Mathf.Lerp(shown, value01, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            if (fill != null)
            {
                fill.fillAmount = shown;
                Color color = useGradient ? colors.Evaluate(shown) : fill.color;
                color.a = warning ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 8f)) : 1f;
                fill.color = color;
            }

            if (valueText != null && text != shownText)
            {
                shownText = text;
                valueText.text = text;
            }
        }
    }
}
