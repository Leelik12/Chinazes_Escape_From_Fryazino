using System.Collections;
using _2DOF;
using UnityEngine;

public class CarTelemetryHandler : MonoBehaviour
{
    private const float WAIT_TIME = SendingData.WAIT_TIME / 1000f;

    [Header("References")]
    [SerializeField] private Transform vehicleTransform;
    [SerializeField] private Rigidbody rb;

    [Header("Effect factors")]
    [Tooltip("Влияние продольного ускорения на наклон вперёд/назад (положительное => наклон вперёд при торможении)")]
    [SerializeField] private float accelPitchFactor = 0.02f;

    [Tooltip("Влияние бокового ускорения на наклон в сторону")]
    [SerializeField] private float cornerRollFactor = 0.03f;

    [Tooltip("Влияние углового ускорения (yaw accel) на боковой наклон")]
    [SerializeField] private float angularAccelFactor = 0.8f;

    [Header("Sudden accel/brake detection")]
    [Tooltip("Порог резкого изменения продольного ускорения (м/с²)")]
    [SerializeField] private float suddenAccelThreshold = 3.0f;
    [Tooltip("Мультипликатор эффекта при резком ускорении/торможении")]
    [SerializeField] private float suddenAccelMultiplier = 0.5f;

    [Header("Impact (collision)")]
    [Tooltip("Множитель силы удара для pitch")]
    [SerializeField] private float impactFactor = 0.02f;
    [Tooltip("Затухание сдвига от удара")]
    [SerializeField] private float impactDamping = 3.0f;

    [Header("Limits & smoothing")]
    [SerializeField] private float maxPitch = 12f;
    [SerializeField] private float maxRoll = 12f;
    [Tooltip("Плавность выхода (чем выше — тем мягче), 0 = без сглаживания")]
    [SerializeField] private float outputSmoothing = 6f;

    private SendingData _sendingData;
    private ObjectTelemetryData _telemetryData;

    // состояния между сэмплами
    private Vector3 lastLinearVelocity = Vector3.zero;
    private Vector3 lastAngularVelocity = Vector3.zero;
    private float lastSampleTime = 0f;

    // накопленные импульсы от ударов
    private float impactPitch = 0f;
    private float impactRoll = 0f;

    // текущее (выходное) значение углов платформы
    private float outPitch = 0f;
    private float outRoll = 0f;

    private void Awake()
    {
        _sendingData = new SendingData();
        _telemetryData = _sendingData.ObjectTelemetryData;
    }

    private void OnEnable()
    {
        StartCoroutine(TelemetryHandler());
        _sendingData.SendingStart();

        if (rb != null)
        {
            lastLinearVelocity = rb.linearVelocity;
            lastAngularVelocity = rb.angularVelocity;
        }
        lastSampleTime = Time.time;
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
            if (_telemetryData != null && vehicleTransform != null && rb != null)
                UpdatePlatformMotion();

            yield return new WaitForSeconds(Mathf.Max(WAIT_TIME, 0.001f));
        }
    }

    private void UpdatePlatformMotion()
    {
        // время и dt
        float now = Time.time;
        float dt = Mathf.Max(now - lastSampleTime, 0.0001f);
        lastSampleTime = now;

        // скорости
        Vector3 velocity = rb.linearVelocity;
        Vector3 angularVel = rb.angularVelocity; // рад/с в мировых координатах

        // линейное ускорение (м/с^2)
        Vector3 linearAccel = (velocity - lastLinearVelocity) / dt;

        // угловое ускорение (рад/с^2)
        Vector3 angularAccelWorld = (angularVel - lastAngularVelocity) / dt;

        // переводим угловую скорость и ускорение в локальные координаты машины
        Vector3 localAngularVel = vehicleTransform.InverseTransformDirection(angularVel);
        Vector3 localAngularAccel = vehicleTransform.InverseTransformDirection(angularAccelWorld);

        // Угловое ускорение по локальной вертикали (yaw)
        float yawAccel = localAngularAccel.y;


        // продольное и боковое ускорение в локальных координатах автомобиля
        float forwardAccel = Vector3.Dot(linearAccel, vehicleTransform.forward); // + при ускорении вперёд
        float lateralAccel = Vector3.Dot(linearAccel, vehicleTransform.right);   // + при движении вправо

        // Угловое ускорение вокруг вертикальной оси (yaw accel) — влияет на боковой наклон

        // --- вычисление эффектов ---
        // 1) эффект от продольного ускорения: при торможении (negative forwardAccel) — наклон вперёд
        float pitchFromAccel = -forwardAccel * accelPitchFactor;

        // 2) эффект от бокового ускорения
        float rollFromLateral = -lateralAccel * cornerRollFactor;

        // 3) эффект от углового ускорения (поворот руля дает наклон)
        float rollFromAngularAccel = -yawAccel * angularAccelFactor;

        // 4) резкие ускорения / торможения — детектируем по forwardAccel величине
        float suddenEffect = 0f;
        if (Mathf.Abs(forwardAccel) >= suddenAccelThreshold)
        {
            suddenEffect = -forwardAccel * suddenAccelMultiplier;
            // можно добавить положительное смещение при резком ускорении и более сильный при резком торможении
        }

        // 5) эффект от удара — затухает со временем
        impactPitch = Mathf.Lerp(impactPitch, 0f, Time.deltaTime * impactDamping);
        impactRoll = Mathf.Lerp(impactRoll, 0f, Time.deltaTime * impactDamping);

        // 6) реальные углы машины (чтобы учитывать положение на рельефе)
        Vector3 localEuler = vehicleTransform.localRotation.eulerAngles;
        float realPitch = NormalizeAngle(localEuler.x);
        float realRoll = NormalizeAngle(localEuler.z);

        // --- финальное суммирование (без весов, просто суммируем вкладов) ---
        float targetPitch = realPitch + pitchFromAccel + suddenEffect + impactPitch;
        float targetRoll = realRoll + rollFromLateral + rollFromAngularAccel + impactRoll;

        // ограничиваем
        targetPitch = Mathf.Clamp(targetPitch, -maxPitch, maxPitch);
        targetRoll = Mathf.Clamp(targetRoll, -maxRoll, maxRoll);

        // сглаживание выхода (если нужно)
        if (outputSmoothing > 0f)
        {
            float t = 1f - Mathf.Exp(-outputSmoothing * Time.deltaTime); // экспоненциальное сглаживание
            outPitch = Mathf.Lerp(outPitch, targetPitch, t);
            outRoll = Mathf.Lerp(outRoll, targetRoll, t);
        }
        else
        {
            outPitch = targetPitch;
            outRoll = targetRoll;
        }

        // записываем в телеметрию
        _telemetryData.Angles = new Vector3(outPitch, 0f, outRoll);
        _telemetryData.Velocity = velocity;
        Debug.Log($"2dof : {_telemetryData.Angles:F2}°");
        // сохраняем для следующего шага
        lastLinearVelocity = velocity;
        lastAngularVelocity = angularVel;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // сила удара: используем относительную скорость при столкновении
        float impactForce = collision.relativeVelocity.magnitude;

        // при ударе делаем быстрое добавление небольшого импульса к pitch/roll
        impactPitch += -impactForce * impactFactor;
        impactRoll += Random.Range(-impactForce, impactForce) * impactFactor * 0.5f;
    }

    private float NormalizeAngle(float angle)
    {
        angle = (angle + 180f) % 360f - 180f;
        return angle;
    }
}
