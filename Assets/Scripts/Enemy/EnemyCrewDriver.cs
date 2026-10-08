using UnityEngine;

namespace RacingProject.Enemy
{
    // Водитель машины врага — та же модель, что у игроков. Сидит в позе из EnemySeated.anim, руки (IK) держат
    // руль за обод. Руль у моделей врагов запечён в кузов, поэтому точки хвата лежат на оси руля (wheelPivot)
    // и поворачиваются по ободу вслед за передними колёсами: руки крутят руль. Голова следит за машиной игроков,
    // когда та рядом и не за спиной. Это только картинка: у каждого игрока считается своя, по сети ничего не идёт
    [RequireComponent(typeof(Animator))]
    public class EnemyCrewDriver : MonoBehaviour
    {
        [SerializeField] private EnemyCarController car;
        [Tooltip("Ось руля: начало в центре обода, ось Z — нормаль плоскости руля к водителю")]
        [SerializeField] private Transform wheelPivot;
        [Tooltip("Точки хвата на ободе (дочерние wheelPivot); оси — оси кисти в T-позе тела")]
        [SerializeField] private Transform leftGrip;
        [SerializeField] private Transform rightGrip;
        [Tooltip("Насколько поворачиваются руки по ободу при полном повороте колёс, градусы")]
        [SerializeField] private float wheelLockAngle = 55f;
        [SerializeField] private float steerSmoothing = 8f;

        [Header("Локти")]
        [Tooltip("Куда отводить локоть от середины плеча и кисти, в осях тела: x — наружу, y — вверх, z — вперёд")]
        [SerializeField] private Vector3 elbowHintOffset = new Vector3(0.3f, -0.35f, -0.05f);

        [Header("Взгляд")]
        [Tooltip("Ближе этого водитель поглядывает на машину игроков, м")]
        [SerializeField] private float lookDistance = 70f;
        [Tooltip("Дальше этого угла от курса машины голову не поворачивает, градусы")]
        [SerializeField] private float maxLookAngle = 110f;
        [SerializeField, Range(0f, 1f)] private float lookWeight = 0.8f;

        private Animator animator;
        private Quaternion pivotRest;
        private float wheelAngle;
        private Transform target;
        private float lookBlend;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (car == null)
                car = GetComponentInParent<EnemyCarController>();
            if (wheelPivot != null)
                pivotRest = wheelPivot.localRotation;
        }

        private void Update()
        {
            float steer = car != null && car.maxSteerAngle > 0f ? Mathf.Clamp(car.SteerAngle / car.maxSteerAngle, -1f, 1f) : 0f;
            wheelAngle = Mathf.Lerp(wheelAngle, steer * wheelLockAngle, 1f - Mathf.Exp(-steerSmoothing * Time.deltaTime));
            // Поворот вправо (steer > 0) — по часовой стрелке со стороны водителя (ось Z руля смотрит на него)
            if (wheelPivot != null)
                wheelPivot.localRotation = pivotRest * Quaternion.AngleAxis(wheelAngle, Vector3.forward);

            if (target == null)
            {
                GameObject playerCar = GameObject.FindWithTag(Tags.Car);
                if (playerCar != null) target = playerCar.transform;
            }
            bool look = false;
            if (target != null && car != null)
            {
                Vector3 toTarget = target.position - transform.position;
                look = toTarget.sqrMagnitude < lookDistance * lookDistance
                    && Vector3.Angle(car.transform.forward, toTarget) < maxLookAngle;
            }
            lookBlend = Mathf.MoveTowards(lookBlend, look ? 1f : 0f, Time.deltaTime * 1.5f);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            SetHand(AvatarIKGoal.LeftHand, leftGrip);
            SetHand(AvatarIKGoal.RightHand, rightGrip);
            SetElbow(AvatarIKHint.LeftElbow, HumanBodyBones.LeftUpperArm, AvatarIKGoal.LeftHand, -1f);
            SetElbow(AvatarIKHint.RightElbow, HumanBodyBones.RightUpperArm, AvatarIKGoal.RightHand, 1f);

            if (target != null && lookBlend > 0f)
            {
                animator.SetLookAtWeight(lookWeight * Mathf.SmoothStep(0f, 1f, lookBlend), 0.1f, 0.8f, 1f, 0.6f);
                animator.SetLookAtPosition(target.position + Vector3.up * 1.5f);
            }
            else
            {
                animator.SetLookAtWeight(0f);
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
