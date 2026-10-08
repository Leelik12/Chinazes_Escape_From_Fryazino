using System;
using RacingProject.Network;
using UnityEngine;

namespace RacingProject.Management
{
    // Приборы пульта в бункере меню: сигнальные лампы и стрелочные приборы показывают состояние лобби.
    // Лампы: связь, водитель готов, поиск (мигает), нет связи, стрелок готов. Стрелки: уровень сигнала,
    // готовность экипажа (0, 1 или 2 из двух) и «напряжение сети», которое просто живёт своей жизнью.
    // Детали — объекты Bunker_Moving.fbx; оси и плоскость панели задаёт BuildBunker.cs в системе родителя детали
    public class BunkerConsole : MonoBehaviour
    {
        [Serializable]
        public class Gauge
        {
            public Transform needle;
            [Tooltip("Нормаль панели, вокруг неё поворачивается стрелка (в системе родителя стрелки)")]
            public Vector3 normal;
            [Tooltip("Направления «вправо» и «вверх по скату» панели (в системе родителя стрелки)")]
            public Vector3 right;
            public Vector3 up;

            [NonSerialized] public Quaternion rest;
            [NonSerialized] public Vector3 restDir;
            [NonSerialized] public float value, velocity;
        }

        public enum Lamp { Link, Driver, Search, NoLink, Gunner }

        [SerializeField] private RoomController room;
        [SerializeField] private LanLobby lobby;
        [Tooltip("Линзы ламп по порядку Lamp: связь, водитель, поиск, нет связи, стрелок")]
        [SerializeField] private Renderer[] lamps = new Renderer[0];
        [SerializeField] private Gauge signal = new Gauge();
        [SerializeField] private Gauge ready = new Gauge();
        [SerializeField] private Gauge volts = new Gauge();

        [Header("Шкалы")]
        [Tooltip("Угол нуля шкалы от направления «вправо», градусы (против часовой стрелки)")]
        [SerializeField] private float zeroAngle = 210f;
        [Tooltip("Размах шкалы по часовой стрелке, градусы")]
        [SerializeField] private float sweep = 240f;
        [Tooltip("Яркость погашенной лампы относительно горящей")]
        [SerializeField, Range(0f, 0.2f)] private float lampOff = 0.03f;
        [Tooltip("Цвет стекла погашенной лампы относительно горящей: без подсветки оно тёмное")]
        [SerializeField, Range(0f, 1f)] private float lensOff = 0.3f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private Color[] lampColors;
        private Color[] lensColors;
        private float[] lampLevels;
        private float seed;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            seed = UnityEngine.Random.value * 100f;
            lampColors = new Color[lamps.Length];
            lensColors = new Color[lamps.Length];
            lampLevels = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null) continue;
                lampColors[i] = lamps[i].sharedMaterial.GetColor(EmissionId);
                lensColors[i] = lamps[i].sharedMaterial.GetColor(BaseColorId);
            }
            foreach (Gauge g in new[] { signal, ready, volts })
                Prepare(g);
        }

        private void Update()
        {
            LobbyState state = LobbyState.Read(room, lobby);
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;

            // «Поиск» мигает, пока ищем игру или подключаемся
            bool busy = state.link == LobbyState.Link.Searching || state.link == LobbyState.Link.Connecting;
            SetLamp(Lamp.Link, state.Online ? 1f : 0f, dt);
            SetLamp(Lamp.Driver, state.driverReady ? 1f : 0f, dt);
            float blink = state.link == LobbyState.Link.Searching ? 0.8f : 0.4f;
            SetLamp(Lamp.Search, busy && Mathf.Repeat(t, blink) < blink / 2f ? 1f : 0f, dt);
            SetLamp(Lamp.NoLink, state.link == LobbyState.Link.Offline ? 1f : 0f, dt);
            SetLamp(Lamp.Gunner, state.gunnerReady ? 1f : 0f, dt);

            // Сигнал: без связи — у нуля, при поиске стрелка рыщет, на связи — высоко с лёгкой дрожью
            float noise = Mathf.PerlinNoise(seed, t * 1.7f) - 0.5f;
            float signalTarget;
            switch (state.link)
            {
                case LobbyState.Link.Searching: signalTarget = 0.3f + Mathf.PerlinNoise(seed + 3f, t * 0.9f) * 0.35f; break;
                case LobbyState.Link.Connecting: signalTarget = 0.6f + noise * 0.1f; break;
                case LobbyState.Link.Online: signalTarget = 0.86f + noise * 0.06f; break;
                default: signalTarget = 0.03f + Mathf.Max(0f, noise) * 0.04f; break;
            }
            Drive(signal, signalTarget, 6f, 0.7f, dt);
            Drive(ready, state.ReadyCount / 2f, 9f, 0.35f, dt);
            Drive(volts, 0.62f + (Mathf.PerlinNoise(seed + 7f, t * 0.6f) - 0.5f) * 0.08f + (Mathf.PerlinNoise(seed + 9f, t * 11f) - 0.5f) * 0.015f, 10f, 0.8f, dt);
        }

        private void SetLamp(Lamp lamp, float target, float dt)
        {
            int i = (int)lamp;
            if (i >= lamps.Length || lamps[i] == null) return;
            // Нить накала разгорается и гаснет не мгновенно
            lampLevels[i] = Mathf.MoveTowards(lampLevels[i], target, dt * 12f);
            lamps[i].GetPropertyBlock(block);
            block.SetColor(EmissionId, lampColors[i] * Mathf.Lerp(lampOff, 1f, lampLevels[i]));
            Color lens = lensColors[i] * Mathf.Lerp(lensOff, 1f, lampLevels[i]);
            lens.a = lensColors[i].a;
            block.SetColor(BaseColorId, lens);
            lamps[i].SetPropertyBlock(block);
        }

        // Направление стрелки в покое — к центру её сетки (стрелка длинная только в одну сторону от оси)
        private static void Prepare(Gauge g)
        {
            if (g.needle == null) return;
            g.rest = g.needle.localRotation;
            var filter = g.needle.GetComponent<MeshFilter>();
            Vector3 dir = filter != null && filter.sharedMesh != null ? g.rest * filter.sharedMesh.bounds.center : g.right;
            g.restDir = Vector3.ProjectOnPlane(dir, g.normal).normalized;
        }

        // Пружинный привод стрелки: жёсткость задаёт скорость, демпфирование — перелёт
        private void Drive(Gauge g, float target, float stiffness, float damping, float dt)
        {
            if (g.needle == null) return;
            float accel = stiffness * stiffness * (target - g.value) - 2f * damping * stiffness * g.velocity;
            g.velocity += accel * dt;
            g.value = Mathf.Clamp(g.value + g.velocity * dt, -0.02f, 1.02f);
            float a = (zeroAngle - sweep * g.value) * Mathf.Deg2Rad;
            Vector3 dir = Mathf.Cos(a) * g.right + Mathf.Sin(a) * g.up;
            g.needle.localRotation = Quaternion.AngleAxis(Vector3.SignedAngle(g.restDir, dir, g.normal), g.normal) * g.rest;
        }
    }
}
