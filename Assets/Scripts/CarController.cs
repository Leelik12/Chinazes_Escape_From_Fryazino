using UnityEngine;

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
    public bool automatic = true; // true = автомат, false = механика
    public int currentGear = 1;   // передача
    public int maxGear = 6;
    public float[] gearRatios = { -3f, 0f, 3f, 2.2f, 1.6f, 1.2f, 1f };
    // 0 = задняя, 1 = нейтраль, 2..6 = вперёд

    private Rigidbody rb;

    private float motorInput;
    private float steeringInput;
    private float brakeInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    void Update()
    {
        // Управление рулём/педалями или клавиатурой
        steeringInput = Input.GetAxis("Horizontal");   // руль
        motorInput = Input.GetAxis("Vertical");       // педали (газ/тормоз)
        brakeInput = Input.GetKey(KeyCode.Space) ? 1f : Input.GetAxis("Jump"); // педаль тормоза

        // Переключение коробки
        if (!automatic)
        {
            if (Input.GetKeyDown(KeyCode.E)) // переключение вверх
                ShiftUp();
            if (Input.GetKeyDown(KeyCode.Q)) // переключение вниз
                ShiftDown();
        }
    }

    void FixedUpdate()
    {
        // РУЛЬ
        float steering = maxSteeringAngle * steeringInput;
        frontLeftWheel.steerAngle = steering;
        frontRightWheel.steerAngle = steering;

        // МОЩНОСТЬ В ЗАВИСИМОСТИ ОТ ПЕРЕДАЧИ
        float torque = 0f;
        if (automatic)
        {
            AutoGearbox();
        }

        float gearRatio = gearRatios[currentGear + 1]; // +1 потому что 0 = задняя
        torque = maxMotorTorque * motorInput * gearRatio;

        rearLeftWheel.motorTorque = torque;
        rearRightWheel.motorTorque = torque;

        // ТОРМОЗ
        float brake = brakeForce * brakeInput;
        frontLeftWheel.brakeTorque = brake;
        frontRightWheel.brakeTorque = brake;
        rearLeftWheel.brakeTorque = brake;
        rearRightWheel.brakeTorque = brake;

        // Визуализация колёс
        UpdateWheelPose(frontLeftWheel, frontLeftTransform);
        UpdateWheelPose(frontRightWheel, frontRightTransform);
        UpdateWheelPose(rearLeftWheel, rearLeftTransform);
        UpdateWheelPose(rearRightWheel, rearRightTransform);
    }

    void UpdateWheelPose(WheelCollider collider, Transform wheelTransform)
    {
        Vector3 pos;
        Quaternion quat;
        collider.GetWorldPose(out pos, out quat);

        wheelTransform.position = pos;
        wheelTransform.rotation = quat;
    }

    // МЕХАНИКА
    void ShiftUp()
    {
        if (currentGear < maxGear) currentGear++;
    }

    void ShiftDown()
    {
        if (currentGear > -1) currentGear--; // -1 = задняя
    }

    //  АВТОМАТ
    void AutoGearbox()
    {
        float speed = rb.velocity.magnitude * 3.6f; // км/ч

        if (speed < 10f) currentGear = 1;
        else if (speed < 25f) currentGear = 2;
        else if (speed < 45f) currentGear = 3;
        else if (speed < 70f) currentGear = 4;
        else if (speed < 100f) currentGear = 5;
        else currentGear = 6;
    }
}
