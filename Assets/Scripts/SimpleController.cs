using System.Collections;
using Futurift;
using Futurift.DataSenders;
using Futurift.Options;
using Photon.Pun;
using UnityEngine;

public class FuturiftTelemetryHandler : MonoBehaviourPun
{
    private const float WAIT_TIME = 0.016f; // 60 Hz

    [Header("Connection Settings")]
    [SerializeField] private string ipAddress = "127.0.0.1";
    [SerializeField] private int port = 6065;
    [SerializeField] private CarNetworkSync networkSync;

    [Header("References")]
    [SerializeField] private Transform vehicleTransform;
    [SerializeField] private Rigidbody vehicleRigidbody;

    [Header("Pitch Settings (Forward/Backward)")]
    [SerializeField] private float maxPitch = 25f;
    [SerializeField] private float pitchSensitivity = 2.0f;
    [SerializeField] private float pitchDamping = 5f;

    [Header("Roll Settings (Left/Right)")]
    [SerializeField] private float maxRoll = 20f;
    [SerializeField] private float rollSensitivity = 1.5f;
    [SerializeField] private float rollDamping = 5f;

    [Header("Road Influence")]
    [SerializeField] private float roadInfluence = 0.8f;

    [Header("Impact settings")]
    [SerializeField] private float impactFactor = 0.1f;

    [Header("Debug GUI")]
    [SerializeField] private bool showGUI = true;
    [SerializeField] private int fontSize = 20;

    private FutuRiftController _controller;

    // Для расчета ускорений
    private Vector3 lastVelocity;
    private Vector3 lastAcceleration;
    private Vector3 smoothedAcceleration;

    // Текущие углы
    private float currentPitch;
    private float currentRoll;

    // FPS variables
    private float fps;
    private float fpsRefreshTime = 0.5f;
    private int frameCount;
    private float timer;

    // GUI style
    private GUIStyle guiStyle;

    private void Awake()
    {
        var udpOptions = new UdpOptions
        {
            ip = ipAddress,
            port = port
        };

        _controller = new FutuRiftController(new UdpPortSender(udpOptions));

        guiStyle = new GUIStyle();
        guiStyle.fontSize = fontSize;
        guiStyle.normal.textColor = Color.white;
    }

    private void OnEnable()
    {
        StartCoroutine(TelemetryHandler());
        _controller?.Start();

        lastVelocity = vehicleRigidbody.linearVelocity;
        lastAcceleration = Vector3.zero;
        smoothedAcceleration = Vector3.zero;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _controller?.Stop();
    }

    private void Update()
    {
        frameCount++;
        timer += Time.unscaledDeltaTime;

        if (timer >= fpsRefreshTime)
        {
            fps = frameCount / timer;
            frameCount = 0;
            timer = 0f;
        }
    }

    private IEnumerator TelemetryHandler()
    {
        while (true)
        {
            UpdatePlatformMotion();
            yield return new WaitForSeconds(WAIT_TIME);
        }
    }

    private void UpdatePlatformMotion()
    {
        Vector3 velocity;
        Vector3 angularVel;

        if (photonView != null && !photonView.IsMine && networkSync != null)
        {
            // Берём переданные значения
            velocity = networkSync.NetworkVelocity;
            angularVel = networkSync.NetworkAngularVelocity;
        }
        else
        {
            // Локальный игрок — берём реальные значения Rigidbody
            velocity = vehicleRigidbody.linearVelocity;
            angularVel = vehicleRigidbody.angularVelocity;
        }
        // Текущая скорость
        Vector3 currentVelocity = vehicleRigidbody.linearVelocity;

        // Мгновенное ускорение (очень быстрое)
        Vector3 instantAcceleration = (currentVelocity - lastVelocity) / Time.fixedDeltaTime;

        // Легкое сглаживание для устранения шума, но без задержек
        smoothedAcceleration = Vector3.Lerp(smoothedAcceleration, instantAcceleration, 0.7f);

        // Преобразуем в локальные координаты автомобиля
        Vector3 localAcceleration = vehicleTransform.InverseTransformDirection(smoothedAcceleration);

        // Углы наклона дороги
        Vector3 localEuler = vehicleTransform.localRotation.eulerAngles;
        float roadPitch = NormalizeAngle(localEuler.x);
        float roadRoll = NormalizeAngle(localEuler.z);

        // РАСЧЕТ PITCH (вперед-назад)
        float accelerationPitch = 0f;

        // Сильное ускорение вперед - наклон назад
        if (localAcceleration.z > 1.0f)
        {
            accelerationPitch = -Mathf.Clamp(localAcceleration.z * pitchSensitivity, 0, maxPitch);
        }
        // Торможение - наклон вперед
        else if (localAcceleration.z < -1.0f)
        {
            accelerationPitch = -Mathf.Clamp(localAcceleration.z * pitchSensitivity, -maxPitch, 0);
        }

        // РАСЧЕТ ROLL (влево-вправо)
        float turnRoll = 0f;

        // Повороты - наклон в противоположную сторону
        if (Mathf.Abs(localAcceleration.x) > 0.8f)
        {
            turnRoll = -Mathf.Clamp(localAcceleration.x * rollSensitivity, -maxRoll, maxRoll);
        }

        // Комбинируем все эффекты
        float targetPitch = accelerationPitch + (roadPitch * roadInfluence);
        float targetRoll = turnRoll + (roadRoll * roadInfluence);

        // Ограничиваем
        targetPitch = Mathf.Clamp(targetPitch, -maxPitch, maxPitch);
        targetRoll = Mathf.Clamp(targetRoll, -maxRoll, maxRoll);

        // Быстрое, но плавное применение
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * pitchDamping);
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * rollDamping);

        // Передача данных
        if (_controller != null)
        {
            _controller.Pitch = currentPitch;
            _controller.Roll = currentRoll;
        }

        // Сохраняем для следующего кадра
        lastVelocity = currentVelocity;
        lastAcceleration = instantAcceleration;
    }

    private void FixedUpdate()
    {
        // Дополнительный расчет в FixedUpdate для более точной физики
        UpdatePlatformMotion();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Удары - мгновенный эффект
        float impactForce = collision.relativeVelocity.magnitude * impactFactor;
        Vector3 localImpact = vehicleTransform.InverseTransformDirection(collision.relativeVelocity.normalized);

        currentPitch += -localImpact.z * impactForce;
        currentRoll += -localImpact.x * impactForce;

        currentPitch = Mathf.Clamp(currentPitch, -maxPitch, maxPitch);
        currentRoll = Mathf.Clamp(currentRoll, -maxRoll, maxRoll);
    }

    private float NormalizeAngle(float angle)
    {
        angle = (angle + 180f) % 360f - 180f;
        return angle;
    }

    private void OnGUI()
    {
        if (!showGUI) return;

        GUILayout.BeginArea(new Rect(Screen.width - 450, 10, 440, 600));

        GUILayout.Label($"FUTURIFT CAPSULE - DIRECT PHYSICS", guiStyle);
        GUILayout.Space(10);

        GUILayout.Label($"FPS: {fps:0.0}", guiStyle);
        GUILayout.Label($"Pitch: {currentPitch:F1}°", guiStyle);
        GUILayout.Label($"Roll: {currentRoll:F1}°", guiStyle);

        float speedKmh = vehicleRigidbody.linearVelocity.magnitude * 3.6f;
        GUILayout.Label($"Speed: {speedKmh:0.0} km/h", guiStyle);

        GUILayout.Space(15);

        // Детальная диагностика физики
        Vector3 localAccel = vehicleTransform.InverseTransformDirection(smoothedAcceleration);
        Vector3 localVel = vehicleTransform.InverseTransformDirection(vehicleRigidbody.linearVelocity);

        GUILayout.Label("PHYSICS DATA:", guiStyle);
        GUILayout.Label($"Accel Forward: {localAccel.z:F2} m/s²", guiStyle);
        GUILayout.Label($"Accel Lateral: {localAccel.x:F2} m/s²", guiStyle);
        GUILayout.Label($"Vel Forward: {localVel.z:F1} m/s", guiStyle);
        GUILayout.Label($"Vel Lateral: {localVel.x:F1} m/s", guiStyle);

        GUILayout.Space(10);

        // Статус эффектов
        string pitchEffect = Mathf.Abs(localAccel.z) > 1.0f ?
            (localAccel.z > 0 ? "ACCEL → TILT BACK" : "BRAKE → TILT FORWARD") : "NO PITCH EFFECT";

        string rollEffect = Mathf.Abs(localAccel.x) > 0.8f ?
            (localAccel.x > 0 ? "TURN RIGHT → TILT LEFT" : "TURN LEFT → TILT RIGHT") : "NO ROLL EFFECT";

        GUILayout.Label($"Pitch Status: {pitchEffect}", guiStyle);
        GUILayout.Label($"Roll Status: {rollEffect}", guiStyle);

        GUILayout.Space(10);

        // Рекомендации
        if (Mathf.Abs(localAccel.z) > 2f && Mathf.Abs(currentPitch) < 5f)
            GUILayout.Label("INCREASE pitchSensitivity!", guiStyle);

        if (Mathf.Abs(localAccel.x) > 1.5f && Mathf.Abs(currentRoll) < 3f)
            GUILayout.Label("INCREASE rollSensitivity!", guiStyle);

        GUILayout.Space(15);
        GUILayout.Label($"Connection: {ipAddress}:{port}", guiStyle);

        GUILayout.EndArea();
    }
}