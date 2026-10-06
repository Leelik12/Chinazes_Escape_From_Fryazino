using System;
using Photon.Pun;
using UnityEngine;

namespace RacingProject.Car
{
    // Вращение мешей колёс машины игроков.
    // У водителя поворот берётся из WheelCollider, у второго игрока машина кинематическая и коллайдеры
    // не крутятся, поэтому вращение считается по пройденному пути, а угол руля приходит по сети.
    // Позиция мешей не меняется: пивоты стоят в центре колеса, подвеска на виде не отражается
    public class CarWheelVisuals : MonoBehaviourPun, IPunObservable
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
        private float remoteSpin;
        private Vector3 lastPosition;

        private void Start()
        {
            lastPosition = transform.position;
        }

        // После физики и сетевой интерполяции позиции машины
        private void LateUpdate()
        {
            if (photonView.IsMine)
                UpdateFromColliders();
            else
                UpdateFromMovement();

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

        private float CurrentSteerAngle()
        {
            foreach (var wheel in wheels)
                if (wheel.steering && wheel.collider != null)
                    return wheel.collider.steerAngle;
            return 0f;
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
                stream.SendNext(CurrentSteerAngle());
            else
                networkSteerAngle = (float)stream.ReceiveNext();
        }
    }
}
