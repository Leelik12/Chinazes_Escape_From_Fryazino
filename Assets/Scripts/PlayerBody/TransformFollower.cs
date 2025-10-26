using UnityEngine;
using Photon.Pun;

public class NetworkedTransformFollower : MonoBehaviourPun, IPunObservable
{
    [Header("Локальное следование")]
    public Transform target;          // Контроллер (только для локального игрока)
    public bool followPosition = true;
    public bool followRotation = true;

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
            // Отправляем свои координаты
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            // Получаем чужие координаты
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
