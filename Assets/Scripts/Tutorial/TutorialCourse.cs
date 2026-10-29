using System;
using System.Collections;
using TMPro;
using UnityEngine;
using RacingProject.Car;
using RacingProject.Management;
using RacingProject.Network;
using RacingProject.Props;
using RacingProject.Turret;

namespace RacingProject.Tutorial
{
    // Режим «Обучение»: полигон из сцены Tutorial, которую RoomController подгружает поверх города.
    // Машина игроков ставится на старт полигона, волны врагов не идут, а шаги обучения сменяют друг друга:
    // подсказка вверху экрана, зелёный маяк — куда ехать, «Выполнено» — шаг засчитан. Игра ничего не делает
    // за игрока: в тупик он заезжает сам, сам переворачивается на трамплине, а подсказка подстраивается под то,
    // что с машиной происходит. В конце — экран «Обучение пройдено» с выходом в меню или сразу в одиночную игру
    public class TutorialCourse : MonoBehaviour
    {
        public const string SceneName = "Tutorial";
        private const int StepCount = 13;

        // У трамплина (в этом радиусе, м) машина на шаге «Трамплин» теряет устойчивость: стабилизатор выключен,
        // центр масс поднят на LooseCenterHeight. Иначе её почти невозможно опрокинуть — так она устроена для боя
        private const float RampZone = 30f;
        private const float LooseCenterHeight = 1f;
        // Гашение вращения уже лежащей на боку или крыше машины: без него она докручивается обратно на колёса
        private const float FlippedAngularDamping = 6f;
        private const string RampText = "Впереди трамплин на левой полосе.\nРазгонитесь и заскочите на него.";
        private const string RetryText = "Машина устояла. Развернитесь и заскочите\nна трамплин ещё раз — быстрее.";
        private static string FlippedText => "Машина перевернулась — так бывает и в бою.\nУдерживайте " + GameKeys.Tag(GameAction.Recover) + ", и она встанет на колёса.";

        [Header("Полигон")]
        [SerializeField] private Transform start;
        [SerializeField] private Transform driveBeacon;
        [SerializeField] private Transform turnBeacon;
        [Tooltip("Конец тупика между гаражами: из него выезжают задним ходом")]
        [SerializeField] private Transform deadEndBeacon;
        [SerializeField] private Transform rangeBeacon;
        [Tooltip("Маяк на дороге перед трамплином")]
        [SerializeField] private Transform rampBeacon;
        [Tooltip("Трамплин под левые колёса, на котором машина переворачивается")]
        [SerializeField] private Transform ramp;
        [Tooltip("Где игрок останавливается перед засадой")]
        [SerializeField] private Transform stopBeacon;
        [Tooltip("Гранатомётчик на холме: стреляет, когда машина встала у stopBeacon")]
        [SerializeField] private TutorialRpg rpg;
        [SerializeField] private Transform kitBeacon;
        [Tooltip("Ремкомплект у маяка kitBeacon: появляется на шаге ремонта")]
        [SerializeField] private GameObject repairKit;
        [Tooltip("Мишени шага стрельбы")]
        [SerializeField] private TutorialTarget[] targets = new TutorialTarget[0];
        [Tooltip("Горящая бочка у отдельной мишени")]
        [SerializeField] private ExplosiveBarrel barrel;
        [Tooltip("На каком расстоянии от маяка он считается достигнутым, м")]
        [SerializeField] private float beaconRadius = 8f;

        [Header("Экран")]
        [SerializeField] private GameObject stepPanel;
        [SerializeField] private TMP_Text stepCounter;
        [SerializeField] private TMP_Text stepTitle;
        [SerializeField] private TMP_Text stepText;
        [SerializeField] private TMP_Text stepProgress;
        [Tooltip("Надпись «Выполнено» после каждого шага")]
        [SerializeField] private GameObject doneBanner;
        [SerializeField] private GameObject finishScreen;
        [Tooltip("Звук засчитанного шага")]
        [SerializeField] private AudioSource doneSound;
        [Tooltip("Звук подобранного ремкомплекта")]
        [SerializeField] private AudioSource pickupSound;

        private RoomController room;
        private LanLobby lobby;
        private Rigidbody body;
        private PlayerHealth health;
        private CarNitro nitro;
        private CarRecovery recovery;
        private VRGun gun;
        private CarStabilizer stabilizer;
        private bool recovered;
        private bool loose;
        private Vector3 savedCenterOfMass;
        private float savedAngularDamping;

        private void Start()
        {
            room = FindFirstObjectByType<RoomController>();
            if (room == null || !room.Tutorial || room.car == null)
            {
                Debug.LogWarning("TutorialCourse: сцена обучения открыта без режима «Обучение» — полигон не запускается");
                enabled = false;
                return;
            }

            lobby = room.GetComponent<LanLobby>();
            GameObject car = room.car;
            body = car.GetComponent<Rigidbody>();
            health = car.GetComponent<PlayerHealth>();
            nitro = car.GetComponent<CarNitro>();
            recovery = car.GetComponent<CarRecovery>();
            stabilizer = car.GetComponent<CarStabilizer>();
            TurretGunAim turret = car.GetComponentInChildren<TurretGunAim>(true);
            gun = turret != null ? turret.GetComponent<VRGun>() : null;
            if (recovery != null) recovery.Recovered += OnRecovered;

            // Бочка полигона взрывается так же, как бочки города
            if (barrel != null && PropNetwork.Instance != null)
                PropNetwork.Instance.Register(barrel);

            PlaceCar(start.position, start.rotation);
            ShowBeacon(null);
            if (repairKit != null) repairKit.SetActive(false);
            stepPanel.SetActive(false);
            doneBanner.SetActive(false);
            finishScreen.SetActive(false);
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (recovery != null) recovery.Recovered -= OnRecovered;
            SetLoose(false);
            PauseMenu.OverlayOpen = false;
        }

        private void Update()
        {
            // Машина на полигоне не гибнет: совсем разбитая чинится сама
            if (health != null && health.CurrentHealth < health.MaxHealth * 3 / 10)
                health.Heal(health.MaxHealth);
        }

        private IEnumerator Run()
        {
            // Шаги начинаются, когда бункер выпустил экипаж и включился риг одиночной игры
            yield return new WaitUntil(() => LocalPlayerRole.IsSolo);
            // Табло волны и счёта в обучении не нужно
            Transform tactical = room.soloRig != null ? room.soloRig.transform.Find("SoloHud/TacticalDisplay") : null;
            if (tactical != null) tactical.gameObject.SetActive(false);
            stepPanel.SetActive(true);

            yield return Step(1, "Газ", "Нажмите " + GameKeys.Tag(GameAction.Throttle) + ", чтобы поехать.\nДоедьте по дороге до зелёного маяка.",
                driveBeacon, () => Reached(driveBeacon));

            yield return Step(2, "Руль", "" + GameKeys.Tag(GameAction.SteerLeft) + " и " + GameKeys.Tag(GameAction.SteerRight) + " поворачивают руль.\nПройдите повороты до следующего маяка.",
                turnBeacon, () => Reached(turnBeacon));

            yield return Step(3, "Тупик", "Сверните к гаражам и заедьте в тупик до маяка.",
                deadEndBeacon, () => Reached(deadEndBeacon));

            float reverse = 0f;
            yield return Step(4, "Задний ход",
                "" + GameKeys.Tag(GameAction.Brake) + " — тормоз, а на месте " + GameKeys.Tag(GameAction.Brake) + " включает задний ход.\nВыедьте из тупика задом.",
                null, () =>
                {
                    if (Vector3.Dot(body.linearVelocity, body.transform.forward) < -1.5f) reverse += Time.deltaTime;
                    return reverse >= 2f;
                }, () => "Задний ход: " + Percent(reverse / 2f));

            float boost = 0f;
            yield return Step(5, "Нитро", "Вернитесь на дорогу. На прямой нажмите " + GameKeys.Tag(GameAction.Nitro) + " — нитро,\nкороткий мощный разгон.",
                null, () =>
                {
                    if (nitro != null && nitro.Boosting) boost += Time.deltaTime;
                    return boost >= 1.5f;
                }, () => "Нитро: " + Percent(boost / 1.5f));

            yield return Step(6, "Стрельбище", "Доедьте до маяка у стрельбища.",
                rangeBeacon, () => Reached(rangeBeacon));

            yield return Step(7, "Стрельба",
                "<b>Мышь</b> — прицел, <b>ЛКМ</b> — огонь. Пулемёт на крыше поворачивается во все стороны.\nУничтожьте подсвеченные машины-мишени — одна стоит у вас за спиной.",
                null, () => DestroyedTargets() == targets.Length, () => "Мишени: " + DestroyedTargets() + " / " + targets.Length);

            yield return Step(8, "Горящая бочка",
                "Горящие бочки взрываются от пуль и ранят всех, кто рядом.\nВыстрелите в бочку возле последней мишени.",
                null, () => barrel == null || barrel.Exploded);

            yield return Step(9, "Перегрев",
                "Пулемёт греется от стрельбы — шкала справа внизу.\nСтреляйте не отпуская, пока ствол не перегреется.",
                null, () => gun == null || gun.IsOverheated, () => "Нагрев: " + Percent(gun != null ? gun.Heat01 : 1f), false);
            yield return Step(9, "Перегрев", "Перегретый ствол не стреляет.\nПодождите, пока он остынет.",
                null, () => gun == null || !gun.IsOverheated, () => "Нагрев: " + Percent(gun != null ? gun.Heat01 : 0f));

            yield return Ramp();

            yield return Ambush();

            if (repairKit != null) repairKit.SetActive(true);
            string repairText = health.CurrentHealth < health.MaxHealth
                ? "Граната сняла много прочности — она показана слева внизу.\nПроедьте через ремкомплект у маяка: он чинит машину."
                : "Ремкомплекты на дорогах чинят машину — в бою они выручат.\nПроедьте через ремкомплект у маяка.";
            yield return Step(12, "Ремонт", repairText, kitBeacon, () => repairKit == null || Near(repairKit.transform, 4.5f));
            if (repairKit != null) repairKit.SetActive(false);
            health.Heal(health.MaxHealth);
            if (pickupSound != null) pickupSound.Play();

            bool paused = false;
            yield return Step(13, "Пауза", "<b>Esc</b> — пауза: игра останавливается, из паузы можно выйти в меню.\nОткройте паузу и вернитесь в игру.",
                null, () =>
                {
                    if (PauseMenu.IsOpen) paused = true;
                    return paused && !PauseMenu.IsOpen;
                });

            Finish();
        }

        // Трамплин: игрок сам заезжает на него, машину кладёт на бок или крышу, и игра сама подсказывает про R.
        // Устояла — подсказка предлагает попробовать ещё раз. Засчитывается, когда перевернувшуюся машину
        // поставили на колёса
        private IEnumerator Ramp()
        {
            recovered = false;
            ShowStep(10, "Трамплин", RampText, rampBeacon);
            bool overturned = false, wasNear = false;
            while (recovery != null)
            {
                bool near = Near(ramp, RampZone);
                // R засчитывается только после переворота на трамплине
                if (recovered && overturned) break;
                recovered = false;
                SetLoose(near || overturned);
                if (loose) body.angularDamping = recovery.Flipped ? FlippedAngularDamping : savedAngularDamping;

                if (!overturned && near && recovery.Flipped)
                {
                    overturned = true;
                    stepText.text = FlippedText;
                    ShowBeacon(null);
                }
                else if (!overturned && wasNear && !near)
                {
                    // Проехал трамплин и устоял: пусть попробует ещё раз
                    stepText.text = RetryText;
                }
                wasNear = near;
                yield return null;
            }
            SetLoose(false);
            ShowBeacon(null);
            yield return Done();
        }

        // Засада: игрок останавливается у маяка, и гранатомётчик с холма попадает в машину самонаводящейся гранатой.
        // Граната оставляет 40% прочности (если было больше) — дальше шаг ремонта
        private IEnumerator Ambush()
        {
            ShowStep(11, "Засада", "Доедьте до маяка и остановитесь полностью.", stopBeacon);
            stepProgress.text = "";
            yield return new WaitUntil(() => Reached(stopBeacon) && body.linearVelocity.magnitude < 1.5f);
            ShowBeacon(null);
            stepText.text = "Засада! С холма бьёт гранатомётчик — граната уже летит.\nТак в городе встречают из окон и с машин.";
            yield return new WaitForSeconds(0.6f);

            bool hit = rpg == null;
            if (rpg != null)
            {
                int damage = Mathf.Max(0, health.CurrentHealth - health.MaxHealth * 4 / 10);
                rpg.Fire(body, health, damage, () => hit = true);
            }
            yield return new WaitUntil(() => hit);
            yield return new WaitForSeconds(1.2f);
            yield return Done();
        }

        // Ослабляет устойчивость машины у трамплина и возвращает обычную
        private void SetLoose(bool value)
        {
            if (value == loose || body == null) return;
            loose = value;
            if (loose)
            {
                savedCenterOfMass = body.centerOfMass;
                savedAngularDamping = body.angularDamping;
            }
            body.centerOfMass = loose ? new Vector3(0f, LooseCenterHeight, 0f) : savedCenterOfMass;
            body.angularDamping = savedAngularDamping;
            if (stabilizer != null) stabilizer.enabled = !loose;
        }

        private void OnRecovered()
        {
            recovered = true;
        }

        // Показывает шаг и ждёт его выполнения; done проверяется каждый кадр
        private IEnumerator Step(int number, string title, string text, Transform beacon, Func<bool> done,
            Func<string> progress = null, bool showDone = true)
        {
            ShowStep(number, title, text, beacon);
            while (!done())
            {
                if (progress != null) stepProgress.text = progress();
                yield return null;
            }
            if (progress != null) stepProgress.text = progress();
            ShowBeacon(null);
            if (showDone)
                yield return Done();
        }

        private void ShowStep(int number, string title, string text, Transform beacon)
        {
            stepCounter.text = "Обучение · шаг " + number + " из " + StepCount;
            stepTitle.text = title;
            stepText.text = text;
            stepProgress.text = "";
            ShowBeacon(beacon);
        }

        private IEnumerator Done()
        {
            doneBanner.SetActive(true);
            if (doneSound != null) doneSound.Play();
            yield return new WaitForSeconds(1.2f);
            doneBanner.SetActive(false);
        }

        private void Finish()
        {
            stepPanel.SetActive(false);
            finishScreen.SetActive(true);
            // Экран забирает мышь у камеры и пулемёта, как пауза
            PauseMenu.OverlayOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Кнопки экрана «Обучение пройдено»
        public void ToMenu()
        {
            PauseMenu.OverlayOpen = false;
            if (lobby != null) lobby.OnLeavePressed();
        }

        public void StartGame()
        {
            PauseMenu.OverlayOpen = false;
            LanLobby.StartSoloAfterReload();
            if (lobby != null) lobby.OnLeavePressed();
        }

        private void ShowBeacon(Transform beacon)
        {
            foreach (Transform b in new[] { driveBeacon, turnBeacon, deadEndBeacon, rangeBeacon, rampBeacon, stopBeacon, kitBeacon })
                if (b != null) b.gameObject.SetActive(b == beacon);
        }

        private bool Reached(Transform beacon)
        {
            return Near(beacon, beaconRadius);
        }

        private bool Near(Transform point, float radius)
        {
            Vector3 offset = point.position - body.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        private int DestroyedTargets()
        {
            int count = 0;
            foreach (TutorialTarget target in targets)
                if (target == null || target.Destroyed) count++;
            return count;
        }

        private void PlaceCar(Vector3 position, Quaternion rotation)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            body.transform.SetPositionAndRotation(position, rotation);
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }
    }
}
