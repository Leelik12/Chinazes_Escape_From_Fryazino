using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(100)]
public class NetworkedTransformFollower : MonoBehaviourPun, IPunObservable
{
    [Header("Локальное следование")]
    public Transform target;
    public bool followPosition = true;
    public bool followRotation = true;

    [Header("Сетевая интерполяция")]
    [Range(1f, 60f)] public float lerpSpeed = 20f;

    private Vector3 networkPosition;
    private Quaternion networkRotation;

    void LateUpdate()
    {
        if (photonView.IsMine)
        {
            if (!target) return;

            if (followPosition)
                transform.position = target.position;
            if (followRotation)
                transform.rotation = target.rotation;
        }
        else
        {
            if (followPosition)
                transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * lerpSpeed);
            if (followRotation)
                transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * lerpSpeed);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
