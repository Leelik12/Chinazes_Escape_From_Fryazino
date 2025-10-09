using UnityEngine;
using UnityEngine.UI;

public class HealthBarGradient : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider healthSlider2;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image fillImage2;

    [Header("Gradient Settings")]
    [SerializeField] private Gradient healthGradient; // задаём в инспекторе
    [SerializeField] private float updateSpeed = 5f;

    private float targetValue;

    private void Start()
    {
        if (healthSlider != null)
        {
            targetValue = healthSlider.value;
            UpdateColor();
        }
    }

    private void Update()
    {
        if (healthSlider == null || fillImage == null) return;

        // Плавное изменение
        healthSlider.value = Mathf.Lerp(healthSlider.value, targetValue, Time.deltaTime * updateSpeed);
        healthSlider2.value = Mathf.Lerp(healthSlider.value, targetValue, Time.deltaTime * updateSpeed);
        UpdateColor();
    }

    public void SetHealth(float currentHealth, float maxHealth)
    {
        targetValue = Mathf.Clamp01(currentHealth / maxHealth) * healthSlider.maxValue;
    }

    private void UpdateColor()
    {
        float normalized = healthSlider.normalizedValue;
        fillImage.color = healthGradient.Evaluate(normalized);
        fillImage2.color = healthGradient.Evaluate(normalized);
    }
}
