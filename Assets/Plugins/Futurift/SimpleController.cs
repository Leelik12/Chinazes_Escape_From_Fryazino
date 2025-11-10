using System.Collections;
using Futurift;
using Futurift.DataSenders;
using Futurift.Options;
using UnityEngine;

public class FuturiftTelemetryHandler : MonoBehaviour
{
    private const float WAIT_TIME = 0.02f; // 50 Hz update rate

    [Header("Connection Settings")]
    [SerializeField] private string ipAddress = "127.0.0.1";
    [SerializeField] private int port = 6065;

    [Header("References")]
    [SerializeField] private Transform vehicleTransform;
    [SerializeField] private Rigidbody rigidbody;

    [Header("Effect Factors")]
    [Tooltip("Множитель наклона капсулы от продольного ускорения (вперед/назад).")]
    [SerializeField] private float accelPitchFactor = 0.02f;

    [Tooltip("Множитель наклона капсулы от бокового ускорения (в поворотах).")]
    [SerializeField] private float cornerRollFactor = 0.02f;

    [Header("Acceleration Boost")]
    [Tooltip("Дополнительный множитель для усиления влияния ускорений.")]
    [SerializeField] private float accelerationBoost = 2.0f;

    [Tooltip("Минимальная скорость (км/ч) для применения усиления ускорений.")]
    [SerializeField] private float minSpeedForBoost = 5.0f;

    [Header("Impact settings")]
    [Tooltip("Множитель силы удара для отката.")]
    [SerializeField] private float impactFactor = 0.015f;

    [Tooltip("Скорость гашения удара отката.")]
    [SerializeField] private float impactDamping = 2.5f;

    [Header("Limits")]
    [Tooltip("Максимальный угол наклона по тангажу (Pitch).")]
    [SerializeField] private float maxPitch = 10f;
    [Tooltip("Максимальный угол наклона по крену (Roll).")]
    [SerializeField] private float maxRoll = 10f;

    [Tooltip("Скорость сглаживания изменений.")]
    [SerializeField] private float smoothSpeed = 8f;

    [Header("Debug GUI")]
    [SerializeField] private bool showGUI = true;
    [SerializeField] private int fontSize = 20;

    private FutuRiftController _controller;

    private Vector3 lastVelocity;
    private Vector3 lastPosition;
    private float currentPitch;
    private float currentRoll;
    private float impactPitch;
    private float impactRoll;

    // Сглаживающие фильтры для ускорения
    private Vector3 smoothedAcceleration;
    private float accelerationSmoothFactor = 0.2f;

    // FPS variables
    private float fps;
    private float fpsRefreshTime = 0.5f;
    private int frameCount;
    private float timer;

    // GUI style
    private GUIStyle guiStyle;

    private void Awake()
    {
        // Инициализация подключения к Futurift
        var udpOptions = new UdpOptions
        {
            ip = ipAddress,
            port = port
        };

        _controller = new FutuRiftController(new UdpPortSender(udpOptions));

        // Initialize GUI style
        guiStyle = new GUIStyle();
        guiStyle.fontSize = fontSize;
        guiStyle.normal.textColor = Color.white;
    }

    private void OnEnable()
    {
        StartCoroutine(TelemetryHandler());
        _controller?.Start();

        lastVelocity = rigidbody.linearVelocity;
        lastPosition = vehicleTransform.position;
        smoothedAcceleration = Vector3.zero;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _controller?.Stop();
    }

    private void Update()
    {
        // FPS calculation
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
        Vector3 velocity = rigidbody.linearVelocity;
        float speedKmh = velocity.magnitude * 3.6f;

        // Более плавный расчет ускорения
        Vector3 worldAcceleration = (velocity - lastVelocity) / Time.fixedDeltaTime;

        // Сглаживаем ускорение для уменьшения резких изменений
        smoothedAcceleration = Vector3.Lerp(smoothedAcceleration, worldAcceleration, accelerationSmoothFactor);
        Vector3 localAcceleration = vehicleTransform.InverseTransformDirection(smoothedAcceleration);

        // Учитываем наклон дороги
        Vector3 localGravity = vehicleTransform.InverseTransformDirection(Physics.gravity);

        // --- Эффекты от ускорений ---
        float forwardAccel = localAcceleration.z;
        float lateralAccel = localAcceleration.x;

        // Базовые эффекты от ускорений
        float targetEffectPitch = -forwardAccel * accelPitchFactor;
        float targetEffectRoll = lateralAccel * cornerRollFactor;

        // --- УСИЛЕНИЕ ВЛИЯНИЯ УСКОРЕНИЙ ---
        if (speedKmh > minSpeedForBoost)
        {
            // Применяем дополнительный множитель для усиления эффектов ускорения
            targetEffectPitch *= accelerationBoost;
            targetEffectRoll *= accelerationBoost;
        }

        // --- Реальные углы: наклон дороги ---
        Vector3 localEuler = vehicleTransform.localRotation.eulerAngles;
        float realPitch = NormalizeAngle(localEuler.x);
        float realRoll = NormalizeAngle(localEuler.z);

        // Учитываем гравитацию в наклонах дороги
        float gravityPitchInfluence = -localGravity.z * 0.1f;
        float gravityRollInfluence = -localGravity.x * 0.1f;

        realPitch += gravityPitchInfluence;
        realRoll += gravityRollInfluence;

        // --- Гашение ударов ---
        impactPitch = Mathf.Lerp(impactPitch, 0f, Time.deltaTime * impactDamping);
        impactRoll = Mathf.Lerp(impactRoll, 0f, Time.deltaTime * impactDamping);

        // --- ПРОСТОЕ СУММИРОВАНИЕ ---
        float finalPitch = realPitch + targetEffectPitch + impactPitch;
        float finalRoll = realRoll + targetEffectRoll + impactRoll;

        // --- Ограничения ---
        finalPitch = Mathf.Clamp(finalPitch, -maxPitch, maxPitch);
        finalRoll = Mathf.Clamp(finalRoll, -maxRoll, maxRoll);

        // --- Улучшенное сглаживание ---
        float smoothFactor = Mathf.Clamp(Time.deltaTime * smoothSpeed, 0.01f, 0.5f);
        currentPitch = Mathf.Lerp(currentPitch, finalPitch, smoothFactor);
        currentRoll = Mathf.Lerp(currentRoll, finalRoll, smoothFactor);

        // --- Передача на капсулу Futurift ---
        if (_controller != null)
        {
            _controller.Pitch = currentPitch;
            _controller.Roll = currentRoll;
        }

        lastVelocity = velocity;
        lastPosition = vehicleTransform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Сглаживаем силу удара
        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime * 0.5f;

        Vector3 localImpact = vehicleTransform.InverseTransformDirection(collision.impulse.normalized);

        // Правильное направление для ударов
        impactPitch = -localImpact.z * impactForce * impactFactor;
        impactRoll = localImpact.x * impactForce * impactFactor * 0.3f;
    }

    private float NormalizeAngle(float angle)
    {
        angle = (angle + 180f) % 360f - 180f;
        return angle;
    }

    private void OnGUI()
    {
        if (!showGUI) return;

        // Позиционируем GUI справа, чтобы не пересекался с 2DOF GUI
        GUILayout.BeginArea(new Rect(Screen.width - 430, 10, 400, 600));

        GUILayout.Label($"FUTURIFT CAPSULE", guiStyle);

        GUILayout.Space(15);

        // Применяем стиль с увеличенным шрифтом ко всем элементам GUI
        GUILayout.Label($"FPS: {fps:0.0}", guiStyle);
        GUILayout.Label($"Pitch: {currentPitch:F2}°", guiStyle);
        GUILayout.Label($"Roll: {currentRoll:F2}°", guiStyle);

        float speedKmh = rigidbody.linearVelocity.magnitude * 3.6f;
        GUILayout.Label($"Speed: {speedKmh:0.0} km/h", guiStyle);

        GUILayout.Space(15);

        // ВЫВОД VELOCITY - вектор скорости
        Vector3 velocity = rigidbody.linearVelocity;
        Vector3 localVelocity = vehicleTransform.InverseTransformDirection(velocity);

        GUILayout.Label("VELOCITY VECTOR", guiStyle);
        GUILayout.Label($"World X: {velocity.x:F2} m/s", guiStyle);
        GUILayout.Label($"World Y: {velocity.y:F2} m/s", guiStyle);
        GUILayout.Label($"World Z: {velocity.z:F2} m/s", guiStyle);

        GUILayout.Space(5);

        GUILayout.Label($"Local Forward: {localVelocity.z:F2} m/s", guiStyle);
        GUILayout.Label($"Local Right: {localVelocity.x:F2} m/s", guiStyle);
        GUILayout.Label($"Local Up: {localVelocity.y:F2} m/s", guiStyle);

        GUILayout.Space(15);

        // Вектор ускорения в локальных координатах
        Vector3 localAccel = vehicleTransform.InverseTransformDirection((rigidbody.linearVelocity - lastVelocity) / Time.fixedDeltaTime);
        GUILayout.Label($"Forward Accel: {localAccel.z:F2} m/s²", guiStyle);
        GUILayout.Label($"Lateral Accel: {localAccel.x:F2} m/s²", guiStyle);

        // Добавим информацию о направлении поворота
        string turnDirection = localAccel.x > 0 ? "RIGHT" : (localAccel.x < 0 ? "LEFT" : "STRAIGHT");
        GUILayout.Label($"Turn Direction: {turnDirection}", guiStyle);

        GUILayout.Space(15);

        // Углы машины
        Vector3 localEuler = vehicleTransform.localRotation.eulerAngles;
        GUILayout.Label($"Car Pitch: {NormalizeAngle(localEuler.x):F2}°", guiStyle);
        GUILayout.Label($"Car Roll: {NormalizeAngle(localEuler.z):F2}°", guiStyle);

        GUILayout.Space(15);

        // Состояние капсулы
        GUILayout.Label("Capsule Status: ACTIVE", guiStyle);
        GUILayout.Label($"Update Rate: {1f / WAIT_TIME:0} Hz", guiStyle);
        GUILayout.Label($"Smoothing: {smoothSpeed:0.0}", guiStyle);

        // Информация об усилении ускорений
        bool isBoostActive = speedKmh > minSpeedForBoost;
        string boostStatus = isBoostActive ? $"ACTIVE (x{accelerationBoost})" : "INACTIVE";
        GUILayout.Label($"Accel Boost: {boostStatus}", guiStyle);

        // Информация о подключении
        GUILayout.Label($"Connection: {ipAddress}:{port}", guiStyle);

        GUILayout.EndArea();
    }
}