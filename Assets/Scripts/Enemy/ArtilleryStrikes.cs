using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using RacingProject.Props;

namespace RacingProject.Enemy
{
    // Артналёт: опасность окружения, а не враг. Сервер (GameDirector) выбирает квадрат на пути машины игроков,
    // у обоих игроков на земле загорается красный круг и мигает сигнальный огонь, издалека доносятся выстрелы
    // орудий, затем со свистом падают снаряды в случайные точки круга. Точки и время задаёт одно зерно,
    // поэтому разрывы у игроков совпадают; урон машине и врагам, поджог бочек и падение опор считает сервер
    public class ArtilleryStrikes : NetworkBehaviour
    {
        public static ArtilleryStrikes Instance { get; private set; }
        // Только сервер: объявлен налёт (центр квадрата) — для рации
        public static event System.Action<Vector3> ServerStrikeCalled;
        // У всех: налёт идёт (для музыки)
        public bool Active => active > 0;

        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private Material markerMaterial;
        [SerializeField] private AudioClip[] launchSounds = new AudioClip[0];
        [SerializeField] private AudioClip[] whistleSounds = new AudioClip[0];
        [SerializeField] private AudioMixerGroup output;

        [Header("Налёт")]
        [Tooltip("Радиус квадрата под огнём, м")]
        [SerializeField] private float strikeRadius = 18f;
        [Tooltip("Сколько горит круг до первого разрыва, с")]
        [SerializeField] private float warningTime = 5f;
        [Tooltip("Пауза между разрывами, с (случайно в диапазоне)")]
        [SerializeField] private Vector2 shellInterval = new Vector2(0.3f, 0.75f);
        [Tooltip("За сколько до разрыва слышен свист снаряда, с")]
        [SerializeField] private float whistleLead = 1.4f;

        [Header("Разрыв")]
        [SerializeField] private float blastRadius = 9f;
        [Tooltip("Урон машине игроков в центре разрыва, к краю — 25 %")]
        [SerializeField] private int playerDamage = 240;
        [SerializeField] private int enemyDamage = 700;
        [SerializeField] private float carPush = 8000f;
        [SerializeField] private float propPush = 12f;

        private const int MarkerPoints = 48;
        private int active;
        private readonly HashSet<Object> hitThisShell = new HashSet<Object>();

        private void Awake()
        {
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        // Сервер: налёт на квадрат с центром center из shells снарядов
        public void CallStrike(Vector3 center, int shells)
        {
            if (IsSpawned && !IsServer) return;
            ServerStrikeCalled?.Invoke(center);
            int seed = Random.Range(1, int.MaxValue);
            if (IsSpawned)
                StrikeRpc(center, shells, seed);
            else
                StartCoroutine(Strike(center, shells, seed));
        }

        [Rpc(SendTo.Everyone)]
        private void StrikeRpc(Vector3 center, int shells, int seed)
        {
            StartCoroutine(Strike(center, shells, seed));
        }

        private IEnumerator Strike(Vector3 center, int shells, int seed)
        {
            active++;
            var rnd = new System.Random(seed);
            center = OnGround(center);

            // Точки и время разрывов — заранее, из зерна
            var points = new Vector3[shells];
            var times = new float[shells];
            float t = warningTime;
            for (int i = 0; i < shells; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rnd.NextDouble()) * strikeRadius;
                points[i] = OnGround(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                times[i] = t;
                t += Mathf.Lerp(shellInterval.x, shellInterval.y, (float)rnd.NextDouble());
            }

            LineRenderer marker = MakeMarker(center);
            Light flare = MakeFlare(center);
            // Выстрелы орудий издалека: за пару секунд до первых свистов
            Vector3 battery = center + Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f) * Vector3.forward * 120f;
            for (int i = 0; i < Mathf.Min(shells, 4); i++)
                if (launchSounds.Length > 0)
                    StartCoroutine(PlayLater(launchSounds[rnd.Next(launchSounds.Length)], battery, 0.4f + i * 0.35f, 0.9f, 400f, 1f));

            float start = Time.time;
            int next = 0, whistled = 0;
            while (next < shells)
            {
                float now = Time.time - start;
                // Круг пульсирует, пока не упадёт последний снаряд
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 9f);
                if (marker != null)
                {
                    marker.widthMultiplier = Mathf.Lerp(0.25f, 0.6f, pulse);
                    marker.startColor = marker.endColor = new Color(1f, 0.12f, 0.05f, Mathf.Lerp(0.5f, 1f, pulse));
                }
                if (flare != null)
                    flare.intensity = Mathf.Lerp(2f, 8f, Mathf.PerlinNoise(now * 6f, 0.3f));

                while (whistled < shells && now >= times[whistled] - whistleLead)
                {
                    if (whistleSounds.Length > 0)
                        PlayAt(whistleSounds[whistled % whistleSounds.Length], points[whistled], 1f, 260f, 0.94f + 0.03f * (whistled % 4));
                    whistled++;
                }
                while (next < shells && now >= times[next])
                    Impact(points[next++]);
                yield return null;
            }
            if (marker != null) Destroy(marker.gameObject);
            if (flare != null) Destroy(flare.gameObject);
            active--;
        }

        private void Impact(Vector3 point)
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, point, Quaternion.identity);
            LooseProp.Blast(point, blastRadius * 1.5f, propPush);
            if (IsSpawned && !IsServer) return;

            hitThisShell.Clear();
            foreach (Collider c in Physics.OverlapSphere(point, blastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                // ClosestPoint умеют только простые формы и выпуклый меш; невыпуклый меш, террейн и колёса — по габаритам
                bool simple = c is BoxCollider || c is SphereCollider || c is CapsuleCollider || c is MeshCollider mesh && mesh.convex;
                Vector3 nearest = simple ? c.ClosestPoint(point) : c.bounds.ClosestPoint(point);
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(point, nearest) / blastRadius);

                PlayerHealth player = c.GetComponentInParent<PlayerHealth>();
                if (player != null && hitThisShell.Add(player))
                {
                    player.RequestDamage(Mathf.RoundToInt(playerDamage * Mathf.Lerp(0.25f, 1f, falloff)), point);
                    Rigidbody body = player.GetComponent<Rigidbody>();
                    if (body != null && !body.isKinematic)
                        body.AddExplosionForce(carPush * falloff, point, blastRadius * 1.5f, 0.8f, ForceMode.Impulse);
                }
                EnemyHealth enemy = c.GetComponentInParent<EnemyHealth>();
                if (enemy != null && hitThisShell.Add(enemy))
                {
                    enemy.RequestDamage(Mathf.RoundToInt(enemyDamage * falloff));
                    Rigidbody body = enemy.GetComponent<Rigidbody>();
                    if (body != null && !body.isKinematic)
                        body.AddExplosionForce(carPush * falloff, point, blastRadius * 1.5f, 0.8f, ForceMode.Impulse);
                }
            }
            if (PropNetwork.Instance != null)
                PropNetwork.Instance.IgniteInRadius(point, blastRadius * 0.6f);
            if (PowerLines.Instance != null)
                PowerLines.Instance.ToppleInRadius(point, blastRadius * 0.7f);
        }

        // Красный круг по рельефу: светящаяся линия без тумана, видна издалека
        private LineRenderer MakeMarker(Vector3 center)
        {
            var go = new GameObject("ArtilleryMarker");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = MarkerPoints;
            line.sharedMaterial = markerMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < MarkerPoints; i++)
            {
                float a = i * Mathf.PI * 2f / MarkerPoints;
                line.SetPosition(i, OnGround(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * strikeRadius) + Vector3.up * 0.25f);
            }
            return line;
        }

        // Сигнальный огонь в центре квадрата
        private Light MakeFlare(Vector3 center)
        {
            var go = new GameObject("ArtilleryFlare");
            go.transform.SetParent(transform, false);
            go.transform.position = center + Vector3.up * 1.5f;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.15f, 0.08f);
            light.range = strikeRadius * 1.6f;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Vector3 OnGround(Vector3 p)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == null)
                return hit.point;
            if (terrain != null)
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            return p;
        }

        private IEnumerator PlayLater(AudioClip clip, Vector3 position, float delay, float volume, float maxDistance, float pitch)
        {
            yield return new WaitForSeconds(delay);
            PlayAt(clip, position, volume, maxDistance, pitch);
        }

        private void PlayAt(AudioClip clip, Vector3 position, float volume, float maxDistance, float pitch)
        {
            if (clip == null) return;
            var go = new GameObject("ArtillerySound");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            AudioSource source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = output;
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 10f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.Play();
            Destroy(go, clip.length / pitch + 0.1f);
        }
    }
}
