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

        [Header("Хват (необязательно)")]
        [Tooltip("Если задано, левая рука держится за эту точку вместо прокси, например за приклад пулемёта. Оси точки — оси кисти в T-позе тела")]
        public Transform leftGrip;
        [Tooltip("То же для правой руки, например пистолетная рукоять")]
        public Transform rightGrip;

        [Header("Локти")]
        [Tooltip("Куда отводить локоть от середины плеча и кисти, в осях тела: x — наружу, y — вверх, z — вперёд. Без подсказки IK при скрещённых руках выворачивает локти внутрь")]
        public Vector3 elbowHintOffset = new Vector3(0.3f, -0.3f, -0.1f);
        [Range(0f, 1f)] public float elbowHintWeight = 1f;

        // Прокси-точки синхронизируются по сети, поэтому для своего и чужого аватара IK одинаковый
        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            if (leftGrip != null) SetHandIK(AvatarIKGoal.LeftHand, leftGrip, Vector3.zero);
            else SetHandIK(AvatarIKGoal.LeftHand, proxyLeft, leftOffset);
            if (rightGrip != null) SetHandIK(AvatarIKGoal.RightHand, rightGrip, Vector3.zero);
            else SetHandIK(AvatarIKGoal.RightHand, proxyRight, rightOffset);

            SetElbowHint(AvatarIKHint.LeftElbow, HumanBodyBones.LeftUpperArm, AvatarIKGoal.LeftHand, -1f);
            SetElbowHint(AvatarIKHint.RightElbow, HumanBodyBones.RightUpperArm, AvatarIKGoal.RightHand, 1f);
        }

        private void SetHandIK(AvatarIKGoal goal, Transform proxy, Vector3 offset)
        {
            if (proxy == null) return;
            animator.SetIKPositionWeight(goal, 1);
            animator.SetIKRotationWeight(goal, 1);
            animator.SetIKPosition(goal, proxy.TransformPoint(offset));
            animator.SetIKRotation(goal, proxy.rotation);
        }

        // Подсказка ставится между плечом и кистью и отводится наружу и вниз, поэтому локоть
        // остаётся снаружи, куда бы ни ушла кисть: при повороте руля и наведении пулемёта
        private void SetElbowHint(AvatarIKHint hint, HumanBodyBones upperArm, AvatarIKGoal hand, float side)
        {
            Transform shoulder = animator.GetBoneTransform(upperArm);
            if (shoulder == null || elbowHintWeight <= 0f) return;

            Vector3 middle = (shoulder.position + animator.GetIKPosition(hand)) / 2f;
            Vector3 offset = new Vector3(elbowHintOffset.x * side, elbowHintOffset.y, elbowHintOffset.z);
            animator.SetIKHintPositionWeight(hint, elbowHintWeight);
            animator.SetIKHintPosition(hint, middle + animator.transform.TransformVector(offset));
        }
    }
}
