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

            Vector3 velocity = vehicleRigidbody.velocity;
            Vector3 acceleration = (velocity - lastVelocity) / Time.fixedDeltaTime;

            float forwardAccel = Vector3.Dot(acceleration, vehicleTransform.forward);
            float lateralAccel = Vector3.Dot(acceleration, vehicleTransform.right);

            float targetPitch = -forwardAccel * accelPitchFactor; // вперёд при торможении
            float targetRoll = -lateralAccel * cornerRollFactor;

            // затухание удара
            impactPitch = Mathf.Lerp(impactPitch, 0f, Time.deltaTime * impactDamping);
            impactRoll = Mathf.Lerp(impactRoll, 0f, Time.deltaTime * impactDamping);

            targetPitch += impactPitch;
            targetRoll += impactRoll;

            // ограничение углов
            targetPitch = Mathf.Clamp(targetPitch, -maxPitch, maxPitch);
            targetRoll = Mathf.Clamp(targetRoll, -maxRoll, maxRoll);

            // сглаживание
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * smoothSpeed);
            currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * smoothSpeed);

            // отправка в Futurift
            if (currentPitch < 0.01f)
            {
                currentPitch = 0f;
            }
            if (currentRoll < 0.01f)
            {
                currentRoll = 0f;
            }
            _controller.Pitch = currentPitch;
            _controller.Roll = currentRoll;
            Debug.Log("FUTURIFT отработал   " + _controller.Pitch + "       " + _controller.Roll);
            lastVelocity = velocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactForce = collision.relativeVelocity.magnitude;
            impactPitch = -impactForce * impactFactor;
            impactRoll = Random.Range(-impactForce, impactForce) * impactFactor * 0.5f;
        }
    }
}