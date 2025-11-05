using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(100)]
public class NetworkedTransformFollower : MonoBehaviourPun, IPunObservable
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

    void LateUpdate()
    {
        if (!target) return;

        if (photonView.IsMine)
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

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.localPosition);
            stream.SendNext(transform.localRotation);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
