using UnityEngine;
using Photon.Pun;

public class NetworkedTransformFollower : MonoBehaviourPun, IPunObservable
{
    [Header("Локальное следование")]
    public Transform target; // Контроллер (только локальный)
    public Transform seatAnchor; // Точка в машине, относительно которой считаем локальные координаты
    public bool followPosition = true;
    public bool followRotation = true;

    [Header("Сетевая интерполяция")]
    [Range(1f, 60f)] public float lerpSpeed = 20f;
    [Range(0.01f, 0.5f)] public float jitterSmoothing = 0.15f; // чем выше — тем мягче сглаживание мелких колебаний

    private Vector3 networkPosition;
    private Quaternion networkRotation;

    private Vector3 smoothVelocity; // используется SmoothDamp
    private Vector3 lastReceivedPosition;
    private Quaternion lastReceivedRotation;

    void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            if (!target) return;

            transform.position = target.position;
            transform.rotation = target.rotation;
        }
        else
        {
            // === Основное сглаживание позиции ===
            if (followPosition)
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    networkPosition,
                    ref smoothVelocity,
                    jitterSmoothing
                );
            }

            // === Плавное вращение ===
            if (followRotation)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    networkRotation,
                    Time.deltaTime * lerpSpeed
                );
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Передаём локальные координаты относительно машины
            Vector3 localPos = seatAnchor.InverseTransformPoint(transform.position);
            Quaternion localRot = Quaternion.Inverse(seatAnchor.rotation) * transform.rotation;

            stream.SendNext(localPos);
            stream.SendNext(localRot);
        }
        else
        {
            // Получаем локальные координаты
            Vector3 localPos = (Vector3)stream.ReceiveNext();
            Quaternion localRot = (Quaternion)stream.ReceiveNext();

            // Мягкое усреднение сетевых апдейтов (гасим мелкие скачки)
            Vector3 worldPos = seatAnchor.TransformPoint(localPos);
            Quaternion worldRot = seatAnchor.rotation * localRot;

            networkPosition = Vector3.Lerp(lastReceivedPosition, worldPos, 0.5f);
            networkRotation = Quaternion.Slerp(lastReceivedRotation, worldRot, 0.5f);

            lastReceivedPosition = networkPosition;
            lastReceivedRotation = networkRotation;
        }
    }
}