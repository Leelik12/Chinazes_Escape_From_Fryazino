using UnityEngine;
using Photon.Pun;

public class VRHeadIK : MonoBehaviourPun, IPunObservable
{
    [Header("Ссылки")]
    public Animator animator;
    public Transform headTarget; // XR-камера
    public Transform headBone;   // Кость головы модели
    public GameObject headVisualRoot; // Меш головы

    private Vector3 networkedHeadPos;
    private Quaternion networkedHeadRot;
    private float lerpSpeed = 10f;

    private void Start()
    {
        // Скрываем голову локального игрока, чтобы не мешала XR-камере
        if (photonView.IsMine && headVisualRoot != null)
        {
            SetHeadVisible(false);
        }
    }
    private void Update()
    {
        if (photonView.IsMine && headVisualRoot != null)
        {
            SetHeadVisible(false);
        }
        else
        {
            SetHeadVisible(true);
        }
    }
    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || headBone == null)
            return;

        if (photonView.IsMine)
        {
            // Локально: ставим голову туда, где камера XR
            if (headTarget != null)
            {
                animator.SetLookAtWeight(1.0f);
                animator.SetLookAtPosition(headTarget.position + headTarget.forward * 10f);

                headBone.position = headTarget.position;
                headBone.rotation = headTarget.rotation;
            }
        }
        else
        {
            // Удалённо: плавно двигаем к полученным сетевым координатам
            headBone.position = Vector3.Lerp(headBone.position, networkedHeadPos, Time.deltaTime * lerpSpeed);
            headBone.rotation = Quaternion.Slerp(headBone.rotation, networkedHeadRot, Time.deltaTime * lerpSpeed);

            animator.SetLookAtWeight(1.0f);
            animator.SetLookAtPosition(headBone.position + headBone.forward * 10f);
        }
    }

    private void SetHeadVisible(bool visible)
    {
        if (headVisualRoot == null) return;
        var renderers = headVisualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.enabled = visible;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Отправляем позицию/вращение головы XR
            if (headTarget != null)
            {
                stream.SendNext(headTarget.position);
                stream.SendNext(headTarget.rotation);
            }
        }
        else
        {
            // Получаем позицию/вращение от другого игрока
            networkedHeadPos = (Vector3)stream.ReceiveNext();
            networkedHeadRot = (Quaternion)stream.ReceiveNext();
        }
    }
}
