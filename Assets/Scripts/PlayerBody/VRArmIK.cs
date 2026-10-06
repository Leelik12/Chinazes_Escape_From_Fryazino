using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(400)]
public class VRArmIK : MonoBehaviourPun
{
    [Header("Ссылки")]
    public Animator animator;

    [Header("Прокси точки")]
    public Transform proxyLeft;
    public Transform proxyRight;

    [Header("Настройки смещения IK")]
    public Vector3 leftOffset = Vector3.zero;   // смещение относительно прокси для левой руки
    public Vector3 rightOffset = Vector3.zero;  // смещение относительно прокси для правой руки

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        // Локальный игрок
        if (photonView.IsMine)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, proxyLeft.TransformPoint(leftOffset));
            animator.SetIKRotation(AvatarIKGoal.LeftHand, proxyLeft.rotation);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.RightHand, proxyRight.TransformPoint(rightOffset));
            animator.SetIKRotation(AvatarIKGoal.RightHand, proxyRight.rotation);
        }
        else
        {
            // Другие игроки — используем прокси точки без изменений
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, proxyLeft.TransformPoint(leftOffset));
            animator.SetIKRotation(AvatarIKGoal.LeftHand, proxyLeft.rotation);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.RightHand, proxyRight.TransformPoint(rightOffset));
            animator.SetIKRotation(AvatarIKGoal.RightHand, proxyRight.rotation);
        }
    }
}
