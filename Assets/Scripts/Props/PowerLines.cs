using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using RacingProject.Enemy;

namespace RacingProject.Props
{
    // Линии электропередачи: бетонные опоры вдоль дорог и деревянные столбы частного сектора. Провода строятся
    // здесь при запуске (запечённые меши строителей скрываются) и перестраиваются, пока опора падает: пролёты
    // остаются привязаны к её крепёжным точкам и ложатся вместе с ней на землю. Опору валит удар машины на скорости
    // или взрыв рядом; решает сервер, падение по одной формуле отыгрывают оба игрока. На верхушке упавшей опоры
    // оборванные провода искрят и бьют током машины рядом, через sparkTime линия обесточивается
    public class PowerLines : NetworkBehaviour
    {
        [System.Serializable]
        public class Line
        {
            public Transform[] poles = new Transform[0];
            [Tooltip("Точки крепления проводов в системе опоры")]
            public Vector3[] attach = new Vector3[0];
            [Tooltip("Запечённые провода строителя: скрываются, провода строятся заново")]
            public Renderer bakedWires;
            [Tooltip("Соседние опоры дальше этого не соединены (между ними опора не поставлена), м")]
            public float maxSpan = 60f;
            [Tooltip("Высота опоры, м: от неё скорость падения")]
            public float height = 19f;
        }

        public static PowerLines Instance { get; private set; }
        // Линии сцены: опоры и высота подвеса (на верхушках сидят вороны, CityBirds)
        public Line[] Lines => lines;
        // Только сервер: опора упала (для рации)
        public static event System.Action<Vector3> ServerToppled;

        [SerializeField] private Line[] lines = new Line[0];
        [SerializeField] private Material wireMaterial;
        [SerializeField] private float wireWidth = 0.03f;
        [Tooltip("Скорость удара, после которой опора падает, м/с")]
        [SerializeField] private float toppleSpeed = 9f;
        [Tooltip("Масса тела, которое может свалить опору (машины, а не предметы разрухи), кг")]
        [SerializeField] private float toppleMass = 600f;
        [Tooltip("Урон машине, на которую упала опора")]
        [SerializeField] private int crushDamage = 150;

        [Header("Оборванные провода")]
        [Tooltip("Образец искр (система частиц сцены, как у сбитого фонаря)")]
        [SerializeField] private ParticleSystem sparksTemplate;
        [SerializeField] private AudioClip arcLoop;
        [SerializeField] private AudioClip[] arcCracks = new AudioClip[0];
        [SerializeField] private AudioClip fallCrack;
        [SerializeField] private AudioClip fallCrash;
        [SerializeField] private AudioMixerGroup output;
        [Tooltip("Сколько искрят провода упавшей опоры, с")]
        [SerializeField] private float sparkTime = 45f;
        [SerializeField] private float shockRadius = 4.5f;
        [Tooltip("Урон за один разряд: машине игроков и врагу")]
        [SerializeField] private int shockDamage = 14;
        [SerializeField] private int enemyShockDamage = 70;
        [SerializeField] private float shockInterval = 0.35f;

        private const float FallenAngle = 84f;
        private const int Segments = 8;

        private class Pole
        {
            public Transform root;
            public Quaternion rest;
            public bool linkedToPrev;
            public bool fallen;
            public bool landed;
            public float angle;
            public float speed;
            public Vector3 axis;
            public float landTime;
            public Collider[] colliders;
            // Искры и звук разряда на верхушке
            public ParticleSystem sparks;
            public AudioSource arc;
            public Light glow;
            public float nextBurst;
        }

        private class LineState
        {
            public Pole[] poles;
            public Mesh mesh;
            public float rest;
            public bool dirty;
        }

        private LineState[] states;
        private readonly List<Pole> sparking = new List<Pole>();
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Vector3> norms = new List<Vector3>();
        private readonly List<int> tris = new List<int>();
        private readonly HashSet<Object> shocked = new HashSet<Object>();
        private float nextShock;

        private void Awake()
        {
            Instance = this;
            states = new LineState[lines.Length];
            for (int li = 0; li < lines.Length; li++)
            {
                Line line = lines[li];
                var state = new LineState { poles = new Pole[line.poles.Length] };
                for (int i = 0; i < line.poles.Length; i++)
                {
                    Transform t = line.poles[i];
                    var pole = new Pole { root = t, rest = t != null ? t.rotation : Quaternion.identity };
                    if (t != null)
                    {
                        pole.colliders = t.GetComponentsInChildren<Collider>();
                        foreach (Collider c in pole.colliders)
                        {
                            var contact = c.gameObject.AddComponent<PowerPoleContact>();
                            contact.owner = this;
                            contact.line = li;
                            contact.pole = i;
                        }
                    }
                    pole.linkedToPrev = i > 0 && t != null && line.poles[i - 1] != null
                        && Vector3.Distance(t.position, line.poles[i - 1].position) <= line.maxSpan;
                    state.poles[i] = pole;
                }
                if (line.bakedWires != null) line.bakedWires.enabled = false;
                state.mesh = new Mesh { name = "PowerLines_" + li, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                var go = new GameObject("PowerLines_" + li);
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                go.AddComponent<MeshFilter>().sharedMesh = state.mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = wireMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                states[li] = state;
                Rebuild(li);
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        // Сервер: удар по опоре (из PowerPoleContact)
        public void OnPoleHit(int li, int pi, Collision collision)
        {
            if (IsSpawned && !IsServer) return;
            Rigidbody body = collision.rigidbody;
            if (body == null || body.mass < toppleMass || collision.relativeVelocity.magnitude < toppleSpeed) return;
            Vector3 dir = -collision.relativeVelocity;
            Topple(li, pi, dir);
        }

        // Сервер: взрыв валит опоры в радиусе — от центра взрыва
        public void ToppleInRadius(Vector3 point, float radius)
        {
            if (IsSpawned && !IsServer) return;
            for (int li = 0; li < states.Length; li++)
                for (int pi = 0; pi < states[li].poles.Length; pi++)
                {
                    Pole pole = states[li].poles[pi];
                    if (pole.root == null || pole.fallen) continue;
                    Vector3 d = pole.root.position - point;
                    d.y = 0f;
                    if (d.magnitude <= radius)
                        Topple(li, pi, d.sqrMagnitude > 0.01f ? d : Vector3.forward);
                }
        }

        // Сервер: стоящие опоры — для событий карты (MapEvents); id — (линия, опора)
        public void StandingPoles(List<Vector3> positions, List<Vector2Int> ids)
        {
            positions.Clear();
            ids.Clear();
            for (int li = 0; li < states.Length; li++)
                for (int pi = 0; pi < states[li].poles.Length; pi++)
                {
                    Pole pole = states[li].poles[pi];
                    if (pole.root == null || pole.fallen) continue;
                    positions.Add(pole.root.position);
                    ids.Add(new Vector2Int(li, pi));
                }
        }

        // Сервер: событие карты — опора падает в сторону точки (поперёк дороги)
        public void ToppleToward(Vector2Int id, Vector3 point)
        {
            if (IsSpawned && !IsServer) return;
            if (id.x < 0 || id.x >= states.Length || id.y < 0 || id.y >= states[id.x].poles.Length) return;
            Pole pole = states[id.x].poles[id.y];
            if (pole.root == null) return;
            Topple(id.x, id.y, point - pole.root.position);
        }

        private void Topple(int li, int pi, Vector3 dir)
        {
            Pole pole = states[li].poles[pi];
            if (pole.root == null || pole.fallen) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = pole.root.forward;
            dir.Normalize();
            ServerToppled?.Invoke(pole.root.position);
            if (IsSpawned)
                ToppleRpc(li, pi, dir);
            else
                StartFall(li, pi, dir);
        }

        [Rpc(SendTo.Everyone)]
        private void ToppleRpc(int li, int pi, Vector3 dir)
        {
            if (li >= 0 && li < states.Length && pi >= 0 && pi < states[li].poles.Length)
                StartFall(li, pi, dir);
        }

        private void StartFall(int li, int pi, Vector3 dir)
        {
            Pole pole = states[li].poles[pi];
            if (pole.fallen) return;
            pole.fallen = true;
            pole.axis = Vector3.Cross(Vector3.up, dir).normalized;
            pole.angle = 3f;
            pole.speed = 25f;
            foreach (Collider c in pole.colliders)
                if (c != null) c.enabled = false;
            PlayAt(fallCrack, pole.root.position + Vector3.up * 2f, 1f, 80f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int li = 0; li < states.Length; li++)
            {
                LineState state = states[li];
                float g = 1.5f * Physics.gravity.magnitude / Mathf.Max(1f, lines[li].height);
                foreach (Pole pole in state.poles)
                {
                    if (!pole.fallen || pole.landed) continue;
                    // Падение стержня вокруг основания: угловое ускорение растёт с наклоном
                    pole.speed += g * Mathf.Sin(pole.angle * Mathf.Deg2Rad) * Mathf.Rad2Deg * dt;
                    pole.angle += pole.speed * dt;
                    if (pole.angle >= FallenAngle)
                    {
                        pole.angle = FallenAngle;
                        pole.landed = true;
                        pole.landTime = Time.time;
                        Land(li, pole);
                    }
                    pole.root.rotation = Quaternion.AngleAxis(pole.angle, pole.axis) * pole.rest;
                    state.dirty = true;
                }
                if (state.dirty)
                {
                    state.dirty = false;
                    Rebuild(li);
                }
            }
            UpdateSparks();
        }

        private void Land(int li, Pole pole)
        {
            Line line = lines[li];
            Vector3 top = pole.root.position + Quaternion.AngleAxis(pole.angle, pole.axis) * Vector3.up * line.height;
            PlayAt(fallCrash, top, 1f, 90f);

            // Сервер: опора упала на машину
            if (!IsSpawned || IsServer)
                foreach (Collider c in Physics.OverlapCapsule(pole.root.position + Vector3.up, top, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    PlayerHealth player = c.GetComponentInParent<PlayerHealth>();
                    if (player != null && shocked.Add(player))
                        player.RequestDamage(crushDamage, top);
                }
            shocked.Clear();

            // Искры на верхушке: оборванные провода
            if (sparksTemplate != null && line.attach.Length > 0)
            {
                pole.sparks = Instantiate(sparksTemplate, top, Quaternion.identity, transform);
                pole.sparks.gameObject.SetActive(true);
                var main = pole.sparks.main;
                main.loop = false;
                main.playOnAwake = false;
            }
            var lightGo = new GameObject("ArcGlow");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.position = top + Vector3.up * 0.5f;
            pole.glow = lightGo.AddComponent<Light>();
            pole.glow.type = LightType.Point;
            pole.glow.color = new Color(0.65f, 0.8f, 1f);
            pole.glow.range = 14f;
            pole.glow.intensity = 0f;
            pole.glow.shadows = LightShadows.None;
            if (arcLoop != null)
            {
                pole.arc = MakeSource(top, 60f);
                pole.arc.clip = arcLoop;
                pole.arc.loop = true;
                pole.arc.volume = 0.7f;
                pole.arc.Play();
            }
            sparking.Add(pole);
        }

        // Разряды: вспышки искр в случайные моменты, сервер бьёт током машины рядом
        private void UpdateSparks()
        {
            bool server = !IsSpawned || IsServer;
            bool shockNow = server && Time.time >= nextShock;
            if (shockNow) nextShock = Time.time + shockInterval;

            for (int i = sparking.Count - 1; i >= 0; i--)
            {
                Pole pole = sparking[i];
                float age = Time.time - pole.landTime;
                Vector3 point = pole.glow != null ? pole.glow.transform.position : pole.root.position;
                if (age > sparkTime)
                {
                    // Линия обесточена
                    if (pole.arc != null) Destroy(pole.arc.gameObject);
                    if (pole.glow != null) Destroy(pole.glow.gameObject);
                    if (pole.sparks != null) Destroy(pole.sparks.gameObject, 3f);
                    sparking.RemoveAt(i);
                    continue;
                }
                if (pole.glow != null)
                    pole.glow.intensity = Mathf.MoveTowards(pole.glow.intensity, 0f, Time.deltaTime * 40f);
                if (Time.time >= pole.nextBurst)
                {
                    pole.nextBurst = Time.time + Random.Range(0.08f, 0.7f);
                    if (pole.sparks != null) pole.sparks.Emit(Random.Range(8, 30));
                    if (pole.glow != null) pole.glow.intensity = Random.Range(3f, 9f);
                    if (arcCracks.Length > 0 && Random.value < 0.6f)
                        PlayAt(arcCracks[Random.Range(0, arcCracks.Length)], point, Random.Range(0.5f, 0.9f), 50f);
                }
                if (shockNow)
                {
                    shocked.Clear();
                    foreach (Collider c in Physics.OverlapSphere(point, shockRadius, ~0, QueryTriggerInteraction.Ignore))
                    {
                        PlayerHealth player = c.GetComponentInParent<PlayerHealth>();
                        if (player != null && shocked.Add(player))
                            player.RequestDamage(shockDamage, point);
                        EnemyHealth enemy = c.GetComponentInParent<EnemyHealth>();
                        if (enemy != null && shocked.Add(enemy))
                            enemy.RequestDamage(enemyShockDamage);
                    }
                }
            }
        }

        // Провода линии: провисающие пролёты между соединёнными опорами по их текущему положению
        private void Rebuild(int li)
        {
            Line line = lines[li];
            LineState state = states[li];
            verts.Clear();
            norms.Clear();
            tris.Clear();
            Terrain terrain = Terrain.activeTerrain;
            for (int i = 1; i < state.poles.Length; i++)
            {
                Pole a = state.poles[i - 1], b = state.poles[i];
                if (!b.linkedToPrev || a.root == null || b.root == null) continue;
                foreach (Vector3 off in line.attach)
                {
                    Vector3 pa = a.root.TransformPoint(off), pb = b.root.TransformPoint(off);
                    // Длина провода — как у целого пролёта: когда опора падает, он провисает сильнее
                    Vector3 ra = a.root.position + a.rest * Vector3.Scale(off, a.root.lossyScale);
                    Vector3 rb = b.root.position + b.rest * Vector3.Scale(off, b.root.lossyScale);
                    float length = Vector3.Distance(ra, rb) * 1.01f;
                    float span = Vector3.Distance(pa, pb);
                    float sag = Mathf.Max(span * 0.02f, 0.5f * Mathf.Sqrt(Mathf.Max(0f, length * length - span * span)));
                    AddSpan(pa, pb, sag, terrain);
                }
            }
            state.mesh.Clear();
            state.mesh.SetVertices(verts);
            state.mesh.SetNormals(norms);
            // Без UV шейдер Lit получает NaN (см. BuildRoadside)
            state.mesh.SetUVs(0, new Vector2[verts.Count]);
            state.mesh.SetTriangles(tris, 0);
            state.mesh.RecalculateBounds();
        }

        private void AddSpan(Vector3 a, Vector3 b, float sag, Terrain terrain)
        {
            Vector3 prev = a;
            for (int s = 1; s <= Segments; s++)
            {
                float t = s / (float)Segments;
                Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * sag * 4f * t * (1f - t);
                // Провисший провод лежит на земле, а не уходит под неё
                if (terrain != null)
                {
                    float ground = terrain.SampleHeight(p) + terrain.transform.position.y + 0.05f;
                    if (p.y < ground) p.y = ground;
                }
                AddSegment(prev, p);
                prev = p;
            }
        }

        // Две перекрещенные ленты с треугольниками в обе стороны, как у проводов строителей
        private void AddSegment(Vector3 p0, Vector3 p1)
        {
            Vector3 ax = (p1 - p0).normalized;
            Vector3 side = Vector3.Cross(ax, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            Vector3 u = side.normalized * wireWidth;
            Vector3 v = Vector3.Cross(u, ax).normalized * wireWidth;
            int bi = verts.Count;
            verts.Add(p0 + u); verts.Add(p0 - u); verts.Add(p1 + u); verts.Add(p1 - u);
            verts.Add(p0 + v); verts.Add(p0 - v); verts.Add(p1 + v); verts.Add(p1 - v);
            Vector3 nu = Vector3.Cross(ax, u).normalized, nv = Vector3.Cross(ax, v).normalized;
            for (int k = 0; k < 4; k++) norms.Add(nu);
            for (int k = 0; k < 4; k++) norms.Add(nv);
            foreach (int q in new[] { 0, 4 })
            {
                tris.Add(bi + q); tris.Add(bi + q + 2); tris.Add(bi + q + 1);
                tris.Add(bi + q + 1); tris.Add(bi + q + 2); tris.Add(bi + q + 3);
                tris.Add(bi + q); tris.Add(bi + q + 1); tris.Add(bi + q + 2);
                tris.Add(bi + q + 1); tris.Add(bi + q + 3); tris.Add(bi + q + 2);
            }
        }

        private AudioSource MakeSource(Vector3 position, float maxDistance)
        {
            var go = new GameObject("PowerLineSound");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            AudioSource source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = output;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.playOnAwake = false;
            return source;
        }

        private void PlayAt(AudioClip clip, Vector3 position, float volume, float maxDistance)
        {
            if (clip == null) return;
            AudioSource source = MakeSource(position, maxDistance);
            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(0.92f, 1.08f);
            source.Play();
            Destroy(source.gameObject, clip.length / source.pitch + 0.1f);
        }
    }
}
