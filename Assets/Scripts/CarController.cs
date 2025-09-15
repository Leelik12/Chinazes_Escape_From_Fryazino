using System;
using UnityEngine;
using TMPro;

public class CarController : MonoBehaviour
{
    [Header("Колёса (WheelColliders)")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Визуальные модели колёс")]
    public Transform frontLeftTransform;
    public Transform frontRightTransform;
    public Transform rearLeftTransform;
    public Transform rearRightTransform;

    [Header("Параметры машины")]
    public float maxMotorTorque = 1500f;
    public float maxSteeringAngle = 30f;
    public float brakeForce = 3000f;

    [Header("Коробка передач")]
    public bool automatic = true;
    public int currentGear = 1;
    public int maxGear = 6;
    public float[] gearRatios = { -3f, 0f, 3f, 2.2f, 1.6f, 1.2f, 1f };
    // 0 = задняя, 1 = нейтраль, 2..6 = вперёд

    [Header("Двигатель")]
    public float maxRPM = 7000f;
    public float idleRPM = 900f;
    public float engineRPM;
    public float engineResponse = 5f;

    [Header("UI Текст")]
    public TMP_Text speedText;
    public TMP_Text rpmText;

    [Header("UI Стрелки")]
    public RectTransform speedNeedle;
    public RectTransform rpmNeedle;
    public float speedMaxAngle = -220f;
    public float speedMinAngle = 40f;
    public float rpmMaxAngle = -220f;
    public float rpmMinAngle = 40f;
    public float maxSpeed = 240f;

    private Rigidbody rb;
    private float motorInput;
    private float steeringInput;
    private float brakeInput;

    private Quaternion flRotOffset, frRotOffset, rlRotOffset, rrRotOffset;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.7f, 0);

        // сохраняем смещения колёс
        flRotOffset = frontLeftTransform.localRotation;
        frRotOffset = frontRightTransform.localRotation;
        rlRotOffset = rearLeftTransform.localRotation;
        rrRotOffset = rearRightTransform.localRotation;
    }

    void Update()
    {
        steeringInput = Input.GetAxis("Horizontal");
        motorInput = Mathf.Clamp01(Input.GetAxis("Vertical")); // только газ, без заднего хода
        brakeInput = Input.GetKey(KeyCode.Space) ? 1f : Input.GetAxis("Jump");

        if (!automatic)
        {
            if (Input.GetKeyDown(KeyCode.E)) ShiftUp();
            if (Input.GetKeyDown(KeyCode.Q)) ShiftDown();
        }

        UpdateGauges();

    }

    void FixedUpdate()
    {
        float speed = rb.velocity.magnitude * 3.6f;

        // динамический руль
        float speedFactor = Mathf.Clamp01(speed / 100f);
        float dynamicSteer = Mathf.Lerp(maxSteeringAngle, maxSteeringAngle * 0.2f, speedFactor);
        float steering = dynamicSteer * steeringInput;
        frontLeftWheel.steerAngle = steering;
        frontRightWheel.steerAngle = steering;

        if (automatic) AutoGearbox();

        // обновляем обороты
        UpdateEngine(motorInput);

        // считаем крутящий момент
        float gearRatio = gearRatios[currentGear + 1];
        float torque = (engineRPM / maxRPM) * maxMotorTorque * gearRatio;
        rearLeftWheel.motorTorque = torque * motorInput;
        rearRightWheel.motorTorque = torque * motorInput;

        // тормоз
        float brake = brakeForce * brakeInput;
        frontLeftWheel.brakeTorque = brake;
        frontRightWheel.brakeTorque = brake;
        rearLeftWheel.brakeTorque = brake;
        rearRightWheel.brakeTorque = brake;

        // визуализация колёс
        UpdateWheelPose(frontLeftWheel, frontLeftTransform, flRotOffset);
        UpdateWheelPose(frontRightWheel, frontRightTransform, frRotOffset);
        UpdateWheelPose(rearLeftWheel, rearLeftTransform, rlRotOffset);
        UpdateWheelPose(rearRightWheel, rearRightTransform, rrRotOffset);

        //float downforce = torque * motorInput * 10f; // сила пропорциональна скорости
        //rb.AddForce(-transform.up * downforce);
    }

    void UpdateEngine(float throttle)
    {
        // среднее RPM задних колёс (в реале оно связано через трансмиссию)
        float wheelRPM = (rearLeftWheel.rpm + rearRightWheel.rpm);

        // считаем обороты двигателя через передачу
        float gearRatio = gearRatios[currentGear + 1];
        float targetRPM = Mathf.Abs(wheelRPM * gearRatio);

        // если машина стоит, даём газ → обороты растут сами (букс на сцеплении)
        if (rb.velocity.magnitude < 1f && throttle > 0.1f)
        {
            targetRPM = Mathf.Lerp(engineRPM, maxRPM * throttle, Time.deltaTime * engineResponse);
        }

        // не даём опускаться ниже холостых
        targetRPM = Mathf.Max(targetRPM, idleRPM);

        // сглаживаем
        engineRPM = Mathf.Lerp(engineRPM, targetRPM, Time.deltaTime * engineResponse);
    }


    void UpdateGauges()
    {
        float speed = rb.velocity.magnitude * 3.6f;

        if (speedText) speedText.text = $"{Mathf.RoundToInt(speed)} km/h";
        if (rpmText) rpmText.text = $"{Mathf.RoundToInt(engineRPM)} rpm";

        if (speedNeedle)
        {
            float speedNorm = Mathf.Clamp01(speed / maxSpeed);
            speedNeedle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(speedMinAngle, speedMaxAngle, speedNorm));
        }

        if (rpmNeedle)
        {
            float rpmNorm = Mathf.Clamp01(engineRPM / maxRPM);
            rpmNeedle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(rpmMinAngle, rpmMaxAngle, rpmNorm));
        }
    }

    void UpdateWheelPose(WheelCollider collider, Transform wheelTransform, Quaternion rotOffset)
    {
        Vector3 pos;
        Quaternion quat;
        collider.GetWorldPose(out pos, out quat);

        wheelTransform.position = pos;
        wheelTransform.rotation = quat * rotOffset;
    }

    void ShiftUp()
    {
        if (currentGear < maxGear)
        {
            currentGear++;
            RecalculateRPM();
        }
    }

    void ShiftDown()
    {
        if (currentGear > -1)
        {
            currentGear--;
            RecalculateRPM();
        }
    }

    void RecalculateRPM()
    {
        float wheelRPM = (rearLeftWheel.rpm + rearRightWheel.rpm) * 0.5f;
        float gearRatio = gearRatios[currentGear + 1];
        engineRPM = Mathf.Max(idleRPM, Mathf.Abs(wheelRPM * gearRatio));
    }

    void AutoGearbox()
    {
        float speed = rb.velocity.magnitude * 3.6f;

        if (speed < 10f) currentGear = 1;
        else if (speed < 25f) currentGear = 2;
        else if (speed < 45f) currentGear = 3;
        else if (speed < 70f) currentGear = 4;
        else if (speed < 100f) currentGear = 5;
        else currentGear = 6;
    }
}
