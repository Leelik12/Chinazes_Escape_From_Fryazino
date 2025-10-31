using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class EnemyCarController : MonoBehaviourPun
{
    [Header("Target (Player Car)")]
    public Transform target;

    [Header("Navigator (NavMeshAgent holder)")]
    public NavMeshAgent navigatorAgent; // �������� ������ � NavMeshAgent

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
    public float stuckSpeedThreshold = 0.4f;   // ��������, ���� ������� ������� "����� �� ����"
    public float stuckTimeThreshold = 0.9f;    // ����� � �������� �� ��������� "�����������"

    [Header("Front obstacle check")]
    public float frontCheckDistance = 1.2f;    // �������� ����������� ������ ��� ������������ �������
    public LayerMask obstacleMask = ~0;

    [Header("Debug")]
    public bool debugGizmos = false;

    private Rigidbody rb;

    // ��������� �������
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

        // ��������� ������ ����
        navigatorAgent.SetDestination(target.position);

        // ���� �������� � steeringTarget ������
        Vector3 worldTarget = navigatorAgent.steeringTarget;
        Vector3 localTarget = transform.InverseTransformPoint(worldTarget);
        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        // ��������� ������ "�����������" �� ��������
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (Mathf.Abs(forwardSpeed) < stuckSpeedThreshold)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;

        // �������� ����������� ��� � ���� ����� ������� ������
        bool frontVeryClose = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, frontCheckDistance, obstacleMask);

        if (reversing)
        {
            reverseTimer -= Time.fixedDeltaTime;

            // ��� ������� ���� ������ � ��������� �������, ����� ��������
            frontLeftWheel.steerAngle = chosenReverseSteer;
            frontRightWheel.steerAngle = chosenReverseSteer;

            frontLeftWheel.motorTorque = -reverseForce;
            frontRightWheel.motorTorque = -reverseForce;
            ApplyBrake(0f);

            // ��������� ������, ���� ����� ������� � ����� ������� ��� �������� �����������
            if (reverseTimer <= 0f && !frontVeryClose)
            {
                reversing = false;
                stuckTimer = 0f;
            }
        }
        else
        {
            // ��������� ���� �� ����� �� ������
            float steerAngle = CalculateSteerToLocalTarget(localTarget);

            // �� ������� ������������ ��������� ����� � �������
            bool inStoppingRange = distanceToPlayer <= stoppingDistance + 0.5f;

            // ������� �������: ���� ����� ��������, ���� ����� ����� (� �� ������ ��� ����� � �������)
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
                    // ����� �����, �� ������� ����
                    frontLeftWheel.steerAngle = 0f;
                    frontRightWheel.steerAngle = 0f;
                    frontLeftWheel.motorTorque = 0f;
                    frontRightWheel.motorTorque = 0f;
                    ApplyBrake(brakeForce);
                }
            }
        }

        UpdateWheelPoses();

        // ����������� ��������� � ������� ������, ����� ����� �� "��������"
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

        // ��� ������� �������� ������� ������� � ����������� �� ���������� ������������ �����
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 leftBackPos = origin - transform.right * 1.0f;  // �������� �����-���
        Vector3 rightBackPos = origin + transform.right * 1.0f; // �������� ������-���

        bool leftBlocked = Physics.Raycast(leftBackPos, -transform.forward, frontCheckDistance, obstacleMask);
        bool rightBlocked = Physics.Raycast(rightBackPos, -transform.forward, frontCheckDistance, obstacleMask);

        if (!leftBlocked && rightBlocked)
            chosenReverseSteer = -maxSteerAngle; // ���� ����� � ���� �����
        else if (!rightBlocked && leftBlocked)
            chosenReverseSteer = maxSteerAngle;  // ���� ������ � ���� ������
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

        // �������� ����������� ���
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + transform.forward * frontCheckDistance);

        // steeringTarget (�� ������) � ���� �� �������� �����
        if (navigatorAgent != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(navigatorAgent.steeringTarget, 0.2f);
            Gizmos.DrawLine(transform.position, navigatorAgent.steeringTarget);
        }

        // ���������� ����������� �������� �� �������� ���
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
    }
}



