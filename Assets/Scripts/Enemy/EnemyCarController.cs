using UnityEngine;
using Photon.Pun;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public class EnemyCarController : MonoBehaviourPun
{
    [Header("Target (Player Car)")]
    public Transform target;

    [Header("Car Settings")]
    public float motorForce = 1500f;
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

    private Rigidbody rb;
    private NavMeshAgent agent;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();

        // Агент будет считать путь, но не управлять физикой
        agent.updatePosition = false;
        agent.updateRotation = false;

        if (!photonView.IsMine && PhotonNetwork.IsConnected)
        {
            rb.isKinematic = true;
            return;
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
        if (target == null) return;

        // Обновляем цель в NavMeshAgent
        agent.SetDestination(target.position);

        // Берём следующую точку пути
        Vector3 nextPoint = agent.steeringTarget;
        Vector3 localTarget = transform.InverseTransformPoint(nextPoint);

        float distance = Vector3.Distance(transform.position, target.position);

        // Поворот на ближайшую точку
        float steer = Mathf.Clamp(localTarget.x / localTarget.magnitude, -1f, 1f);
        float steerAngle = steer * maxSteerAngle;
        frontLeftWheel.steerAngle = steerAngle;
        frontRightWheel.steerAngle = steerAngle;

        if (distance > stoppingDistance)
        {
            // едем
            frontLeftWheel.motorTorque = motorForce;
            frontRightWheel.motorTorque = motorForce;
            ApplyBrake(0);
        }
        else
        {
            // тормозим
            ApplyBrake(brakeForce);
        }

        UpdateWheelPoses();
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
        Vector3 pos;
        Quaternion quat;
        col.GetWorldPose(out pos, out quat);
        mesh.position = pos;
        mesh.rotation = quat;
    }
}
    