using Futurift.DataSenders;
using Futurift.Options;
using UnityEngine;

namespace Futurift
{
    public class SimpleController : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] private string ipAddress = "127.0.0.1";
        [SerializeField] private int port = 6065;

        [Header("References")]
        [SerializeField] private Transform vehicleTransform;
        [SerializeField] private Rigidbody vehicleRigidbody;

        [Header("Motion response")]
        [Tooltip("Насколько сильно капсула реагирует на продольное ускорение (наклон вперёд/назад).")]
        [SerializeField] private float accelPitchFactor = 0.2f;
        [Tooltip("Насколько сильно капсула реагирует на боковое ускорение (наклон в повороте).")]
        [SerializeField] private float cornerRollFactor = 0.2f;

        [Header("Impact response")]
        [Tooltip("Множитель силы наклона при ударе.")]
        [SerializeField] private float impactFactor = 0.15f;
        [Tooltip("Скорость затухания эффекта удара.")]
        [SerializeField] private float impactDamping = 2.5f;

        [Header("Smoothing & Limits")]
        [SerializeField] private float maxPitch = 10f;
        [SerializeField] private float maxRoll = 10f;

        private FutuRiftController _controller;

        private Vector3 lastVelocity;
        private float currentPitch;
        private float currentRoll;
        private float impactPitch;
        private float impactRoll;

        // Для фильтрации изменений
        private float lastSentPitch;
        private float lastSentRoll;

        private void Awake()
        {
            var udpOptions = new UdpOptions
            {
                ip = ipAddress,
                port = port
            };

            _controller = new FutuRiftController(new UdpPortSender(udpOptions));
        }
        private void Update()
        {
            // Тест по нажатию клавиши
            if (Input.GetKeyDown(KeyCode.T))
            {
                _controller.Pitch = 15f;
                _controller.Roll = 15f;
                Debug.Log("Test data sent to Futurift");
            }
        }
        private void OnEnable()
        {
            _controller?.Start();
            if (vehicleRigidbody != null)
                lastVelocity = vehicleRigidbody.linearVelocity;

            lastSentPitch = 0f;
            lastSentRoll = 0f;
        }

        private void OnDisable()
        {
            _controller?.Stop();
        }

        private void FixedUpdate()
        {
            if (vehicleRigidbody == null || vehicleTransform == null)
                return;

            // --- Получаем ускорение ---
            Vector3 velocity = vehicleRigidbody.linearVelocity;
            Vector3 acceleration = (velocity - lastVelocity) / Time.fixedDeltaTime;

            float forwardAccel = Vector3.Dot(acceleration, vehicleTransform.forward);
            float lateralAccel = Vector3.Dot(acceleration, vehicleTransform.right);

            // --- Эффекты ускорения ---
            float targetPitchEffect = -forwardAccel * accelPitchFactor;
            float targetRollEffect = -lateralAccel * cornerRollFactor;

            // --- Эффект удара ---
            impactPitch *= 0.95f;
            impactRoll *= 0.95f;

            targetPitchEffect += impactPitch;
            targetRollEffect += impactRoll;

            // --- Угол автомобиля ---
            float realPitch = vehicleTransform.localEulerAngles.x;
            if (realPitch > 180f) realPitch -= 360f;

            float realRoll = vehicleTransform.localEulerAngles.z;
            if (realRoll > 180f) realRoll -= 360f;

            float targetPitch = Mathf.Clamp(realPitch + targetPitchEffect, -maxPitch, maxPitch);
            float targetRoll = Mathf.Clamp(realRoll + targetRollEffect, -maxRoll, maxRoll);

            // --- Без сглаживания ---
            currentPitch = targetPitch;
            currentRoll = targetRoll;

            _controller.Pitch = currentPitch;
            _controller.Roll = currentRoll;

            Debug.Log(_controller.Pitch);
            Debug.Log(_controller.Roll);

            lastSentPitch = currentPitch;
            lastSentRoll = currentRoll;
            lastVelocity = velocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactForce = collision.relativeVelocity.magnitude;
            Debug.Log("АВАРИЯ");
            impactPitch = -impactForce * impactFactor;
            impactRoll = Random.Range(-impactForce, impactForce) * impactFactor * 0.5f;
        }
    }
}
