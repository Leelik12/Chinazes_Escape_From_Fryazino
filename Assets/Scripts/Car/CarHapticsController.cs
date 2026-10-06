using UnityEngine;
using Bhaptics.SDK2;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class CarHapticsController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Rigidbody carRigidbody;
    [SerializeField] private PlayerHealth carHealth;

    [Header("Пороги срабатывания")]
    [SerializeField] private float accelThreshold = 4f;
    [SerializeField] private float brakeThreshold = -4f;
    [SerializeField] private float sideAccelThreshold = 3f;

    [Header("Интенсивности вибраций")]
    [SerializeField] private float intensityAccel = 0.5f;
    [SerializeField] private float intensityBrake = 0.5f;
    [SerializeField] private float intensityTurn = 0.5f;
    [SerializeField] private float intensityDamage = 0.5f;
    [SerializeField] private float intensityLowHealth = 1f;

    [Header("Настройки GUI")]
    [SerializeField] private bool showHapticsGUI = true;
    [SerializeField] private int guiFontSize = 15;
    [SerializeField] private int guiWidth = 300;
    [SerializeField] private int guiHeight = 400;

    // Счетчики срабатываний
    private Dictionary<string, int> hapticEventCounts = new Dictionary<string, int>();
    private Vector3 lastVelocity;
    private bool lowHealthTriggered = false;

    // GUI стиль
    private GUIStyle guiStyle;

    private void Reset()
    {
        carRigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // Инициализация счетчиков
        InitializeEventCounters();

        // Инициализация GUI стиля
        guiStyle = new GUIStyle();
        guiStyle.fontSize = guiFontSize;
        guiStyle.normal.textColor = Color.yellow;

        if (carHealth != null)
        {
            carHealth.OnDamageTaken += OnCarDamage;
            carHealth.OnDeath += OnCarDeath;
        }
    }

    private void InitializeEventCounters()
    {
        hapticEventCounts.Clear();
        hapticEventCounts.Add("razgon", 0);
        hapticEventCounts.Add("remen_bezopasnosty", 0);
        hapticEventCounts.Add("povorot_pravo", 0);
        hapticEventCounts.Add("povorot_levo", 0);
        hapticEventCounts.Add("damage_hands", 0);
        hapticEventCounts.Add("suit_low_hp", 0);
        hapticEventCounts.Add("hand_low_hp", 0);
    }

    private void OnDestroy()
    {
        if (carHealth != null)
        {
            carHealth.OnDamageTaken -= OnCarDamage;
            carHealth.OnDeath -= OnCarDeath;
        }
    }

    private void Update()
    {
        CheckMovementHaptics();
        CheckHealth();
    }

    private void CheckMovementHaptics()
    {
        Vector3 accel = (carRigidbody.linearVelocity - lastVelocity) / Time.deltaTime;
        Vector3 localAccel = transform.InverseTransformDirection(accel);

        // Ускорение вперед в разгоне/торможении
        if (localAccel.z > accelThreshold)
            PlayMovementEvent("razgon", intensityAccel);
        else if (localAccel.z < brakeThreshold)
            PlayMovementEvent("remen_bezopasnosty", intensityBrake);

        // Боковое ускорение в поворотах
        if (Mathf.Abs(localAccel.x) > sideAccelThreshold)
            PlayMovementEvent(localAccel.x > 0 ? "povorot_pravo" : "povorot_levo", intensityTurn);

        lastVelocity = carRigidbody.linearVelocity;
    }

    // Пока ускорение выше порога, условие верно каждый кадр: без проверки паттерн
    // перезапускался бы с начала в каждом кадре и не доигрывал до конца
    private void PlayMovementEvent(string eventId, float intensity)
    {
        if (BhapticsLibrary.IsPlayingByEventId(eventId)) return;

        BhapticsLibrary.Play(eventId: eventId, startMillis: 0, intensity: intensity, duration: 0.5f, angleX: 0, offsetY: 0);
        hapticEventCounts[eventId]++;
    }

    private void CheckHealth()
    {
        if (carHealth == null) return;

        float healthPercent = (float)carHealth.CurrentHealth / carHealth.MaxHealth;

        if (healthPercent <= 0.3f && !lowHealthTriggered)
        {
            lowHealthTriggered = true;
            BhapticsLibrary.Play(eventId: "suit_low_hp", startMillis: 0, intensity: intensityLowHealth, duration: 0.5f, angleX: 0, offsetY: 0);
            BhapticsLibrary.Play(eventId: "hand_low_hp", startMillis: 0, intensity: intensityLowHealth, duration: 0.3f, angleX: 0, offsetY: 0);
            hapticEventCounts["suit_low_hp"]++;
            hapticEventCounts["hand_low_hp"]++;
        }
        else if (healthPercent > 0.3f && lowHealthTriggered)
        {
            lowHealthTriggered = false;
        }
    }

    private void OnCarDamage(int damage)
    {
        BhapticsLibrary.Play(eventId: "damage_hands", startMillis: 0, intensity: intensityDamage, duration: 0.3f, angleX: 0, offsetY: 0);
        hapticEventCounts["damage_hands"]++;
    }

    private void OnCarDeath()
    {
        BhapticsLibrary.Play(eventId: "suit_low_hp", startMillis: 0, intensity: intensityLowHealth, duration: 0.5f, angleX: 0, offsetY: 0);
        BhapticsLibrary.Play(eventId: "hand_low_hp", startMillis: 0, intensity: intensityLowHealth, duration: 0.3f, angleX: 0, offsetY: 0);
        hapticEventCounts["suit_low_hp"]++;
        hapticEventCounts["hand_low_hp"]++;
    }

    // Отладочная панель только в редакторе и development-сборках
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (!showHapticsGUI) return;

        // Позиционируем GUI в правом верхнем углу
        GUILayout.BeginArea(new Rect(Screen.width - guiWidth - 30, 10, guiWidth, guiHeight));

        GUILayout.Label("BHAPTICS EVENTS", guiStyle);
        GUILayout.Space(10);

        // Отображаем счетчики для каждого события
        foreach (var eventCount in hapticEventCounts)
        {
            GUILayout.Label($"{eventCount.Key}: {eventCount.Value}", guiStyle);
        }

        GUILayout.Space(15);

        // Отображаем текущие настройки интенсивности
        GUILayout.Label("INTENSITY SETTINGS", guiStyle);
        GUILayout.Label($"Accel: {intensityAccel:F2}", guiStyle);
        GUILayout.Label($"Brake: {intensityBrake:F2}", guiStyle);
        GUILayout.Label($"Turn: {intensityTurn:F2}", guiStyle);
        GUILayout.Label($"Damage: {intensityDamage:F2}", guiStyle);
        GUILayout.Label($"Low Health: {intensityLowHealth:F2}", guiStyle);

        GUILayout.Space(15);

        // Отображаем статус низкого здоровья
        string healthStatus = lowHealthTriggered ? "LOW HEALTH ACTIVE" : "Health OK";
        GUILayout.Label($"Health Status: {healthStatus}", guiStyle);

        // Кнопка сброса счетчиков
        if (GUILayout.Button("Reset Counters", GUILayout.Height(30)))
        {
            InitializeEventCounters();
        }

        GUILayout.EndArea();
    }
#endif

    // Метод для сброса счетчиков из других скриптов
    public void ResetHapticCounters()
    {
        InitializeEventCounters();
    }

    // Метод для получения счетчика конкретного события
    public int GetEventCount(string eventId)
    {
        if (hapticEventCounts.ContainsKey(eventId))
        {
            return hapticEventCounts[eventId];
        }
        return 0;
    }
}