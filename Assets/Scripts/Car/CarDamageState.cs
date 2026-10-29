using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using RacingProject.Audio;
using RacingProject.Enemy;
using RacingProject.Network;

namespace RacingProject.Car
{
    // Поломки машины игроков, которые чувствуются в управлении. Решает сервер по каждому попаданию
    // (PlayerHealth.ServerHit: урон и откуда он пришёл), состояние уходит обоим игрокам в NetworkVariable:
    //  - пробитое колесо: машину тянет в его сторону, оно хуже держит дорогу, спущенная шина садится,
    //    голый диск искрит и скрежещет об асфальт;
    //  - пробитый радиатор (урон спереди): из-под капота пар, на газу мотор греется и теряет мощность;
    //  - трещины на лобовом стекле от попаданий спереди и разбитые пулями фары.
    // Ремкомплект (RepairKits) чинит всё сразу
    public class CarDamageState : NetworkBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private CarControllerSample controller;
        [SerializeField] private CarHeadlights headlights;
        [SerializeField] private CarWheelVisuals wheelVisuals;
        [SerializeField] private RadioChatter radio;
        [Tooltip("Колёса: передние, потом задние")]
        [SerializeField] private WheelCollider[] wheels = new WheelCollider[0];
        [Tooltip("С этого урона попадание считается тяжёлым (взрыв, таран, авария), меньше — пуля")]
        [SerializeField] private int heavyHit = 70;

        [Header("Колёса")]
        [SerializeField, Range(0f, 1f)] private float bulletPunctureChance = 0.006f;
        [SerializeField, Range(0f, 1f)] private float heavyPunctureChance = 0.3f;
        [Tooltip("Не чаще одного прокола за это время, с")]
        [SerializeField] private float punctureCooldown = 25f;
        [SerializeField] private int maxFlats = 2;
        [Tooltip("Радиус спущенного колеса, доля целого")]
        [SerializeField] private float flatRadius = 0.88f;
        [Tooltip("Боковое сцепление спущенного колеса, доля целого")]
        [SerializeField] private float flatGrip = 0.6f;
        [Tooltip("Сопротивление спущенного колеса: доля веса машины на каждый м/с скорости")]
        [SerializeField] private float flatDrag = 0.012f;
        [Tooltip("На сколько садится меш спущенного колеса, м")]
        [SerializeField] private float flatVisualDrop = 0.07f;
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private AudioClip rimScrape;

        [Header("Радиатор")]
        [Tooltip("Столько урона спереди пробивает радиатор")]
        [SerializeField] private int frontDamageToBreak = 220;
        [Tooltip("Ниже этой доли прочности радиатор пробит в любом случае")]
        [SerializeField, Range(0f, 1f)] private float radiatorBelowHealth = 0.2f;
        [SerializeField] private ParticleSystem steam;
        [SerializeField] private AudioSource steamHiss;
        [Tooltip("Нагрев на полном газу и остывание без газа, доля в секунду")]
        [SerializeField] private float heatUp = 0.05f;
        [SerializeField] private float coolDown = 0.04f;
        [Tooltip("Мощность совсем перегретого мотора")]
        [SerializeField, Range(0f, 1f)] private float overheatPower = 0.45f;

        [Header("Стекло и фары")]
        [Tooltip("Лобовое стекло: центр, forward — наружу, масштаб — ширина и высота, м")]
        [SerializeField] private Transform windshield;
        [Tooltip("Атлас трещин 2×2, двусторонний прозрачный")]
        [SerializeField] private Material crackMaterial;
        [SerializeField] private int maxCracks = 7;
        [SerializeField, Range(0f, 1f)] private float bulletCrackChance = 0.1f;
        [SerializeField, Range(0f, 1f)] private float bulletHeadlightChance = 0.05f;
        [SerializeField, Range(0f, 1f)] private float heavyHeadlightChance = 0.5f;

        private readonly NetworkVariable<byte> flatMask = new NetworkVariable<byte>();
        private readonly NetworkVariable<bool> radiator = new NetworkVariable<bool>();
        private readonly NetworkVariable<float> heat = new NetworkVariable<float>();
        private readonly NetworkVariable<byte> headlightMask = new NetworkVariable<byte>();
        private readonly NetworkVariable<int> cracks = new NetworkVariable<int>();
        private readonly NetworkVariable<int> crackSeed = new NetworkVariable<int>();

        private float nextPuncture;
        private int frontDamage;
        private bool overheatSaid;
        private float[] baseRadius;
        private float[] baseForward;
        private ParticleSystem[] rimSparks;
        private AudioSource[] rimAudio;
        private readonly List<GameObject> crackQuads = new List<GameObject>();
        private MaterialPropertyBlock block;
        // Скорость машины по смещению: у второго игрока Rigidbody кинематический и своей скорости не знает
        private Vector3 lastPosition;
        private Vector3 velocity;
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        // Для руля G29 и приборов
        public float Heat01 => heat.Value;
        public bool RadiatorBroken => radiator.Value;
        public bool Overheated => heat.Value > 0.85f;
        public int FlatCount { get; private set; }
        // Куда тянет спущенное колесо: −1 влево … 1 вправо
        public float FlatPull { get; private set; }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            baseRadius = new float[wheels.Length];
            baseForward = new float[wheels.Length];
            rimSparks = new ParticleSystem[wheels.Length];
            rimAudio = new AudioSource[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null) continue;
                baseRadius[i] = wheels[i].radius;
                baseForward[i] = wheels[i].forwardFriction.stiffness;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                crackSeed.Value = Random.Range(1, int.MaxValue);
                if (health != null) health.ServerHit += OnServerHit;
            }
            flatMask.OnValueChanged += OnFlatChanged;
            headlightMask.OnValueChanged += OnHeadlightsChanged;
            cracks.OnValueChanged += OnCracksChanged;
            ApplyFlats(flatMask.Value);
            OnHeadlightsChanged(0, headlightMask.Value);
            OnCracksChanged(0, cracks.Value);
        }

        public override void OnNetworkDespawn()
        {
            if (health != null) health.ServerHit -= OnServerHit;
            flatMask.OnValueChanged -= OnFlatChanged;
            headlightMask.OnValueChanged -= OnHeadlightsChanged;
            cracks.OnValueChanged -= OnCracksChanged;
        }

        // ---------- сервер: решение по попаданию ----------

        private void OnServerHit(int damage, Vector3 source)
        {
            if (health.IsDead) return;
            bool heavy = damage >= heavyHit;
            Vector3 local = transform.InverseTransformPoint(source);
            local.y = 0f;
            Vector3 direction = local.sqrMagnitude > 0.01f ? local.normalized : Vector3.forward;

            int flats = CountBits(flatMask.Value);
            if (Time.time >= nextPuncture && flats < maxFlats && Random.value < (heavy ? heavyPunctureChance : bulletPunctureChance))
            {
                int wheel = NearestWheel(direction);
                if (wheel >= 0 && (flatMask.Value & (1 << wheel)) == 0)
                {
                    flatMask.Value = (byte)(flatMask.Value | (1 << wheel));
                    nextPuncture = Time.time + punctureCooldown;
                    TireBurstRpc(wheel);
                    if (radio != null) radio.SayCarEvent(RadioChatter.CarEvent.Flat);
                }
            }

            bool front = direction.z > 0.5f;
            if (front)
            {
                frontDamage += damage;
                if (headlights != null && Random.value < (heavy ? heavyHeadlightChance : bulletHeadlightChance))
                {
                    int lamp = NearestHeadlight(direction);
                    if (lamp >= 0) headlightMask.Value = (byte)(headlightMask.Value | (1 << lamp));
                }
                if (cracks.Value < maxCracks && (heavy || Random.value < bulletCrackChance))
                    cracks.Value = Mathf.Min(maxCracks, cracks.Value + (heavy ? 2 : 1));
            }
            bool wrecked = health.MaxHealth > 0 && (float)health.CurrentHealth / health.MaxHealth < radiatorBelowHealth;
            if (!radiator.Value && (frontDamage >= frontDamageToBreak || wrecked))
            {
                radiator.Value = true;
                if (radio != null) radio.SayCarEvent(RadioChatter.CarEvent.Radiator);
            }
        }

        // Колесо со стороны попадания
        private int NearestWheel(Vector3 direction)
        {
            int best = -1;
            float bestDot = float.MinValue;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null) continue;
                Vector3 local = transform.InverseTransformPoint(wheels[i].transform.position);
                local.y = 0f;
                float dot = Vector3.Dot(local.normalized, direction) + Random.Range(-0.3f, 0.3f);
                if (dot > bestDot) { bestDot = dot; best = i; }
            }
            return best;
        }

        private int NearestHeadlight(Vector3 direction)
        {
            int best = -1;
            float bestDot = float.MinValue;
            for (int i = 0; i < headlights.Count && i < 8; i++)
            {
                Vector3 local = headlights.LocalGlassPosition(i);
                float dot = Mathf.Sign(local.x) * direction.x + Random.Range(-0.4f, 0.4f);
                if (dot > bestDot) { bestDot = dot; best = i; }
            }
            return best;
        }

        // Ремкомплект: чинит всё; false — чинить было нечего
        public bool Repair()
        {
            if (!IsServer) return false;
            bool any = flatMask.Value != 0 || radiator.Value || headlightMask.Value != 0 || cracks.Value > 0 || heat.Value > 0.01f;
            flatMask.Value = 0;
            radiator.Value = false;
            heat.Value = 0f;
            headlightMask.Value = 0;
            cracks.Value = 0;
            crackSeed.Value = Random.Range(1, int.MaxValue);
            frontDamage = 0;
            overheatSaid = false;
            if (headlights != null) RepairHeadlightsRpc();
            return any;
        }

        [Rpc(SendTo.Everyone)]
        private void RepairHeadlightsRpc()
        {
            if (headlights != null) headlights.RepairAll();
        }

        [Rpc(SendTo.Everyone)]
        private void TireBurstRpc(int wheel)
        {
            if (wheel >= 0 && wheel < wheels.Length && wheels[wheel] != null)
                SoundLibrary.Play(SoundLibrary.Sfx.TireBurst, wheels[wheel].transform.position);
        }

        // ---------- состояние у обоих игроков ----------

        private void OnFlatChanged(byte previous, byte current)
        {
            ApplyFlats(current);
        }

        private void ApplyFlats(byte mask)
        {
            FlatCount = CountBits(mask);
            float pull = 0f;
            for (int i = 0; i < wheels.Length; i++)
            {
                WheelCollider wheel = wheels[i];
                if (wheel == null) continue;
                bool flat = (mask & (1 << i)) != 0;
                if (wheelVisuals != null) wheelVisuals.SetFlat(wheel, flat ? flatVisualDrop : 0f);
                // Физику машины считает только тот, кто её ведёт
                wheel.radius = baseRadius[i] * (flat ? flatRadius : 1f);
                WheelFrictionCurve forward = wheel.forwardFriction;
                forward.stiffness = baseForward[i] * (flat ? 0.75f : 1f);
                wheel.forwardFriction = forward;
                if (controller != null) controller.SetDamageGrip(wheel, flat ? flatGrip : 1f);
                if (flat)
                {
                    float side = Mathf.Sign(transform.InverseTransformPoint(wheel.transform.position).x);
                    pull += side * (i < 2 ? 1f : 0.5f);
                    EnsureRimEffects(i);
                }
                else StopRimEffects(i);
            }
            FlatPull = Mathf.Clamp(pull, -1f, 1f);
        }

        private void EnsureRimEffects(int i)
        {
            if (rimSparks[i] == null)
                rimSparks[i] = EnemyWreck.MakeSparks(transform, sparkMaterial);
            if (rimAudio[i] == null && rimScrape != null)
            {
                var source = wheels[i].gameObject.AddComponent<AudioSource>();
                source.clip = rimScrape;
                source.loop = true;
                source.spatialBlend = 1f;
                source.minDistance = 4f;
                source.maxDistance = 60f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                source.volume = 0f;
                source.Play();
                rimAudio[i] = source;
            }
        }

        private void StopRimEffects(int i)
        {
            if (rimSparks[i] != null)
            {
                Destroy(rimSparks[i].gameObject);
                rimSparks[i] = null;
            }
            if (rimAudio[i] != null)
            {
                Destroy(rimAudio[i]);
                rimAudio[i] = null;
            }
        }

        private void OnHeadlightsChanged(byte previous, byte current)
        {
            if (headlights == null) return;
            for (int i = 0; i < headlights.Count && i < 8; i++)
                if ((current & (1 << i)) != 0 && (previous & (1 << i)) == 0)
                {
                    headlights.Break(i);
                    SoundLibrary.Play(SoundLibrary.Sfx.Glass, transform.TransformPoint(headlights.LocalGlassPosition(i)), 0.6f);
                }
        }

        // Трещины: позиции из общего зерна, поэтому у обоих игроков одинаковые
        private void OnCracksChanged(int previous, int current)
        {
            if (windshield == null || crackMaterial == null) return;
            if (current < crackQuads.Count)
            {
                foreach (GameObject quad in crackQuads) Destroy(quad);
                crackQuads.Clear();
            }
            Mesh quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            for (int i = crackQuads.Count; i < current; i++)
            {
                var random = new System.Random(crackSeed.Value + i * 7919);
                var go = new GameObject("Crack " + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(windshield, false);
                float x = ((float)random.NextDouble() - 0.5f) * 0.8f;
                float y = ((float)random.NextDouble() - 0.5f) * 0.75f;
                go.transform.localPosition = new Vector3(x, y, 0.01f);
                go.transform.localRotation = Quaternion.Euler(0f, 180f, (float)random.NextDouble() * 360f);
                float size = Mathf.Lerp(0.28f, 0.5f, (float)random.NextDouble());
                Vector3 scale = windshield.lossyScale;
                go.transform.localScale = new Vector3(size / Mathf.Max(0.01f, scale.x), size / Mathf.Max(0.01f, scale.y), 1f);
                go.GetComponent<MeshFilter>().sharedMesh = quadMesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = crackMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                int cell = random.Next(4);
                block.SetVector(BaseMapSt, new Vector4(0.5f, 0.5f, (cell % 2) * 0.5f, (cell / 2) * 0.5f));
                renderer.SetPropertyBlock(block);
                crackQuads.Add(go);
                if (i == current - 1 && previous < current)
                    SoundLibrary.Play(SoundLibrary.Sfx.Glass, go.transform.position, 0.35f, 1.3f);
            }
        }

        private void Update()
        {
            if (IsServer && IsSpawned) UpdateHeat();
            UpdateSteam();
            if (Time.deltaTime > 0f)
                velocity = Vector3.Lerp(velocity, (transform.position - lastPosition) / Time.deltaTime, 0.3f);
            lastPosition = transform.position;
            UpdateRims();
        }

        // Пробитый радиатор: на газу мотор греется, без газа остывает; перегретый теряет мощность
        private void UpdateHeat()
        {
            float value = heat.Value;
            if (radiator.Value && !health.IsDead)
            {
                float throttle = controller != null ? Mathf.Abs(controller.Throttle) : 0f;
                float target = 0.3f + 0.7f * throttle;
                value = target > value ? Mathf.MoveTowards(value, target, heatUp * throttle * Time.deltaTime)
                                       : Mathf.MoveTowards(value, target, coolDown * Time.deltaTime);
            }
            else value = Mathf.MoveTowards(value, 0f, coolDown * Time.deltaTime);
            if (Mathf.Abs(value - heat.Value) > 0.005f || (value == 0f && heat.Value != 0f)) heat.Value = value;

            if (controller != null)
            {
                float power = radiator.Value ? 0.9f : 1f;
                if (heat.Value > 0.6f) power *= Mathf.Lerp(1f, overheatPower, Mathf.InverseLerp(0.6f, 1f, heat.Value));
                controller.DamagePower = power;
            }
            if (Overheated && !overheatSaid)
            {
                overheatSaid = true;
                if (radio != null) radio.SayCarEvent(RadioChatter.CarEvent.Overheat);
            }
            else if (heat.Value < 0.5f) overheatSaid = false;
        }

        private void UpdateSteam()
        {
            float amount = radiator.Value && (health == null || !health.IsDead) ? Mathf.Lerp(0.25f, 1f, heat.Value) : 0f;
            if (steam != null)
            {
                var emission = steam.emission;
                emission.rateOverTime = amount * 45f;
                if (amount > 0f && !steam.isPlaying) steam.Play();
            }
            if (steamHiss != null)
            {
                steamHiss.volume = Mathf.MoveTowards(steamHiss.volume, amount * 0.55f, Time.deltaTime);
                if (steamHiss.volume > 0.001f && !steamHiss.isPlaying) steamHiss.Play();
                else if (steamHiss.volume <= 0.001f && steamHiss.isPlaying) steamHiss.Stop();
            }
        }

        // Диск спущенного колеса на ходу высекает искры и скрежещет
        private void UpdateRims()
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                if (rimSparks[i] == null || wheels[i] == null) continue;
                Vector3 center = wheels[i].transform.position;
                float radius = baseRadius[i] * wheels[i].transform.lossyScale.y;
                float speed = velocity.magnitude;
                bool touching = Physics.Raycast(center, -transform.up, out RaycastHit hit, radius + wheels[i].suspensionDistance + 0.3f,
                                                ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform);
                float amount = touching ? Mathf.Clamp01((speed - 3f) / 20f) : 0f;
                if (touching) rimSparks[i].transform.position = hit.point + transform.up * 0.05f;
                if (velocity.sqrMagnitude > 0.5f)
                    rimSparks[i].transform.rotation = Quaternion.LookRotation(-velocity.normalized + transform.up * 0.3f);
                var emission = rimSparks[i].emission;
                emission.rateOverTime = amount * 120f;
                if (rimAudio[i] != null)
                {
                    rimAudio[i].volume = Mathf.MoveTowards(rimAudio[i].volume, amount * 0.7f, Time.deltaTime * 3f);
                    rimAudio[i].pitch = 0.85f + amount * 0.35f;
                }
            }
        }

        // Спущенное колесо тормозит свою сторону — машину тянет вбок
        private void FixedUpdate()
        {
            if (!LocalPlayerRole.SimulatesPhysics || flatMask.Value == 0 || controller == null) return;
            Rigidbody body = controller.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic) return;
            for (int i = 0; i < wheels.Length; i++)
            {
                if ((flatMask.Value & (1 << i)) == 0 || wheels[i] == null || !wheels[i].isGrounded) continue;
                Vector3 point = wheels[i].transform.position;
                float forward = Vector3.Dot(body.GetPointVelocity(point), transform.forward);
                body.AddForceAtPosition(-transform.forward * (forward * flatDrag * body.mass * Physics.gravity.magnitude * 0.25f), point);
            }
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }
    }
}
