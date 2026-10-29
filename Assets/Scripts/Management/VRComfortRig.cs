using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace RacingProject.Management
{
    // Комфорт на VR-риге в машине: включает плавный поворот стиком или поворот рывками (VRComfort.Turn)
    // и сужает обзор затемнением краёв, когда машину резко разгоняет, тормозит, бросает в сторону или разворачивает.
    // Движение считается по самой машине (transform), поэтому работает и у клиента, где машину двигает сеть
    [DefaultExecutionOrder(100)]
    public class VRComfortRig : MonoBehaviour
    {
        [SerializeField] private ContinuousTurnProvider smoothTurn;
        [SerializeField] private SnapTurnProvider snapTurn;

        [Header("Затемнение краёв")]
        [Tooltip("Полусфера TunnelingVignette (XRI Starter Assets) на камере шлема")]
        [SerializeField] private Renderer vignette;
        [Tooltip("Машина, движение которой ощущает игрок")]
        [SerializeField] private Transform vehicle;
        [Tooltip("Ускорение машины, при котором затемнение полное, м/с²")]
        [SerializeField] private float fullAcceleration = 9f;
        [Tooltip("Скорость разворота машины, при которой затемнение полное, градусы в секунду")]
        [SerializeField] private float fullYawRate = 80f;
        [Tooltip("Ниже этой доли (от полного) затемнения нет: обычная езда не сужает обзор")]
        [Range(0f, 1f)] [SerializeField] private float threshold = 0.25f;
        [Tooltip("Раскрытие обзора при полном затемнении (1 — без затемнения)")]
        [Range(0f, 1f)] [SerializeField] private float minAperture = 0.6f;
        [SerializeField] private float closeSpeed = 4f;
        [SerializeField] private float openSpeed = 1.5f;

        private static readonly int ApertureId = Shader.PropertyToID("_ApertureSize");
        // Раскрытие на паузе (VRPauseMenu): остаётся узкое окно на надпись
        private const float PausedAperture = 0.35f;

        private MaterialPropertyBlock block;
        private Vector3 lastPosition;
        private Vector3 lastVelocity;
        private float lastYaw;
        private float smoothedAcceleration;
        private float aperture = 1f;
        private bool hasSample;

        private void OnEnable()
        {
            VRComfort.Changed += Apply;
            ViewModeService.Changed += OnViewModeChanged;
            hasSample = false;
            Apply();
        }

        private void OnDisable()
        {
            VRComfort.Changed -= Apply;
            ViewModeService.Changed -= OnViewModeChanged;
        }

        private void OnViewModeChanged(ViewMode mode) => Apply();

        private void Apply()
        {
            bool vr = ViewModeService.IsVR;
            bool snap = VRComfort.Turn == VRComfort.TurnMode.Snap;
            if (smoothTurn != null) smoothTurn.enabled = vr && !snap;
            if (snapTurn != null) snapTurn.enabled = vr && snap;
        }

        private void LateUpdate()
        {
            if (vignette == null) return;
            float target = 1f;
            if (VRPauseMenu.IsOpen)
                target = PausedAperture;
            else if (VRComfort.Vignette && vehicle != null)
                target = Mathf.Lerp(1f, minAperture, MotionIntensity());

            float speed = target < aperture ? closeSpeed : openSpeed;
            aperture = Mathf.MoveTowards(aperture, target, speed * Time.unscaledDeltaTime);

            bool visible = aperture < 0.995f;
            if (vignette.enabled != visible) vignette.enabled = visible;
            if (!visible) return;
            if (block == null) block = new MaterialPropertyBlock();
            vignette.GetPropertyBlock(block);
            block.SetFloat(ApertureId, aperture);
            vignette.SetPropertyBlock(block);
        }

        // 0–1: насколько сильно машину сейчас бросает (ускорение в горизонтальной плоскости и разворот)
        private float MotionIntensity()
        {
            float dt = Time.deltaTime;
            Vector3 position = vehicle.position;
            float yaw = vehicle.eulerAngles.y;
            if (!hasSample || dt <= 0f)
            {
                hasSample = true;
                lastPosition = position;
                lastVelocity = Vector3.zero;
                lastYaw = yaw;
                return 0f;
            }

            Vector3 velocity = (position - lastPosition) / dt;
            // Телепорт (восстановление машины, перезапуск) — не рывок
            if (velocity.sqrMagnitude > 120f * 120f) velocity = lastVelocity;
            Vector3 acceleration = (velocity - lastVelocity) / dt;
            acceleration.y = 0f;
            float yawRate = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / dt;
            lastPosition = position;
            lastVelocity = velocity;
            lastYaw = yaw;

            // Ускорение по кадрам шумит — сглаживаем
            smoothedAcceleration = Mathf.Lerp(smoothedAcceleration, acceleration.magnitude, 1f - Mathf.Exp(-8f * dt));
            float intensity = Mathf.Max(smoothedAcceleration / fullAcceleration, yawRate / fullYawRate);
            return Mathf.Clamp01(Mathf.InverseLerp(threshold, 1f, intensity));
        }
    }
}
