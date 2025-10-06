using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class EnemyCarController : MonoBehaviourPun
{
    [Header("Target (Player Car)")]
    public Transform target;

    [Header("Navigator (NavMeshAgent holder)")]
    public NavMeshAgent navigatorAgent; // дочерний объект с NavMeshAgent

    [Header("Car Settings")]
    public float motorForce = 1500f;
    public float reverseForce = 800f;
    public float maxSteerAngle = 30f;
    public float brakeForce = 3000f;
    public float stoppingDistance = 5f;

    [Header("Wheels")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Stuck / Reverse")]
    public float reverseDuration = 1.6f;
    public float stuckSpeedThreshold = 0.4f;   // скорость, ниже которой считаем "почти не едет"
    public float stuckTimeThreshold = 0.9f;    // время в секундах до признания "застревания"

    [Header("Front obstacle check")]
    public float frontCheckDistance = 1.2f;    // короткий фронтальный датчик для немедленного реверса
    public LayerMask obstacleMask = ~0;

    [Header("Debug")]
    public bool debugGizmos = false;

    private Rigidbody rb;

    // состояние реверса
    private bool reversing = false;
    private float reverseTimer = 0f;
    private float stuckTimer = 0f;
    private float chosenReverseSteer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

        if (navigatorAgent != null)
        {
            navigatorAgent.updatePosition = false;
            navigatorAgent.updateRotation = false;
        }

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
    }

    void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        if (target == null || navigatorAgent == null) return;

        // навигатор строит путь
        navigatorAgent.SetDestination(target.position);

        // цель движения — steeringTarget агента
        Vector3 worldTarget = navigatorAgent.steeringTarget;
        Vector3 localTarget = transform.InverseTransformPoint(worldTarget);
        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        // обновляем таймер "застревания" по скорости
        float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        if (Mathf.Abs(forwardSpeed) < stuckSpeedThreshold)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;

        // короткий фронтальный чек — если упёрся слишком близко
        bool frontVeryClose = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, frontCheckDistance, obstacleMask);

        if (reversing)
        {
            reverseTimer -= Time.fixedDeltaTime;

            // при реверсе рулём крутим в выбранную сторону, чтобы вырулить
            frontLeftWheel.steerAngle = chosenReverseSteer;
            frontRightWheel.steerAngle = chosenReverseSteer;

            frontLeftWheel.motorTorque = -reverseForce;
            frontRightWheel.motorTorque = -reverseForce;
            ApplyBrake(0f);

            // завершаем реверс, если время истекло и перед машиной нет близкого препятствия
            if (reverseTimer <= 0f && !frontVeryClose)
            {
                reversing = false;
                stuckTimer = 0f;
            }
        }
        else
        {
            // вычисляем угол на точку из агента
            float steerAngle = CalculateSteerToLocalTarget(localTarget);

            // не считаем застреванием остановку рядом с игроком
            bool inStoppingRange = distanceToPlayer <= stoppingDistance + 0.5f;

            // условие реверса: либо упёрся вплотную, либо долго стоял (и не потому что рядом с игроком)
            if ((frontVeryClose || stuckTimer >= stuckTimeThreshold) && !inStoppingRange)
            {
                StartReverseMode();
            }
            else
            {
                if (distanceToPlayer > stoppingDistance)
                {
                    frontLeftWheel.steerAngle = steerAngle;
                    frontRightWheel.steerAngle = steerAngle;

                    frontLeftWheel.motorTorque = motorForce;
                    frontRightWheel.motorTorque = motorForce;
                    ApplyBrake(0f);
                }
                else
                {
                    // стоим ровно, не дрыгаем рулём
                    frontLeftWheel.steerAngle = 0f;
                    frontRightWheel.steerAngle = 0f;
                    frontLeftWheel.motorTorque = 0f;
                    frontRightWheel.motorTorque = 0f;
                    ApplyBrake(brakeForce);
                }
            }
        }

        UpdateWheelPoses();

        // привязываем навигатор к позиции машины, чтобы агент не "двигался"
        navigatorAgent.nextPosition = transform.position;
    }

    private float CalculateSteerToLocalTarget(Vector3 localTarget)
    {
        float steer = 0f;
        float localMag = localTarget.magnitude;
        if (localMag > 0.001f) steer = Mathf.Clamp(localTarget.x / localMag, -1f, 1f);
        float steerAngle = steer * maxSteerAngle;
        return Mathf.Clamp(steerAngle, -maxSteerAngle, maxSteerAngle);
    }

    private void StartReverseMode()
    {
        reversing = true;
        reverseTimer = reverseDuration;

        // при реверсе выбираем сторону руления в зависимости от свободного пространства задом
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 leftBackPos = origin - transform.right * 1.0f;  // проверка слева-зад
        Vector3 rightBackPos = origin + transform.right * 1.0f; // проверка справа-зад

        bool leftBlocked = Physics.Raycast(leftBackPos, -transform.forward, frontCheckDistance, obstacleMask);
        bool rightBlocked = Physics.Raycast(rightBackPos, -transform.forward, frontCheckDistance, obstacleMask);

        if (!leftBlocked && rightBlocked)
            chosenReverseSteer = -maxSteerAngle; // рулём влево — сдаём влево
        else if (!rightBlocked && leftBlocked)
            chosenReverseSteer = maxSteerAngle;  // рулём вправо — сдаём вправо
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
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }

    private void OnDrawGizmos()
    {
        if (!debugGizmos) return;

        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // короткий фронтальный чек
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + transform.forward * frontCheckDistance);

        // steeringTarget (из агента) — куда мы пытаемся ехать
        if (navigatorAgent != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(navigatorAgent.steeringTarget, 0.2f);
            Gizmos.DrawLine(transform.position, navigatorAgent.steeringTarget);
        }

        // показываем направление движения по передней оси
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
    }
}



