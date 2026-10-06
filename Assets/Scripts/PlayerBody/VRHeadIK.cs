using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(400)]
public class VRHeadIK : MonoBehaviourPun
{
    [Header("Ссылки")]
    public Animator animator;
    public Transform headProxy;      // Прокси-точка в машине, повторяет XR камеру
    public GameObject headVisualRoot; // Меш головы

    [Header("Смещение головы")]
    public Vector3 headOffset = Vector3.zero;

    [Header("Настройки")]
    [Range(0f, 1f)]
    public float headWeight = 1f;    // влияние на голову
    [Range(0f, 1f)]
    public float bodyWeight = 0f;    // влияние на тело (0 = только голова)
    [Range(0f, 1f)]
    public float eyesWeight = 0.3f;  // глаза/шея
    [Range(0f, 1f)]
    public float clampWeight = 0.7f; // ограничение угла поворота головы

    void Start()
    {
        // Скрываем голову локального игрока
        if (photonView.IsMine && headVisualRoot != null)
            SetHeadVisible(false);
    }

    void Update()
    {
        if (photonView.IsMine && headVisualRoot != null)
            SetHeadVisible(false);
        else
            SetHeadVisible(true);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || headProxy == null) return;

        Vector3 lookPos = headProxy.position + headOffset + headProxy.forward * 10f;

        // Настройка IK
        animator.SetLookAtWeight(
            headWeight,
            bodyWeight,
            eyesWeight,
            clampWeight,
            0.5f // плавность
        );

        animator.SetLookAtPosition(lookPos);
    }

    private void SetHeadVisible(bool visible)
    {
        if (headVisualRoot == null) return;
        foreach (var r in headVisualRoot.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    // Синхронизация прокси по сети, если нужно
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(headProxy.localPosition);
            stream.SendNext(headProxy.localRotation);
        }
        else
        {
            headProxy.localPosition = (Vector3)stream.ReceiveNext();
            headProxy.localRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
