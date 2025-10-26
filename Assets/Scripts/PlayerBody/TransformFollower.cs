using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(100)]
public class NetworkedTransformFollower : MonoBehaviourPun, IPunObservable
{
    [Header("Локальное следование")]
    public Transform target; // Контроллер (только локальный)
    public bool followPosition = true;
    public bool followRotation = true;

    [Header("Сетевая интерполяция")]
    [Range(1f, 60f)] public float lerpSpeed = 20f;
    [Range(0.01f, 0.5f)] public float jitterSmoothing = 0.15f; // сглаживание дрожания

    private Vector3 networkPosition;
    private Quaternion networkRotation;

    private Vector3 smoothVelocity; // для SmoothDamp

    void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        if (!target) return;

        // локальный игрок — полностью следуем за контроллером
        transform.position = target.position;
        transform.rotation = target.rotation;
    }

    void LateUpdate()
    {
        if (photonView.IsMine) return;

        // === Интерполяция позиции ===
        if (followPosition)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                networkPosition,
                ref smoothVelocity,
                jitterSmoothing
            );
        }

        // === Интерполяция вращения ===
        if (followRotation)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                networkRotation,
                Time.deltaTime * lerpSpeed
            );
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Отправляем абсолютные мировые координаты
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            // Получаем абсолютные мировые координаты
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
