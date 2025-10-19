using UnityEngine;

public class VRArmIK : MonoBehaviour
{
    public Animator animator;
    public Transform leftTarget;
    public Transform rightTarget;

    void OnAnimatorIK(int layerIndex)
    {
        Debug.Log("Руки проследовали за контроллером1");
        if (animator == null) return;
        Debug.Log("Руки проследовали за контроллером2");
        // Left hand
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, leftTarget.position);
        animator.SetIKRotation(AvatarIKGoal.LeftHand, leftTarget.rotation);

        // Right hand
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
        animator.SetIKPosition(AvatarIKGoal.RightHand, rightTarget.position);
        animator.SetIKRotation(AvatarIKGoal.RightHand, rightTarget.rotation);
        Debug.Log("Руки проследовали за контроллером3");
    }
}
