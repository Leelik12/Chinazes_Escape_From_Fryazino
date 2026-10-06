using UnityEngine;

namespace RacingProject.Network
{
    // Локальная поза объекта, которую двигает игрок-автор (например, руль у водителя).
    // У автора компонент только отправляет позу, у напарника плавно подтягивает объект к ней
    public class RoleSyncedTransform : RoleSyncedBehaviour
    {
        [SerializeField] private bool syncPosition = true;
        [SerializeField] private bool syncRotation = true;
        [Range(1f, 60f)]
        [SerializeField] private float lerpSpeed = 20f;

        private Vector3 networkPosition;
        private Quaternion networkRotation;

        private void Awake()
        {
            networkPosition = transform.localPosition;
            networkRotation = transform.localRotation;
        }

        private void LateUpdate()
        {
            if (HasAuthority) return;

            float t = Time.deltaTime * lerpSpeed;
            if (syncPosition)
                transform.localPosition = Vector3.Lerp(transform.localPosition, networkPosition, t);
            if (syncRotation)
                transform.localRotation = Quaternion.Slerp(transform.localRotation, networkRotation, t);
        }

        public override void Serialize(SyncStream stream)
        {
            if (stream.IsWriting)
            {
                networkPosition = transform.localPosition;
                networkRotation = transform.localRotation;
            }
            stream.Serialize(ref networkPosition);
            stream.Serialize(ref networkRotation);
        }
    }
}
