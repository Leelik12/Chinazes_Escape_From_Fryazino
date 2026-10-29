using UnityEngine;
using RacingProject.Network;
using G29 = LogitechG29.Runtime.LogitechG29;

namespace RacingProject.Car
{
    // Отдача руля Logitech G29 у водителя: руль тяжелеет и сильнее возвращается к центру с ростом скорости,
    // на гравии и траве дрожит, в прыжке становится лёгким, удары и попадания дёргают его, пробитое колесо
    // тянет в свою сторону, светодиоды на руле показывают обороты. Работает у того, кто ведёт машину,
    // и только если руль подключён
    public class WheelForceFeedback : MonoBehaviour
    {
        [SerializeField] private CarControllerSample controller;
        [SerializeField] private CarDeformation deformation;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private CarDamageState damage;
        [SerializeField] private WheelCollider[] wheels = new WheelCollider[0];

        [Header("Возврат к центру и вязкость")]
        [Tooltip("Сила пружины к центру стоя и на fullSpeedKmh, %")]
        [SerializeField] private Vector2 spring = new Vector2(15f, 55f);
        [Tooltip("Вязкость стоя и на скорости, %: на месте руль тяжёлый")]
        [SerializeField] private Vector2 damper = new Vector2(35f, 10f);
        [SerializeField] private float fullSpeedKmh = 120f;

        [Header("Дорога")]
        [Tooltip("Дрожь на гравии и траве на fullSpeedKmh, %")]
        [SerializeField] private float roughRoad = 30f;

        [Header("Удары")]
        [Tooltip("Удар этой скорости даёт полный толчок, м/с")]
        [SerializeField] private float impactFull = 15f;
        [Tooltip("Толчок от попадания на каждые 100 урона, %")]
        [SerializeField] private float hitKick = 25f;
        [Tooltip("Тяга пробитого колеса на fullSpeedKmh, %")]
        [SerializeField] private float flatPull = 22f;

        private bool playing;
        private bool airborne;
        private bool rough;
        private float lastLeds = -1f;

        private void OnEnable()
        {
            if (deformation != null) deformation.Impacted += OnImpacted;
            if (health != null) health.OnDamageTaken += OnDamage;
        }

        private void OnDisable()
        {
            if (deformation != null) deformation.Impacted -= OnImpacted;
            if (health != null) health.OnDamageTaken -= OnDamage;
            StopAll();
        }

        private static G29 Wheel
        {
            get
            {
                G29 wheel = G29.Current;
                return wheel != null && wheel.IsConnected ? wheel : null;
            }
        }

        private bool Active => controller != null && LocalPlayerRole.Controls(PlayerRole.Driver) && (health == null || !health.IsDead);

        private void Update()
        {
            G29 wheel = Wheel;
            if (wheel == null || !Active)
            {
                if (playing) StopAll();
                return;
            }
            playing = true;

            float speed01 = Mathf.Clamp01(controller.SpeedKmh / fullSpeedKmh);
            wheel.PlaySpringForce(0, 80, Mathf.RoundToInt(Mathf.Lerp(spring.x, spring.y, speed01)));
            wheel.PlayDamperForce(Mathf.RoundToInt(Mathf.Lerp(damper.x, damper.y, speed01)));

            // Поверхность под колёсами и полёт
            int grounded = 0, loose = 0;
            foreach (WheelCollider collider in wheels)
            {
                if (collider == null || !collider.GetGroundHit(out WheelHit hit)) continue;
                grounded++;
                if (WheelEffects.SurfaceOf(hit.collider) != WheelEffects.Surface.Asphalt) loose++;
            }
            bool inAir = wheels.Length > 0 && grounded == 0;
            if (inAir != airborne)
            {
                airborne = inAir;
                if (airborne) wheel.PlayCarAirborne();
                else wheel.StopCarAirborne();
            }
            bool onRough = loose > 0 && controller.SpeedKmh > 8f;
            if (onRough) wheel.PlayDirtRoadEffect(Mathf.RoundToInt(roughRoad * Mathf.Lerp(0.3f, 1f, speed01) * loose / Mathf.Max(1, grounded)));
            else if (rough) wheel.StopDirtRoadEffect();
            rough = onRough;

            // Спущенное колесо тянет руль в свою сторону
            float pull = damage != null ? damage.FlatPull : 0f;
            if (Mathf.Abs(pull) > 0.01f && !airborne)
                wheel.PlayConstantForce(Mathf.RoundToInt(pull * flatPull * Mathf.Lerp(0.3f, 1f, speed01)));
            else if (wheel.IsPlayingConstantForce)
                wheel.StopConstantForce();

            // Светодиоды оборотов
            float rpm = controller.EngineRpm01;
            if (Mathf.Abs(rpm - lastLeds) > 0.02f)
            {
                lastLeds = rpm;
                wheel.PlayLeds(rpm, 0.55f, 0.97f);
            }
        }

        // Удар кузова: лобовой или боковой толчок по направлению удара
        private void OnImpacted(Vector3 point, float speed)
        {
            G29 wheel = Wheel;
            if (wheel == null || !Active) return;
            int magnitude = Mathf.RoundToInt(Mathf.Clamp01(speed / impactFull) * 100f);
            if (magnitude < 8) return;
            Vector3 local = transform.InverseTransformPoint(point);
            if (Mathf.Abs(local.z) > Mathf.Abs(local.x)) wheel.PlayFontalCollisionForce(magnitude);
            else wheel.PlaySideCollisionForce(local.x > 0f ? magnitude : -magnitude);
        }

        // Попадание пуль, гранаты, взрыва — короткий рывок
        private void OnDamage(int amount)
        {
            G29 wheel = Wheel;
            if (wheel == null || !Active) return;
            int magnitude = Mathf.Clamp(Mathf.RoundToInt(amount / 100f * hitKick), 5, 80);
            wheel.PlaySideCollisionForce(Random.value < 0.5f ? magnitude : -magnitude);
        }

        private void StopAll()
        {
            playing = false;
            airborne = false;
            rough = false;
            lastLeds = -1f;
            G29 wheel = Wheel;
            if (wheel == null) return;
            wheel.StopAllForceFeedback();
            wheel.PlayLeds(0f, 0.55f, 0.97f);
        }
    }
}
