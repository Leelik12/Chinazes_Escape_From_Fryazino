using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarNetworkSync : MonoBehaviourPun, IPunObservable
{
    private Rigidbody rb;

    private Vector3 receivedVelocity;
    private Vector3 receivedAngularVelocity;

    public Vector3 NetworkVelocity => receivedVelocity;
    public Vector3 NetworkAngularVelocity => receivedAngularVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(rb.linearVelocity);
            stream.SendNext(rb.angularVelocity);
        }
        else
        {
            receivedVelocity = (Vector3)stream.ReceiveNext();
            receivedAngularVelocity = (Vector3)stream.ReceiveNext();
        }
    }
}
