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
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private float maxPitch = 10f;
        [SerializeField] private float maxRoll = 10f;

        private FutuRiftController _controller;

        private Vector3 lastVelocity;
        private float currentPitch;
        private float currentRoll;
        private float impactPitch;
        private float impactRoll;

        private void Awake()
        {
            var udpOptions = new UdpOptions
            {
                ip = ipAddress,
                port = port
            };

            _controller = new FutuRiftController(new UdpPortSender(udpOptions));
        }

        private void OnEnable()
        {
            _controller?.Start();
            if (vehicleRigidbody != null)
                lastVelocity = vehicleRigidbody.velocity;
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
            Vector3 velocity = vehicleRigidbody.velocity;
            Vector3 acceleration = (velocity - lastVelocity) / Time.fixedDeltaTime;

            float forwardAccel = Vector3.Dot(acceleration, vehicleTransform.forward);
            float lateralAccel = Vector3.Dot(acceleration, vehicleTransform.right);

            // --- Эффекты ускорения ---
            float targetPitchEffect = -forwardAccel * accelPitchFactor; // вперёд при торможении
            float targetRollEffect = -lateralAccel * cornerRollFactor;

            // --- Эффект удара ---
            impactPitch = Mathf.Lerp(impactPitch, 0f, Time.deltaTime * impactDamping);
            impactRoll = Mathf.Lerp(impactRoll, 0f, Time.deltaTime * impactDamping);

            targetPitchEffect += impactPitch;
            targetRollEffect += impactRoll;

            // --- Угол самого автомобиля ---
            float realPitch = vehicleTransform.localEulerAngles.x;
            if (realPitch > 180f) realPitch -= 360f;

            float realRoll = vehicleTransform.localEulerAngles.z;
            if (realRoll > 180f) realRoll -= 360f;

            // --- Смешиваем реальный наклон и эффект ---
            float targetPitch = (realPitch * 0.5f) + (targetPitchEffect * 0.5f);
            float targetRoll = (realRoll * 0.5f) + (targetRollEffect * 0.5f);

            // --- Ограничение ---
            targetPitch = Mathf.Clamp(targetPitch, -maxPitch, maxPitch);
            targetRoll = Mathf.Clamp(targetRoll, -maxRoll, maxRoll);

            // --- Сглаживание ---
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * smoothSpeed);
            currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * smoothSpeed);

            // --- Отправка на капсулу Futurift ---
            _controller.Pitch = currentPitch;
            _controller.Roll = currentRoll;

            Debug.Log($"FUTURIFT отработал   Pitch: {_controller.Pitch:F2}°   Roll: {_controller.Roll:F2}°");

            lastVelocity = velocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactForce = collision.relativeVelocity.magnitude;

            // Импульс удара
            impactPitch = -impactForce * impactFactor;
            impactRoll = Random.Range(-impactForce, impactForce) * impactFactor * 0.5f;
        }
    }
}
