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

    [Header("����������� ��������")]
    [Range(1f, 30f)] public float positionLerpSpeed = 10f;
    [Range(1f, 30f)] public float rotationLerpSpeed = 10f;
    [Range(0f, 1f)] public float velocitySmoothing = 0.25f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (photonView.IsMine)
            return;

        // ������������ ������� � �������� � ��� ������������
        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.fixedDeltaTime * positionLerpSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.fixedDeltaTime * rotationLerpSpeed);

        // ������ ����������� ������� �������� (���� ����� ����������� ��� ������, ����� � �.�.)
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, networkVelocity, velocitySmoothing);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(rb.linearVelocity);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();
        }
    }
}
