using System.Collections.Generic;
using RacingProject.Enemy;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;

namespace RacingProject.Props
{
    // Электричка в духе ЭД4М: четыре вагона (головной, моторный, прицепной и развёрнутый головной) проходят по
    // железной дороге через лес и переезды (Tools/Unity/BuildRailway.cs). Запуск решает сервер (событие карты
    // MapEvents), всем уходит только время отправления и направление: положение поезда каждый считает сам по
    // времени сервера, поэтому у обоих игроков он едет одинаково. Вагоны — кинематические тела, отталкивают машины;
    // сервер наносит урон машине игроков и сносит врагов, оказавшихся на пути. Переезды закрываются заранее,
    // перед каждым поезд гудит. Вне линии вагоны скрыты
    public class ElectricTrain : NetworkBehaviour
    {
        [Tooltip("Ось пути на уровне подошвы шпал (низ моделей вагонов) — точки через равный шаг, от начала линии к концу")]
        [SerializeField] private Vector3[] track = new Vector3[0];
        [Tooltip("Вагоны по порядку от переднего (смотрит к концу линии) к заднему (развёрнут)")]
        [SerializeField] private Rigidbody[] cars = new Rigidbody[0];
        [Tooltip("Расстояние между центрами соседних вагонов и между тележками вагона, м")]
        [SerializeField] private float carPitch = 44.1f;
        [SerializeField] private float bogieBase = 30f;
        [SerializeField] private float speed = 32f;
        [SerializeField] private RailCrossing[] crossings = new RailCrossing[0];
        [Tooltip("За сколько секунд до подхода поезда переезд закрывается")]
        [SerializeField] private float closeAhead = 14f;
        [SerializeField] private float hornDistance = 260f;

        [Header("Урон")]
        [SerializeField] private int carDamage = 400;
        [SerializeField] private float carDamageCooldown = 2f;
        [SerializeField] private int enemyDamage = 5000;

        [Header("Звук")]
        [SerializeField] private AudioMixerGroup output;
        [SerializeField] private AudioClip horn;
        [SerializeField] private AudioClip passLoop;

        public static ElectricTrain Instance { get; private set; }
        public bool Running => running;
        public float LineLength => (track.Length - 1) * step;
        public IReadOnlyList<RailCrossing> Crossings => crossings;
        public IEnumerable<Transform> Cars
        {
            get { foreach (Rigidbody car in cars) if (car != null) yield return car.transform; }
        }

        private float step = 1f;
        private bool running;
        private double departure;
        private int direction = 1;
        private float nextCarHit;
        private readonly bool[] honked = new bool[16];
        private AudioSource[] rumble;
        private AudioSource[] hornSources;
        private readonly HashSet<Object> hitNow = new HashSet<Object>();
        private readonly Collider[] overlap = new Collider[32];

        private void Awake()
        {
            Instance = this;
            if (track.Length > 1) step = Vector3.Distance(track[0], track[1]);
            rumble = new AudioSource[cars.Length];
            hornSources = new AudioSource[cars.Length];
            for (int i = 0; i < cars.Length; i++)
            {
                if (cars[i] == null) continue;
                // Звучат только крайние вагоны: гул идёт вдоль всего поезда, гудок — из кабины
                if (i == 0 || i == cars.Length - 1)
                {
                    rumble[i] = MakeSource(cars[i].transform, 320f, passLoop, true);
                    hornSources[i] = MakeSource(cars[i].transform, 900f, horn, false);
                }
                cars[i].gameObject.SetActive(false);
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        // Сервер: отправить поезд; dir = 1 — от начала линии к концу, -1 — обратно
        public bool Launch(int dir)
        {
            if (!IsServer || running || track.Length < 2) return false;
            StartRpc(NetworkManager.ServerTime.Time, dir >= 0 ? 1 : -1);
            return true;
        }

        // Когда голова поезда, отправленного сейчас в направлении dir, дойдёт до точки линии s, с
        public float TimeTo(float s, int dir)
        {
            return (dir >= 0 ? s : LineLength - s) / speed;
        }

        [Rpc(SendTo.Everyone)]
        private void StartRpc(double time, int dir)
        {
            departure = time;
            direction = dir;
            running = true;
            for (int i = 0; i < honked.Length; i++) honked[i] = false;
        }

        // Голова (центр крайнего вагона по ходу) — в точке линии, считается от времени отправления
        private float HeadDistance()
        {
            float travelled = (float)(NetworkManager.ServerTime.Time - departure) * speed;
            float lead = carPitch * 0.5f;
            return direction > 0 ? -lead + travelled : LineLength + lead - travelled;
        }

        private float Span => carPitch * (cars.Length - 1);

        private void FixedUpdate()
        {
            if (!running || NetworkManager == null || !NetworkManager.IsListening) return;
            float head = HeadDistance();
            // Передний по ходу вагон: при движении к началу линии впереди оказывается последний
            float front = direction > 0 ? head : head + Span;
            float tail = front - Span - carPitch;
            if (direction > 0 ? tail > LineLength : front + carPitch < 0f)
            {
                Finish();
                return;
            }

            bool server = IsServer;
            for (int i = 0; i < cars.Length; i++)
            {
                Rigidbody car = cars[i];
                if (car == null) continue;
                float s = front - i * carPitch;
                bool inside = s > carPitch * 0.5f && s < LineLength - carPitch * 0.5f;
                if (car.gameObject.activeSelf != inside)
                {
                    car.gameObject.SetActive(inside);
                    if (inside)
                    {
                        Pose(s, out Vector3 p, out Quaternion r, i == cars.Length - 1);
                        car.position = p;
                        car.rotation = r;
                        car.transform.SetPositionAndRotation(p, r);
                        if (rumble[i] != null) rumble[i].Play();
                    }
                }
                if (!inside) continue;
                Pose(s, out Vector3 position, out Quaternion rotation, i == cars.Length - 1);
                car.MovePosition(position);
                car.MoveRotation(rotation);
                if (server) HitAlong(car, position, rotation);
            }
            UpdateCrossings(front, front - Span);
        }

        private void Finish()
        {
            running = false;
            for (int i = 0; i < cars.Length; i++)
            {
                if (cars[i] == null) continue;
                if (rumble[i] != null) rumble[i].Stop();
                cars[i].gameObject.SetActive(false);
            }
            foreach (RailCrossing crossing in crossings)
                if (crossing != null) crossing.SetClosed(false);
        }

        // Положение вагона по двум тележкам на линии; последний вагон развёрнут кабиной назад
        private void Pose(float s, out Vector3 position, out Quaternion rotation, bool reversed)
        {
            Vector3 a = Sample(s + bogieBase * 0.5f);
            Vector3 b = Sample(s - bogieBase * 0.5f);
            position = (a + b) * 0.5f;
            Vector3 forward = a - b;
            if (reversed) forward = -forward;
            rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward : Vector3.forward, Vector3.up);
        }

        private Vector3 Sample(float s)
        {
            float f = Mathf.Clamp(s / step, 0f, track.Length - 1.001f);
            int i = (int)f;
            return Vector3.Lerp(track[i], track[i + 1], f - i);
        }

        // Сервер: кто оказался в габарите вагона — машине игроков урон (с паузой), враг сносится
        private void HitAlong(Rigidbody car, Vector3 position, Quaternion rotation)
        {
            BoxCollider box = car.GetComponent<BoxCollider>();
            if (box == null) return;
            Vector3 center = car.transform.TransformPoint(box.center);
            int count = Physics.OverlapBoxNonAlloc(center, box.size * 0.5f + new Vector3(0.6f, 0f, 0.6f), overlap, rotation, ~0,
                QueryTriggerInteraction.Ignore);
            hitNow.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider c = overlap[i];
                if (c.attachedRigidbody == car) continue;
                PlayerHealth player = c.GetComponentInParent<PlayerHealth>();
                if (player != null && !player.IsDead && Time.time >= nextCarHit && hitNow.Add(player))
                {
                    nextCarHit = Time.time + carDamageCooldown;
                    player.RequestDamage(carDamage, center);
                }
                EnemyHealth enemy = c.GetComponentInParent<EnemyHealth>();
                if (enemy != null && hitNow.Add(enemy))
                    enemy.ApplyServerDamage(enemyDamage, NetworkManager.ServerClientId);
            }
        }

        // Переезд закрыт, пока поезд на нём или подойдёт к нему меньше чем за closeAhead секунд; перед ним — гудок
        private void UpdateCrossings(float front, float back)
        {
            float head = direction > 0 ? front : back;
            for (int i = 0; i < crossings.Length; i++)
            {
                RailCrossing crossing = crossings[i];
                if (crossing == null) continue;
                float s = crossing.TrackDistance;
                float ahead = (s - head) * direction;
                bool onIt = s > back - carPitch * 0.6f && s < front + carPitch * 0.6f;
                crossing.SetClosed(onIt || (ahead > 0f && ahead < closeAhead * speed));
                if (i < honked.Length && !honked[i] && ahead > 0f && ahead < hornDistance)
                {
                    honked[i] = true;
                    AudioSource source = hornSources[direction > 0 ? 0 : hornSources.Length - 1];
                    if (source != null && source.gameObject.activeInHierarchy) source.Play();
                }
            }
        }

        private AudioSource MakeSource(Transform parent, float maxDistance, AudioClip clip, bool loop)
        {
            if (clip == null) return null;
            var go = new GameObject(loop ? "TrainRumble" : "TrainHorn");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 3f + Vector3.forward * 18f;
            AudioSource source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = output;
            source.clip = clip;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 15f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0.6f;
            source.playOnAwake = false;
            return source;
        }
    }
}
