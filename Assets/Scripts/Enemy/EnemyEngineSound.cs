using UnityEngine;

namespace RacingProject.Enemy
{
    // Звук двигателя врага: высота тона растёт со скоростью. Скорость считается по смещению,
    // потому что у клиента машину двигает NetworkTransform, а не физика
    [RequireComponent(typeof(AudioSource))]
    public class EnemyEngineSound : MonoBehaviour
    {
        [SerializeField] private float minPitch = 0.7f;
        [SerializeField] private float maxPitch = 1.6f;
        [Tooltip("Скорость, при которой тон максимальный, м/с")]
        [SerializeField] private float maxSpeed = 25f;
        [SerializeField] private float smoothing = 3f;

        private AudioSource source;
        private Vector3 lastPosition;
        private float speed;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;

            float current = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
            lastPosition = transform.position;
            speed = Mathf.Lerp(speed, current, Time.deltaTime * smoothing);
            source.pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.Clamp01(speed / maxSpeed));
        }
    }
}
