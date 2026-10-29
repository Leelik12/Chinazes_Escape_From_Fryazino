using System;
using System.Collections.Generic;
using LogitechG29.Sample.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using RacingProject.Management;
using RacingProject.Network;

namespace RacingProject.Car
{
    public class CarControllerSample : MonoBehaviour
    {
        [Header("Важное, не трогать!")]
        [SerializeField] private InputControllerReader inputControllerReader;
        [SerializeField] private List<AxleInfo> axleInfos;
        private Rigidbody rb;

        [SerializeField] private float maxMotorTorque;
        [SerializeField] private float maxSteeringAngle;
        [Tooltip("Доля угла поворота колёс на highSpeedKmh: на скорости руль острее, и машину бросает из стороны в сторону")]
        [SerializeField, Range(0.1f, 1f)] private float highSpeedSteerFactor = 0.4f;
        [SerializeField] private float highSpeedKmh = 110f;
        [Tooltip("С клавиатуры руль поворачивается плавно: доля полного поворота в секунду (руль G29 — без задержки)")]
        [SerializeField] private float keyboardSteerSpeed = 3f;
        [Tooltip("Скорость возврата руля к центру с клавиатуры, доля в секунду")]
        [SerializeField] private float keyboardSteerReturn = 5f;
        [SerializeField] private float maxBrakeTorque = 10000f;
        [Tooltip("Доля тормозного усилия на передней оси: при торможении вес уходит вперёд, и передние колёса держат дорогу лучше")]
        [SerializeField, Range(0f, 1f)] private float frontBrakeBias = 0.6f;
        [Tooltip("АБС: если колесо проскальзывает сильнее этого, тормоз на нём ослабевает, и машина остаётся управляемой")]
        [SerializeField] private float absSlip = 0.35f;
        [Tooltip("Центр масс машины в её локальных координатах: чем ниже, тем меньше крен и тем труднее перевернуться")]
        [SerializeField] private Vector3 centerOfMass = new Vector3(0f, -0.8f, 0f);

        [Header("Ручник (пробел или рычаг)")]
        [Tooltip("Тормозной момент ручника на задних колёсах")]
        [SerializeField] private float handbrakeTorque = 8000f;
        [Tooltip("Доля бокового сцепления задних колёс при затянутом ручнике: зад уходит в занос")]
        [SerializeField, Range(0.1f, 1f)] private float handbrakeSideGrip = 0.45f;
        [Tooltip("Скорость возврата сцепления после ручника, доля в секунду")]
        [SerializeField] private float handbrakeGripReturn = 1.5f;
        [Tooltip("Доля бокового сцепления на мокрой дороге (дождь, WeatherController): в поворотах машину легче сорвать в занос")]
        [SerializeField, Range(0.5f, 1f)] private float wetSideGrip = 0.82f;
        [Tooltip("Сцепление выжато, если педаль нажата сильнее этого значения: можно переключать передачу, двигатель отсоединён от колёс")]
        [SerializeField, Range(0f, 1f)] private float clutchThreshold = 0.5f;
        [SerializeField] private float[] gearRatios = { 0f, 3.8f, 2.2f, 1.5f, 1.2f, 1f, 0.8f, -0.5f };

        [Header("Ограничение скорости (км/ч)")]
        [SerializeField] private float[] gearSpeedLimits = { 0f, 20f, 40f, 60f, 90f, 120f, 150f, 20f };

        [Header("Автоматическая коробка (одиночная игра)")]
        [Tooltip("Повышать передачу при такой доле предела скорости текущей передачи")]
        [SerializeField, Range(0.5f, 1f)] private float autoUpshift = 0.93f;
        [Tooltip("Понижать передачу, когда скорость ниже такой доли предела предыдущей передачи")]
        [SerializeField, Range(0.1f, 1f)] private float autoDownshift = 0.6f;
        [Tooltip("Ниже этой скорости (км/ч) тормоз включает заднюю передачу, а газ — первую")]
        [SerializeField] private float autoReverseSpeed = 3f;
        [Tooltip("Сколько секунд держать тормоз на месте, чтобы включилась задняя: короткое нажатие только тормозит")]
        [SerializeField] private float autoReverseDelay = 0.35f;

        [Header("Звуки")]
        [SerializeField] private AudioSource Engine;
        [SerializeField] private AudioClip Racing;

        [Header("UI")]
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text GearText;

        [Header("Визуальный руль")]
        [SerializeField] private Transform steeringWheelVisual;
        [SerializeField] private float visualWheelRotationAngle = 450f;
        [SerializeField] private float steeringSmoothness = 10f;
        [Tooltip("Наклон руля к водителю, градусы: руль вращается вокруг оси рулевой колонки")]
        [SerializeField] private float steeringWheelTilt = 25f;

        [Header("Повреждения")]
        [SerializeField] private PlayerHealth health;
        [Tooltip("Ниже этой доли прочности мотор слабеет")]
        [SerializeField, Range(0f, 1f)] private float powerLossBelow = 0.5f;
        [Tooltip("Доля мощности и предельной скорости на передаче при нулевой прочности")]
        [SerializeField, Range(0f, 1f)] private float minPower = 0.55f;
        [Tooltip("Ниже этой доли прочности мотор чихает: тяга на мгновения пропадает")]
        [SerializeField, Range(0f, 1f)] private float misfireBelow = 0.25f;

        private float currentVisualAngle = 0f;

        [Header("Временные переменные")]
        private float throttleInput;
        private float motor;
        private float steering;
        private float steerInput;
        private bool keyboardSteering;
        private float finalmotor;
        private int currentGear;
        private float brakeInput;
        private float reverseHold;
        private bool handbrake;
        private bool misfiring;
        private float rearGrip = 1f;
        private float appliedWetness;
        private readonly Dictionary<WheelCollider, float> baseSideStiffness = new Dictionary<WheelCollider, float>();
        // Боковое сцепление пробитых колёс (CarDamageState): множитель к обычному
        private readonly Dictionary<WheelCollider, float> damageGrip = new Dictionary<WheelCollider, float>();

        // Доля мощности мотора от поломок (перегрев при пробитом радиаторе), 1 — целый
        public float DamagePower { get; set; } = 1f;

        // Для приборов: передача (0 — нейтраль, 7 — задняя) и скорость по Rigidbody
        public int CurrentGear => currentGear;
        public float SpeedKmh => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
        // Угол поворота модели руля, градусы (может быть больше 360)
        public float SteeringWheelAngle => currentVisualAngle;
        // Ручник затянут (для камеры и звука)
        public bool Handbrake => handbrake;
        // Для звука мотора (CarEngineAudio): газ 0–1 и перебои разбитого мотора
        public float Throttle => throttleInput;
        public bool Misfiring => misfiring;

        // Обороты 0–1: доля предела скорости включённой передачи. На нейтрали и при выжатом сцеплении мотор
        // свободно раскручивается газом; с места на первой обороты не падают ниже доли газа (пробуксовка сцепления)
        public float EngineRpm01
        {
            get
            {
                if (rb == null) return 0f;
                float speed = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.forward)) * 3.6f;
                bool free = currentGear <= 0 || currentGear >= gearSpeedLimits.Length || gearSpeedLimits[currentGear] <= 0f
                            || (!LocalPlayerRole.IsSolo && inputControllerReader != null && inputControllerReader.Clutch > clutchThreshold);
                if (free) return throttleInput * 0.9f;
                float rpm = speed / gearSpeedLimits[currentGear];
                return Mathf.Clamp(Mathf.Max(rpm, throttleInput * 0.3f), 0f, 1.05f);
            }
        }

        [Header("Импульс для освобождения")]
        public float impulseForce = 5000f;      // сила импульса
        public float impulseCooldown = 5f;      // задержка между импульсами (в секундах)
        private float impulseTimer = 0f;

        private RigidbodyInterpolation ownerInterpolation;
        private bool? hasAuthority;

        private void Start()
        {
            Engine.Play();
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = centerOfMass;
            if (health == null)
                health = GetComponent<PlayerHealth>();
            ownerInterpolation = rb.interpolation;
            foreach (var axleInfo in axleInfos)
            {
                baseSideStiffness[axleInfo.leftWheel] = axleInfo.leftWheel.sidewaysFriction.stiffness;
                baseSideStiffness[axleInfo.rightWheel] = axleInfo.rightWheel.sidewaysFriction.stiffness;
            }
            GameKeys.ApplyTo(inputControllerReader);
            GameKeys.Changed += OnKeysChanged;
        }

        private void OnDestroy()
        {
            GameKeys.Changed -= OnKeysChanged;
        }

        private void OnKeysChanged()
        {
            GameKeys.ApplyTo(inputControllerReader);
        }

        // Модель руля крутится в кадре отрисовки: из FixedUpdate она дёргалась при высокой частоте кадров
        private void Update()
        {
            UpdateSteeringWheelVisual();
        }

        public void FixedUpdate()
        {
            ApplyNetworkAuthority();
            // Управляет только водитель (в одиночной игре — сам игрок); до старта раунда машина стоит
            if (!LocalPlayerRole.Controls(PlayerRole.Driver)) return;

            // Газ
            throttleInput = inputControllerReader.Throttle;
            // Тормоз
            brakeInput = inputControllerReader.Brake > 0.2f ? inputControllerReader.Brake : 0f;

            // Ручник: пробел (или своя клавиша) либо рычаг ручника
            handbrake = GameKeys.IsPressed(GameAction.Handbrake) || inputControllerReader.Handbrake > 0.5f;

            // Базовый момент
            motor = maxMotorTorque * throttleInput;
            steering = maxSteeringAngle * SteerInput() * SpeedSteerFactor();
            finalmotor = motor;

            // В одиночной игре коробка автоматическая: сцепление не нужно, тормоз на месте включает заднюю
            bool automatic = LocalPlayerRole.IsSolo;
            bool clutchPressed = !automatic && inputControllerReader.Clutch > clutchThreshold;

            if (automatic)
            {
                ShiftAutomatically();
                if (currentGear == 7)
                {
                    // Задним ходом S — газ, а W — тормоз
                    float reverseThrottle = inputControllerReader.Brake > 0.2f ? inputControllerReader.Brake : 0f;
                    brakeInput = throttleInput > 0.2f ? throttleInput : 0f;
                    throttleInput = reverseThrottle;
                    motor = maxMotorTorque * throttleInput;
                    finalmotor = motor;
                }
            }
            else if (clutchPressed) // коробас отрабатывает только если сцепа выжата
            {
                if (inputControllerReader.Shifter1)
                {
                    currentGear = 1;
                    if (GearText) GearText.text = "1";
                }
                else if (inputControllerReader.Shifter2)
                {
                    currentGear = 2;
                    if (GearText) GearText.text = "2";
                }
                else if (inputControllerReader.Shifter3)
                {
                    currentGear = 3;
                    if (GearText) GearText.text = "3";
                }
                else if (inputControllerReader.Shifter4)
                {
                    currentGear = 4;
                    if (GearText) GearText.text = "4";
                }
                else if (inputControllerReader.Shifter5)
                {
                    currentGear = 5;
                    if (GearText) GearText.text = "5";
                }
                else if (inputControllerReader.Shifter6)
                {
                    currentGear = 6;
                    if (GearText) GearText.text = "6";
                }
                else if (inputControllerReader.Shifter7)
                {
                    currentGear = 7;
                    if (GearText) GearText.text = "-1";
                }
                else
                {
                    currentGear = 0;
                    if (GearText) GearText.text = "N";
                }
            }

            // Разбитый мотор слабеет, а совсем разбитый ещё и чихает; улучшение «форсированный мотор» добавляет тяги
            bool misfire;
            float power = EnginePower(out misfire);
            misfiring = misfire;
            finalmotor *= power * RunUpgrades.EngineTorque * DamagePower;

            // Ограничение скорости по передаче
            float currentSpeed = rb.linearVelocity.magnitude * 3.6f;
            if (currentGear > 0 && currentGear < gearSpeedLimits.Length)
            {
                if (currentSpeed >= gearSpeedLimits[currentGear] * Mathf.Lerp(minPower, 1f, DamageHealth()))
                {
                    finalmotor = 0f; // перестаём ускоряться
                }
            }

            // Применение передаточного отношения
            finalmotor *= gearRatios[currentGear];

            // Аудио двигателя
            float correction = Mathf.Lerp(1f, 1.4f, throttleInput);
            Engine.pitch = misfire ? correction * 0.82f : correction;

            // UI
            UpdateGauges();

            // Передача момента и тормоза на колёса
            foreach (var axleInfo in axleInfos)
            {
                if (axleInfo.steering)
                {
                    axleInfo.leftWheel.steerAngle = steering;
                    axleInfo.rightWheel.steerAngle = steering;
                }

                if (axleInfo.motor)
                {
                    // При выжатом сцеплении момент снимается, иначе на колёсах оставалось последнее значение
                    float wheelTorque = clutchPressed ? 0f : finalmotor;
                    axleInfo.leftWheel.motorTorque = wheelTorque;
                    axleInfo.rightWheel.motorTorque = wheelTorque;

                }

                // Тормоза на обеих осях (независимо от сцепления), ручник — только задние колёса
                float share = axleInfo.steering ? frontBrakeBias : 1f - frontBrakeBias;
                float brakeTorque = brakeInput * maxBrakeTorque * 2f * share;
                bool rear = !axleInfo.steering;
                ApplyBrake(axleInfo.leftWheel, brakeTorque, rear);
                ApplyBrake(axleInfo.rightWheel, brakeTorque, rear);
            }
            UpdateRearGrip();
            UpdateWetGrip();
            // обновляем кулдаун импульса
            if (impulseTimer > 0f)
                impulseTimer -= Time.fixedDeltaTime;

            // проверяем крестовину (HatSwitch)
            HandleHatSwitchImpulse();

        }
        // Тормоз колеса с АБС; на задних колёсах ручник блокирует колесо без АБС
        private void ApplyBrake(WheelCollider wheel, float torque, bool rear)
        {
            if (torque > 0f && absSlip > 0f && wheel.GetGroundHit(out WheelHit hit) && Mathf.Abs(hit.forwardSlip) > absSlip)
                torque *= 0.35f;
            if (rear && handbrake)
            {
                torque = Mathf.Max(torque, handbrakeTorque);
                wheel.motorTorque = 0f;
            }
            wheel.brakeTorque = torque;
        }

        // С ручником задние колёса теряют боковое сцепление, после — плавно его возвращают
        private void UpdateRearGrip()
        {
            float target = handbrake ? handbrakeSideGrip : 1f;
            float rate = handbrake ? 6f : handbrakeGripReturn;
            float grip = Mathf.MoveTowards(rearGrip, target, rate * Time.fixedDeltaTime);
            if (Mathf.Approximately(grip, rearGrip)) return;
            rearGrip = grip;
            foreach (var axleInfo in axleInfos)
            {
                if (axleInfo.steering) continue;
                SetSideGrip(axleInfo.leftWheel, rearGrip);
                SetSideGrip(axleInfo.rightWheel, rearGrip);
            }
        }

        // Дорога мокнет постепенно: сцепление пересчитывается, когда мокрота заметно изменилась
        private void UpdateWetGrip()
        {
            float wetness = WheelEffects.Wetness;
            if (Mathf.Abs(wetness - appliedWetness) < 0.02f) return;
            appliedWetness = wetness;
            foreach (var axleInfo in axleInfos)
            {
                float factor = axleInfo.steering ? 1f : rearGrip;
                SetSideGrip(axleInfo.leftWheel, factor);
                SetSideGrip(axleInfo.rightWheel, factor);
            }
        }

        // Пробитое колесо держит дорогу хуже: factor — доля обычного бокового сцепления
        public void SetDamageGrip(WheelCollider wheel, float factor)
        {
            if (!baseSideStiffness.ContainsKey(wheel)) return;
            damageGrip[wheel] = factor;
            bool rear = false;
            foreach (var axleInfo in axleInfos)
                if (!axleInfo.steering && (axleInfo.leftWheel == wheel || axleInfo.rightWheel == wheel)) rear = true;
            SetSideGrip(wheel, rear ? rearGrip : 1f);
        }

        private void SetSideGrip(WheelCollider wheel, float factor)
        {
            WheelFrictionCurve friction = wheel.sidewaysFriction;
            if (damageGrip.TryGetValue(wheel, out float damaged)) factor *= damaged;
            factor *= Mathf.Lerp(1f, wetSideGrip, appliedWetness);
            friction.stiffness = baseSideStiffness[wheel] * factor;
            wheel.sidewaysFriction = friction;
        }

        // Передача по скорости и педалям: вперёд 1–6 по пределам скоростей, стоя на тормозе — задняя
        private void ShiftAutomatically()
        {
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;
            bool gas = inputControllerReader.Throttle > 0.2f;
            bool brake = inputControllerReader.Brake > 0.2f;

            // Задняя включается, только если тормоз на месте держат autoReverseDelay секунд
            bool holdingToReverse = brake && !gas && forwardSpeed < autoReverseSpeed;
            reverseHold = holdingToReverse ? reverseHold + Time.fixedDeltaTime : 0f;

            if (currentGear == 7)
            {
                // Из задней — на первую, когда машина почти встала и нажат газ
                if (gas && !brake && forwardSpeed > -autoReverseSpeed)
                    currentGear = 1;
            }
            else if (holdingToReverse && reverseHold >= autoReverseDelay)
            {
                currentGear = 7;
            }
            else if (currentGear == 0)
            {
                if (gas) currentGear = 1;
            }
            else
            {
                float limit = DamageLimit();
                if (currentGear < 6 && forwardSpeed >= gearSpeedLimits[currentGear] * limit * autoUpshift)
                    currentGear++;
                else if (currentGear > 1 && forwardSpeed < gearSpeedLimits[currentGear - 1] * limit * autoDownshift)
                    currentGear--;
            }

            if (GearText) GearText.text = currentGear == 7 ? "-1" : currentGear == 0 ? "N" : currentGear.ToString();
        }

        private float DamageLimit()
        {
            return Mathf.Lerp(minPower, 1f, DamageHealth());
        }

        // Руль G29 даёт угол как есть, а клавиши руля — сразу полный поворот, поэтому с клавиатуры он нарастает плавно
        private float SteerInput()
        {
            float target = inputControllerReader.Steering;
            bool keys = GameKeys.IsPressed(GameAction.SteerLeft) || GameKeys.IsPressed(GameAction.SteerRight);
            // Клавиши отпущены — руль плавно возвращается к центру, пока не встретит угол руля G29
            if (keys)
                keyboardSteering = true;
            else if (keyboardSteering && Mathf.Abs(target) > 0.01f)
                keyboardSteering = false;

            if (!keyboardSteering)
            {
                steerInput = target;
                return steerInput;
            }
            bool returning = Mathf.Abs(target) < Mathf.Abs(steerInput) || target * steerInput < 0f;
            float rate = returning ? keyboardSteerReturn : keyboardSteerSpeed;
            steerInput = Mathf.MoveTowards(steerInput, target, rate * Time.fixedDeltaTime);
            return steerInput;
        }

        private float SpeedSteerFactor()
        {
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;
            return Mathf.Lerp(1f, highSpeedSteerFactor, Mathf.Clamp01(speedKmh / highSpeedKmh));
        }

        // 0 при нулевой прочности, 1 — пока прочность не ниже powerLossBelow
        private float DamageHealth()
        {
            if (health == null || health.MaxHealth <= 0 || powerLossBelow <= 0f) return 1f;
            float fraction = Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
            return Mathf.Clamp01(fraction / powerLossBelow);
        }

        private float EnginePower(out bool misfire)
        {
            float power = Mathf.Lerp(minPower, 1f, DamageHealth());
            misfire = false;
            if (health != null && health.MaxHealth > 0 && (float)health.CurrentHealth / health.MaxHealth < misfireBelow)
            {
                // Пропуски зажигания: короткие провалы тяги в случайные моменты
                misfire = Mathf.PerlinNoise(Time.time * 3f, 0.37f) < 0.3f;
                if (misfire) power *= 0.15f;
            }
            return power;
        }

        // Физику машины считает только хост (водитель). У второго игрока машину двигает
        // NetworkTransform, а собственная физика и интерполяция Rigidbody спорили бы с сетью
        private void ApplyNetworkAuthority()
        {
            bool mine = LocalPlayerRole.SimulatesPhysics;
            if (hasAuthority == mine) return;
            hasAuthority = mine;

            rb.isKinematic = !mine;
            rb.interpolation = mine ? ownerInterpolation : RigidbodyInterpolation.None;
        }

        private void HandleHatSwitchImpulse()
        {
            if (impulseTimer > 0f) return; // ждём перезарядку

            Vector2 hat = inputControllerReader.HatSwitch; // (0,1) вперёд, (0,-1) назад
            Vector3 direction = Vector3.zero;

            if (hat.y > 0.5f) direction = transform.forward;   // толчок вперёд
            if (hat.y < -0.5f) direction = -transform.forward; // толчок назад

            if (direction != Vector3.zero)
            {
                rb.AddForce(direction * impulseForce, ForceMode.Impulse);
                impulseTimer = impulseCooldown; // сброс кулдауна
            }
        }

        [Serializable]
        public class AxleInfo
        {
            public WheelCollider leftWheel;
            public WheelCollider rightWheel;
            public bool motor;
            public bool steering;
        }

        private void UpdateGauges()
        {
            float speed = rb.linearVelocity.magnitude * 3.6f;
            if (speedText) speedText.text = $"{Mathf.RoundToInt(speed)} km/h";
        }

        private void UpdateSteeringWheelVisual()
        {
            if (steeringWheelVisual == null) return;

            // Итоговый руль: с клавиатуры — плавно нарастающий, с G29 — как есть
            float targetAngle = steerInput * visualWheelRotationAngle;
            currentVisualAngle = Mathf.Lerp(currentVisualAngle, targetAngle, Time.deltaTime * steeringSmoothness);
            steeringWheelVisual.localRotation = Quaternion.Euler(steeringWheelTilt, 0f, -currentVisualAngle);
        }
    }
}
