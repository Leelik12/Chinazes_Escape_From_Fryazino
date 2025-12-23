using System.Collections;
using UnityEngine;
using Futurift;
using Futurift.DataSenders;
using Futurift.Options;

public class FuturiftTelemetryHandler : MonoBehaviour
{
    private const float WAIT_TIME = 0.016f; // 60 Hz

    [Header("Connection Settings")]
    [SerializeField] private string ipAddress = "127.0.0.1";
    [SerializeField] private int port = 6065;

    [Header("References")]
    [SerializeField] private Transform proxyTransform;

    [Header("Smoothing")]
    [SerializeField] private float smoothingSpeed = 8f;
    // Чем больше — тем резче, 5–10 обычно оптимально

    [Header("Debug GUI")]
    [SerializeField] private bool showGUI = true;
    [SerializeField] private int fontSize = 20;

    private FutuRiftController controller;

    private float currentPitch;
    private float currentRoll;

    private float smoothedPitch;
    private float smoothedRoll;

    private GUIStyle guiStyle;

    private void Awake()
    {
        var udpOptions = new UdpOptions { ip = ipAddress, port = port };
        controller = new FutuRiftController(new UdpPortSender(udpOptions));

        guiStyle = new GUIStyle
        {
            fontSize = fontSize,
            normal = { textColor = Color.white }
        };
    }

    private void OnEnable()
    {
        controller.Start();
        StartCoroutine(TelemetryLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        controller.Stop();
    }

    private IEnumerator TelemetryLoop()
    {
        while (true)
        {
            UpdateAngles();
            yield return new WaitForSeconds(WAIT_TIME);
        }
    }

    private void UpdateAngles()
    {
        if (proxyTransform == null) return;

        Vector3 angles = proxyTransform.localEulerAngles;

        float targetPitch = -NormalizeAngle(angles.x) * 20f;
        float targetRoll = NormalizeAngle(angles.z) * 20f;

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

    private void OnGUI()
    {
        if (!showGUI) return;

        GUILayout.BeginArea(new Rect(Screen.width - 420, 20, 400, 150));
        GUILayout.Label("FUTURIFT TELEMETRY DEBUG", guiStyle);
        GUILayout.Space(10);

        GUILayout.Label($"Pitch: {currentPitch:F2}°", guiStyle);
        GUILayout.Label($"Roll:  {currentRoll:F2}°", guiStyle);

        GUILayout.EndArea();
    }
}
