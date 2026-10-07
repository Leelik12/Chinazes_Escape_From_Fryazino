using LogitechG29.Sample.Input;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using RacingProject.Network;

namespace RacingProject.Car
{
    // Нитро водителя: пока зажата кнопка и есть заряд, машину толкает вперёд сверх ограничения передачи.
    // Заряд тратится за duration секунд и восстанавливается за rechargeTime. Физику и заряд считает
    // хост (он же водитель), пламя из выхлопа, звук и шкала заряда видны обоим игрокам
    public class CarNitro : NetworkBehaviour
    {
        [SerializeField] private InputControllerReader inputControllerReader;
        [SerializeField] private Rigidbody body;

        [Header("Ускорение")]
        [Tooltip("Добавочное ускорение, м/с²")]
        [SerializeField] private float acceleration = 12f;
        [Tooltip("Выше этой скорости нитро не разгоняет, км/ч")]
        [SerializeField] private float maxSpeedKmh = 180f;

        [Header("Заряд")]
        [Tooltip("Время работы на полном заряде, с")]
        [SerializeField] private float duration = 3f;
        [Tooltip("Время восстановления от нуля до полного, с")]
        [SerializeField] private float rechargeTime = 12f;
        [Tooltip("Меньше этого заряда нитро не включается, иначе оно дёргалось бы на остатках")]
        [SerializeField, Range(0f, 1f)] private float minChargeToStart = 0.2f;

        [Header("Эффекты")]
        [SerializeField] private ParticleSystem[] flames;
        [Tooltip("Зацикленный звук работы нитро")]
        [SerializeField] private AudioSource boostSound;
        [Tooltip("Полоска заряда на приборке")]
        [SerializeField] private Slider chargeBar;
        [Tooltip("Текст заряда в процентах (необязательно)")]
        [SerializeField] private TMP_Text chargeText;

        // Шкала заряда обновляется ступенями, чтобы не слать переменную каждый тик
        private const float ChargeSendStep = 0.02f;

        private readonly NetworkVariable<bool> boosting = new NetworkVariable<bool>();
        private readonly NetworkVariable<float> networkCharge = new NetworkVariable<float>(1f);

        private float charge = 1f;
        private bool active;

        // Для приборов: одинаково у хоста и клиента
        public float Charge01 => IsSpawned ? networkCharge.Value : 1f;
        public bool Boosting => IsSpawned && boosting.Value;
        private bool shownBoosting;
        private int shownPercent = -1;

        private void Awake()
        {
            if (body == null)
                body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned) return;

            bool pressed = LocalPlayerRole.Current == PlayerRole.Driver && IsPressed();
            if (!active && pressed && charge >= minChargeToStart)
                active = true;
            else if (active && (!pressed || charge <= 0f))
                active = false;

            if (active)
            {
                charge = Mathf.Max(0f, charge - Time.fixedDeltaTime / duration);
                if (body.linearVelocity.magnitude * 3.6f < maxSpeedKmh)
                    body.AddForce(transform.forward * acceleration, ForceMode.Acceleration);
            }
            else
            {
                charge = Mathf.Min(1f, charge + Time.fixedDeltaTime / rechargeTime);
            }

            if (boosting.Value != active)
                boosting.Value = active;
            if (Mathf.Abs(networkCharge.Value - charge) >= ChargeSendStep || (charge >= 1f && networkCharge.Value < 1f) || (charge <= 0f && networkCharge.Value > 0f))
                networkCharge.Value = charge;
        }

        // Кнопка Right Bumper руля G29 или левый Shift на клавиатуре
        private bool IsPressed()
        {
            bool wheel = inputControllerReader != null && inputControllerReader.RightBumper;
            bool keyboard = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            return wheel || keyboard;
        }

        private void Update()
        {
            bool isBoosting = IsSpawned && boosting.Value;
            if (isBoosting != shownBoosting)
            {
                shownBoosting = isBoosting;
                foreach (ParticleSystem flame in flames)
                {
                    if (flame == null) continue;
                    if (isBoosting) flame.Play();
                    else flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                if (boostSound != null)
                {
                    if (isBoosting) boostSound.Play();
                    else boostSound.Stop();
                }
            }

            float shownCharge = IsSpawned ? networkCharge.Value : 1f;
            int percent = Mathf.RoundToInt(shownCharge * 100f);
            if (percent != shownPercent)
            {
                shownPercent = percent;
                if (chargeBar != null)
                    chargeBar.value = Mathf.Lerp(chargeBar.minValue, chargeBar.maxValue, shownCharge);
                if (chargeText != null)
                    chargeText.text = $"Нитро {percent}%";
            }
        }
    }
}
