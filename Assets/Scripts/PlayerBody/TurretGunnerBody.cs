using UnityEngine;

namespace RacingProject.PlayerBody
{
    // Тело стрелка в люке поворачивается вместе с пулемётом и наклоняется к нему: кисти держат пулемёт
    // (хват у VRArmIK), и без поворота при наведении вбок руки перекручивались бы через грудь.
    // Рысканье берётся из позы пулемёта, а её VRGun передаёт по сети, поэтому оба игрока видят одно и то же
    [RequireComponent(typeof(Animator))]
    public class TurretGunnerBody : MonoBehaviour
    {
        [Tooltip("Пулемёт на кольце люка (объект с TurretGunAim): его локальный поворот по Y — рысканье станка")]
        [SerializeField] private Transform gun;
        [Tooltip("Наклон тела вперёд к пулемёту вокруг таза, градусы: так кисти дотягиваются до рукояти")]
        [SerializeField] private float lean = 12f;

        private Animator animator;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Vector3 pivot;
        private Vector3 hips;
        private bool hipsKnown;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            hips = basePosition;
            // Тело вращается вокруг вертикали через центр кольца люка, на котором ходит пулемёт
            pivot = gun != null ? transform.parent.InverseTransformPoint(gun.parent.position) : basePosition;
        }

        // Пулемёт наводится в Update (TurretGunAim), анимация с IK считается после Update
        private void Update()
        {
            if (gun == null) return;

            Quaternion tilt = Quaternion.Euler(lean, 0f, 0f);
            Quaternion turn = Quaternion.Euler(0f, Mathf.DeltaAngle(0f, gun.localEulerAngles.y), 0f);
            Vector3 position = hips + tilt * (basePosition - hips);
            transform.localPosition = pivot + turn * (position - pivot);
            transform.localRotation = turn * tilt * baseRotation;
        }

        // Таз до первого кадра анимации стоит в позе импорта модели, поэтому точку наклона берём
        // после анимации и переводим в несдвинутое положение тела
        private void LateUpdate()
        {
            if (hipsKnown || animator == null || !animator.isInitialized) return;
            Transform hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsBone == null) return;
            Vector3 local = Vector3.Scale(transform.localScale, transform.InverseTransformPoint(hipsBone.position));
            hips = basePosition + baseRotation * local;
            hipsKnown = true;
        }
    }
}
