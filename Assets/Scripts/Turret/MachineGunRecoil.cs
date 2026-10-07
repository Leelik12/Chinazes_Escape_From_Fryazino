using UnityEngine;

namespace RacingProject.Turret
{
    // Отдача пулемёта на крыше: модель при выстреле отбрасывает назад и вверх с небольшим случайным уводом
    // в сторону, затем она плавно возвращается. В очереди отдача копится до предела, поэтому ствол
    // заметно «дышит». Каждый выстрел выбрасывает гильзу. Вызывается из VRGun у обоих игроков.
    // Двигается только модель (visual), а не объект с VRGun, поэтому наведение и синхронизация не сбиваются
    public class MachineGunRecoil : MonoBehaviour
    {
        [Tooltip("Модель пулемёта, которую отбрасывает отдача")]
        [SerializeField] private Transform visual;

        [Header("Отдача")]
        [Tooltip("Отброс назад за выстрел, в локальных единицах родителя модели")]
        [SerializeField] private float kickBack = 0.025f;
        [Tooltip("Подброс ствола вверх за выстрел, градусы")]
        [SerializeField] private float kickPitch = 1.8f;
        [Tooltip("Случайный увод в сторону за выстрел, градусы")]
        [SerializeField] private float kickYaw = 0.6f;
        [Tooltip("Во сколько раз накопленная в очереди отдача может превысить отдачу одного выстрела")]
        [SerializeField] private float maxAccumulation = 2.5f;
        [Tooltip("Скорость возврата, 1/с: чем больше, тем быстрее модель встаёт на место")]
        [SerializeField] private float returnSpeed = 14f;

        [Header("Гильзы")]
        [SerializeField] private ParticleSystem shellEjector;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float back;
        private float pitch;
        private float yaw;

        private void Awake()
        {
            if (visual == null) return;
            basePosition = visual.localPosition;
            baseRotation = visual.localRotation;
        }

        public void Kick()
        {
            back = Mathf.Min(back + kickBack, kickBack * maxAccumulation);
            pitch = Mathf.Min(pitch + kickPitch, kickPitch * maxAccumulation);
            yaw = Mathf.Clamp(yaw + Random.Range(-kickYaw, kickYaw), -kickYaw * maxAccumulation, kickYaw * maxAccumulation);

            if (shellEjector != null)
                shellEjector.Emit(1);
        }

        private void LateUpdate()
        {
            if (visual == null) return;

            float decay = Mathf.Exp(-returnSpeed * Time.deltaTime);
            back *= decay;
            pitch *= decay;
            yaw *= decay;

            visual.localPosition = basePosition + baseRotation * Vector3.back * back;
            visual.localRotation = baseRotation * Quaternion.Euler(-pitch, yaw, 0f);
        }
    }
}
