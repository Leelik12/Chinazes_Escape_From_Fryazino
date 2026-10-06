using _2DOF;
using Photon.Pun;
using UnityEngine;
using RacingProject.Network;

namespace RacingProject.Telemetry
{
    // Телеметрия машины для платформы водителя 2DOF.
    // Наклоны считает владелец машины (водитель) и рассылает по сети; у стрелка по ним
    // поворачивается прокси-точка, которую читает FuturiftTelemetryHandler.
    // В платформу 2DOF данные уходят только с компьютера водителя.
    // Расчёт идёт в FixedUpdate: скорость Rigidbody меняется только на шаге физики,
    // а при опросе по кадрам ускорение зависело от FPS
    [RequireComponent(typeof(PhotonView))]
    public class CarTelemetryHandler : MonoBehaviour, IPunObservable
    {
        private ObjectTelemetryData telemetryDataData;
        private SendingData _sendingData;

        [SerializeField] private Transform proxyTransform; // <-- прокси-точка, которая повторяет наклоны


        [SerializeField] private Transform vehicleTransform;
        [SerializeField] private Rigidbody rb;

        [Tooltip("Отправлять на 2DOF без роли водителя — для проверки платформы без второго игрока")]
        [SerializeField] private bool sendWithoutRole = false;

        [Tooltip("Множитель продольного ускорения. Старый расчёт по кадрам завышал ускорение примерно вдвое при 72–90 FPS, " +
                 "значение 2 сохраняет прежнюю силу наклона")]
        [SerializeField] private float accelerationGain = 2f;

        private const float maxPlatformAngle = 15f; // Максимальные Angles платформы 2DOF (влияет на статичные наклоны, зависящие от поверхности)
        private const float maxPlatformVelocity = 100f; // Максимальная Velocity платформы 2DOF (влияет на наклоны в зависимости от линейного ускорения/угловой скорости)
        private float currentPitch = 0f; // текущий наклон платформы 2DOF по x (учет наклона поверхности)
        private float currentRoll = 0f; // текущий наклон платформы 2DOF по z (учет наклона поверхности)
        private float currentLinearAcceleration = 0f; // текущий наклон платформы 2DOF по x (учет линейного ускорения)
        private float lastLinearVelocity = 0f;
        private float currentAngularVelocity = 0f; // текущий наклон платформы 2DOF по z (учет угловой скорости)

        private PhotonView photonView;
        private bool sending;

        private bool ShouldSendToPlatform => sendWithoutRole || LocalPlayerRole.Current == PlayerRole.Driver;

        private void Awake()
        {
            photonView = GetComponent<PhotonView>();
            _sendingData = new SendingData();
            telemetryDataData = _sendingData.ObjectTelemetryData;
        }


        public void OnDisable()
        {
            StopSending();
        }

        private void FixedUpdate()
        {
            // У стрелка значения приходят по сети, локальный пересчёт сбивал бы прокси-точку
            if (photonView.IsMine)
            {
                UpdatePlatformVelocity();
                UpdatePlatformAngles();
            }

            if (ShouldSendToPlatform)
                StartSending();
            else
                StopSending();
        }

        // Поток плагина пишет данные в memory-mapped file, который читает ПО платформы
        private void StartSending()
        {
            if (sending) return;
            _sendingData.SendingStart();
            sending = true;
        }

        private void StopSending()
        {
            if (!sending) return;
            _sendingData.SendingStop();
            sending = false;
        }

        private float NormalizeAngle(float angle) // нормализуем угол в диапазон -180 до 180
        {
            angle = angle > 180 ? angle - 360 : angle;
            return angle;
        }

        private void UpdatePlatformVelocity() // отправка данных о скорости на платформу
        {
            Vector3 globalLinearVelocity = rb.linearVelocity;
            Vector3 localLinearVelocity = transform.InverseTransformVector(globalLinearVelocity); // считаем линейную скорость относительно локальных координат

            // считаем линейное ускорение
            float linearAcceleration = (localLinearVelocity.z - lastLinearVelocity) / Time.fixedDeltaTime * accelerationGain;
            lastLinearVelocity = localLinearVelocity.z;

            linearAcceleration = Mathf.Clamp(linearAcceleration, -maxPlatformVelocity, maxPlatformVelocity);

            currentLinearAcceleration = Mathf.Lerp(currentLinearAcceleration, linearAcceleration, 0.02f);

            Vector3 globalAngularVelocity = rb.angularVelocity;
            Vector3 localAngularVelocity = transform.InverseTransformVector(globalAngularVelocity); // считаем угловую скорость относительно локальных координат

            currentAngularVelocity = Mathf.Lerp(currentAngularVelocity, Mathf.Clamp(localAngularVelocity.y, -maxPlatformVelocity, maxPlatformVelocity), 0.03f);

            // Углы здесь не пишем: поток плагина мог прочитать промежуточное значение до UpdatePlatformAngles
            telemetryDataData.Velocity = new Vector3(currentLinearAcceleration * 70, currentAngularVelocity * 300, 0);

            // Добавляем наклоны на прокси-точку
            if (proxyTransform != null)
            {
                proxyTransform.localRotation = Quaternion.Euler(currentPitch * 0.5f - currentLinearAcceleration, 0f, currentRoll);
            }

        }

        private void UpdatePlatformAngles()
        {
            float targetPitch = NormalizeAngle(vehicleTransform.eulerAngles.x); // учет наклона поверхности
            targetPitch = Mathf.Clamp(targetPitch, -maxPlatformAngle, maxPlatformAngle);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, 0.04f);

            //---------------------------------------------------------------------------
            float targetRoll = NormalizeAngle(vehicleTransform.eulerAngles.z);
            targetRoll = Mathf.Clamp(targetRoll, -maxPlatformAngle, maxPlatformAngle);
            currentRoll = Mathf.Lerp(currentRoll, targetRoll, 0.04f);

            Vector3 resultAngles = new Vector3(currentPitch, currentRoll, 0); // конечный возврат углов для передачи данных в платформу
            telemetryDataData.Angles = resultAngles;
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting) // Владелец объекта отправляет данные
            {
                stream.SendNext(currentPitch);
                stream.SendNext(currentRoll);
                stream.SendNext(currentLinearAcceleration);
                stream.SendNext(currentAngularVelocity);
            }
            else // Другие получают и применяют
            {
                currentPitch = (float)stream.ReceiveNext();
                currentRoll = (float)stream.ReceiveNext();
                currentLinearAcceleration = (float)stream.ReceiveNext();
                currentAngularVelocity = (float)stream.ReceiveNext();

                if (proxyTransform != null)
                    proxyTransform.localRotation = Quaternion.Euler(currentPitch*0.5f - currentLinearAcceleration, 0f, currentRoll);
            }
        }

    }
}
