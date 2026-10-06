using UnityEngine;
using RacingProject.Network;

namespace RacingProject.PlayerBody
{
    [DefaultExecutionOrder(100)]
    public class NetworkedTransformFollower : RoleSyncedBehaviour
    {
        [Header("Локальное следование")]
        public Transform target;           // То позицию чего надо повторить
        public bool followPosition = true;
        public bool followRotation = true;

        [Header("Сетевая интерполяция")]
        [Range(1f, 60f)] public float lerpSpeed = 20f;

        [Header("Настройки синхронизации")]
        public bool useLocal = true; // Локальные координаты относительно родителя или мировые

        [Header("Специальные настройки")]
        public bool isHandProxy = false;           // руки это или нет
        public Vector3 handRotationOffsetEuler = Vector3.zero; // Оффсет для рук (Euler)

        private Vector3 networkPosition;
        private Quaternion networkRotation;

        // До первых данных из сети прокси стоит на месте, а не уезжает в начало координат родителя
        void Awake()
        {
            networkPosition = transform.localPosition;
            networkRotation = transform.localRotation;
        }

        void LateUpdate()
        {
            if (!target) return;

            if (HasAuthority)
            {
                // === Локальное следование ===
                if (followPosition)
                {
                    if (useLocal)
                        transform.localPosition = transform.parent.InverseTransformPoint(target.position);
                    else
                        transform.position = target.position;
                }

                if (followRotation)
                {
                    Quaternion targetRot = useLocal
                        ? Quaternion.Inverse(transform.parent.rotation) * target.rotation
                        : target.rotation;

                    // Оффсет применяем ТОЛЬКО у локального владельца
                    if (isHandProxy)
                        targetRot *= Quaternion.Euler(handRotationOffsetEuler);

                    transform.localRotation = targetRot;
                }
            }
            else
            {
                // === Сетевая интерполяция ===
                if (followPosition)
                    transform.localPosition = Vector3.Lerp(transform.localPosition, networkPosition, Time.deltaTime * lerpSpeed);

                if (followRotation)
                {
                    // Оффсет НЕ применяем здесь!
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, networkRotation, Time.deltaTime * lerpSpeed);
                }
            }
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
