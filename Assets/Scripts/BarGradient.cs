using UnityEngine;
using UnityEngine.UI;

namespace RacingProject
{
    public class BarGradient : MonoBehaviour
    {
        [Header("=== Health Settings ===")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Gradient healthGradient;
        [SerializeField] private float healthUpdateSpeed = 5f;

        [Header("=== Overheat Settings ===")]
        [SerializeField] private Slider overheatSlider;
        [SerializeField] private Image overheatFillImage;
        [SerializeField] private Gradient overheatGradient;
        [SerializeField] private float overheatUpdateSpeed = 5f;

        private float targetHealthValue;
        private float targetOverheatValue;

        private void Start()
        {
            if (healthSlider != null)
            {
                targetHealthValue = healthSlider.value;
                UpdateHealthColor();
            }

            if (overheatSlider != null)
            {
                targetOverheatValue = overheatSlider.value;
                UpdateOverheatColor();
            }
        }

        private void Update()
        {
            if (healthSlider != null && healthFillImage != null)
            {
                healthSlider.value = Mathf.Lerp(healthSlider.value, targetHealthValue, Time.deltaTime * healthUpdateSpeed);
                UpdateHealthColor();
            }

            if (overheatSlider != null && overheatFillImage != null)
            {
                overheatSlider.value = Mathf.Lerp(overheatSlider.value, targetOverheatValue, Time.deltaTime * overheatUpdateSpeed);
                UpdateOverheatColor();
            }
        }

        // === HEALTH ===
        public void SetHealth(float currentHealth, float maxHealth)
        {
            if (healthSlider == null) return;
            targetHealthValue = Mathf.Clamp01(currentHealth / maxHealth) * healthSlider.maxValue;
        }

        private void UpdateHealthColor()
        {
            float normalized = healthSlider.normalizedValue;
            healthFillImage.color = healthGradient.Evaluate(normalized);
        }

        // === OVERHEAT ===
        public void SetOverheat(float currentHeat, float maxHeat)
        {
            if (overheatSlider == null) return;
            targetOverheatValue = Mathf.Clamp01(currentHeat / maxHeat) * overheatSlider.maxValue;
        }

        private void UpdateOverheatColor()
        {
            float normalized = overheatSlider.normalizedValue;
            overheatFillImage.color = overheatGradient.Evaluate(normalized);
        }
    }
}
