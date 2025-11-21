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
    [SerializeField] private Transform proxyTransform; // Точка, чей поворот отправляем
    // Rigidbody и vehicleTransform больше не нужны, можно убрать, если хочешь

    [Header("Debug GUI")]
    [SerializeField] private bool showGUI = true;
    [SerializeField] private int fontSize = 20;

    private FutuRiftController controller;

    private float currentPitch;
    private float currentRoll;

    private GUIStyle guiStyle;

    private void Awake()
    {
        var udpOptions = new UdpOptions { ip = ipAddress, port = port };
        controller = new FutuRiftController(new UdpPortSender(udpOptions));

        guiStyle = new GUIStyle();
        guiStyle.fontSize = fontSize;
        guiStyle.normal.textColor = Color.white;
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

        // Получаем локальные углы прокси-точки (или можно worldAngles, если нужно)
        Vector3 angles = proxyTransform.localEulerAngles;

        // Нормализуем углы в диапазон -180..180
        currentPitch = NormalizeAngle(angles.x);
        currentRoll = NormalizeAngle(angles.z);
        currentPitch = -currentPitch;
        // Просто передаем текущий поворот в контроллер без изменений
        controller.Pitch = currentPitch*80f;
        controller.Roll = currentRoll*10f;
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
