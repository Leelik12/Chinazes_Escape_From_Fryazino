using System.Collections.Generic;
using RacingProject.Management;
using RacingProject.Network;
using RacingProject.Pickups;
using Unity.Netcode;
using UnityEngine;

namespace RacingProject.Enemy
{
    // Задания волн: с fromWave волна иногда приходит с поручением штаба, и кончается она, только когда задание
    // выполнено или провалено (EnemyManager.TryEndWave). Виды:
    // — бочка топлива: проехать через точку погрузки и довезти бочку до блокпоста; сильный удар — бочка рвётся;
    // — удержать блокпост: суммарно holdSeconds в круге (вне круга отсчёт стоит);
    // — раненый: остановиться у него, подождать погрузки, довезти до санчасти и снова остановиться;
    //   если машине достанется слишком много, раненый не доживёт;
    // — колонна: convoySize машин противника за convoyTime — кто не уничтожен, уходит (пропадает без очков).
    // Точки — на дорогах RoadNetwork. Состояние считает сервер, остальным его рассылают NetworkVariable;
    // у всех над точкой светится столб маяка, на мониторе — строка задания и стрелка к точке у края экрана
    public class WaveObjectives : NetworkBehaviour
    {
        public enum Kind : byte { None, Fuel, Hold, Wounded, Convoy }

        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private PlayerHealth carHealth;
        [SerializeField] private RadioChatter radio;
        [SerializeField] private RepairKits repairKits;
        [SerializeField] private Material beaconMaterial;

        [Header("Когда")]
        [SerializeField] private int fromWave = 2;
        [Range(0f, 1f)] [SerializeField] private float chance = 0.6f;

        [Header("Точки")]
        [SerializeField] private Vector2 pickupDistance = new Vector2(120f, 300f);
        [SerializeField] private Vector2 deliveryDistance = new Vector2(250f, 450f);
        [SerializeField] private float pointRadius = 9f;
        [Tooltip("Ниже этой скорости машина «стоит» (погрузка раненого), м/с")]
        [SerializeField] private float stopSpeed = 2.5f;
        [SerializeField] private float loadTime = 2.5f;

        [Header("Задания")]
        [SerializeField] private float fuelTime = 130f;
        [Tooltip("Удар сильнее этого (урон за раз) — бочка рвётся")]
        [SerializeField] private int fuelHitLimit = 120;
        [SerializeField] private float holdSeconds = 60f;
        [SerializeField] private float holdRadius = 18f;
        [SerializeField] private float holdTime = 180f;
        [SerializeField] private float woundedTime = 160f;
        [Tooltip("Столько урона машина может получить, пока раненый внутри")]
        [SerializeField] private int woundedDamageLimit = 250;
        [SerializeField] private int convoySize = 3;
        [SerializeField] private float convoyTime = 100f;

        [Header("Награда")]
        [SerializeField] private int rewardPerWave = 150;

        public static WaveObjectives Instance { get; private set; }

        // Сервер: задание идёт — волна не закончится
        public bool Active => serverKind != Kind.None;

        // У всех: что сейчас показывать
        public Kind Current => (Kind)kind.Value;
        public int Stage => stage.Value;
        public Vector3 Point => point.Value;
        public float TimeLeft => timeLeft.Value;
        public float Progress => progress.Value;
        public float Radius => Current == Kind.Hold ? holdRadius : pointRadius;

        private readonly NetworkVariable<byte> kind = new NetworkVariable<byte>();
        private readonly NetworkVariable<byte> stage = new NetworkVariable<byte>();
        private readonly NetworkVariable<Vector3> point = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<float> timeLeft = new NetworkVariable<float>();
        private readonly NetworkVariable<float> progress = new NetworkVariable<float>();

        private Kind serverKind;
        private Kind lastKind;
        private float deadline;
        private Vector3 target;
        private Vector3 delivery;
        private float counter;
        private int damageTaken;
        private bool failedByDamage;
        private Vector3 lastCarPosition;
        private float carSpeed;
        private readonly List<GameObject> convoy = new List<GameObject>();
        private readonly List<Vector3> candidates = new List<Vector3>();

        private Transform beacon;
        private Renderer beaconColumn;
        private Renderer beaconRing;
        private MaterialPropertyBlock block;
        private GUIStyle style;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            if (enemyManager != null) enemyManager.WaveStarted += OnWaveStarted;
            if (carHealth != null) carHealth.OnDamageTaken += OnCarDamaged;
        }

        public override void OnNetworkDespawn()
        {
            if (enemyManager != null) enemyManager.WaveStarted -= OnWaveStarted;
            if (carHealth != null) carHealth.OnDamageTaken -= OnCarDamaged;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        // ---------- сервер ----------

        private void OnWaveStarted(int wave)
        {
            if (!IsServer || serverKind != Kind.None || carHealth == null) return;
            if (wave < fromWave || enemyManager.BossAlive || Random.value > chance) return;
            var kinds = new List<Kind> { Kind.Fuel, Kind.Hold, Kind.Wounded, Kind.Convoy };
            kinds.Remove(lastKind);
            Begin(kinds[Random.Range(0, kinds.Count)]);
        }

        private void Begin(Kind next)
        {
            Vector3 car = carHealth.transform.position;
            lastCarPosition = car;
            counter = 0f;
            damageTaken = 0;
            failedByDamage = false;
            convoy.Clear();
            string text;
            switch (next)
            {
                case Kind.Fuel:
                    if (!RoadPoint(car, pickupDistance, out target) || !RoadPoint(target, deliveryDistance, out delivery)) return;
                    deadline = Time.time + fuelTime;
                    text = $"«Тахо», на дороге бочка солярки, {Where(target)}. Заберите и довезите до блокпоста. Не бейте машину — рванёт!";
                    break;
                case Kind.Hold:
                    if (!RoadPoint(car, pickupDistance, out target)) return;
                    deadline = Time.time + holdTime;
                    text = $"Нужно удержать блокпост {Where(target)}: {Mathf.RoundToInt(holdSeconds)} секунд, пока подтянутся наши.";
                    break;
                case Kind.Wounded:
                    if (!RoadPoint(car, pickupDistance, out target) || !RoadPoint(target, deliveryDistance, out delivery)) return;
                    deadline = Time.time + woundedTime;
                    text = $"У нас раненый {Where(target)}. Остановитесь рядом, заберите и везите в санчасть. Аккуратно!";
                    break;
                case Kind.Convoy:
                    convoy.AddRange(enemyManager.SpawnConvoy(convoySize));
                    if (convoy.Count == 0) return;
                    deadline = Time.time + convoyTime;
                    target = ConvoyCenter();
                    text = $"Перехват: колонна противника {Where(target)}, {convoy.Count} машины. Уйдут через {Mathf.RoundToInt(convoyTime)} секунд — не дайте!";
                    break;
                default:
                    return;
            }
            serverKind = next;
            lastKind = next;
            kind.Value = (byte)next;
            stage.Value = 0;
            point.Value = target;
            progress.Value = 0f;
            timeLeft.Value = deadline - Time.time;
            Say(text);
        }

        private void OnCarDamaged(int damage)
        {
            if (serverKind == Kind.None || stage.Value != 1) return;
            if (serverKind == Kind.Fuel && damage >= fuelHitLimit) failedByDamage = true;
            if (serverKind == Kind.Wounded)
            {
                damageTaken += damage;
                if (damageTaken >= woundedDamageLimit) failedByDamage = true;
            }
        }

        private void Update()
        {
            if (IsServer && IsSpawned) ServerUpdate();
            UpdateBeacon();
        }

        private void ServerUpdate()
        {
            if (serverKind == Kind.None || carHealth == null) return;
            float dt = Time.deltaTime;
            Vector3 car = carHealth.transform.position;
            // Скорость — по смещению: у сервера машина может быть и чужой (её двигает NetworkTransform)
            carSpeed = Mathf.Lerp(carSpeed, (car - lastCarPosition).magnitude / Mathf.Max(1e-4f, dt), 1f - Mathf.Exp(-6f * dt));
            lastCarPosition = car;
            float left = deadline - Time.time;
            if (Mathf.Abs(timeLeft.Value - left) > 0.2f) timeLeft.Value = Mathf.Max(0f, left);

            if (carHealth.IsDead) return;
            if (failedByDamage)
            {
                Finish(false, serverKind == Kind.Fuel ? "Бочка рванула! Солярки не будет — держитесь сами." : "Раненый не доехал… Мы его потеряли.");
                return;
            }
            if (left <= 0f)
            {
                Finish(false, serverKind == Kind.Convoy ? "Колонна ушла. Плохо, «Тахо»." : "Не успели. Задание отменяю, работайте по противнику.");
                return;
            }

            bool inside = Flat(car - target) <= (serverKind == Kind.Hold ? holdRadius : pointRadius);
            switch (serverKind)
            {
                case Kind.Fuel:
                    if (inside && stage.Value == 0) NextStage("Бочка на борту. Блокпост — " + Where(delivery) + ".");
                    else if (inside && stage.Value == 1) Finish(true, "Солярка у блокпоста! Спасибо, «Тахо», держите ремкомплект.");
                    break;
                case Kind.Hold:
                    if (inside) counter += dt;
                    SetProgress(counter / holdSeconds);
                    if (counter >= holdSeconds) Finish(true, "Блокпост держится, наши на месте. Отличная работа!");
                    break;
                case Kind.Wounded:
                    if (inside && carSpeed < stopSpeed) counter += dt;
                    else counter = Mathf.Max(0f, counter - dt * 2f);
                    SetProgress(counter / loadTime);
                    if (counter < loadTime) break;
                    counter = 0f;
                    if (stage.Value == 0) NextStage("Раненый в машине. Санчасть — " + Where(delivery) + ". Не подставляйтесь!");
                    else Finish(true, "Раненого приняли, будет жить. Спасибо, мужики.");
                    break;
                case Kind.Convoy:
                    convoy.RemoveAll(e => e == null || e.GetComponent<EnemyHealth>() == null || e.GetComponent<EnemyHealth>().HealthFraction <= 0f);
                    if (convoy.Count == 0)
                    {
                        Finish(true, "Колонна уничтожена! Так держать.");
                        break;
                    }
                    target = ConvoyCenter();
                    if ((point.Value - target).sqrMagnitude > 4f) point.Value = target;
                    SetProgress(1f - convoy.Count / (float)convoySize);
                    break;
            }
        }

        private void NextStage(string text)
        {
            stage.Value = 1;
            target = delivery;
            point.Value = target;
            counter = 0f;
            SetProgress(0f);
            Say(text);
        }

        private void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(progress.Value - value) > 0.01f || (value >= 1f && progress.Value < 1f)) progress.Value = value;
        }

        private void Finish(bool success, string text)
        {
            if (!success && serverKind == Kind.Convoy)
                foreach (GameObject enemy in convoy) enemyManager.RemoveEscaped(enemy);
            convoy.Clear();
            serverKind = Kind.None;
            kind.Value = (byte)Kind.None;
            if (success)
            {
                enemyManager.AddScore(rewardPerWave * Mathf.Max(1, enemyManager.Wave));
                if (repairKits != null) repairKits.Drop(carHealth.transform.position + carHealth.transform.forward * 6f);
            }
            Say(text);
            enemyManager.TryEndWave();
        }

        private void Say(string text)
        {
            if (radio != null) radio.SayObjective(text);
        }

        private Vector3 ConvoyCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (GameObject enemy in convoy)
                if (enemy != null) { sum += enemy.transform.position; count++; }
            return count > 0 ? sum / count : target;
        }

        private string Where(Vector3 position)
        {
            Vector3 to = position - carHealth.transform.position;
            return $"в {Mathf.RoundToInt(Flat(to) / 10f) * 10} м {RadioChatter.CompassTo(to)}";
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        // Случайная точка дороги на нужном расстоянии от from
        private bool RoadPoint(Vector3 from, Vector2 range, out Vector3 result)
        {
            result = from;
            RoadNetwork roads = RoadNetwork.Instance;
            if (roads == null || roads.Roads == null) return false;
            candidates.Clear();
            foreach (RoadNetwork.Road road in roads.Roads)
                foreach (Vector3 p in road.points)
                {
                    float d = Flat(p - from);
                    if (d >= range.x && d <= range.y) candidates.Add(p);
                }
            if (candidates.Count == 0) return false;
            result = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        // ---------- у всех: маяк и строка задания ----------

        private void UpdateBeacon()
        {
            Kind current = IsSpawned ? Current : Kind.None;
            if (current == Kind.None)
            {
                if (beacon != null) beacon.gameObject.SetActive(false);
                return;
            }
            if (beacon == null) BuildBeacon();
            if (beacon == null) return;
            beacon.gameObject.SetActive(true);
            beacon.position = Vector3.Lerp(beacon.position, Point, beacon.position == Vector3.zero ? 1f : 1f - Mathf.Exp(-4f * Time.deltaTime));
            float radius = Radius;
            beaconRing.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
            Color color = BeaconColor;
            color.a *= 0.7f + 0.3f * Mathf.Sin(Time.time * 3f);
            block.SetColor(BaseColorId, color);
            beaconColumn.SetPropertyBlock(block);
            color.a *= 0.6f;
            block.SetColor(BaseColorId, color);
            beaconRing.SetPropertyBlock(block);
        }

        public Color BeaconColor
        {
            get
            {
                switch (Current)
                {
                    case Kind.Convoy: return new Color(1f, 0.3f, 0.2f, 0.6f);
                    case Kind.Hold: return new Color(0.35f, 0.65f, 1f, 0.6f);
                    default: return Stage == 0 ? new Color(1f, 0.8f, 0.25f, 0.6f) : new Color(0.35f, 1f, 0.45f, 0.6f);
                }
            }
        }

        private void BuildBeacon()
        {
            if (beaconMaterial == null) return;
            block = new MaterialPropertyBlock();
            beacon = new GameObject("ObjectiveBeacon").transform;
            beacon.position = Point;
            beaconColumn = MakePart("Column", new Vector3(0f, 40f, 0f), new Vector3(2.2f, 40f, 2.2f));
            beaconRing = MakePart("Ring", new Vector3(0f, 0.3f, 0f), Vector3.one);
        }

        private Renderer MakePart(string name, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(part.GetComponent<Collider>());
            part.name = name;
            part.transform.SetParent(beacon, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = beaconMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        // Строка задания под счётом волны: что делать, сколько до точки, сколько времени
        public string Describe()
        {
            switch (Current)
            {
                case Kind.Fuel: return Stage == 0 ? "Заберите бочку солярки" : "Довезите бочку до блокпоста — без сильных ударов";
                case Kind.Hold: return $"Удержите блокпост: {Mathf.RoundToInt(Progress * holdSeconds)} / {Mathf.RoundToInt(holdSeconds)} с";
                case Kind.Wounded:
                    string action = Stage == 0 ? "Заберите раненого" : "Довезите раненого в санчасть";
                    return Progress > 0.01f && Progress < 1f ? action + $" — стойте ({Mathf.RoundToInt(Progress * 100f)} %)" : action + " — остановитесь у точки";
                case Kind.Convoy: return $"Уничтожьте колонну: {Mathf.RoundToInt(Progress * convoySize)} / {convoySize}";
                default: return null;
            }
        }

        private void OnGUI()
        {
            if (!IsSpawned || Current == Kind.None || ViewModeService.IsVR) return;
            if (LocalPlayerRole.Current == PlayerRole.None) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            string text = Describe();
            Camera cam = Camera.main;
            if (cam != null)
                text += $"   ·   {Mathf.RoundToInt(Flat(Point - cam.transform.position))} м";
            int seconds = Mathf.CeilToInt(TimeLeft);
            text += $"   ·   {seconds / 60}:{seconds % 60:00}";
            Color saved = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            Vector2 size = style.CalcSize(new GUIContent(text)) + new Vector2(28f, 12f);
            GUI.Box(new Rect((Screen.width - size.x) * 0.5f, 92f, size.x, size.y), text, style);
            GUI.color = saved;
        }
    }
}
