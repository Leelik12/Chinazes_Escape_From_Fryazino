using UnityEngine;
using UnityEngine.InputSystem;
using RacingProject.Car;
using RacingProject.Management;

namespace RacingProject.Desktop
{
    // Камера от третьего лица для одиночной игры: висит позади и выше машины и вращается мышью вокруг неё.
    // Направление обзора задаётся в мировых осях, поэтому прицел не сбивается, когда машина поворачивает;
    // если мышь не трогать и не стрелять, на ходу камера плавно заходит за корму. Если между машиной и камерой
    // стена, камера придвигается к машине. Курсор освобождает меню паузы (Esc), клик в игре снова захватывает.
    // Ощущение скорости: угол обзора растёт со скоростью и рывком при нитро, в поворотах камера чуть кренится,
    // а удары, жёсткие приземления и попадания её встряхивают (прицел из центра экрана при этом почти не гуляет).
    // Когда машина погибает, камера отходит и медленно облетает горящий остов, пока не появится экран гибели
    [DefaultExecutionOrder(40)] // раньше DesktopGunnerAim (50), который целится из центра этой камеры
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("Высота точки, вокруг которой вращается камера, над началом машины, м")]
        [SerializeField] private float pivotHeight = 3.5f;
        [SerializeField] private float distance = 11f;
        [SerializeField] private float minDistance = 2f;

        [Header("Мышь")]
        [Tooltip("Градусов на пиксель движения мыши")]
        [SerializeField] private float sensitivity = 0.15f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 60f;
        [Tooltip("Наклон камеры вниз при включении, градусы")]
        [SerializeField] private float startPitch = 10f;

        [Header("Возврат за корму")]
        [Tooltip("Через сколько секунд без движения мыши и стрельбы камера заходит за корму")]
        [SerializeField] private float recenterDelay = 2.5f;
        [Tooltip("Скорость возврата, градусы в секунду")]
        [SerializeField] private float recenterSpeed = 90f;
        [Tooltip("Медленнее этого (км/ч) камера не возвращается: стоя можно спокойно осматриваться")]
        [SerializeField] private float recenterMinSpeedKmh = 10f;

        [Header("Препятствия")]
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Ощущение скорости")]
        [Tooltip("Насколько шире угол обзора на fovFullSpeedKmh, градусы")]
        [SerializeField] private float speedFov = 12f;
        [SerializeField] private float fovFullSpeedKmh = 150f;
        [Tooltip("Добавка к углу обзора, пока горит нитро, градусы")]
        [SerializeField] private float nitroFov = 7f;
        [Tooltip("Скорость, с которой угол обзора догоняет нужный, 1/с")]
        [SerializeField] private float fovResponse = 4f;
        [Tooltip("Крен камеры в повороте на каждое 1 g бокового ускорения, градусы")]
        [SerializeField] private float tiltPerG = 2.5f;
        [SerializeField] private float maxTilt = 4f;

        [Header("Тряска")]
        [Tooltip("Наибольшее отклонение камеры при полной тряске, градусы")]
        [SerializeField] private float shakeAngle = 2.2f;
        [Tooltip("Наибольший сдвиг камеры при полной тряске, м")]
        [SerializeField] private float shakeOffset = 0.25f;
        [SerializeField] private float shakeFrequency = 18f;
        [Tooltip("Как быстро тряска затухает, доля в секунду")]
        [SerializeField] private float shakeDecay = 1.6f;
        [Tooltip("Удар машины (резкое изменение скорости), с которого начинается тряска, м/с за кадр физики")]
        [SerializeField] private float impactThreshold = 4f;
        [Tooltip("Изменение скорости, дающее полную тряску, м/с")]
        [SerializeField] private float impactFull = 14f;
        [Tooltip("Урон в долях прочности, дающий полную тряску")]
        [SerializeField] private float damageFull = 0.2f;
        [Tooltip("Лёгкая дрожь на большой скорости: тряска на fovFullSpeedKmh")]
        [SerializeField] private float speedRumble = 0.08f;

        [Header("Облёт после гибели")]
        [Tooltip("Скорость облёта, градусы в секунду")]
        [SerializeField] private float deathOrbitSpeed = 22f;
        [SerializeField] private float deathDistance = 13f;
        [SerializeField] private float deathPitch = 18f;
        [Tooltip("Высота точки облёта над машиной, м")]
        [SerializeField] private float deathPivotHeight = 1.2f;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Camera cam;
        private CarNitro nitro;
        private PlayerHealth health;
        private float baseFov;
        private float tilt;
        private float trauma;
        private Vector3 lastVelocity;
        private Vector3 lastPosition;
        private float lastFixedTime;
        private Rigidbody body;
        private float yaw;
        private float pitch;
        private float idleTime;
        private float deathBlend;
        private float orbitDistance;

        private void OnEnable()
        {
            if (target != null)
            {
                body = target.GetComponentInParent<Rigidbody>();
                yaw = HeadingOf(target);
                nitro = target.GetComponentInParent<CarNitro>();
                health = target.GetComponentInParent<PlayerHealth>();
                if (health != null) health.OnDamageTaken += OnDamage;
            }
            cam = GetComponent<Camera>();
            if (cam != null) baseFov = cam.fieldOfView;
            if (body != null)
            {
                lastVelocity = body.linearVelocity;
                lastPosition = body.position;
            }
            pitch = startPitch;
            idleTime = 0f;
            deathBlend = 0f;
            trauma = 0f;
            tilt = 0f;
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            if (health != null) health.OnDamageTaken -= OnDamage;
            if (cam != null) cam.fieldOfView = baseFov;
            SetCursorLocked(false);
        }

        private void OnDamage(int damage)
        {
            if (health == null || health.MaxHealth <= 0 || damageFull <= 0f) return;
            AddTrauma((float)damage / health.MaxHealth / damageFull);
        }

        // Встряхнуть камеру: 1 — полная тряска (взрыв рядом, лобовой удар)
        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Mouse mouse = Mouse.current;
            bool active = false;
            bool dead = health != null && health.IsDead;
            if (dead)
            {
                UpdateDeathOrbit();
                return;
            }
            if (mouse != null && UpdateCursorLock(mouse))
            {
                Vector2 delta = mouse.delta.ReadValue() * sensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
                active = delta.sqrMagnitude > 0.0001f || mouse.leftButton.isPressed;
            }

            idleTime = active ? 0f : idleTime + Time.deltaTime;
            float speedKmh = body != null ? body.linearVelocity.magnitude * 3.6f : 0f;
            if (idleTime > recenterDelay && speedKmh > recenterMinSpeedKmh)
                yaw = Mathf.MoveTowardsAngle(yaw, HeadingOf(target), recenterSpeed * Time.deltaTime);

            UpdateFeel(speedKmh);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Vector3 back = rotation * Vector3.back;
            Vector3 position = pivot + back * FreeDistance(pivot, back, distance);

            // Тряска — сглаженный шум, сила растёт как квадрат «травмы»: слабые толчки почти незаметны
            float shake = trauma * trauma;
            if (shake > 0.0001f)
            {
                float time = Time.time * shakeFrequency;
                Vector3 angles = new Vector3(Noise(time, 0.1f), Noise(time, 3.7f), Noise(time, 7.3f)) * (shakeAngle * shake);
                Vector3 offset = new Vector3(Noise(time, 11.1f), Noise(time, 13.9f), 0f) * (shakeOffset * shake);
                rotation *= Quaternion.Euler(angles.x, angles.y, angles.z * 0.5f);
                position += rotation * offset;
            }
            rotation *= Quaternion.Euler(0f, 0f, tilt);
            transform.SetPositionAndRotation(position, rotation);
        }

        // Облёт погибшей машины: камера плавно отходит дальше и ниже и кружит вокруг остова
        private void UpdateDeathOrbit()
        {
            float dt = Time.deltaTime;
            if (deathBlend <= 0f) orbitDistance = distance;
            deathBlend = Mathf.MoveTowards(deathBlend, 1f, dt / 1.5f);
            float ease = Mathf.SmoothStep(0f, 1f, deathBlend);
            yaw += deathOrbitSpeed * ease * dt;
            pitch = Mathf.Lerp(pitch, deathPitch, 1f - Mathf.Exp(-2f * dt));
            orbitDistance = Mathf.Lerp(orbitDistance, deathDistance, 1f - Mathf.Exp(-1.5f * dt));
            tilt = Mathf.Lerp(tilt, 0f, 1f - Mathf.Exp(-3f * dt));
            trauma = Mathf.Max(0f, trauma - shakeDecay * dt);
            if (cam != null && !cam.stereoEnabled)
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov, 1f - Mathf.Exp(-2f * dt));

            Quaternion rotation = Quaternion.Euler(pitch, yaw, tilt);
            Vector3 pivot = target.position + Vector3.up * Mathf.Lerp(pivotHeight, deathPivotHeight, ease);
            Vector3 back = rotation * Vector3.back;
            float free = Mathf.Min(orbitDistance, FreeDistance(pivot, back, orbitDistance));
            transform.SetPositionAndRotation(pivot + back * free, rotation);
        }

        // Угол обзора, крен в повороте и тряска от ударов машины
        private void UpdateFeel(float speedKmh)
        {
            float dt = Time.deltaTime;
            if (cam != null && !cam.stereoEnabled)
            {
                float speed01 = Mathf.Clamp01(speedKmh / fovFullSpeedKmh);
                float targetFov = baseFov + speedFov * speed01 * speed01;
                if (nitro != null && nitro.Boosting) targetFov += nitroFov;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovResponse * dt));
            }

            float lateralG = 0f;
            if (body != null)
            {
                // Скорость Rigidbody меняется только в шагах физики: удар считаем по разнице между ними
                if (Time.fixedTime != lastFixedTime)
                {
                    Vector3 velocity = body.linearVelocity;
                    float impact = (velocity - lastVelocity).magnitude;
                    // Перенос машины (R, обучение) — не удар
                    bool teleported = (body.position - lastPosition).sqrMagnitude > 25f;
                    if (impact > impactThreshold && !teleported)
                        AddTrauma((impact - impactThreshold) / Mathf.Max(0.01f, impactFull - impactThreshold));
                    lastVelocity = velocity;
                    lastPosition = body.position;
                    lastFixedTime = Time.fixedTime;
                }
                // Боковое ускорение на ходу: скорость × скорость поворота
                lateralG = Vector3.Dot(body.angularVelocity, body.transform.up) * Vector3.Dot(body.linearVelocity, body.transform.forward) / 9.81f;
            }
            float targetTilt = Mathf.Clamp(-lateralG * tiltPerG, -maxTilt, maxTilt);
            tilt = Mathf.Lerp(tilt, targetTilt, 1f - Mathf.Exp(-3f * dt));

            float rumble = speedRumble * Mathf.Clamp01((speedKmh - 60f) / Mathf.Max(1f, fovFullSpeedKmh - 60f));
            trauma = Mathf.Max(rumble, trauma - shakeDecay * dt);
        }

        private static float Noise(float time, float seed)
        {
            return Mathf.PerlinNoise(time, seed) * 2f - 1f;
        }

        // Насколько камера может отойти от точки вращения, не заходя за стену (своя машина не мешает)
        private float FreeDistance(Vector3 pivot, Vector3 direction, float distance)
        {
            int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, direction, hits, distance, collisionMask, QueryTriggerInteraction.Ignore);
            float free = distance;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(target)) continue;
                // Касание в самом начале SphereCast отдаёт без точки: камера внутри препятствия
                if (hits[i].distance <= 0f) continue;
                free = Mathf.Min(free, hits[i].distance);
            }
            return Mathf.Max(minDistance, free);
        }

        private static float HeadingOf(Transform t)
        {
            Vector3 forward = t.forward;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        // Возвращает true, если курсор захвачен и мышь управляет камерой
        private static bool UpdateCursorLock(Mouse mouse)
        {
            if (PauseMenu.BlocksInput) return false;
            if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);

            return Cursor.lockState == CursorLockMode.Locked;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
