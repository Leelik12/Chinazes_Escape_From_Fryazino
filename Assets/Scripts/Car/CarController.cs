using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class CarController : MonoBehaviourPun
{
    [Header("Звуки")]
    public AudioSource Engine;
    public AudioClip Idle;
    public AudioClip Racing;

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

    [Header("Руль")]
    public Transform steeringWheel;              // сам руль
    public float maxSteeringWheelAngle = 450f;   // максимальный угол поворота руля
    public float steeringWheelSmoothness = 10f;  // скорость поворота

    [Header("Параметры машины")]
    public float maxMotorTorque = 1500f;
    public float minStartTorque = 200f;
    public float maxSteeringAngle = 30f;
    public float brakeForce = 15000f;
    public float idleRPM = 900f;
    public float maxRPM = 7000f;
    public float engineSmoothTime = 0.2f;

    [Header("Коробка передач")]
    public int currentGear = 1; // -1 = задняя, 0 = нейтраль, 1..n = вперед
    public int maxGear = 5;
    public float[] gearRatios = { -3f, 0f, 3.6f, 2.2f, 1.6f, 1.2f };

    [Header("UI")]
    public TMP_Text speedText;
    public TMP_Text rpmText;
    public TMP_Text GearText;
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
    private float handbrakeInput;
    private float engineRPM;
    private float rpmVelocity;

    private Quaternion flRotOffset, frRotOffset, rlRotOffset, rrRotOffset;
    private bool fl;
    private bool lastfl;

    private float currentWheelRotation = 0f; // текущее вращение руля (в градусах)

    void Start()
    {
        var cam = GetComponentInChildren<Camera>(true);
        if (cam != null) cam.gameObject.SetActive(photonView.IsMine);

        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        flRotOffset = frontLeftTransform.localRotation;
        frRotOffset = frontRightTransform.localRotation;
        rlRotOffset = rearLeftTransform.localRotation;
        rrRotOffset = rearRightTransform.localRotation;
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        steeringInput = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical"); // W=1, S=-1

        // Вращение руля
        if (steeringWheel != null)
        {
            float targetRotation = -steeringInput * maxSteeringWheelAngle;
            currentWheelRotation = Mathf.Lerp(currentWheelRotation, targetRotation, Time.deltaTime * steeringWheelSmoothness);
            steeringWheel.localRotation = Quaternion.Euler(25f, 0f, currentWheelRotation);
        }

        // Обработка коробки передач
        if (currentGear == -1)
        {
            motorInput = vertical > 0f ? vertical : 0f;
            brakeInput = vertical < 0f ? -vertical : 0f;
        }
        else if (currentGear == 0)
        {
            motorInput = 0f;
            brakeInput = vertical < 0f ? -vertical : 0f;
        }
        else
        {
            motorInput = vertical > 0f ? vertical : 0f;
            brakeInput = vertical < 0f ? -vertical : 0f;
        }

        handbrakeInput = Input.GetKey(KeyCode.Space) ? 1f : 0f;

        if (Input.GetKeyDown(KeyCode.O))
            SceneManager.LoadScene(0);

        // Переключение передач вручную
        if (Input.GetKeyDown(KeyCode.E)) ShiftUp();
        if (Input.GetKeyDown(KeyCode.Q)) ShiftDown();

        // Аэродинамическая прижимная сила
        float downforce = rb.linearVelocity.magnitude * 300f;
        rb.AddForce(-transform.up * downforce);

        UpdateGauges();
    }

    void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        float speed = rb.linearVelocity.magnitude * 3.6f;

        // Руль (управление колёсами)
        float speedFactor = Mathf.Clamp01(speed / 200f);
        float dynamicSteer = Mathf.Lerp(maxSteeringAngle, maxSteeringAngle * 0.2f, speedFactor);
        frontLeftWheel.steerAngle = dynamicSteer * steeringInput;
        frontRightWheel.steerAngle = dynamicSteer * steeringInput;

        // RPM двигателя
        UpdateEngine();

        int gearIndex = Mathf.Clamp(currentGear + 1, 0, gearRatios.Length - 1);
        float torque = maxMotorTorque * motorInput * Mathf.Abs(gearRatios[gearIndex]);

        if (rb.linearVelocity.magnitude < 1f)
            torque = Mathf.Max(torque, minStartTorque * Mathf.Abs(gearRatios[gearIndex]));

        if (engineRPM >= maxRPM) torque = 0f;
        if (engineRPM <= 1000)
        {
            Engine.clip = Idle;
            fl = true;
        }
        else
        {
            Engine.clip = Racing;
            fl = false;
            Engine.pitch = currentGear <= 1 ? 1f : 1f + 0.1f * currentGear;
        }
        if (fl != lastfl)
        {
            lastfl = fl;
            Engine.Play();
        }

        // Применяем момент
        if (currentGear == -1)
        {
            rearLeftWheel.motorTorque = -torque;
            rearRightWheel.motorTorque = -torque;
        }
        else
        {
            rearLeftWheel.motorTorque = torque;
            rearRightWheel.motorTorque = torque;
        }

        // Тормоза
        float brake = brakeForce * brakeInput;
        frontLeftWheel.brakeTorque = brake;
        frontRightWheel.brakeTorque = brake;
        rearLeftWheel.brakeTorque = brake;
        rearRightWheel.brakeTorque = brake;

        // Ручник
        float handbrake = brakeForce * 1.5f * handbrakeInput;
        rearLeftWheel.brakeTorque += handbrake;
        rearRightWheel.brakeTorque += handbrake;

        // Визуализация колёс
        UpdateWheelPose(frontLeftWheel, frontLeftTransform, flRotOffset);
        UpdateWheelPose(frontRightWheel, frontRightTransform, frRotOffset);
        UpdateWheelPose(rearLeftWheel, rearLeftTransform, rlRotOffset);
        UpdateWheelPose(rearRightWheel, rearRightTransform, rrRotOffset);
    }

    void UpdateEngine()
    {
        int gearIndex = Mathf.Clamp(currentGear + 1, 0, gearRatios.Length - 1);
        float wheelRPM = (rearLeftWheel.rpm + rearRightWheel.rpm) * 0.5f;
        float targetRPM = idleRPM + wheelRPM * Mathf.Abs(gearRatios[gearIndex]);
        engineRPM = Mathf.SmoothDamp(engineRPM, targetRPM, ref rpmVelocity, engineSmoothTime);
        engineRPM = Mathf.Clamp(engineRPM, idleRPM, maxRPM);
    }

    void UpdateWheelPose(WheelCollider collider, Transform wheelTransform, Quaternion rotOffset)
    {
        collider.GetWorldPose(out Vector3 pos, out Quaternion quat);
        wheelTransform.position = pos;
        wheelTransform.rotation = quat * rotOffset;
    }

    void UpdateGauges()
    {
        float speed = rb.linearVelocity.magnitude * 3.6f;

        if (speedText) speedText.text = $"{Mathf.RoundToInt(speed)} km/h";
        if (rpmText) rpmText.text = $"{Mathf.RoundToInt(engineRPM)} rpm";
        if (GearText) GearText.text = $"{currentGear}";

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

    void ShiftUp()
    {
        if (currentGear < maxGear) currentGear++;
    }

    void ShiftDown()
    {
        if (currentGear > -1) currentGear--;
    }
}
