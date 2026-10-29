using RacingProject.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace RacingProject.Effects
{
    // Бездомные собаки: бродят вокруг своих мест (дворы, частный сектор), нюхают землю, облаивают машину игроков
    // издалека, а когда она мчится прямо на них — с визгом удирают; от выстрелов и взрывов тоже. Чистая косметика:
    // у каждого игрока свои собаки, без коллайдеров и сети. Собака — тело и четыре ноги (модели StrayDog_Body/Leg из
    // Tools/Blender/level_props), ноги качаются рысью, окрас — свой _BaseColor у каждой. Дальше activeRadius от
    // слушателя собака спрятана и не думает
    public class StrayDogs : MonoBehaviour
    {
        [SerializeField] private Mesh bodyMesh;
        [SerializeField] private Mesh legMesh;
        [SerializeField] private Material material;
        [Tooltip("Нос и глаза: вторая подсетка тела")]
        [SerializeField] private Material darkMaterial;
        [SerializeField] private Color[] coats = { new Color(0.75f, 0.6f, 0.42f), new Color(0.35f, 0.3f, 0.26f), new Color(0.9f, 0.85f, 0.75f) };
        [SerializeField] private AudioClip[] barks = new AudioClip[0];
        [SerializeField] private AudioClip yelp;
        [SerializeField] private AudioMixerGroup output;
        [Tooltip("Места, вокруг которых бродят собаки (по одной на точку)")]
        [SerializeField] private Vector3[] homes = new Vector3[0];
        [Tooltip("Крепления ног в координатах тела: передняя левая, задние — с обратным z")]
        [SerializeField] private Vector3 hip = new Vector3(0.14f, 0.72f, 0.44f);

        [SerializeField] private float roamRadius = 16f;
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float runSpeed = 11f;
        [SerializeField] private float activeRadius = 160f;

        [Header("Что пугает")]
        [SerializeField] private float barkRadius = 40f;
        [SerializeField] private float fleeRadius = 18f;
        [SerializeField] private float carSpeed = 3f;
        [SerializeField] private float shotRadius = 45f;
        [SerializeField] private float explosionRadius = 90f;

        private enum State { Idle, Walk, Bark, Flee }

        private class Dog
        {
            public Transform root;
            public Transform body;
            public Transform[] legs;
            public AudioSource source;
            public Vector3 home;
            public Vector3 target;
            public State state;
            public float until;
            public float speed;
            public float phase;
            public float nextBark;
            public float nextSteer;
            public bool visible = true;
        }

        private Dog[] dogs = new Dog[0];
        private readonly RaycastHit[] hits = new RaycastHit[6];
        private Transform car;
        private Vector3 lastCar;
        private Vector3 carVelocity;
        private float nextCarCheck;

        private void Start()
        {
            if (bodyMesh == null || legMesh == null || material == null || homes.Length == 0) { enabled = false; return; }
            dogs = new Dog[homes.Length];
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < homes.Length; i++)
            {
                var dog = new Dog { home = homes[i], legs = new Transform[4] };
                dog.root = new GameObject("StrayDog").transform;
                dog.root.SetParent(transform, false);
                Vector2 start = Random.insideUnitCircle * roamRadius * 0.5f;
                dog.root.position = Ground(homes[i] + new Vector3(start.x, 0f, start.y));
                dog.root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                dog.root.localScale = Vector3.one * Random.Range(0.85f, 1.15f);
                dog.body = MakePart("Body", dog.root, bodyMesh);
                for (int k = 0; k < 4; k++)
                {
                    dog.legs[k] = MakePart("Leg", dog.body, legMesh);
                    dog.legs[k].localPosition = new Vector3(k % 2 == 0 ? -hip.x : hip.x, hip.y, k < 2 ? hip.z : -hip.z);
                }
                if (coats.Length > 0)
                {
                    block.SetColor("_BaseColor", coats[Random.Range(0, coats.Length)] * Random.Range(0.85f, 1.1f));
                    foreach (Renderer r in dog.root.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
                }
                var source = dog.root.gameObject.AddComponent<AudioSource>();
                source.spatialBlend = 1f;
                source.minDistance = 5f;
                source.maxDistance = 110f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                source.playOnAwake = false;
                source.outputAudioMixerGroup = output;
                dog.source = source;
                Rest(dog);
                dogs[i] = dog;
            }
            ShotEffects.Fired += OnShot;
            ExplosionEffect.Exploded += OnExplosion;
        }

        private void OnDestroy()
        {
            ShotEffects.Fired -= OnShot;
            ExplosionEffect.Exploded -= OnExplosion;
        }

        private Transform MakePart(string name, Transform parent, Mesh mesh)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = mesh.subMeshCount > 1 && darkMaterial != null ? new[] { material, darkMaterial } : new[] { material };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        // Земля под точкой: ближайшая сверху поверхность без Rigidbody (машины и сбитые предметы не в счёт)
        private Vector3 Ground(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 4f, Vector3.down, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float y = p.y;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].rigidbody != null || hits[i].distance >= best) continue;
                best = hits[i].distance;
                y = hits[i].point.y;
            }
            return new Vector3(p.x, y, p.z);
        }

        // ---------- поведение ----------

        private void Rest(Dog dog)
        {
            dog.state = State.Idle;
            dog.until = Time.time + Random.Range(2f, 7f);
        }

        private void Wander(Dog dog)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * roamRadius;
                Vector3 target = Ground(dog.home + new Vector3(offset.x, 0f, offset.y));
                if (Blocked(dog.root.position, target)) continue;
                dog.target = target;
                dog.state = State.Walk;
                dog.until = Time.time + 20f;
                return;
            }
            Rest(dog);
        }

        // Забор, стена или машина на уровне колен
        private static bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 up = Vector3.up * 0.6f;
            return Physics.Linecast(from + up, to + up, ~0, QueryTriggerInteraction.Ignore);
        }

        private void Flee(Dog dog, Vector3 danger, bool squeal)
        {
            Vector3 away = dog.root.position - danger;
            away.y = 0f;
            dog.target = dog.root.position + (away.sqrMagnitude > 0.01f ? away.normalized : dog.root.forward) * 30f;
            if (dog.state != State.Flee && squeal && dog.visible && yelp != null) dog.source.PlayOneShot(yelp, Random.Range(0.7f, 0.95f));
            dog.state = State.Flee;
            dog.until = Time.time + Random.Range(4f, 7f);
            dog.nextSteer = 0f;
        }

        private void OnShot(Vector3 point) => Scare(point, shotRadius, false);

        private void OnExplosion(Vector3 point, float power) => Scare(point, explosionRadius, true);

        private void Scare(Vector3 point, float radius, bool squeal)
        {
            float radiusSqr = radius * radius;
            foreach (Dog dog in dogs)
                if ((dog.root.position - point).sqrMagnitude <= radiusSqr) Flee(dog, point, squeal);
        }

        // ---------- кадр ----------

        private void Update()
        {
            float dt = Time.deltaTime;
            CheckCar();
            Vector3 listener = ActiveListener.Position;
            float active = activeRadius * activeRadius;
            foreach (Dog dog in dogs)
            {
                bool visible = (dog.root.position - listener).sqrMagnitude < active;
                if (visible != dog.visible)
                {
                    dog.visible = visible;
                    dog.root.gameObject.SetActive(visible);
                }
                if (!visible) continue;
                React(dog);
                Move(dog, dt);
            }
        }

        private void React(Dog dog)
        {
            if (car == null) return;
            Vector3 to = car.position - dog.root.position;
            to.y = 0f;
            float distance = to.magnitude;
            bool moving = carVelocity.magnitude > carSpeed;
            // Машина мчится к собаке — бежать; совсем рядом — с визгом
            bool coming = moving && Vector3.Dot(carVelocity, -to) > 0f;
            if (distance < fleeRadius && (coming || distance < fleeRadius * 0.4f) && moving)
            {
                Flee(dog, car.position - carVelocity * 0.3f, distance < fleeRadius * 0.5f);
                return;
            }
            if (dog.state == State.Flee) return;
            if (distance < barkRadius)
            {
                if (dog.state != State.Bark)
                {
                    dog.state = State.Bark;
                    dog.nextBark = Time.time + Random.Range(0.2f, 0.8f);
                }
                dog.until = Time.time + 3f;
            }
        }

        private void Move(Dog dog, float dt)
        {
            Vector3 position = dog.root.position;
            float goalSpeed = 0f;
            Vector3 facing = dog.root.forward;
            switch (dog.state)
            {
                case State.Idle:
                    if (Time.time >= dog.until) Wander(dog);
                    break;
                case State.Walk:
                {
                    Vector3 to = dog.target - position;
                    to.y = 0f;
                    if (to.magnitude < 0.6f || Time.time >= dog.until) { Rest(dog); break; }
                    goalSpeed = walkSpeed;
                    facing = to;
                    break;
                }
                case State.Bark:
                {
                    if (Time.time >= dog.until) { Rest(dog); break; }
                    if (car != null) facing = car.position - position;
                    if (Time.time >= dog.nextBark)
                    {
                        dog.nextBark = Time.time + Random.Range(0.7f, 2.2f);
                        if (barks.Length > 0) dog.source.PlayOneShot(barks[Random.Range(0, barks.Length)], Random.Range(0.6f, 0.9f));
                    }
                    break;
                }
                case State.Flee:
                {
                    if (Time.time >= dog.until) { Rest(dog); break; }
                    // Раз в 0.3 с проверка пути вперёд: упёрлась — сворачивает
                    if (Time.time >= dog.nextSteer)
                    {
                        dog.nextSteer = Time.time + 0.3f;
                        Vector3 ahead = position + dog.root.forward * 3f;
                        if (Blocked(position, ahead))
                        {
                            Vector3 side = Quaternion.Euler(0f, Random.value < 0.5f ? 70f : -70f, 0f) * (dog.target - position);
                            dog.target = position + side;
                        }
                    }
                    facing = dog.target - position;
                    goalSpeed = runSpeed;
                    break;
                }
            }
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
            {
                float turn = dog.state == State.Flee ? 9f : 4f;
                dog.root.rotation = Quaternion.Slerp(dog.root.rotation, Quaternion.LookRotation(facing), 1f - Mathf.Exp(-turn * dt));
            }
            dog.speed = Mathf.MoveTowards(dog.speed, goalSpeed, (goalSpeed > dog.speed ? 20f : 12f) * dt);
            if (dog.speed > 0.01f)
            {
                Vector3 next = position + dog.root.forward * dog.speed * dt;
                Vector3 ground = Ground(next);
                // Не лезет на высокие уступы (крыши машин, заборы)
                if (ground.y - position.y < 0.8f) dog.root.position = ground;
                else dog.speed = 0f;
            }
            Animate(dog, dt);
        }

        // Рысь: диагональные пары ног в фазе, тело чуть качается; стоя — ноги прямо, при лае тело подпрыгивает
        private void Animate(Dog dog, float dt)
        {
            float speed = dog.speed;
            dog.phase += dt * (2.2f + speed * 0.9f) * Mathf.PI * 2f * (speed > 0.05f ? 1f : 0f);
            float swing = Mathf.Clamp(speed * 9f, 0f, 38f);
            for (int k = 0; k < 4; k++)
            {
                float side = (k == 0 || k == 3) ? 1f : -1f;
                float angle = Mathf.Sin(dog.phase) * swing * side;
                dog.legs[k].localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
            float bob = Mathf.Abs(Mathf.Cos(dog.phase)) * Mathf.Min(0.05f, speed * 0.006f);
            float hop = dog.state == State.Bark ? Mathf.Max(0f, Mathf.Sin(Time.time * 9f)) * 0.04f : 0f;
            dog.body.localPosition = Vector3.up * (bob + hop);
            dog.body.localRotation = Quaternion.Euler(dog.state == State.Bark ? -6f : 0f, 0f, 0f);
        }

        // Машина игрока: скорость по разнице положений раз в 0.2 с
        private void CheckCar()
        {
            if (Time.time < nextCarCheck) return;
            float interval = 0.2f;
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
            carVelocity = (position - lastCar) / interval;
            carVelocity.y = 0f;
            lastCar = position;
        }
    }
}
