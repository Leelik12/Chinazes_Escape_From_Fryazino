using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

namespace RacingProject.Enemy
{
    [RequireComponent(typeof(Rigidbody))]
    // Врагом управляет сервер, клиенту позицию передаёт NetworkTransform на этом же объекте
    public class EnemyCarController : NetworkBehaviour
    {
        [Header("Target (Player Car)")]
        public Transform target;

        [Header("Navigator (NavMeshAgent holder)")]
        public NavMeshAgent navigatorAgent;

        [Header("Car Settings")]
        public float motorForce = 1500f;
        public float reverseForce = 800f;
        public float maxSteerAngle = 30f;
        public float brakeForce = 3000f;
        public float stoppingDistance = 5f;
        [Tooltip("Как часто пересчитывать путь до игрока, сек")]
        public float repathInterval = 0.25f;

        [Header("Wheels")]
        public WheelCollider frontLeftWheel;
        public WheelCollider frontRightWheel;
        public WheelCollider rearLeftWheel;
        public WheelCollider rearRightWheel;

        public Transform frontLeftMesh;
        public Transform frontRightMesh;
        public Transform rearLeftMesh;
        public Transform rearRightMesh;

        [Header("Extra Wheels (middle axles)")]
        [Tooltip("Колёса средних осей у многоосных машин; ведущие и тормозят вместе с остальными")]
        public WheelCollider[] extraWheels = new WheelCollider[0];
        public Transform[] extraWheelMeshes = new Transform[0];
        [Tooltip("Крутящий момент на все колёса, а не только на передние")]
        public bool driveAllWheels;

        [Header("Ramming")]
        [Tooltip("Таран: машина не держит дистанцию, а вблизи едет прямо в упреждённую точку игрока")]
        public bool ramTarget;
        [Tooltip("С этого расстояния таран перестаёт ехать по навигации и бьёт напрямую")]
        public float ramDistance = 45f;
        [Tooltip("Упреждение по скорости игрока, сек")]
        public float ramLeadTime = 0.5f;

        [Header("Reverse Logic")]
        public float reverseDuration = 1.6f;
        public float stuckSpeedThreshold = 0.4f;
        public float stuckTimeThreshold = 0.9f;

        [Header("Obstacle Check")]
        public float frontCheckDistance = 1.2f;
        public LayerMask obstacleMask = ~0;

        private Rigidbody rb;
        private bool reversing;
        private float reverseTimer;
        private float stuckTimer;
        private float chosenReverseSteer;

        private float repathTimer;
        private Rigidbody targetBody;

        // Колёса у клиента: машина кинематическая, WheelCollider не крутятся,
        // поэтому вращение мешей считается по пройденному пути, а угол руля задаёт сервер
        private readonly NetworkVariable<float> networkSteerAngle = new NetworkVariable<float>();
        private float remoteWheelSpin;
        private Vector3 lastRemotePosition;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

            if (navigatorAgent != null)
            {
                navigatorAgent.updatePosition = false;
                navigatorAgent.updateRotation = false;
            }

            // Физику считает только сервер, у клиента её интерполяция спорила бы с NetworkTransform
            if (IsSpawned && !IsServer)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
            else
            {
                rb.isKinematic = false;
            }

            if (target == null)
            {
                GameObject playerCar = GameObject.FindWithTag(Tags.Car);
                if (playerCar != null) target = playerCar.transform;
            }
            if (target != null)
                targetBody = target.GetComponentInParent<Rigidbody>();

            lastRemotePosition = transform.position;
        }

        void FixedUpdate()
        {
            // Только сервер управляет движением
            if (!IsSpawned || IsServer)
            {
                HandleMovement();
                if (IsSpawned)
                    networkSteerAngle.Value = frontLeftWheel.steerAngle;
            }
            else
            {
                // У клиента машину двигает NetworkTransform, здесь только колёса
                AnimateRemoteWheels();
            }
        }

        private void HandleMovement()
        {
            if (target == null || navigatorAgent == null)
                return;

            // Пересчёт пути каждый физический кадр слишком дорог, цель смещается медленно
            repathTimer -= Time.fixedDeltaTime;
            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                navigatorAgent.SetDestination(target.position);
            }

            Vector3 worldTarget = navigatorAgent.steeringTarget;
            float distanceToPlayer = Vector3.Distance(transform.position, target.position);
            // Вблизи таран едет не по пути навигации, а прямо в точку, где игрок будет через ramLeadTime
            if (ramTarget && distanceToPlayer < ramDistance)
            {
                worldTarget = target.position;
                if (targetBody != null)
                    worldTarget += targetBody.linearVelocity * ramLeadTime;
            }
            Vector3 localTarget = transform.InverseTransformPoint(worldTarget);

            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            if (Mathf.Abs(forwardSpeed) < stuckSpeedThreshold)
                stuckTimer += Time.fixedDeltaTime;
            else
                stuckTimer = 0f;

            bool frontVeryClose = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, frontCheckDistance, obstacleMask);

            if (reversing)
            {
                reverseTimer -= Time.fixedDeltaTime;
                frontLeftWheel.steerAngle = chosenReverseSteer;
                frontRightWheel.steerAngle = chosenReverseSteer;
                SetMotorTorque(-reverseForce);
                ApplyBrake(0f);

                if (reverseTimer <= 0f && !frontVeryClose)
                {
                    reversing = false;
                    stuckTimer = 0f;
                }
            }
            else
            {
                float steerAngle = CalculateSteerToLocalTarget(localTarget);
                bool inStoppingRange = distanceToPlayer <= stoppingDistance + 0.5f;

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

                        SetMotorTorque(motorForce);
                        ApplyBrake(0f);
                    }
                    else
                    {
                        frontLeftWheel.steerAngle = 0f;
                        frontRightWheel.steerAngle = 0f;
                        SetMotorTorque(0f);
                        ApplyBrake(brakeForce);
                    }
                }
            }

            UpdateWheelPoses();
            navigatorAgent.nextPosition = transform.position;
        }

        private float CalculateSteerToLocalTarget(Vector3 localTarget)
        {
            float steer = 0f;
            float localMag = localTarget.magnitude;
            if (localMag > 0.001f)
                steer = Mathf.Clamp(localTarget.x / localMag, -1f, 1f);
            float steerAngle = steer * maxSteerAngle;
            return Mathf.Clamp(steerAngle, -maxSteerAngle, maxSteerAngle);
        }

        private void StartReverseMode()
        {
            reversing = true;
            reverseTimer = reverseDuration;

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 leftBackPos = origin - transform.right * 1.0f;
            Vector3 rightBackPos = origin + transform.right * 1.0f;

            bool leftBlocked = Physics.Raycast(leftBackPos, -transform.forward, frontCheckDistance, obstacleMask);
            bool rightBlocked = Physics.Raycast(rightBackPos, -transform.forward, frontCheckDistance, obstacleMask);

            if (!leftBlocked && rightBlocked)
                chosenReverseSteer = -maxSteerAngle;
            else if (!rightBlocked && leftBlocked)
                chosenReverseSteer = maxSteerAngle;
            else
                chosenReverseSteer = (Random.value > 0.5f ? maxSteerAngle : -maxSteerAngle);
        }

        private void SetMotorTorque(float torque)
        {
            frontLeftWheel.motorTorque = torque;
            frontRightWheel.motorTorque = torque;
            if (!driveAllWheels) return;
            rearLeftWheel.motorTorque = torque;
            rearRightWheel.motorTorque = torque;
            foreach (WheelCollider wheel in extraWheels)
                wheel.motorTorque = torque;
        }

        private void ApplyBrake(float brake)
        {
            frontLeftWheel.brakeTorque = brake;
            frontRightWheel.brakeTorque = brake;
            rearLeftWheel.brakeTorque = brake;
            rearRightWheel.brakeTorque = brake;
            foreach (WheelCollider wheel in extraWheels)
                wheel.brakeTorque = brake;
        }

        private void UpdateWheelPoses()
        {
            UpdateWheelPose(frontLeftWheel, frontLeftMesh);
            UpdateWheelPose(frontRightWheel, frontRightMesh);
            UpdateWheelPose(rearLeftWheel, rearLeftMesh);
            UpdateWheelPose(rearRightWheel, rearRightMesh);
            for (int i = 0; i < extraWheels.Length && i < extraWheelMeshes.Length; i++)
                UpdateWheelPose(extraWheels[i], extraWheelMeshes[i]);
        }

        private void UpdateWheelPose(WheelCollider col, Transform mesh)
        {
            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.position = pos;
            mesh.rotation = rot;
        }

        private void AnimateRemoteWheels()
        {
            float distance = Vector3.Dot(transform.position - lastRemotePosition, transform.forward);
            lastRemotePosition = transform.position;

            float radius = rearLeftWheel != null ? rearLeftWheel.radius : 0.35f;
            remoteWheelSpin = Mathf.Repeat(remoteWheelSpin + distance / radius * Mathf.Rad2Deg, 360f);

            SetRemoteWheelRotation(frontLeftWheel, frontLeftMesh, networkSteerAngle.Value);
            SetRemoteWheelRotation(frontRightWheel, frontRightMesh, networkSteerAngle.Value);
            SetRemoteWheelRotation(rearLeftWheel, rearLeftMesh, 0f);
            SetRemoteWheelRotation(rearRightWheel, rearRightMesh, 0f);
            for (int i = 0; i < extraWheels.Length && i < extraWheelMeshes.Length; i++)
                SetRemoteWheelRotation(extraWheels[i], extraWheelMeshes[i], 0f);
        }

        private void SetRemoteWheelRotation(WheelCollider col, Transform mesh, float steer)
        {
            if (col == null || mesh == null) return;
            mesh.rotation = col.transform.rotation * Quaternion.Euler(remoteWheelSpin, steer, 0f);
        }
    }
}
