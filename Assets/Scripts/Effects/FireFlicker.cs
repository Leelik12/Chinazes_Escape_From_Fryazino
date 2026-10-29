using UnityEngine;

namespace RacingProject.Effects
{
    // Мерцание света от пожара: яркость плавно гуляет по шуму Перлина, у каждого огня — своя фаза
    [RequireComponent(typeof(Light))]
    public class FireFlicker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float amount = 0.35f;
        [SerializeField] private float speed = 3f;

        private Light fireLight;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            fireLight = GetComponent<Light>();
            baseIntensity = fireLight.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float noise = Mathf.PerlinNoise(seed, Time.time * speed) * 0.7f + Mathf.PerlinNoise(seed + 7f, Time.time * speed * 3.1f) * 0.3f;
            fireLight.intensity = baseIntensity * (1f - amount + amount * 2f * noise);
        }
    }
}
