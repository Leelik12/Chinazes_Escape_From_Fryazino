using System.Collections.Generic;
using RacingProject.Audio;
using RacingProject.Props;
using UnityEngine;
using UnityEngine.Audio;

namespace RacingProject.Effects
{
    // Вороны над городом: стаи сидят на верхушках опор ЛЭП (PowerLines), иногда каркают. От выстрела рядом
    // (ShotEffects.Fired), взрыва (ExplosionEffect.Exploded), проезжающей машины или упавшей опоры стая с шумом
    // взлетает, кружит над местом и садится на другую опору неподалёку. Чистая косметика: у каждого игрока свои
    // птицы, по сети не синхронизируются. Птица — несколько треугольников (тело и два крыла), машут крылья сами,
    // а стаи дальше activeRadius от слушателя спрятаны
    public class CityBirds : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private AudioClip[] caws = new AudioClip[0];
        [SerializeField] private AudioClip flockClip;
        [SerializeField] private AudioMixerGroup output;

        [SerializeField] private int flockCount = 26;
        [SerializeField] private Vector2Int flockSize = new Vector2Int(3, 7);
        [SerializeField] private float birdScale = 1.3f;
        [SerializeField] private float activeRadius = 260f;

        [Header("Что пугает")]
        [SerializeField] private float shotRadius = 70f;
        [SerializeField] private float explosionRadius = 150f;
        [SerializeField] private float carRadius = 24f;
        [SerializeField] private float carSpeed = 4f;

        [Header("Полёт")]
        [SerializeField] private float flySpeed = 11f;
        [SerializeField] private Vector2 flightTime = new Vector2(9f, 16f);
        [SerializeField] private float circleHeight = 22f;
        [SerializeField] private float circleRadius = 14f;
        [SerializeField] private float landDistance = 220f;
        [Tooltip("Карканье сидящей стаи: раз в столько секунд")]
        [SerializeField] private Vector2 cawInterval = new Vector2(6f, 20f);

        private class Bird
        {
            public Transform root;
            public Transform wingLeft;
            public Transform wingRight;
            public Vector3 velocity;
            public float slot;
            public float phase;
            public float orbit;
        }

        private class Flock
        {
            public int perch;
            public Bird[] birds;
            public bool flying;
            public bool landing;
            public float landAt;
            public Vector3 center;
            public AudioSource source;
            public float nextCaw;
            public bool visible = true;
            public GameObject group;
        }

        private readonly List<Transform> poles = new List<Transform>();
        private readonly List<float> heights = new List<float>();
        private readonly List<Flock> flocks = new List<Flock>();
        private readonly HashSet<int> taken = new HashSet<int>();
        private Mesh bodyMesh;
        private Mesh wingMesh;
        private Transform car;
        private Vector3 lastCar;
        private float carVelocity;
        private float nextCarCheck;

        private void Start()
        {
            PowerLines lines = PowerLines.Instance != null ? PowerLines.Instance : FindFirstObjectByType<PowerLines>();
            if (lines == null || material == null) { enabled = false; return; }
            foreach (PowerLines.Line line in lines.Lines)
                foreach (Transform pole in line.poles)
                    if (pole != null) { poles.Add(pole); heights.Add(line.height); }
            if (poles.Count == 0) { enabled = false; return; }
            bodyMesh = BuildBody();
            wingMesh = BuildWing();
            var random = new System.Random(1703);
            for (int i = 0; i < flockCount && taken.Count < poles.Count; i++)
            {
                int perch;
                do perch = random.Next(poles.Count); while (taken.Contains(perch));
                CreateFlock(perch, random.Next(flockSize.x, flockSize.y + 1));
            }
            ShotEffects.Fired += OnShot;
            ExplosionEffect.Exploded += OnExplosion;
        }

        private void OnDestroy()
        {
            ShotEffects.Fired -= OnShot;
            ExplosionEffect.Exploded -= OnExplosion;
            if (bodyMesh != null) Destroy(bodyMesh);
            if (wingMesh != null) Destroy(wingMesh);
        }

        // ---------- птицы ----------

        private void CreateFlock(int perch, int count)
        {
            var flock = new Flock { perch = perch, birds = new Bird[count], group = new GameObject("Crows") };
            flock.group.transform.SetParent(transform, false);
            taken.Add(perch);
            for (int i = 0; i < count; i++)
            {
                var bird = new Bird
                {
                    root = MakePart("Crow", flock.group.transform, bodyMesh),
                    slot = count > 1 ? i / (count - 1f) * 2f - 1f : 0f,
                    phase = Random.value * 10f,
                    orbit = Random.value * Mathf.PI * 2f,
                };
                bird.root.localScale = Vector3.one * birdScale * Random.Range(0.9f, 1.1f);
                bird.wingRight = MakePart("WingR", bird.root, wingMesh);
                bird.wingRight.localPosition = new Vector3(0.04f, 0.03f, 0.02f);
                bird.wingLeft = MakePart("WingL", bird.root, wingMesh);
                bird.wingLeft.localPosition = new Vector3(-0.04f, 0.03f, 0.02f);
                bird.wingLeft.localScale = new Vector3(-1f, 1f, 1f);
                bird.root.SetPositionAndRotation(Seat(flock, bird), PerchRotation(perch));
                FoldWings(bird);
                flock.birds[i] = bird;
            }
            var source = flock.group.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.minDistance = 6f;
            source.maxDistance = 140f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.playOnAwake = false;
            source.outputAudioMixerGroup = output;
            flock.source = source;
            flock.nextCaw = Time.time + Random.Range(cawInterval.x, cawInterval.y);
            flocks.Add(flock);
        }

        private Transform MakePart(string name, Transform parent, Mesh mesh)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        // Тело: заострённый к клюву ромб с плоским хвостом, вперёд по +Z
        private static Mesh BuildBody()
        {
            var v = new List<Vector3>
            {
                new Vector3(0f, 0.02f, 0.24f),                                   // 0 клюв
                new Vector3(0f, 0.08f, 0.05f), new Vector3(0.07f, 0.02f, 0.05f),   // 1 спина, 2 правый бок
                new Vector3(0f, -0.05f, 0.05f), new Vector3(-0.07f, 0.02f, 0.05f), // 3 грудь, 4 левый бок
                new Vector3(0f, 0.03f, -0.14f),                                  // 5 конец тела
                new Vector3(0.06f, 0.03f, -0.3f), new Vector3(-0.06f, 0.03f, -0.3f), // 6, 7 хвост
            };
            int[] t =
            {
                0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1,
                5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4,
                5, 6, 7, 5, 7, 6,
            };
            var mesh = new Mesh { name = "Crow" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Крыло: от плеча вправо (+X), конец отведён назад; двустороннее
        private static Mesh BuildWing()
        {
            var v = new List<Vector3>
            {
                new Vector3(0f, 0f, 0.05f), new Vector3(0f, 0f, -0.08f),
                new Vector3(0.24f, 0f, 0.03f), new Vector3(0.44f, 0f, -0.12f), new Vector3(0.2f, 0f, -0.12f),
            };
            int[] t = { 0, 2, 1, 1, 2, 4, 2, 3, 4, 0, 1, 2, 1, 4, 2, 2, 4, 3 };
            var mesh = new Mesh { name = "CrowWing" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void FoldWings(Bird bird)
        {
            bird.wingRight.localRotation = Quaternion.Euler(0f, 70f, -15f);
            bird.wingLeft.localRotation = Quaternion.Euler(0f, -70f, 15f);
        }

        private bool Standing(int perch) => poles[perch] != null && poles[perch].up.y > 0.97f;

        // Место птицы на траверсе опоры: в ряд поперёк, чуть вразнобой
        private Vector3 Seat(Flock flock, Bird bird)
        {
            Transform pole = poles[flock.perch];
            Vector3 across = Vector3.ProjectOnPlane(pole.right, Vector3.up).normalized;
            return pole.position + pole.up * (heights[flock.perch] + 0.15f) + across * bird.slot * 1.3f;
        }

        private Quaternion PerchRotation(int perch)
        {
            Vector3 forward = Vector3.ProjectOnPlane(poles[perch].forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized) * Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f);
        }

        // ---------- испуг ----------

        private void OnShot(Vector3 point) => Scare(point, shotRadius);

        private void OnExplosion(Vector3 point, float power) => Scare(point, explosionRadius);

        private void Scare(Vector3 point, float radius)
        {
            float radiusSqr = radius * radius;
            foreach (Flock flock in flocks)
            {
                if (flock.flying) continue;
                if ((PerchTop(flock) - point).sqrMagnitude <= radiusSqr) TakeOff(flock, point);
            }
        }

        private Vector3 PerchTop(Flock flock)
        {
            Transform pole = poles[flock.perch];
            return pole.position + pole.up * heights[flock.perch];
        }

        private void TakeOff(Flock flock, Vector3 danger)
        {
            flock.flying = true;
            flock.landing = false;
            flock.landAt = Time.time + Random.Range(flightTime.x, flightTime.y);
            Vector3 away = PerchTop(flock) - danger;
            away.y = 0f;
            away = away.sqrMagnitude > 1f ? away.normalized : Random.insideUnitSphere;
            flock.center = PerchTop(flock) + away * 25f + Vector3.up * circleHeight;
            foreach (Bird bird in flock.birds)
                bird.velocity = (Vector3.up * 1.2f + away + Random.insideUnitSphere * 0.5f).normalized * flySpeed * 0.7f;
            taken.Remove(flock.perch);
            if (flock.visible && flock.source != null)
            {
                flock.source.transform.position = PerchTop(flock);
                if (flockClip != null) flock.source.PlayOneShot(flockClip, 0.8f);
                if (caws.Length > 0) flock.source.PlayOneShot(caws[Random.Range(0, caws.Length)], 0.9f);
            }
        }

        // Новая опора: стоящая, свободная, недалеко от места кружения
        private void ChooseLanding(Flock flock)
        {
            int best = -1;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                int candidate = Random.Range(0, poles.Count);
                if (taken.Contains(candidate) || !Standing(candidate)) continue;
                Vector3 to = poles[candidate].position - flock.center;
                to.y = 0f;
                if (to.magnitude > landDistance) continue;
                best = candidate;
                break;
            }
            if (best < 0)
            {
                flock.landAt = Time.time + 5f;
                return;
            }
            flock.perch = best;
            flock.landing = true;
            taken.Add(best);
        }

        // ---------- кадр ----------

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 listener = ActiveListener.Position;
            CheckCar();
            float active = activeRadius * activeRadius;
            foreach (Flock flock in flocks)
            {
                Vector3 anchor = flock.flying ? flock.center : PerchTop(flock);
                bool visible = flock.flying || (anchor - listener).sqrMagnitude < active;
                if (visible != flock.visible)
                {
                    flock.visible = visible;
                    flock.group.SetActive(visible);
                }
                if (!visible) continue;

                if (!flock.flying)
                {
                    // Опора падает — птицы срываются
                    if (!Standing(flock.perch)) { TakeOff(flock, PerchTop(flock) + Random.insideUnitSphere); continue; }
                    UpdatePerched(flock, dt);
                    continue;
                }
                if (!flock.landing && Time.time >= flock.landAt) ChooseLanding(flock);
                UpdateFlying(flock, dt);
            }
        }

        private void UpdatePerched(Flock flock, float dt)
        {
            foreach (Bird bird in flock.birds)
            {
                // Сидят на месте опоры (она может качнуться от удара), изредка переминаются
                bird.root.position = Seat(flock, bird) + Vector3.up * Mathf.Max(0f, Mathf.Sin(Time.time * 3f + bird.phase * 7f) - 0.97f) * 2f;
            }
            if (Time.time >= flock.nextCaw)
            {
                flock.nextCaw = Time.time + Random.Range(cawInterval.x, cawInterval.y);
                if (caws.Length > 0 && flock.source != null)
                {
                    flock.source.transform.position = PerchTop(flock);
                    flock.source.PlayOneShot(caws[Random.Range(0, caws.Length)], Random.Range(0.5f, 0.85f));
                }
            }
        }

        private void UpdateFlying(Flock flock, float dt)
        {
            bool allSeated = flock.landing;
            for (int i = 0; i < flock.birds.Length; i++)
            {
                Bird bird = flock.birds[i];
                Vector3 position = bird.root.position;
                Vector3 goal;
                if (flock.landing)
                {
                    goal = Seat(flock, bird);
                    // Сначала к опоре сверху, последние метры — плавно вниз
                    Vector3 flat = goal - position;
                    flat.y = 0f;
                    if (flat.magnitude > 6f) goal += Vector3.up * Mathf.Min(10f, flat.magnitude * 0.3f);
                }
                else
                {
                    bird.orbit += dt * flySpeed / circleRadius * (i % 2 == 0 ? 1f : 0.85f);
                    goal = flock.center + new Vector3(Mathf.Cos(bird.orbit), Mathf.Sin(bird.orbit * 1.7f) * 0.15f, Mathf.Sin(bird.orbit)) * circleRadius;
                }
                Vector3 to = goal - position;
                float distance = to.magnitude;
                if (flock.landing && distance < 0.25f)
                {
                    bird.root.position = goal;
                    bird.velocity = Vector3.zero;
                    FoldWings(bird);
                    continue;
                }
                allSeated = false;
                float speed = flock.landing ? Mathf.Min(flySpeed, distance * 1.5f + 1f) : flySpeed;
                Vector3 desired = to / Mathf.Max(0.001f, distance) * speed;
                bird.velocity = Vector3.Lerp(bird.velocity, desired, 1f - Mathf.Exp(-2.5f * dt));
                bird.root.position = position + bird.velocity * dt;
                if (bird.velocity.sqrMagnitude > 0.5f)
                    bird.root.rotation = Quaternion.Slerp(bird.root.rotation, Quaternion.LookRotation(bird.velocity), 1f - Mathf.Exp(-6f * dt));
                // Взмахи; при снижении — планирует
                bool glide = bird.velocity.y < -1.5f && !flock.landing;
                bird.phase += dt * (glide ? 0f : 9f + i * 0.4f) * Mathf.PI * 2f;
                float flap = glide ? 8f : Mathf.Sin(bird.phase) * 55f;
                bird.wingRight.localRotation = Quaternion.Euler(0f, 0f, flap);
                bird.wingLeft.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
            if (allSeated)
            {
                flock.flying = false;
                flock.landing = false;
                foreach (Bird bird in flock.birds)
                    bird.root.rotation = PerchRotation(flock.perch);
            }
        }

        // Машина игроков на ходу проезжает под опорой — стая взлетает
        private void CheckCar()
        {
            if (Time.time < nextCarCheck) return;
            float interval = 0.25f;
            nextCarCheck = Time.time + interval;
            if (car == null)
            {
                PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
                if (health == null) return;
                car = health.transform;
                lastCar = car.position;
                return;
            }
            Vector3 position = car.position;
            carVelocity = (position - lastCar).magnitude / interval;
            lastCar = position;
            if (carVelocity < carSpeed) return;
            float radiusSqr = carRadius * carRadius;
            foreach (Flock flock in flocks)
            {
                if (flock.flying || !flock.visible) continue;
                Vector3 to = PerchTop(flock) - position;
                to.y *= 0.3f;
                if (to.sqrMagnitude <= radiusSqr) TakeOff(flock, position);
            }
        }
    }
}
