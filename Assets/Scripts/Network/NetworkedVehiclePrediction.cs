using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class NetworkedVehiclePrediction : MonoBehaviourPun, IPunObservable
{
    private Rigidbody rb;
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private Vector3 networkVelocity;
    private Vector3 smoothedVelocity;
    private float lastReceivedTime;

    [Header("Настройки предсказания")]
    [Range(0f, 2f)] public float predictionMultiplier = 1.0f;
    [Range(0f, 1f)] public float velocitySmoothing = 0.3f; // коэффициент сглаживания скорости

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (photonView.IsMine)
            return;

        // Интерполяция позиции и поворота
        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.fixedDeltaTime * 10f);
        transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.fixedDeltaTime * 10f);

        // Мягкое сглаживание сетевой скорости
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, networkVelocity, velocitySmoothing);

        // Простое предсказание вперёд по сглаженной скорости
        float pingTime = PhotonNetwork.GetPing() / 1000f;
        transform.position += smoothedVelocity * pingTime * predictionMultiplier;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(rb.velocity);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();
            lastReceivedTime = (float)info.SentServerTime;
        }
    }
}
