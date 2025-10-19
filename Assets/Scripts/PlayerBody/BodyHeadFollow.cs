using Photon.Pun;
using UnityEngine;

public class BodyHeadFollow : MonoBehaviourPun
{
    public Transform headTarget; // Camera Transform
    public Transform headBone;   // Головной костяк модели

    [Header("Renderer")]
    public SkinnedMeshRenderer bodyRenderer; // SkinnedMeshRenderer всего тела
    void Start()
    {
        if (photonView.IsMine && bodyRenderer != null)
        {
            // Локальному игроку скрываем голову
            bodyRenderer.enabled = true; // тело видно
            headBone.gameObject.SetActive(false);
            Debug.Log("djfbhlkdg");
        }
    }
    void LateUpdate()
    {
        if (!headTarget || !headBone) return;

        if (!photonView.IsMine)
        {
            // Удалённые игроки: голова следит за камерой игрока
            headBone.position = headTarget.position;
            headBone.rotation = headTarget.rotation;
        }
    }
}
