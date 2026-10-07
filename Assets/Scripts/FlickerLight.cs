using UnityEngine;

namespace RacingProject
{
    // Мерцание света от огня: яркость гуляет по шуму Перлина вокруг исходной, у каждого огня свой сдвиг шума
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [Tooltip("Доля яркости, на которую свет проседает")]
        [SerializeField, Range(0f, 1f)] private float depth = 0.45f;
        [Tooltip("Скорость мерцания")]
        [SerializeField] private float speed = 7f;

        private Light target;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float noise = Mathf.PerlinNoise(seed, Time.time * speed);
            target.intensity = baseIntensity * (1f - depth * noise);
        }
    }
}
