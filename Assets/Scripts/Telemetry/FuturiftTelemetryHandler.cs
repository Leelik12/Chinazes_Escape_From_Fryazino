using System.Collections;
using UnityEngine;
using Futurift;
using Futurift.DataSenders;
using Futurift.Options;
using UnityEngine.InputSystem;
using RacingProject.Hud;
using RacingProject.Network;

namespace RacingProject.Telemetry
{
    // Телеметрия для платформы стрелка FutuRift: наклоны берутся с прокси-точки,
    // которую поворачивает CarTelemetryHandler по данным водителя.
    // На платформу данные уходят только с компьютера стрелка
    public class FuturiftTelemetryHandler : MonoBehaviour
    {
        private const float WAIT_TIME = 0.016f; // 60 Hz

        [Header("Connection Settings")]
        [SerializeField] private string ipAddress = "127.0.0.1";
        [SerializeField] private int port = 6065;

        [Tooltip("Отправлять на FutuRift без роли стрелка — для проверки платформы без второго игрока")]
        [SerializeField] private bool sendWithoutRole = false;

        [Header("References")]
        [SerializeField] private Transform proxyTransform;

        [Header("Smoothing")]
        [SerializeField] private float smoothingSpeed = 8f;
        // Чем больше — тем резче, 5–10 обычно оптимально

        [Header("HUD")]
        [SerializeField] private Key hudToggleKey = Key.F3;
        [SerializeField] private bool showGUI = true;

        private FutuRiftController controller;

        private float currentPitch;
        private float currentRoll;

        private float smoothedPitch;
        private float smoothedRoll;

        private HudPanel hudPanel;
        private WaitForSeconds wait;
        private bool sending;

        private bool ShouldSendToPlatform => sendWithoutRole || LocalPlayerRole.Current == PlayerRole.Gunner;

        private void Awake()
        {
            var udpOptions = new UdpOptions { ip = ipAddress, port = port };
            controller = new FutuRiftController(new UdpPortSender(udpOptions));
            wait = new WaitForSeconds(WAIT_TIME);
            hudPanel = new HudPanel("FUTURIFT TELEMETRY", hudToggleKey, HudCorner.TopRight, 0, 420f, showGUI);
        }

        private void OnEnable()
        {
            StartCoroutine(TelemetryLoop());
            hudPanel.Show();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            StopSending();
            hudPanel.Hide();
        }

        private void Update()
        {
            hudPanel.HandleInput();
        }

        private IEnumerator TelemetryLoop()
        {
            while (true)
            {
                UpdateAngles();

                if (ShouldSendToPlatform)
                    StartSending();
                else
                    StopSending();

                yield return wait;
            }
        }

        // Контроллер сам отправляет последние Pitch/Roll по таймеру, пока запущен
        private void StartSending()
        {
            if (sending) return;
            controller.Start();
            sending = true;
        }

        private void StopSending()
        {
            if (!sending) return;
            controller.Stop();
            sending = false;
        }

        private void UpdateAngles()
        {
            if (proxyTransform == null) return;

            Vector3 angles = proxyTransform.localEulerAngles;

            float targetPitch = -NormalizeAngle(angles.x) * 3f;
            float targetRoll = NormalizeAngle(angles.z) * 5f;

            // LERP-сглаживание
            smoothedPitch = Mathf.Lerp(
                smoothedPitch,
                targetPitch,
                smoothingSpeed * WAIT_TIME
            );

            smoothedRoll = Mathf.Lerp(
                smoothedRoll,
                targetRoll,
                smoothingSpeed * WAIT_TIME
            );
            if (smoothedPitch < -16f)
            {
                smoothedPitch = -16f;
            }
            else if (smoothedPitch > 16f)
            {
                smoothedPitch = 16f;
            }
            if (smoothedRoll < -16f)
            {
                smoothedRoll = -16f;
            }
            else if (smoothedRoll > 16f)
            {
                smoothedRoll = 16f;
            }
            currentPitch = smoothedPitch;
            currentRoll = smoothedRoll;

            controller.Pitch = smoothedPitch;
            controller.Roll = smoothedRoll;
        }

        private float NormalizeAngle(float angle)
        {
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        // Панель телеметрии FutuRift; сворачивается клавишей hudToggleKey
        private void OnGUI()
        {
            hudPanel.Draw(
                $"Pitch: {currentPitch:F2}°\n" +
                $"Roll:  {currentRoll:F2}°\n" +
                $"Sending: {sending}  ({ipAddress}:{port})");
        }
    }
}
