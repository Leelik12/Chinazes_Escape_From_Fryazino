using UnityEngine;
using Photon.Pun;

public class VRHeadIK : MonoBehaviourPun, IPunObservable
{
    [Header("Ссылки")]
    public Animator animator;
    public Transform headTarget;     // XR-камера
    public GameObject headVisualRoot; // Меш головы
    [Header("Смещение головы")]
    public Vector3 headOffset = Vector3.zero;

    [Header("Настройки")]
    [Tooltip("Насколько сильно поворачивается только голова (0.7–1 оптимально)")]
    [Range(0f, 1f)] public float headWeight = 1f;

    [Tooltip("Насколько сильно вовлекается тело (0 = только голова)")]
    [Range(0f, 1f)] public float bodyWeight = 0f;

    [Tooltip("Насколько сильно вовлекается глаза/шея")]
    [Range(0f, 1f)] public float eyesWeight = 0.3f;

    [Tooltip("Насколько плавно удерживать поворот")]
    [Range(0f, 1f)] public float clampWeight = 0.7f;
    // Сетевые данные
    private Vector3 networkHeadPos;
    private Quaternion networkHeadRot;
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
    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        Vector3 lookPos;

        if (photonView.IsMine)
        {
            lookPos = headTarget.position + headTarget.forward * 10f;
        }
        else
        {
            lookPos = networkHeadPos + networkHeadRot * Vector3.forward * 10f;
        }

        // Настраиваем IK головы без вращения туловища
        animator.SetLookAtWeight(
            headWeight, // общее влияние
            bodyWeight, // тело (0 — не двигается)
            eyesWeight, // глаза/шея
            clampWeight, // ограничение угла
            0.5f // плавность
        );

        animator.SetLookAtPosition(lookPos);
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
            stream.SendNext(headTarget.position);
            stream.SendNext(headTarget.rotation);
        }
        else
        {
            networkHeadPos = (Vector3)stream.ReceiveNext();
            networkHeadRot = (Quaternion)stream.ReceiveNext();
        }
    }
}
