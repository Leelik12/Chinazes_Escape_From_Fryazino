using UnityEngine;

namespace RacingProject.Enemy
{
    // Гранатомётчик в салоне машины врага — та же модель, что у игроков. Стоит у боковых окон и высовывается
    // по пояс в окно с той стороны, где машина игроков; гранатомёт лежит у него на правом плече. Когда цель уходит
    // на другую сторону, он прячется в салон, перебирается к другому окну и высовывается там. Пока он не высунулся
    // целиком, гранатомёт не стреляет (EnemyRocketLauncher.HoldFire), а наведение ограничено сектором окна:
    // EnemyRocketLauncher крутит гранатомёт свободно, а этот компонент после него зажимает поворот в сектор,
    // поворачивает тело наполовину вслед за стволом и ставит гранатомёт на плечо. Сторона и наклон считаются
    // у каждого игрока из положения цели и синхронизированного поворота гранатомёта, по сети отсюда ничего не идёт
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Animator))]
    public class EnemyWindowGunner : MonoBehaviour
    {
        [SerializeField] private EnemyRocketLauncher launcher;
        [Tooltip("Корень машины: от него считаются стороны и сектор окна")]
        [SerializeField] private Transform car;

        [Header("Окна")]
        [Tooltip("Таз стрелка в салоне посередине между окнами, в осях машины (x — в сторону окна)")]
        [SerializeField] private Vector3 hipsCenter = new Vector3(0f, 2.45f, -0.2f);
        [Tooltip("Насколько таз отходит к окну, когда стрелок спрятан и когда высунулся, м")]
        [SerializeField] private float insideOffset = 0.55f;
        [SerializeField] private float outsideOffset = 1.45f;
        [Tooltip("Наклон корпуса вперёд, к окну, градусы")]
        [SerializeField] private float leanPitch = 40f;
        [Tooltip("Сектор окна: насколько ствол отклоняется от перпендикуляра к борту, градусы")]
        [SerializeField] private float aimArc = 80f;
        [SerializeField] private float maxPitchUp = 15f;
        [SerializeField] private float maxPitchDown = 20f;
        [Tooltip("Доля поворота ствола, на которую поворачивается тело")]
        [SerializeField, Range(0f, 1f)] private float bodyFollow = 0.5f;
        [Tooltip("Сколько секунд высовываться или прятаться")]
        [SerializeField] private float leanTime = 0.7f;
        [Tooltip("Сколько секунд перебираться к другому окну")]
        [SerializeField] private float crossTime = 1.2f;
        [Tooltip("Цель ближе этого угла к курсу или к корме не заставляет менять окно, градусы")]
        [SerializeField] private float switchDeadZone = 10f;

        [Header("Гранатомёт на плече")]
        [Tooltip("Точка трубы, которая лежит на плече, в осях гранатомёта")]
        [SerializeField] private Vector3 shoulderRest = new Vector3(0f, 0.02f, -0.17f);
        // Постоянная, а не кость: когда стрелка не видно, аниматор не обновляет кости, и гранатомёт с точкой вылета
        // гранаты уехал бы вслед за позой покоя
        [Tooltip("Правый плечевой сустав в позе стрелка, в осях модели (без масштаба)")]
        [SerializeField] private Vector3 rightShoulder = new Vector3(0.205f, 0.393f, -0.186f);
        [Tooltip("Где лежит труба относительно правого плечевого сустава, в осях тела: x — вправо, y — вверх, z — вперёд")]
        [SerializeField] private Vector3 tubeOverShoulder = new Vector3(-0.06f, 0.17f, 0f);

        [Header("Руки")]
        [Tooltip("Точки хвата (дочерние гранатомёта); оси — оси кисти в T-позе тела")]
        [SerializeField] private Transform leftGrip;
        [SerializeField] private Transform rightGrip;
        [SerializeField] private Vector3 elbowHintOffset = new Vector3(0.35f, -0.3f, 0f);

        private Animator animator;
        private Transform target;
        private int side = 1;
        private float cross = 1f;
        private float lean;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (launcher == null || car == null) return;
            if (target == null)
                target = launcher.target;

            // Сторона, где цель; у носа и кормы окно не меняется
            int want = side;
            if (target != null)
            {
                Vector3 toTarget = car.InverseTransformDirection(target.position - car.position);
                float targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(targetYaw) > switchDeadZone && Mathf.Abs(targetYaw) < 180f - switchDeadZone)
                    want = targetYaw > 0f ? 1 : -1;
            }
            if (want != side && lean <= 0f)
                side = want;
            if (lean <= 0f)
                cross = Mathf.MoveTowards(cross, side, 2f / crossTime * Time.deltaTime);
            bool atWindow = Mathf.Approximately(cross, side);
            lean = Mathf.MoveTowards(lean, want == side && atWindow ? 1f : 0f, Time.deltaTime / leanTime);
            float smoothLean = Mathf.SmoothStep(0f, 1f, lean);
            launcher.HoldFire = lean < 1f;

            // Наведение в секторе окна
            Vector3 aim = car.InverseTransformDirection(launcher.transform.forward);
            float aimYaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            float aimPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg, -maxPitchDown, maxPitchUp);
            float normal = 90f * side;
            float sectorYaw = Mathf.Clamp(aimYaw, normal - aimArc, normal + aimArc);
            float restYaw = 90f * cross;
            float yaw = Mathf.Lerp(restYaw, sectorYaw, smoothLean);
            float pitch = aimPitch * smoothLean;
            float bodyYaw = Mathf.Lerp(restYaw, normal + (sectorYaw - normal) * bodyFollow, smoothLean);

            // Тело: таз у окна, корпус наклонён к окну
            float offset = Mathf.Lerp(insideOffset, outsideOffset, smoothLean);
            Vector3 hips = hipsCenter + Vector3.right * (cross * offset);
            transform.SetPositionAndRotation(car.TransformPoint(hips),
                car.rotation * Quaternion.Euler(0f, bodyYaw, 0f) * Quaternion.Euler(leanPitch, 0f, 0f));

            // Гранатомёт: поворот из сектора, труба на правом плече
            Quaternion launcherRotation = car.rotation * Quaternion.Euler(-pitch, yaw, 0f);
            Vector3 shoulder = transform.TransformPoint(rightShoulder);
            Vector3 rest = shoulder + transform.rotation * Quaternion.Inverse(Quaternion.Euler(leanPitch, 0f, 0f)) * tubeOverShoulder;
            launcher.transform.SetPositionAndRotation(rest - launcherRotation * shoulderRest, launcherRotation);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            SetHand(AvatarIKGoal.LeftHand, leftGrip);
            SetHand(AvatarIKGoal.RightHand, rightGrip);
            SetElbow(AvatarIKHint.LeftElbow, HumanBodyBones.LeftUpperArm, AvatarIKGoal.LeftHand, -1f);
            SetElbow(AvatarIKHint.RightElbow, HumanBodyBones.RightUpperArm, AvatarIKGoal.RightHand, 1f);

            if (launcher != null)
            {
                animator.SetLookAtWeight(1f, 0.3f, 1f, 1f, 0.5f);
                animator.SetLookAtPosition(launcher.transform.position + launcher.transform.forward * 30f);
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
        private void SetElbow(AvatarIKHint hint, HumanBodyBones upperArm, AvatarIKGoal hand, float sideSign)
        {
            Transform shoulderBone = animator.GetBoneTransform(upperArm);
            if (shoulderBone == null) return;
            Vector3 middle = (shoulderBone.position + animator.GetIKPosition(hand)) / 2f;
            Vector3 offset = new Vector3(elbowHintOffset.x * sideSign, elbowHintOffset.y, elbowHintOffset.z);
            animator.SetIKHintPositionWeight(hint, 1f);
            animator.SetIKHintPosition(hint, middle + transform.TransformVector(offset));
        }
    }
}
