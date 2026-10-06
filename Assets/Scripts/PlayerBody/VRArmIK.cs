using UnityEngine;

namespace RacingProject.PlayerBody
{
    [DefaultExecutionOrder(400)]
    public class VRArmIK : MonoBehaviour
    {
        [Header("Ссылки")]
        public Animator animator;

        [Header("Прокси точки")]
        public Transform proxyLeft;
        public Transform proxyRight;

        [Header("Настройки смещения IK")]
        public Vector3 leftOffset = Vector3.zero;   // смещение относительно прокси для левой руки
        public Vector3 rightOffset = Vector3.zero;  // смещение относительно прокси для правой руки

        // Прокси-точки синхронизируются по сети, поэтому для своего и чужого аватара IK одинаковый
        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            SetHandIK(AvatarIKGoal.LeftHand, proxyLeft, leftOffset);
            SetHandIK(AvatarIKGoal.RightHand, proxyRight, rightOffset);
        }

        private void SetHandIK(AvatarIKGoal goal, Transform proxy, Vector3 offset)
        {
            animator.SetIKPositionWeight(goal, 1);
            animator.SetIKRotationWeight(goal, 1);
            animator.SetIKPosition(goal, proxy.TransformPoint(offset));
            animator.SetIKRotation(goal, proxy.rotation);
        }
    }
}
