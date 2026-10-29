using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using RacingProject.Audio;
using RacingProject.Effects;
using RacingProject.Enemy;

namespace RacingProject.Props
{
    // Озеро: триггер от поверхности до дна. Машина в воде вязнет (сопротивление растёт с глубиной) и медленно
    // тонет — плавучесть меньше веса; при падении в воду теряет часть скорости, поэтому удар о дно мягче удара о землю.
    // Силы действуют там, где тело считает физику (на хосте), всплеск и звук — у каждого игрока по позиции тела.
    // Враг, пробывший под водой дольше enemyDrownTime, гибнет. Камера под поверхностью — мутный туман (FogZones.Underwater)
    [RequireComponent(typeof(BoxCollider))]
    public class WaterVolume : MonoBehaviour
    {
        [Tooltip("Сопротивление у поверхности, 1/с")]
        [SerializeField] private float surfaceDrag = 1.2f;
        [Tooltip("Сопротивление под водой, 1/с")]
        [SerializeField] private float submergedDrag = 3.5f;
        [SerializeField] private float angularDrag = 2f;
        [Tooltip("Плавучесть — доля веса; меньше 1, поэтому машина тонет")]
        [SerializeField, Range(0f, 1f)] private float buoyancy = 0.75f;
        [Tooltip("Какая доля скорости остаётся после удара о воду")]
        [SerializeField, Range(0f, 1f)] private float entrySpeedKeep = 0.45f;
        [SerializeField] private float splashMinSpeed = 3f;
        [Tooltip("Брызги: частицы в мировых координатах, выпускаются в точке падения")]
        [SerializeField] private ParticleSystem splash;
        [Tooltip("Через сколько секунд под водой враг гибнет")]
        [SerializeField] private float enemyDrownTime = 4f;

        // Глубина центра масс, с которой тело считается утонувшим, м
        public const float SubmergedDepth = 0.8f;

        private class Entry
        {
            public int colliders;
            public float underwater;
            public float lastSplash = float.NegativeInfinity;
            public Vector3 lastPosition;
        }

        private static readonly List<WaterVolume> volumes = new List<WaterVolume>();
        private readonly Dictionary<Rigidbody, Entry> inside = new Dictionary<Rigidbody, Entry>();
        private readonly List<Rigidbody> removed = new List<Rigidbody>();
        private BoxCollider box;

        public float Surface => box.bounds.max.y;

        // Сколько секунд тело уже под водой (0 — не утонуло)
        public static float UnderwaterTime(Rigidbody body)
        {
            foreach (WaterVolume volume in volumes)
                if (body != null && volume.inside.TryGetValue(body, out Entry entry))
                    return entry.underwater;
            return 0f;
        }

        public static bool Contains(Vector3 point)
        {
            foreach (WaterVolume volume in volumes)
            {
                Bounds bounds = volume.box.bounds;
                if (point.y < bounds.max.y && point.y > bounds.min.y && point.x > bounds.min.x && point.x < bounds.max.x
                    && point.z > bounds.min.z && point.z < bounds.max.z)
                    return true;
            }
            return false;
        }

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        private void OnEnable() => volumes.Add(this);

        private void OnDisable()
        {
            volumes.Remove(this);
            inside.Clear();
            if (volumes.Count == 0) FogZones.Underwater = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null) return;
            if (!inside.TryGetValue(body, out Entry entry))
            {
                entry = new Entry { lastPosition = body.position };
                inside.Add(body, entry);
            }
            entry.colliders++;
            if (entry.colliders == 1) Splash(body, entry);
        }

        private void OnTriggerExit(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null || !inside.TryGetValue(body, out Entry entry)) return;
            if (--entry.colliders <= 0) inside.Remove(body);
        }

        private void Splash(Rigidbody body, Entry entry)
        {
            if (Time.time - entry.lastSplash < 1.5f) return;
            entry.lastSplash = Time.time;
            // У клиента тело кинематическое и скорости не знает — берём её по смещению за прошлый кадр физики
            Vector3 velocity = body.isKinematic ? (body.position - entry.lastPosition) / Time.fixedDeltaTime : body.linearVelocity;
            float speed = velocity.magnitude;
            if (!body.isKinematic && speed > splashMinSpeed) body.linearVelocity *= entrySpeedKeep;
            Vector3 point = new Vector3(body.position.x, Surface, body.position.z);
            float strength = Mathf.InverseLerp(1f, 20f, speed);
            SoundLibrary.Play(SoundLibrary.Sfx.Splash, point, Mathf.Lerp(0.35f, 1f, strength), Random.Range(0.9f, 1.05f));
            if (splash != null)
            {
                splash.transform.position = point;
                splash.Emit(Mathf.RoundToInt(Mathf.Lerp(20f, 90f, strength)));
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float surface = Surface;
            foreach (KeyValuePair<Rigidbody, Entry> pair in inside)
            {
                Rigidbody body = pair.Key;
                Entry entry = pair.Value;
                if (body == null || !body.gameObject.activeInHierarchy)
                {
                    removed.Add(body);
                    continue;
                }
                float depth = surface - body.worldCenterOfMass.y;
                entry.underwater = depth > SubmergedDepth ? entry.underwater + dt : 0f;
                entry.lastPosition = body.position;
                if (body.isKinematic) continue;

                float submerged = Mathf.Clamp01((depth + 0.5f) / 1.5f);
                body.AddForce(-body.linearVelocity * Mathf.Lerp(surfaceDrag, submergedDrag, submerged), ForceMode.Acceleration);
                body.AddForce(Vector3.up * (-Physics.gravity.y * buoyancy * submerged), ForceMode.Acceleration);
                body.AddTorque(-body.angularVelocity * angularDrag * submerged, ForceMode.Acceleration);

                if (entry.underwater > enemyDrownTime && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    EnemyHealth health = body.GetComponent<EnemyHealth>();
                    if (health != null)
                    {
                        health.RequestDamage(1000000);
                        entry.underwater = 0f;
                    }
                }
            }
            foreach (Rigidbody body in removed) inside.Remove(body);
            removed.Clear();
        }

        private void LateUpdate()
        {
            if (volumes.Count == 0 || volumes[0] != this) return;
            Camera camera = Camera.main;
            FogZones.Underwater = camera != null && Contains(camera.transform.position);
        }
    }
}
