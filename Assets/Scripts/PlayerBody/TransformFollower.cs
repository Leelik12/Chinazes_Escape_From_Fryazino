using UnityEngine;
using Photon.Pun;

public class NetworkedTransformFollower : MonoBehaviourPun, IPunObservable
{
    [Header("Локальное следование")]
    public Transform target;          // Контроллер (только для локального игрока)
    public bool followPosition = true;
    public bool followRotation = true;

    public Transform seatAnchor; // Точка в машине, к которой привязана прокси

    private Vector3 positionOffset;
    private Quaternion rotationOffset;

    // Для сетевой синхронизации
    private Vector3 networkPosition;
    private Quaternion networkRotation;

    [Header("Сетевая интерполяция")]
    [Range(1f, 60f)] public float lerpSpeed = 30f;

    void Start()
    {
        if (target != null)
        {
            positionOffset = transform.position - target.position;
            rotationOffset = Quaternion.Inverse(target.rotation) * transform.rotation;
        }
    }

    void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            // Локальный игрок — движем по target
            if (!target) return;

            if (followPosition)
                transform.position = target.position + target.rotation * positionOffset;

            if (followRotation)
                transform.rotation = target.rotation * rotationOffset;
        }
        else
        {
            // Остальные игроки — интерполяция полученных сетевых данных
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * lerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * lerpSpeed);
        }
    }

    void OnBeforeRender()
    {
        if (!photonView.IsMine || target == null) return;

        if (followPosition)
            transform.position = target.position + target.rotation * positionOffset;

        if (followRotation)
            transform.rotation = target.rotation * rotationOffset;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            Vector3 localPos = seatAnchor.InverseTransformPoint(transform.position);
            Quaternion localRot = Quaternion.Inverse(seatAnchor.rotation) * transform.rotation;
            stream.SendNext(localPos);
            stream.SendNext(localRot);
        }
        else
        {
            Vector3 localPos = (Vector3)stream.ReceiveNext();
            Quaternion localRot = (Quaternion)stream.ReceiveNext();
            networkPosition = seatAnchor.TransformPoint(localPos);
            networkRotation = seatAnchor.rotation * localRot;
        }
    }
}
