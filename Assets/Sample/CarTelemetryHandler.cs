using System;
using System.Collections;
using _2DOF;
using UnityEngine;

public class CarTelemetryHandler : MonoBehaviour
{
    private const float WAIT_TIME = SendingData.WAIT_TIME / 1000f;

    [Header("References")]
    [SerializeField] private Transform vehicleTransform;
    [SerializeField] private Rigidbody rigidbody;

    [Header("Effect Factors")]
    [Tooltip("Насколько сильно платформа реагирует на продольное ускорение (вперёд/назад).")]
    [SerializeField] private float accelPitchFactor = 0.02f;

    [Tooltip("Насколько сильно платформа реагирует на боковое ускорение (в повороте).")]
    [SerializeField] private float cornerRollFactor = 0.02f;

    [Header("Impact settings")]
    [Tooltip("Множитель силы наклона при ударе.")]
    [SerializeField] private float impactFactor = 0.015f;

    [Tooltip("Скорость затухания эффекта удара.")]
    [SerializeField] private float impactDamping = 2.5f;

    [Header("Blending and Limits")]
    [Tooltip("Максимальный угол платформы по тангажу (Pitch).")]
    [SerializeField] private float maxPitch = 10f;
    [Tooltip("Максимальный угол платформы по крену (Roll).")]
    [SerializeField] private float maxRoll = 10f;

    [Tooltip("Сколько процентов движения платформы берётся из реального наклона авто (0.5 = 50%).")]
    [Range(0f, 1f)][SerializeField] private float realRotationWeight = 0.5f;

    [Tooltip("Скорость сглаживания итогового движения.")]
    [SerializeField] private float smoothSpeed = 5f;

    private ObjectTelemetryData _telemetryData;
    private SendingData _sendingData;

    private Vector3 lastVelocity;
    private float currentPitch;
    private float currentRoll;
    private float impactPitch;
    private float impactRoll;

    private void Awake()
    {
        _sendingData = new SendingData();
        _telemetryData = _sendingData.ObjectTelemetryData;
    }

    private void OnEnable()
    {
        StartCoroutine(TelemetryHandler());
        _sendingData.SendingStart();

        lastVelocity = rigidbody.velocity;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _sendingData.SendingStop();
    }

    private IEnumerator TelemetryHandler()
    {
        while (true)
        {
            if (_telemetryData == null)
            {
                yield return new WaitForSeconds(WAIT_TIME * 2);
                continue;
            }

            UpdatePlatformMotion();
            yield return new WaitForSeconds(WAIT_TIME);
        }
    }

    private void UpdatePlatformMotion()
    {
        Vector3 velocity = rigidbody.velocity;
        Vector3 acceleration = (velocity - lastVelocity) / Time.fixedDeltaTime;

        // --- Эффектная часть: ускорения ---
        float forwardAccel = Vector3.Dot(acceleration, vehicleTransform.forward);
        float lateralAccel = Vector3.Dot(acceleration, vehicleTransform.right);

        float targetEffectPitch = -forwardAccel * accelPitchFactor; // торможение = наклон вперёд
        float targetEffectRoll = -lateralAccel * cornerRollFactor;

        // --- Реальная часть: наклон автомобиля ---
        Vector3 localEuler = vehicleTransform.localRotation.eulerAngles;

        // Конвертируем углы в диапазон -180..180
        float realPitch = NormalizeAngle(localEuler.x);
        float realRoll = NormalizeAngle(localEuler.z);

        // --- Эффект столкновения ---
        impactPitch = Mathf.Lerp(impactPitch, 0f, Time.deltaTime * impactDamping);
        impactRoll = Mathf.Lerp(impactRoll, 0f, Time.deltaTime * impactDamping);

        // --- Комбинируем ---
        float finalPitch =
            (realPitch * realRotationWeight) +
            ((targetEffectPitch + impactPitch) * (1f - realRotationWeight));

        float finalRoll =
            (realRoll * realRotationWeight) +
            ((targetEffectRoll + impactRoll) * (1f - realRotationWeight));

        // --- Ограничения ---
        finalPitch = Mathf.Clamp(finalPitch, -maxPitch, maxPitch);
        finalRoll = Mathf.Clamp(finalRoll, -maxRoll, maxRoll);

        // --- Сглаживание ---
        currentPitch = Mathf.Lerp(currentPitch, finalPitch, Time.deltaTime * smoothSpeed);
        currentRoll = Mathf.Lerp(currentRoll, finalRoll, Time.deltaTime * smoothSpeed);

        // --- Отправка на платформу ---
        _telemetryData.Angles = new Vector3(currentPitch, 0f, currentRoll);
        _telemetryData.Velocity = velocity;

        lastVelocity = velocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.relativeVelocity.magnitude;
        impactPitch = -impactForce * impactFactor;
        impactRoll = UnityEngine.Random.Range(-impactForce, impactForce) * impactFactor * 0.5f;
    }

    private float NormalizeAngle(float angle)
    {
        angle = (angle + 180f) % 360f - 180f;
        return angle;
    }
}
