using UnityEngine;
using Photon.Pun;

public class VRHeadFollowAndHide : MonoBehaviourPun, IPunObservable
{
    [Header("Ссылки на объекты")]
    [SerializeField] private Transform headTarget; // XR-камера
    [SerializeField] private Transform headBone; // Кость головы модели
    [SerializeField] private GameObject headVisualRoot; // Голова (мэш, очки и т.д.)
    [Header("Настройки позиционирования")]
    [SerializeField] private Vector3 headOffset = new Vector3(0f, 0f, 0f);

    private Vector3 networkedPosition;
    private Quaternion networkedRotation;
    private float lerpSpeed = 10f;

    private void Start()
    {
        if (!PhotonNetwork.IsMasterClient && headVisualRoot != null)
            SetHeadVisible(false); // скрываем голову у локального игрока
    }

    private void LateUpdate()
    {
        if (headBone == null)
            return;
        if (PhotonNetwork.IsMasterClient)
        {
            SetHeadVisible(true);
        }
        if (photonView.IsMine)
        {
            // Локальный игрок — двигаем по XR-камере
            if (headTarget != null)
            {
                headBone.position = headTarget.TransformPoint(headOffset);
                headBone.rotation = headTarget.rotation;
            }
        }
        else
        {
            // Удалённые игроки — плавно интерполируем сетевые данные
            headBone.position = Vector3.Lerp(headBone.position, networkedPosition, Time.deltaTime * lerpSpeed);
            headBone.rotation = Quaternion.Slerp(headBone.rotation, networkedRotation, Time.deltaTime * lerpSpeed);
        }
    }

    private void SetHeadVisible(bool visible)
    {
        var renderers = headVisualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.enabled = visible;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Передаём или принимаем позицию и вращение головы
        if (stream.IsWriting)
        {
            if (headBone != null)
            {
                stream.SendNext(headBone.position);
                stream.SendNext(headBone.rotation);
            }
        }
        else
        {
            networkedPosition = (Vector3)stream.ReceiveNext();
            networkedRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
