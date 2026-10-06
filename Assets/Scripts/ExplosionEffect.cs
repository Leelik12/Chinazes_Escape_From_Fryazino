using UnityEngine;

namespace RacingProject
{
    // Разовый эффект взрыва: частицы проигрываются сами, здесь — вспышка света и удаление объекта.
    // Эффект локальный, каждый игрок создаёт свою копию
    public class ExplosionEffect : MonoBehaviour
    {
        [Tooltip("Через сколько секунд удалить эффект — дольше самой долгой системы частиц")]
        [SerializeField] private float lifetime = 6f;

        [Header("Вспышка")]
        [SerializeField] private Light flashLight;
        [SerializeField] private float flashDuration = 0.5f;

        private float startIntensity;
        private float startTime;

        private void Start()
        {
            startTime = Time.time;
            if (flashLight != null)
                startIntensity = flashLight.intensity;
            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (flashLight == null) return;

            float t = (Time.time - startTime) / flashDuration;
            if (t >= 1f)
            {
                flashLight.enabled = false;
                return;
            }
            flashLight.intensity = startIntensity * (1f - t) * (1f - t);
        }
    }
}
