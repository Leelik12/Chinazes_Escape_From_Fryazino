using UnityEngine;
using Photon.Pun;

public class NetworkedTransformFollower : MonoBehaviourPun, IPunObservable
{
    [Header("Локальное следование")]
    public Transform target;          // Контроллер (только локальный)
    public Transform seatAnchor;      // Точка в машине, относительно которой считаем локальные координаты
    public bool followPosition = true;
    public bool followRotation = true;

    [Header("Сетевая интерполяция")]
    [Range(1f, 60f)] public float lerpSpeed = 20f;
    [Range(0f, 1f)] public float predictionFactor = 1f; // 1 = использовать пинг полностью

    private Vector3 networkPosition;
    private Quaternion networkRotation;

    private Vector3 lastNetworkPosition;
    private Vector3 velocity;

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
            // === Prediction ===
            float pingSeconds = (float)PhotonNetwork.GetPing() / 1000f;
            velocity = (networkPosition - lastNetworkPosition) / Time.fixedDeltaTime;
            Vector3 predictedPosition = networkPosition + velocity * pingSeconds * predictionFactor;

            // === Interpolation ===
            transform.position = Vector3.Lerp(transform.position, predictedPosition, Time.deltaTime * lerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * lerpSpeed);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Передаём локальные координаты
            Vector3 localPos = seatAnchor.InverseTransformPoint(transform.position);
            Quaternion localRot = Quaternion.Inverse(seatAnchor.rotation) * transform.rotation;
            stream.SendNext(localPos);
            stream.SendNext(localRot);
        }
        else
        {
            lastNetworkPosition = networkPosition;

            Vector3 localPos = (Vector3)stream.ReceiveNext();
            Quaternion localRot = (Quaternion)stream.ReceiveNext();

            networkPosition = seatAnchor.TransformPoint(localPos);
            networkRotation = seatAnchor.rotation * localRot;
        }
    }
}
