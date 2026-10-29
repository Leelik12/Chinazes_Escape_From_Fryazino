using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RacingProject.Car;

namespace RacingProject.Enemy
{
    // Горящий остов погибшего врага. Создаётся у каждого игрока в момент взрыва (EnemyHealth.ExplodeRpc) из копии
    // мешей машины: краска сгорает, стёкла вылетают, взрыв подбрасывает и переворачивает корпус, одно колесо
    // отрывается и катится само, а голый диск на его месте высекает искры о дорогу. Водитель вываливается из машины
    // тряпичной куклой. Физика остова у каждого игрока своя (как у разрухи), через wreckLifetime остов уходит под землю
    public class EnemyWreck : MonoBehaviour
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("Материал сгоревшего кузова")]
            public Material burnt;
            [Tooltip("Скрежет диска по асфальту, цикл")]
            public AudioClip scrapeLoop;
            [Tooltip("Подброс корпуса взрывом, м/с")]
            public Vector2 upKick = new Vector2(4f, 7f);
            [Tooltip("Закрутка корпуса вокруг продольной оси, рад/с")]
            public Vector2 rollKick = new Vector2(2f, 4.5f);
            [Tooltip("Доля врагов, у которых водитель вываливается из машины")]
            [Range(0f, 1f)] public float driverBailChance = 0.7f;
        }

        private const int MaxWrecks = 8;
        private const float WreckLifetime = 30f;
        private const float SinkTime = 3f;
        private const float RagdollLifetime = 25f;
        private static readonly List<EnemyWreck> live = new List<EnemyWreck>();

        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private Rigidbody body;
        private Transform rim;
        private float rimRadius;
        private ParticleSystem sparks;
        private AudioSource scrape;
        private ParticleSystem fire;
        private float bornAt;
        private float sinkAt;
        private bool sinking;

        // Остов на месте врага; velocity — скорость врага на сервере в момент гибели
        public static void Spawn(GameObject source, Settings settings, Vector3 velocity, int seed)
        {
            if (source == null || settings == null) return;
            var random = new System.Random(seed);

            var root = new GameObject("Wreck " + source.name);
            root.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            var wreck = root.AddComponent<EnemyWreck>();
            wreck.bornAt = Time.time;

            CarDeformation deformation = source.GetComponent<CarDeformation>();
            var bodyRenderers = new List<Renderer>();
            var wheels = new List<Transform>();
            var wheelParts = new List<Transform>();
            foreach (MeshRenderer renderer in source.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || Skip(renderer)) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                var part = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(root.transform, false);
                part.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                part.transform.localScale = renderer.transform.lossyScale;
                Mesh mesh = filter.sharedMesh;
                // Вмятые меши принадлежат CarDeformation и уничтожаются вместе с врагом — берём копию
                if (deformation != null && deformation.Owns(filter))
                {
                    mesh = Instantiate(mesh);
                    wreck.ownedMeshes.Add(mesh);
                }
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                var copy = part.GetComponent<MeshRenderer>();
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = settings.burnt != null ? settings.burnt : renderer.sharedMaterials[i];
                copy.sharedMaterials = materials;

                if (IsWheel(renderer.name)) wheels.Add(part.transform);
                else if (IsWheelPart(renderer.name)) wheelParts.Add(part.transform);
                else bodyRenderers.Add(copy);
            }

            // Корпус: коробка по габаритам кузова, низ приподнят — остов садится на диски
            Bounds bounds = LocalBounds(root.transform, bodyRenderers);
            var box = root.AddComponent<BoxCollider>();
            float lift = bounds.size.y * 0.18f;
            box.center = bounds.center + Vector3.up * (lift * 0.5f);
            box.size = new Vector3(bounds.size.x * 0.95f, bounds.size.y - lift, bounds.size.z * 0.95f);

            Rigidbody sourceBody = source.GetComponent<Rigidbody>();
            wreck.body = root.AddComponent<Rigidbody>();
            wreck.body.mass = Mathf.Clamp(sourceBody != null ? sourceBody.mass : 1500f, 600f, 6000f);
            wreck.body.linearDamping = 0.05f;
            wreck.body.angularDamping = 0.4f;
            wreck.body.interpolation = RigidbodyInterpolation.Interpolate;
            wreck.body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            wreck.body.centerOfMass = box.center - Vector3.up * box.size.y * 0.15f;
            float side = random.NextDouble() < 0.5 ? -1f : 1f;
            Vector3 kick = Vector3.up * Lerp(random, settings.upKick);
            wreck.body.linearVelocity = velocity * 0.8f + kick;
            // Тяжёлый Урал взрыв почти не переворачивает
            float roll = Lerp(random, settings.rollKick) * Mathf.Clamp01(3000f / wreck.body.mass);
            wreck.body.angularVelocity = root.transform.forward * (roll * side) + root.transform.right * ((float)random.NextDouble() - 0.5f);

            if (wheels.Count > 0)
                wreck.DetachWheel(wheels[random.Next(wheels.Count)], wheelParts, wreck.body.linearVelocity, side, settings, SparkMaterial(source));

            wreck.CopyFireAndSmoke(source);
            if (random.NextDouble() < settings.driverBailChance)
                BailDriver(source, velocity, side);

            live.Add(wreck);
            if (live.Count > MaxWrecks)
                live[0].Sink();
        }

        // Стёкла вылетают при взрыве; свечения фар и проём люка — не железо
        private static bool Skip(Renderer renderer)
        {
            string name = renderer.name.ToLowerInvariant();
            if (name.Contains("window") || name.Contains("glass") || name.Contains("lamp") || name.Contains("glow")
                || name.Contains("hatchopening") || name.Contains("reflector") || name.Contains("lights")) return true;
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.name.StartsWith("Fx_")) return true;
            return false;
        }

        private static bool IsWheel(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.Contains("wheel") && !lower.Contains("brake");
        }

        // Шина и тормоз у некоторых машин — отдельные меши рядом с диском
        private static bool IsWheelPart(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.Contains("tyre") || lower.Contains("tire") || lower.Contains("brake");
        }

        private static float Lerp(System.Random random, Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }

        private static Bounds LocalBounds(Transform root, List<Renderer> renderers)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Bounds b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 local = root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                    else bounds.Encapsulate(local);
                }
            }
            if (!any) bounds = new Bounds(Vector3.up, new Vector3(2f, 1.5f, 4f));
            return bounds;
        }

        // Колесо отлетает и катится само, на его месте остаётся голый диск
        private void DetachWheel(Transform wheel, List<Transform> parts, Vector3 velocity, float side, Settings settings, Material sparkMaterial)
        {
            Bounds b = wheel.GetComponent<MeshFilter>().sharedMesh.bounds;
            Vector3 hub = wheel.TransformPoint(b.center);
            // Шина и тормоз этого колеса улетают вместе с диском
            float reach = Vector3.Scale(b.extents, wheel.lossyScale).magnitude;
            foreach (Transform part in parts)
            {
                Vector3 center = part.TransformPoint(part.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                if ((center - hub).sqrMagnitude <= reach * reach) part.SetParent(wheel, true);
            }
            rimRadius = Mathf.Max(0.2f, Vector3.Scale(b.extents, wheel.lossyScale).y * 0.7f);
            rim = new GameObject("Rim").transform;
            rim.SetParent(transform, false);
            rim.position = hub;

            wheel.SetParent(null, true);
            var collider = wheel.gameObject.AddComponent<SphereCollider>();
            collider.center = b.center;
            collider.radius = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)) * 0.9f;
            var wheelBody = wheel.gameObject.AddComponent<Rigidbody>();
            wheelBody.mass = 30f;
            wheelBody.interpolation = RigidbodyInterpolation.Interpolate;
            Vector3 outward = (hub - transform.position);
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.01f ? outward.normalized : transform.right * side;
            wheelBody.linearVelocity = velocity + outward * 6f + Vector3.up * 5f;
            wheelBody.angularVelocity = transform.right * (velocity.magnitude * 1.5f) + Random.insideUnitSphere * 4f;
            Destroy(wheel.gameObject, WreckLifetime);

            sparks = MakeSparks(rim, sparkMaterial);
            if (settings.scrapeLoop != null)
            {
                scrape = rim.gameObject.AddComponent<AudioSource>();
                scrape.clip = settings.scrapeLoop;
                scrape.loop = true;
                scrape.spatialBlend = 1f;
                scrape.minDistance = 6f;
                scrape.maxDistance = 90f;
                scrape.rolloffMode = AudioRolloffMode.Linear;
                scrape.volume = 0f;
                scrape.dopplerLevel = 0f;
                scrape.Play();
            }
        }

        // Материал искр — тот же, что у искр от попаданий по врагу
        private static Material SparkMaterial(GameObject source)
        {
            foreach (ParticleSystemRenderer renderer in source.GetComponentsInChildren<ParticleSystemRenderer>(true))
                if (renderer.name.Contains("Sparks") && renderer.sharedMaterial != null) return renderer.sharedMaterial;
            return null;
        }

        // Искры диска: отдельная простая система частиц, оранжевые штрихи по скорости (их же берёт CarDamageState)
        public static ParticleSystem MakeSparks(Transform parent, Material material)
        {
            var go = new GameObject("RimSparks", typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            var system = go.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(4f, 1.8f, 0.5f), new Color(3f, 1f, 0.25f));
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (material != null) renderer.sharedMaterial = material;
            system.Play();
            return system;
        }

        // Огонь и дым — копии систем частиц врага, горят на остове
        private void CopyFireAndSmoke(GameObject source)
        {
            CarDamageEffects effects = source.GetComponentInChildren<CarDamageEffects>(true);
            if (effects == null) return;
            foreach (ParticleSystem system in effects.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system.transform.parent != effects.transform) continue;
                ParticleSystem copy = Instantiate(system, transform);
                copy.transform.SetPositionAndRotation(system.transform.position, system.transform.rotation);
                var emission = copy.emission;
                bool isFire = system.name.Contains("Fire");
                emission.rateOverTime = isFire ? 30f : 26f;
                if (!isFire)
                {
                    var main = copy.main;
                    main.startColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
                }
                else fire = copy;
                copy.gameObject.SetActive(true);
                copy.Play();
            }
        }

        private void FixedUpdate()
        {
            if (sinking)
            {
                transform.position += Vector3.down * (1.5f / SinkTime) * Time.fixedDeltaTime;
                if (Time.time >= sinkAt + SinkTime) Destroy(gameObject);
                return;
            }
            if (Time.time >= bornAt + WreckLifetime)
            {
                Sink();
                return;
            }
            // Через 12 с пламя опадает, остаётся дым
            if (fire != null && Time.time > bornAt + 12f && fire.isEmitting)
                fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            UpdateRim();
        }

        // Диск высекает искры и скрежещет, пока трётся о землю на ходу
        private void UpdateRim()
        {
            if (rim == null) return;
            float speed = body != null ? body.GetPointVelocity(rim.position).magnitude : 0f;
            bool touching = Physics.Raycast(rim.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, rimRadius + 0.45f,
                                            ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform);
            float amount = touching ? Mathf.Clamp01((speed - 1.5f) / 12f) : 0f;
            if (sparks != null)
            {
                if (touching) sparks.transform.position = hit.point + Vector3.up * 0.05f;
                if (body != null && body.linearVelocity.sqrMagnitude > 0.1f)
                    sparks.transform.rotation = Quaternion.LookRotation(-body.linearVelocity.normalized + Vector3.up * 0.4f);
                var emission = sparks.emission;
                emission.rateOverTime = amount * 260f;
            }
            if (scrape != null)
            {
                scrape.volume = Mathf.MoveTowards(scrape.volume, amount * 0.9f, Time.fixedDeltaTime * 4f);
                scrape.pitch = 0.8f + amount * 0.4f;
            }
        }

        // Остов уходит под землю и исчезает
        private void Sink()
        {
            if (sinking) return;
            sinking = true;
            sinkAt = Time.time;
            live.Remove(this);
            if (body != null) body.isKinematic = true;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (scrape != null) scrape.Stop();
        }

        private void OnDestroy()
        {
            live.Remove(this);
            foreach (Mesh mesh in ownedMeshes)
                if (mesh != null) Destroy(mesh);
        }

        // Водитель вываливается через левую дверь тряпичной куклой: копия его скелета в текущей позе
        private static void BailDriver(GameObject source, Vector3 velocity, float side)
        {
            EnemyCrewDriver driver = source.GetComponentInChildren<EnemyCrewDriver>();
            if (driver == null || !driver.gameObject.activeInHierarchy) return;
            GameObject original = driver.gameObject;
            Vector3 scale = original.transform.lossyScale;
            Vector3 outward = -source.transform.right;

            // Копия создаётся выключенной, чтобы скрипты врага на ней не проснулись
            original.SetActive(false);
            GameObject doll = Instantiate(original, original.transform.position + outward * 1.1f + Vector3.up * 0.3f, original.transform.rotation);
            original.SetActive(true);
            doll.name = "Ragdoll " + source.name;
            doll.transform.localScale = scale;
            foreach (MonoBehaviour behaviour in doll.GetComponents<MonoBehaviour>()) DestroyImmediate(behaviour);
            Animator animator = doll.GetComponent<Animator>();
            if (animator != null) DestroyImmediate(animator);
            foreach (SkinnedMeshRenderer skin in doll.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;

            if (!BuildRagdoll(doll.transform))
            {
                Destroy(doll);
                return;
            }
            doll.SetActive(true);
            // Скорость задаётся уже включённым телам
            Vector3 start = velocity * 0.6f + outward * 4f + Vector3.up * 3.5f;
            foreach (Rigidbody part in doll.GetComponentsInChildren<Rigidbody>())
                part.linearVelocity = start;
            Rigidbody pelvis = doll.GetComponentInChildren<Rigidbody>();
            if (pelvis != null) pelvis.AddTorque(Random.insideUnitSphere * 30f, ForceMode.Impulse);
            Destroy(doll, RagdollLifetime);
        }

        private static Transform Bone(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "mixamorig:" + name) return t;
            return null;
        }

        // Рэгдолл по костям Mixamo: таз и грудь — коробки, конечности — капсулы до следующей кости, голова — шар
        private static bool BuildRagdoll(Transform root)
        {
            Transform hips = Bone(root, "Hips"), chest = Bone(root, "Spine2"), head = Bone(root, "Head");
            if (hips == null || chest == null || head == null) return false;

            Rigidbody hipsBody = AddBody(hips, 14f);
            AddBox(hips, chest.position, 0.17f);
            Rigidbody chestBody = AddBody(chest, 18f);
            AddBox(chest, head.position, 0.19f);
            Joint(chest, hipsBody, -20f, 25f, 20f);
            Rigidbody headBody = AddBody(head, 5f);
            var sphere = head.gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.11f / head.lossyScale.x;
            sphere.center = head.InverseTransformPoint(head.position + head.up * 0.09f);
            Joint(head, chestBody, -30f, 30f, 35f);

            Limb(root, "LeftArm", "LeftForeArm", chestBody, 3f, 0.055f, 70f);
            Limb(root, "LeftForeArm", "LeftHand", null, 2f, 0.045f, 10f);
            Limb(root, "RightArm", "RightForeArm", chestBody, 3f, 0.055f, 70f);
            Limb(root, "RightForeArm", "RightHand", null, 2f, 0.045f, 10f);
            Limb(root, "LeftUpLeg", "LeftLeg", hipsBody, 8f, 0.08f, 50f);
            Limb(root, "LeftLeg", "LeftFoot", null, 5f, 0.06f, 10f);
            Limb(root, "RightUpLeg", "RightLeg", hipsBody, 8f, 0.08f, 50f);
            Limb(root, "RightLeg", "RightFoot", null, 5f, 0.06f, 10f);
            return true;
        }

        // Капсула от кости до следующей; parent == null — крепится к телу родительской кости
        private static void Limb(Transform root, string boneName, string endName, Rigidbody parent, float mass, float radius, float swing)
        {
            Transform bone = Bone(root, boneName), end = Bone(root, endName);
            if (bone == null || end == null) return;
            if (parent == null)
            {
                Transform up = bone.parent;
                while (up != null && up.GetComponent<Rigidbody>() == null) up = up.parent;
                if (up == null) return;
                parent = up.GetComponent<Rigidbody>();
            }
            AddBody(bone, mass);
            Vector3 local = bone.InverseTransformPoint(end.position);
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            Vector3 abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
            capsule.direction = abs.x > abs.y && abs.x > abs.z ? 0 : abs.y > abs.z ? 1 : 2;
            capsule.center = local * 0.5f;
            float scale = Mathf.Max(0.001f, bone.lossyScale.x);
            capsule.radius = radius / scale;
            capsule.height = local.magnitude + capsule.radius * 2f;
            Joint(bone, parent, -swing * 0.5f, swing, swing * 0.6f);
        }

        private static Rigidbody AddBody(Transform bone, float mass)
        {
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return body;
        }

        private static void AddBox(Transform bone, Vector3 towards, float halfWidth)
        {
            var box = bone.gameObject.AddComponent<BoxCollider>();
            Vector3 local = bone.InverseTransformPoint(towards);
            float scale = Mathf.Max(0.001f, bone.lossyScale.x);
            box.center = local * 0.5f;
            Vector3 size = Vector3.one * (halfWidth * 2f / scale);
            Vector3 abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
            if (abs.x > abs.y && abs.x > abs.z) size.x = abs.x; else if (abs.y > abs.z) size.y = abs.y; else size.z = abs.z;
            box.size = size;
        }

        private static void Joint(Transform bone, Rigidbody parent, float lowTwist, float highTwist, float swing)
        {
            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.enableProjection = true;
            joint.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
            joint.highTwistLimit = new SoftJointLimit { limit = highTwist };
            joint.swing1Limit = new SoftJointLimit { limit = swing };
            joint.swing2Limit = new SoftJointLimit { limit = swing * 0.6f };
        }
    }
}
