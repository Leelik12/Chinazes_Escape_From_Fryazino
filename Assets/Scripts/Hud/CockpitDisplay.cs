using TMPro;
using UnityEngine;
using RacingProject.Car;
using RacingProject.Enemy;
using RacingProject.Management;
using RacingProject.Turret;

namespace RacingProject.Hud
{
    // Приборы в машине: приборка водителя, тактический экран на консоли и табло стрелка на щите.
    // Один компонент на каждый экран: обновляет только назначенные ему элементы, остальные поля пустые.
    // Все данные берутся из синхронизированных компонентов, поэтому экраны одинаково работают у обоих игроков.
    public class CockpitDisplay : MonoBehaviour
    {
        [Header("Источники")]
        [SerializeField] private PlayerHealth health;
        [SerializeField] private EnemyManager enemies;
        [SerializeField] private CarControllerSample car;
        [SerializeField] private CarNitro nitro;
        [SerializeField] private VRGun gun;
        [SerializeField] private CarRecovery recovery;

        [Header("Шкалы")]
        [SerializeField] private HudArcGauge speedGauge;
        [SerializeField] private float maxSpeedKmh = 200f;
        [SerializeField] private HudArcGauge healthGauge;
        [SerializeField] private HudArcGauge heatGauge;
        [SerializeField] private HudBar healthBar;
        [SerializeField] private HudBar nitroBar;
        [SerializeField] private HudBar heatBar;

        [Header("Тексты")]
        [SerializeField] private TMP_Text gearText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text killsText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text comboText;

        [Header("Босс и предупреждения")]
        [SerializeField] private GameObject bossRoot;
        [SerializeField] private HudBar bossBar;
        [Tooltip("Надпись «ПЕРЕГРЕВ», мигает, пока пулемёт перегрет")]
        [SerializeField] private TMP_Text overheatWarning;
        [Tooltip("Подсказка «удерживайте R», когда машина перевернулась или застряла: корень (с подложкой) и текст")]
        [SerializeField] private GameObject recoveryRoot;
        [SerializeField] private TMP_Text recoveryPrompt;
        [Tooltip("Выстрелы РПГ стрелка: строка видна, когда гранатомёт есть")]
        [SerializeField] private RocketLauncher rockets;
        [SerializeField] private TMP_Text rocketsText;
        [Tooltip("Поломки машины: пробитое колесо, радиатор, перегрев мотора (мигает)")]
        [SerializeField] private CarDamageState damage;
        [SerializeField] private TMP_Text damageWarning;

        // Тексты чисел меняются, только когда меняется число: так TMP не перестраивает меш каждый кадр
        private int shownGear = -1;
        private int shownWave = -1;
        private int shownKills = -1;
        private int shownScore = -1;
        private int shownCombo = -1;
        private int shownRockets = -1;
        private int shownDamage = -1;

        private void Update()
        {
            UpdateCar();
            UpdateHealth();
            UpdateGun();
            UpdateRound();
            UpdateRecovery();
            UpdateRockets();
            UpdateDamage();
        }

        private void UpdateRockets()
        {
            if (rocketsText == null || rockets == null) return;
            bool show = rockets.HasLauncher;
            if (rocketsText.gameObject.activeSelf != show) rocketsText.gameObject.SetActive(show);
            if (!show) return;
            // Пустой гранатомёт — серым, заряжается — тусклым
            int state = rockets.Ammo * 2 + (rockets.Ready ? 1 : 0);
            if (state == shownRockets) return;
            shownRockets = state;
            string color = rockets.Ammo == 0 ? "#777777" : rockets.Ready ? "#ffd27a" : "#a08a5a";
            rocketsText.text = $"<color={color}>РПГ ×{rockets.Ammo}</color>";
        }

        private void UpdateDamage()
        {
            if (damageWarning == null || damage == null) return;
            int state = (damage.FlatCount > 0 ? 1 : 0) | (damage.RadiatorBroken ? 2 : 0) | (damage.Overheated ? 4 : 0);
            bool show = state != 0;
            if (damageWarning.gameObject.activeSelf != show) damageWarning.gameObject.SetActive(show);
            if (!show) return;
            if (state != shownDamage)
            {
                shownDamage = state;
                string text = damage.Overheated ? "ПЕРЕГРЕВ МОТОРА" : damage.RadiatorBroken ? "РАДИАТОР" : "";
                if (damage.FlatCount > 0) text += (text.Length > 0 ? " · " : "") + "КОЛЕСО";
                damageWarning.text = text;
            }
            Color color = damageWarning.color;
            color.a = damage.Overheated ? 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 1f;
            damageWarning.color = color;
        }

        private void UpdateRecovery()
        {
            if (recoveryPrompt == null || recovery == null) return;

            bool show = recovery.Available;
            GameObject root = recoveryRoot != null ? recoveryRoot : recoveryPrompt.gameObject;
            if (root.activeSelf != show)
                root.SetActive(show);
            if (!show) return;

            float progress = recovery.HoldProgress;
            string text = (recovery.InWater ? "Машина в воде" : recovery.Flipped ? "Машина перевернулась" : "Машина застряла")
                          + "\nУдерживайте " + GameKeys.Tag(GameAction.Recover) + ", чтобы поставить её на дорогу";
            if (progress > 0f)
                text += "  " + Mathf.RoundToInt(progress * 100f) + "%";
            if (recoveryPrompt.text != text)
                recoveryPrompt.text = text;
        }

        private void UpdateCar()
        {
            if (car == null) return;

            if (speedGauge != null)
            {
                float speed = car.SpeedKmh;
                speedGauge.SetValue(speed / maxSpeedKmh, Mathf.RoundToInt(speed).ToString());
            }

            if (gearText != null && car.CurrentGear != shownGear)
            {
                shownGear = car.CurrentGear;
                gearText.text = shownGear == 0 ? "N" : shownGear == 7 ? "R" : shownGear.ToString();
            }

            if (nitroBar != null && nitro != null)
                nitroBar.SetValue(nitro.Charge01, null, nitro.Boosting);
        }

        private void UpdateHealth()
        {
            if (health == null || health.MaxHealth <= 0) return;

            float value = (float)health.CurrentHealth / health.MaxHealth;
            string text = Mathf.CeilToInt(value * 100f) + "%";
            bool low = value < 0.25f;
            if (healthGauge != null) healthGauge.SetValue(value, text);
            if (healthBar != null) healthBar.SetValue(value, text, low);
        }

        private void UpdateGun()
        {
            if (gun == null) return;

            float heat = gun.Heat01;
            string text = Mathf.RoundToInt(heat * 100f) + "%";
            if (heatGauge != null) heatGauge.SetValue(heat, text);
            if (heatBar != null) heatBar.SetValue(heat, null, gun.IsOverheated);

            if (overheatWarning != null)
            {
                bool show = gun.IsOverheated;
                if (overheatWarning.gameObject.activeSelf != show)
                    overheatWarning.gameObject.SetActive(show);
                if (show)
                {
                    Color color = overheatWarning.color;
                    color.a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
                    overheatWarning.color = color;
                }
            }
        }

        private void UpdateRound()
        {
            if (enemies == null) return;

            SetNumber(waveText, Mathf.Max(enemies.Wave, 0), ref shownWave);
            SetNumber(killsText, enemies.Kills, ref shownKills);
            SetNumber(scoreText, enemies.Score, ref shownScore);

            if (comboText != null && enemies.Combo != shownCombo)
            {
                shownCombo = enemies.Combo;
                comboText.text = shownCombo > 1 ? "×" + shownCombo : string.Empty;
            }

            float boss = enemies.BossHealth01;
            bool hasBoss = boss >= 0f;
            if (bossRoot != null && bossRoot.activeSelf != hasBoss)
                bossRoot.SetActive(hasBoss);
            if (hasBoss && bossBar != null)
                bossBar.SetValue(boss, Mathf.CeilToInt(boss * 100f) + "%");
        }

        private static void SetNumber(TMP_Text text, int value, ref int shown)
        {
            if (text == null || value == shown) return;
            shown = value;
            text.text = value.ToString();
        }
    }
}
