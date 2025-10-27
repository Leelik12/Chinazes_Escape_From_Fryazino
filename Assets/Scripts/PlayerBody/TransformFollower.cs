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

    [Header("Настройки синхронизации")]
    public bool useLocal = false; // аналог галочки Use Local

    private Vector3 networkPosition;
    private Quaternion networkRotation;

    void LateUpdate()
    {
        if (photonView.IsMine)
        {
            if (!target) return;

            if (followPosition)
            {
                if (useLocal)
                    transform.localPosition = target.localPosition;
                else
                    transform.position = target.position;
            }

            if (followRotation)
            {
                if (useLocal)
                    transform.localRotation = target.localRotation;
                else
                    transform.rotation = target.rotation;
            }
        }
        else
        {
            if (followPosition)
            {
                if (useLocal)
                    transform.localPosition = Vector3.Lerp(transform.localPosition, networkPosition, Time.deltaTime * lerpSpeed);
                else
                    transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * lerpSpeed);
            }

            if (followRotation)
            {
                if (useLocal)
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, networkRotation, Time.deltaTime * lerpSpeed);
                else
                    transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * lerpSpeed);
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            if (useLocal)
            {
                stream.SendNext(transform.localPosition);
                stream.SendNext(transform.localRotation);
            }
            else
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);
            }
        }
        else
        {
            if (useLocal)
            {
                networkPosition = (Vector3)stream.ReceiveNext();
                networkRotation = (Quaternion)stream.ReceiveNext();
            }
            else
            {
                networkPosition = (Vector3)stream.ReceiveNext();
                networkRotation = (Quaternion)stream.ReceiveNext();
            }
        }
    }
}
