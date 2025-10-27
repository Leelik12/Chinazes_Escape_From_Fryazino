using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(100)]
public class NetworkedTransformFollower : MonoBehaviourPun, IPunObservable
{
    [Header("Локальное следование")]
    public Transform target;           // контроллер XR Rig
    public bool followPosition = true;
    public bool followRotation = true;

    [Header("Сетевая интерполяция")]
    [Range(1f, 60f)] public float lerpSpeed = 20f;

    [Header("Настройки синхронизации")]
    public bool useLocal = true; // мы будем работать в локальных координатах машины

    private Vector3 networkPosition;
    private Quaternion networkRotation;

    void LateUpdate()
    {
        if (!target) return;

        if (photonView.IsMine)
        {
            // Конвертируем локальные координаты контроллера в локальные координаты машины
            if (followPosition)
                transform.localPosition = transform.parent.InverseTransformPoint(target.position);
            if (followRotation)
                transform.localRotation = Quaternion.Inverse(transform.parent.rotation) * target.rotation;
        }
        else
        {
            // Интерполяция сетевых данных
            if (followPosition)
                transform.localPosition = Vector3.Lerp(transform.localPosition, networkPosition, Time.deltaTime * lerpSpeed);
            if (followRotation)
                transform.localRotation = Quaternion.Slerp(transform.localRotation, networkRotation, Time.deltaTime * lerpSpeed);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Отправляем локальные координаты прокси (относительно машины)
            stream.SendNext(transform.localPosition);
            stream.SendNext(transform.localRotation);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
