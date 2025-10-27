using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(400)]
public class VRArmIK : MonoBehaviourPun
{
    [Header("—сылки")]
    public Animator animator;

    // Ёти точки должны быть те же, что синхронизируютс€ через NetworkedTransformFollower
    public Transform proxyLeft;
    public Transform proxyRight;

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        // Ћокальный игрок Ч напр€мую контроллеры
        if (photonView.IsMine)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, proxyLeft.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, proxyLeft.rotation);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.RightHand, proxyRight.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, proxyRight.rotation);
        }
        else
        {
            // ƒругие игроки Ч используем прокси точки, которые уже сглажены
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, proxyLeft.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, proxyLeft.rotation);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.RightHand, proxyRight.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, proxyRight.rotation);
        }
    }
}
