//using UnityEngine;
//using Photon.Pun;

//[RequireComponent(typeof(Rigidbody))]
//public class EnemyCarController : MonoBehaviourPun
//{
//    [Header("Target (Player Car)")]
//    public Transform target;

//    [Header("Car Settings")]
//    public float motorForce = 1500f;
//    public float reverseForce = 800f;
//    public float maxSteerAngle = 30f;
//    public float brakeForce = 3000f;
//    public float stoppingDistance = 5f;

//    [Header("Car Dimensions")]
//    public float carWidth = 2f;
//    public float sideMargin = 0.5f;

//    [Header("Wheels")]
//    public WheelCollider frontLeftWheel;
//    public WheelCollider frontRightWheel;
//    public WheelCollider rearLeftWheel;
//    public WheelCollider rearRightWheel;

//    public Transform frontLeftMesh;
//    public Transform frontRightMesh;
//    public Transform rearLeftMesh;
//    public Transform rearRightMesh;

//    [Header("Sensors")]
//    public float sensorLength = 5f;
//    public float closeSensorDistance = 1.5f;
//    public float sideSensorAngle = 30f;
//    public float avoidanceSteer = 20f;
//    public LayerMask obstacleMask = ~0;

//    [Header("Stuck / Reverse")]
//    public float reverseDuration = 1.6f;
//    public float stuckSpeedThreshold = 0.4f;
//    public float stuckTimeThreshold = 0.8f;

//    [Header("Debug")]
//    public bool debugSensors = false;

//    private Rigidbody rb;
//    private bool reversing = false;
//    private float reverseTimer = 0f;
//    private float stuckTimer = 0f;
//    private float chosenReverseSteer = 0f;

//    void Start()
//    {
//        rb = GetComponent<Rigidbody>();
//        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

//        if (!photonView.IsMine && PhotonNetwork.IsConnected)
//        {
//            rb.isKinematic = true;
//            return;
//        }
//        else
//        {
//            rb.isKinematic = false;  // мастер управляет движением
//        }

//        if (target == null)
//        {
//            GameObject playerCar = GameObject.FindWithTag("Car");
//            if (playerCar != null) target = playerCar.transform;
//        }
//    }

//    void FixedUpdate()
//    {
//        if (!photonView.IsMine) return;
//        if (target == null) return;

//        Vector3 localTarget = transform.InverseTransformPoint(target.position);
//        float distance = Vector3.Distance(transform.position, target.position);

//        float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
//        if (Mathf.Abs(forwardSpeed) < stuckSpeedThreshold)
//            stuckTimer += Time.fixedDeltaTime;
//        else
//            stuckTimer = 0f;

//        bool frontVeryClose = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, closeSensorDistance, obstacleMask);

//        if (reversing)
//        {
//            reverseTimer -= Time.fixedDeltaTime;

//            frontLeftWheel.steerAngle = chosenReverseSteer;
//            frontRightWheel.steerAngle = chosenReverseSteer;

//            frontLeftWheel.motorTorque = -reverseForce;
//            frontRightWheel.motorTorque = -reverseForce;
//            ApplyBrake(0f);

//            if (reverseTimer <= 0f && !frontVeryClose)
//            {
//                reversing = false;
//                stuckTimer = 0f;
//            }
//        }
//        else
//        {
//            float steerAngle = CalculateSteerWithSensors(localTarget);

//            bool inStoppingRange = distance <= stoppingDistance + 1f;

//            if ((frontVeryClose || stuckTimer >= stuckTimeThreshold) && !inStoppingRange)
//            {
//                StartReverseMode();
//            }
//            else
//            {
//                frontLeftWheel.steerAngle = steerAngle;
//                frontRightWheel.steerAngle = steerAngle;

//                if (distance > stoppingDistance)
//                {
//                    frontLeftWheel.motorTorque = motorForce;
//                    frontRightWheel.motorTorque = motorForce;
//                    ApplyBrake(0f);
//                }
//                else
//                {
//                    // Стоим ровно, не дрыгаем рулём
//                    frontLeftWheel.steerAngle = 0f;
//                    frontRightWheel.steerAngle = 0f;
//                    frontLeftWheel.motorTorque = 0f;
//                    frontRightWheel.motorTorque = 0f;

//                    ApplyBrake(brakeForce);
//                }

//            }
//        }

//        UpdateWheelPoses();
//    }

//    private float CalculateSteerWithSensors(Vector3 localTarget)
//    {
//        float steer = 0f;
//        float localMag = localTarget.magnitude;
//        if (localMag > 0.001f) steer = Mathf.Clamp(localTarget.x / localMag, -1f, 1f);
//        float steerAngle = steer * maxSteerAngle;

//        Vector3 origin = transform.position + Vector3.up * 0.5f;
//        float sensorOffset = carWidth / 2f + sideMargin;

//        bool frontHit = Physics.Raycast(origin, transform.forward, sensorLength, obstacleMask);

//        Vector3 leftDir = Quaternion.AngleAxis(-sideSensorAngle, transform.up) * transform.forward;
//        Vector3 rightDir = Quaternion.AngleAxis(sideSensorAngle, transform.up) * transform.forward;

//        bool leftHit = Physics.Raycast(origin - transform.right * sensorOffset, leftDir, sensorLength, obstacleMask);
//        bool rightHit = Physics.Raycast(origin + transform.right * sensorOffset, rightDir, sensorLength, obstacleMask);

//        if (frontHit)
//        {
//            if (leftHit && !rightHit)
//                steerAngle += avoidanceSteer;
//            else if (!leftHit && rightHit)
//                steerAngle -= avoidanceSteer;
//            else
//                steerAngle += (Random.value > 0.5f ? avoidanceSteer : -avoidanceSteer);
//        }

//        return Mathf.Clamp(steerAngle, -maxSteerAngle, maxSteerAngle);
//    }

//    private void StartReverseMode()
//    {
//        reversing = true;
//        reverseTimer = reverseDuration;

//        Vector3 origin = transform.position + Vector3.up * 0.5f;
//        float sensorOffset = carWidth / 2f + sideMargin;

//        Vector3 leftDir = Quaternion.AngleAxis(-sideSensorAngle, transform.up) * transform.forward;
//        Vector3 rightDir = Quaternion.AngleAxis(sideSensorAngle, transform.up) * transform.forward;

//        bool leftHit = Physics.Raycast(origin - transform.right * sensorOffset, leftDir, sensorLength, obstacleMask);
//        bool rightHit = Physics.Raycast(origin + transform.right * sensorOffset, rightDir, sensorLength, obstacleMask);

//        if (!leftHit && rightHit)
//            chosenReverseSteer = -maxSteerAngle;
//        else if (!rightHit && leftHit)
//            chosenReverseSteer = maxSteerAngle;
//        else
//            chosenReverseSteer = (Random.value > 0.5f ? maxSteerAngle : -maxSteerAngle);
//    }

//    private void ApplyBrake(float brake)
//    {
//        frontLeftWheel.brakeTorque = brake;
//        frontRightWheel.brakeTorque = brake;
//        rearLeftWheel.brakeTorque = brake;
//        rearRightWheel.brakeTorque = brake;
//    }

//    private void UpdateWheelPoses()
//    {
//        UpdateWheelPose(frontLeftWheel, frontLeftMesh);
//        UpdateWheelPose(frontRightWheel, frontRightMesh);
//        UpdateWheelPose(rearLeftWheel, rearLeftMesh);
//        UpdateWheelPose(rearRightWheel, rearRightMesh);
//    }

//    private void UpdateWheelPose(WheelCollider col, Transform mesh)
//    {
//        col.GetWorldPose(out Vector3 pos, out Quaternion quat);
//        mesh.position = pos;
//        mesh.rotation = quat;
//    }

//    private void OnDrawGizmos()
//    {
//        if (!debugSensors) return;

//        Gizmos.color = Color.red;
//        Vector3 origin = transform.position + Vector3.up * 0.5f;
//        float sensorOffset = carWidth / 2f + sideMargin;

//        // Центральный прямой сенсор
//        Gizmos.DrawLine(origin, origin + transform.forward * sensorLength);

//        // Левый и правый сенсоры (угловые)
//        Vector3 leftDir = Quaternion.AngleAxis(-sideSensorAngle, transform.up) * transform.forward;
//        Vector3 rightDir = Quaternion.AngleAxis(sideSensorAngle, transform.up) * transform.forward;

//        Gizmos.color = Color.yellow;
//        Gizmos.DrawLine(origin - transform.right * sensorOffset, origin - transform.right * sensorOffset + leftDir * sensorLength);
//        Gizmos.DrawLine(origin + transform.right * sensorOffset, origin + transform.right * sensorOffset + rightDir * sensorLength);

//        // Ближний центральный сенсор
//        Gizmos.color = Color.green;
//        Gizmos.DrawLine(origin, origin + transform.forward * closeSensorDistance);
//    }
//}


using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class EnemyCarController : MonoBehaviourPun
{
    [Header("Target (Player Car)")]
    public Transform target;

    [Header("Navigator (NavMeshAgent holder)")]
    public NavMeshAgent navigatorAgent; // дочерний объект с агентом

    [Header("Car Settings")]
    public float motorForce = 1500f;
    public float reverseForce = 800f;
    public float maxSteerAngle = 30f;
    public float brakeForce = 3000f;
    public float stoppingDistance = 5f;

    [Header("Car Dimensions")]
    public float carWidth = 2f;
    public float sideMargin = 0.5f;

    [Header("Wheels")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Sensors")]
    public float sensorLength = 5f;
    public float closeSensorDistance = 1.5f;
    public float sideSensorAngle = 30f;
    public float avoidanceSteer = 20f;
    public LayerMask obstacleMask = ~0;

    [Header("Stuck / Reverse")]
    public float reverseDuration = 1.6f;
    public float stuckSpeedThreshold = 0.4f;
    public float stuckTimeThreshold = 0.8f;

    [Header("Debug")]
    public bool debugSensors = false;

    private Rigidbody rb;

    private bool reversing = false;
    private float reverseTimer = 0f;
    private float stuckTimer = 0f;
    private float chosenReverseSteer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

        if (!photonView.IsMine && PhotonNetwork.IsConnected)
        {
            rb.isKinematic = true;
            return;
        }
        else
        {
            rb.isKinematic = false;
        }

        if (target == null)
        {
            GameObject playerCar = GameObject.FindWithTag("Car");
            if (playerCar != null) target = playerCar.transform;
        }

        if (navigatorAgent != null)
        {
            navigatorAgent.updatePosition = false;
            navigatorAgent.updateRotation = false;
        }
    }

    void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        if (target == null || navigatorAgent == null) return;

        // навигатор строит путь
        navigatorAgent.SetDestination(target.position);

        // точка, куда нужно рулить
        Vector3 worldTarget = navigatorAgent.steeringTarget;
        Vector3 localTarget = transform.InverseTransformPoint(worldTarget);
        float distance = Vector3.Distance(transform.position, target.position);

        float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        if (Mathf.Abs(forwardSpeed) < stuckSpeedThreshold)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;

        bool frontVeryClose = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, closeSensorDistance, obstacleMask);

        if (reversing)
        {
            reverseTimer -= Time.fixedDeltaTime;

            frontLeftWheel.steerAngle = chosenReverseSteer;
            frontRightWheel.steerAngle = chosenReverseSteer;

            frontLeftWheel.motorTorque = -reverseForce;
            frontRightWheel.motorTorque = -reverseForce;
            ApplyBrake(0f);

            if (reverseTimer <= 0f && !frontVeryClose)
            {
                reversing = false;
                stuckTimer = 0f;
            }
        }
        else
        {
            float steerAngle = CalculateSteerWithSensors(localTarget);

            bool inStoppingRange = distance <= stoppingDistance + 1f;

            if ((frontVeryClose || stuckTimer >= stuckTimeThreshold) && !inStoppingRange)
            {
                StartReverseMode();
            }
            else
            {
                if (distance > stoppingDistance)
                {
                    frontLeftWheel.steerAngle = steerAngle;
                    frontRightWheel.steerAngle = steerAngle;

                    frontLeftWheel.motorTorque = motorForce;
                    frontRightWheel.motorTorque = motorForce;
                    ApplyBrake(0f);
                }
                else
                {
                    // Стоим ровно, не дрыгаем рулём
                    frontLeftWheel.steerAngle = 0f;
                    frontRightWheel.steerAngle = 0f;
                    frontLeftWheel.motorTorque = 0f;
                    frontRightWheel.motorTorque = 0f;

                    ApplyBrake(brakeForce);
                }
            }
        }

        UpdateWheelPoses();

        // синхронизируем позицию навигатора с машиной
        navigatorAgent.nextPosition = transform.position;
    }

    // ======== вспомогательные методы ========

    private float CalculateSteerWithSensors(Vector3 localTarget)
    {
        float steer = 0f;
        float localMag = localTarget.magnitude;
        if (localMag > 0.001f) steer = Mathf.Clamp(localTarget.x / localMag, -1f, 1f);
        float steerAngle = steer * maxSteerAngle;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        float sensorOffset = carWidth / 2f + sideMargin;

        bool frontHit = Physics.Raycast(origin, transform.forward, sensorLength, obstacleMask);

        Vector3 leftDir = Quaternion.AngleAxis(-sideSensorAngle, transform.up) * transform.forward;
        Vector3 rightDir = Quaternion.AngleAxis(sideSensorAngle, transform.up) * transform.forward;

        bool leftHit = Physics.Raycast(origin - transform.right * sensorOffset, leftDir, sensorLength, obstacleMask);
        bool rightHit = Physics.Raycast(origin + transform.right * sensorOffset, rightDir, sensorLength, obstacleMask);

        if (frontHit)
        {
            if (leftHit && !rightHit)
                steerAngle += avoidanceSteer;
            else if (!leftHit && rightHit)
                steerAngle -= avoidanceSteer;
            else
                steerAngle += (Random.value > 0.5f ? avoidanceSteer : -avoidanceSteer);
        }

        return Mathf.Clamp(steerAngle, -maxSteerAngle, maxSteerAngle);
    }

    private void StartReverseMode()
    {
        reversing = true;
        reverseTimer = reverseDuration;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        float sensorOffset = carWidth / 2f + sideMargin;

        Vector3 leftDir = Quaternion.AngleAxis(-sideSensorAngle, transform.up) * transform.forward;
        Vector3 rightDir = Quaternion.AngleAxis(sideSensorAngle, transform.up) * transform.forward;

        bool leftHit = Physics.Raycast(origin - transform.right * sensorOffset, leftDir, sensorLength, obstacleMask);
        bool rightHit = Physics.Raycast(origin + transform.right * sensorOffset, rightDir, sensorLength, obstacleMask);

        if (!leftHit && rightHit)
            chosenReverseSteer = -maxSteerAngle;
        else if (!rightHit && leftHit)
            chosenReverseSteer = maxSteerAngle;
        else
            chosenReverseSteer = (Random.value > 0.5f ? maxSteerAngle : -maxSteerAngle);
    }

    private void ApplyBrake(float brake)
    {
        frontLeftWheel.brakeTorque = brake;
        frontRightWheel.brakeTorque = brake;
        rearLeftWheel.brakeTorque = brake;
        rearRightWheel.brakeTorque = brake;
    }

    private void UpdateWheelPoses()
    {
        UpdateWheelPose(frontLeftWheel, frontLeftMesh);
        UpdateWheelPose(frontRightWheel, frontRightMesh);
        UpdateWheelPose(rearLeftWheel, rearLeftMesh);
        UpdateWheelPose(rearRightWheel, rearRightMesh);
    }

    private void UpdateWheelPose(WheelCollider col, Transform mesh)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion quat);
        mesh.position = pos;
        mesh.rotation = quat;
    }

    private void OnDrawGizmos()
    {
        if (!debugSensors) return;

        Gizmos.color = Color.red;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        float sensorOffset = carWidth / 2f + sideMargin;

        Gizmos.DrawLine(origin, origin + transform.forward * sensorLength);

        Vector3 leftDir = Quaternion.AngleAxis(-sideSensorAngle, transform.up) * transform.forward;
        Vector3 rightDir = Quaternion.AngleAxis(sideSensorAngle, transform.up) * transform.forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin - transform.right * sensorOffset, origin - transform.right * sensorOffset + leftDir * sensorLength);
        Gizmos.DrawLine(origin + transform.right * sensorOffset, origin + transform.right * sensorOffset + rightDir * sensorLength);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + transform.forward * closeSensorDistance);
    }
}


