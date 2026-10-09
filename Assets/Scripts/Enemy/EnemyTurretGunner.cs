using UnityEngine;

namespace RacingProject.Enemy
{
    // Стрелок пулемётной турели на крыше врага — та же модель, что у игроков, стоит в люке. EnemyGun наводит пулемёт
    // свободным поворотом, а этот компонент после него сажает пулемёт на станок: станок (и стрелок вместе с ним)
    // поворачивается по кольцу люка вслед за пулемётом, пулемёт стоит на вилке станка впереди стрелка, наклон ствола
    // ограничен. Руки (IK) держат рукоять и ствольную коробку, голова смотрит туда же, куда ствол. Поза пулемёта
    // синхронизирована EnemyGun, поэтому у всех игроков картинка одинаковая; по сети отсюда ничего не идёт
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Animator))]
    public class EnemyTurretGunner : MonoBehaviour
    {
        [Tooltip("Пулемёт с EnemyGun")]
        [SerializeField] private Transform gun;
        [Tooltip("Центр кольца люка на высоте оси наклона пулемёта, ось Y — вверх от крыши")]
        [SerializeField] private Transform turret;
        [Tooltip("Поворотный станок (дочерний turret): крутится по рысканью")]
        [SerializeField] private Transform mount;
        [Tooltip("Где ось пулемёта относительно центра кольца, в осях turret до поворота")]
        [SerializeField] private Vector3 mountOffset = new Vector3(0f, 0f, 0.42f);
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 20f;

        [Header("Руки")]
        [Tooltip("Точки хвата (дочерние пулемёта); оси — оси кисти в T-позе тела")]
        [SerializeField] private Transform leftGrip;
        [SerializeField] private Transform rightGrip;
        [Tooltip("Куда отводить локоть от середины плеча и кисти, в осях тела: x — наружу, y — вверх, z — вперёд")]
        [SerializeField] private Vector3 elbowHintOffset = new Vector3(0.35f, -0.3f, -0.1f);
        [SerializeField, Range(0f, 1f)] private float lookWeight = 0.9f;

        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        // В Update, а не в LateUpdate: IK считается в анимации, которая идёт между ними, и руки должны попасть
        // на пулемёт в его позе этого кадра
        private void Update()
        {
            if (gun == null || turret == null) return;

            Vector3 aim = turret.InverseTransformDirection(gun.forward);
            float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);

            if (mount != null)
                mount.localRotation = yawRotation;
            gun.SetPositionAndRotation(turret.TransformPoint(yawRotation * mountOffset),
                turret.rotation * Quaternion.Euler(-pitch, yaw, 0f));
        }

        private void OnAnimatorIK(int layerIndex)
        {
            SetHand(AvatarIKGoal.LeftHand, leftGrip);
            SetHand(AvatarIKGoal.RightHand, rightGrip);
            SetElbow(AvatarIKHint.LeftElbow, HumanBodyBones.LeftUpperArm, AvatarIKGoal.LeftHand, -1f);
            SetElbow(AvatarIKHint.RightElbow, HumanBodyBones.RightUpperArm, AvatarIKGoal.RightHand, 1f);

            if (gun != null)
            {
                animator.SetLookAtWeight(lookWeight, 0.2f, 0.9f, 1f, 0.6f);
                animator.SetLookAtPosition(gun.position + gun.forward * 30f);
            }
        }

        private void SetHand(AvatarIKGoal goal, Transform grip)
        {
            if (grip == null) return;
            animator.SetIKPositionWeight(goal, 1f);
            animator.SetIKRotationWeight(goal, 1f);
            animator.SetIKPosition(goal, grip.position);
            animator.SetIKRotation(goal, grip.rotation);
        }

        // Локоть отводится наружу и вниз от середины плеча и кисти, иначе IK выворачивает его внутрь
        private void SetElbow(AvatarIKHint hint, HumanBodyBones upperArm, AvatarIKGoal hand, float side)
        {
            Transform shoulder = animator.GetBoneTransform(upperArm);
            if (shoulder == null) return;
            Vector3 middle = (shoulder.position + animator.GetIKPosition(hand)) / 2f;
            Vector3 offset = new Vector3(elbowHintOffset.x * side, elbowHintOffset.y, elbowHintOffset.z);
            animator.SetIKHintPositionWeight(hint, 1f);
            animator.SetIKHintPosition(hint, middle + transform.TransformVector(offset));
        }
    }
}
