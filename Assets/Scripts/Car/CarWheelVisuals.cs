using System;
using UnityEngine;
using RacingProject.Network;

namespace RacingProject.Car
{
    // Вращение мешей колёс машины игроков.
    // У хоста (водителя) поворот берётся из WheelCollider, у второго игрока машина кинематическая и коллайдеры
    // не крутятся, поэтому вращение считается по пройденному пути, а угол руля приходит по сети.
    // Позиция мешей не меняется: пивоты стоят в центре колеса, подвеска на виде не отражается. Пробитое колесо
    // (CarDamageState) опускается на высоту спущенной шины
    public class CarWheelVisuals : RoleSyncedBehaviour
    {
        [Serializable]
        public class WheelVisual
        {
            public WheelCollider collider;
            public Transform mesh;
            public bool steering;
        }

        [SerializeField] private WheelVisual[] wheels;

        private float networkSteerAngle;
        private Vector3[] basePositions;
        private float[] flatDrop;
        private float remoteSpin;
        private Vector3 lastPosition;

        private void Start()
        {
            lastPosition = transform.position;
            basePositions = new Vector3[wheels.Length];
            flatDrop = new float[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i].mesh != null) basePositions[i] = wheels[i].mesh.localPosition;
        }

        // Спущенное колесо: drop — насколько опустить меш, м (0 — целое)
        public void SetFlat(WheelCollider collider, float drop)
        {
            if (flatDrop == null) Start();
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i].collider == collider) flatDrop[i] = drop;
        }

        // После физики и сетевой интерполяции позиции машины
        private void LateUpdate()
        {
            if (LocalPlayerRole.SimulatesPhysics)
                UpdateFromColliders();
            else
                UpdateFromMovement();
            ApplyFlats();

            lastPosition = transform.position;
        }

        private void UpdateFromColliders()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.collider == null || wheel.mesh == null) continue;
                wheel.collider.GetWorldPose(out _, out Quaternion rotation);
                wheel.mesh.rotation = rotation;
            }
        }

        private void UpdateFromMovement()
        {
            float distance = Vector3.Dot(transform.position - lastPosition, transform.forward);

            foreach (var wheel in wheels)
            {
                if (wheel.collider == null || wheel.mesh == null) continue;
                float radius = wheel.collider.radius * wheel.collider.transform.lossyScale.y;
                if (radius > 0.001f)
                {
                    remoteSpin = Mathf.Repeat(remoteSpin + distance / radius * Mathf.Rad2Deg, 360f);
                    break;
                }
            }

            foreach (var wheel in wheels)
            {
                if (wheel.collider == null || wheel.mesh == null) continue;
                float steer = wheel.steering ? networkSteerAngle : 0f;
                wheel.mesh.rotation = wheel.collider.transform.rotation * Quaternion.Euler(remoteSpin, steer, 0f);
            }
        }

        private void ApplyFlats()
        {
            if (flatDrop == null) return;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i].mesh == null) continue;
                Vector3 position = basePositions[i];
                if (flatDrop[i] > 0f)
                {
                    // Сдвиг вниз в осях машины, переведённый в локальные оси родителя меша
                    Transform parent = wheels[i].mesh.parent;
                    Vector3 down = -transform.up * flatDrop[i];
                    if (parent != null) down = parent.InverseTransformVector(down);
                    position += down;
                }
                wheels[i].mesh.localPosition = position;
            }
        }

        private float CurrentSteerAngle()
        {
            foreach (var wheel in wheels)
                if (wheel.steering && wheel.collider != null)
                    return wheel.collider.steerAngle;
            return 0f;
        }

        public override void Serialize(SyncStream stream)
        {
            if (stream.IsWriting)
                networkSteerAngle = CurrentSteerAngle();
            stream.Serialize(ref networkSteerAngle);
        }
    }
}
