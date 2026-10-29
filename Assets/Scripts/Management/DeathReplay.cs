using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using RacingProject.Enemy;

namespace RacingProject.Management
{
    // Повтор последних секунд перед гибелью машины (только на мониторе; в VR облёт камерой укачивает).
    // Каждый игрок пишет у себя позы машины игроков и врагов (корень и каждую видимую деталь относительно него)
    // несколько раз в секунду в кольцевой буфер. При гибели настоящие машины прячутся, вместо них по записи едут
    // копии из тех же мешей и материалов, отдельная камера облетает машину игроков, последняя секунда замедлена.
    // Экран гибели (DeathScreen) ждёт конца повтора, RoomController добавляет его длительность к паузе перед перезапуском
    public class DeathReplay : MonoBehaviour
    {
        [SerializeField] private PlayerHealth carHealth;
        [Tooltip("Сколько последних секунд хранится")]
        [SerializeField] private float recordSeconds = 5f;
        [SerializeField] private float recordRate = 20f;
        [Tooltip("Последние секунды повтора идут медленнее, во столько раз")]
        [SerializeField] private float slowSeconds = 1f;
        [SerializeField, Range(0.1f, 1f)] private float slowSpeed = 0.4f;

        [Header("Камера")]
        [SerializeField] private float cameraDistance = 10f;
        [SerializeField] private float cameraHeight = 3.5f;
        [Tooltip("На сколько градусов камера обходит машину за повтор")]
        [SerializeField] private float orbitDegrees = 70f;
        [SerializeField] private LayerMask cameraObstacles = ~0;
        [SerializeField] private TMP_FontAsset font;

        public static DeathReplay Instance { get; private set; }
        // Идёт повтор: экран гибели ждёт
        public bool Playing { get; private set; }
        // Для интерфейса на OnGUI (стрелки на врагов, задание волны): пока идёт повтор, не рисуется
        public static bool Active => Instance != null && Instance.Playing;
        // Сколько займёт повтор (для паузы перед перезапуском раунда)
        public float Duration => Mathf.Max(0f, recordSeconds - slowSeconds) + slowSeconds / slowSpeed;

        private class Part
        {
            public Renderer renderer;
            public Mesh mesh;
            public Material[] materials;
            public UnityEngine.Rendering.ShadowCastingMode shadows;
            public Vector3 scale;
        }

        private class Track
        {
            public Transform root;
            public Vector3 rootScale;
            public readonly List<Part> parts = new List<Part>();
            public Vector3[] rootPosition;
            public Quaternion[] rootRotation;
            public Vector3[] partPosition;
            public Quaternion[] partRotation;
            public bool[] partVisible;
            // Кадры (сквозной номер), в которые объект был записан
            public int firstFrame;
            public int lastFrame = -1;
            public bool player;
        }

        private readonly List<Track> tracks = new List<Track>();
        private readonly HashSet<Transform> known = new HashSet<Transform>();
        // Снимки поз персонажей: удаляются после повтора
        private readonly List<Mesh> baked = new List<Mesh>();
        private int capacity;
        private int frame;
        private float nextRecord;
        private float nextScan;
        private bool recording = true;

        private void Awake()
        {
            Instance = this;
            capacity = Mathf.Max(2, Mathf.CeilToInt(recordSeconds * recordRate));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (carHealth != null) carHealth.OnDeath += OnCarDeath;
        }

        private void OnDisable()
        {
            if (carHealth != null) carHealth.OnDeath -= OnCarDeath;
        }

        private void Update()
        {
            if (!recording || carHealth == null || !carHealth.IsSpawned) return;
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.5f;
                Scan();
            }
            if (Time.time < nextRecord) return;
            nextRecord = Time.time + 1f / recordRate;
            Record();
        }

        // Новые машины: игроков, врагов и вагоны поезда (их спавнит сервер, у клиента они появляются сами)
        private void Scan()
        {
            if (!known.Contains(carHealth.transform)) Register(carHealth.transform, true);
            foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
                if (!known.Contains(enemy.transform)) Register(enemy.transform, false);
            // Вагоны электрички (ElectricTrain): видны, только пока поезд на линии
            if (Props.ElectricTrain.Instance != null)
                foreach (Transform car in Props.ElectricTrain.Instance.Cars)
                    if (!known.Contains(car)) Register(car, false);
            // Давно пропавшие враги (подбиты) выпадают из буфера
            tracks.RemoveAll(t => t.root == null && frame - t.lastFrame > capacity);
        }

        private void Register(Transform root, bool player)
        {
            known.Add(root);
            var track = new Track { root = root, rootScale = root.lossyScale, player = player, firstFrame = frame };
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (renderer is MeshRenderer)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                }
                else if (renderer is SkinnedMeshRenderer skinned)
                    mesh = skinned.sharedMesh;
                if (mesh == null) continue;
                Vector3 rootScale = root.lossyScale;
                Vector3 scale = renderer.transform.lossyScale;
                track.parts.Add(new Part
                {
                    renderer = renderer,
                    mesh = mesh,
                    materials = renderer.sharedMaterials,
                    shadows = renderer.shadowCastingMode,
                    scale = new Vector3(scale.x / rootScale.x, scale.y / rootScale.y, scale.z / rootScale.z),
                });
            }
            int count = track.parts.Count;
            track.rootPosition = new Vector3[capacity];
            track.rootRotation = new Quaternion[capacity];
            track.partPosition = new Vector3[capacity * count];
            track.partRotation = new Quaternion[capacity * count];
            track.partVisible = new bool[capacity * count];
            tracks.Add(track);
        }

        private void Record()
        {
            int slot = frame % capacity;
            foreach (Track track in tracks)
            {
                if (track.root == null) continue;
                Transform root = track.root;
                track.rootPosition[slot] = root.position;
                track.rootRotation[slot] = root.rotation;
                Quaternion inverse = Quaternion.Inverse(root.rotation);
                int count = track.parts.Count;
                for (int p = 0; p < count; p++)
                {
                    Renderer renderer = track.parts[p].renderer;
                    int i = slot * count + p;
                    bool visible = renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
                    track.partVisible[i] = visible;
                    if (!visible) continue;
                    Transform t = renderer.transform;
                    track.partPosition[i] = root.InverseTransformPoint(t.position);
                    track.partRotation[i] = inverse * t.rotation;
                }
                track.lastFrame = frame;
            }
            frame++;
        }

        private void OnCarDeath()
        {
            if (!recording) return;
            Record();
            recording = false;
            if (ViewModeService.IsVR || frame < recordRate) return;
            StartCoroutine(Play());
        }

        // --- Проигрывание ---

        private class Ghost
        {
            public Track track;
            public GameObject root;
            public Transform[] parts;
        }

        private IEnumerator Play()
        {
            Playing = true;
            Camera main = Camera.main;
            int last = frame - 1;
            int first = Mathf.Max(0, frame - capacity);

            // Настоящие машины прячутся на время повтора
            var hidden = new List<Renderer>();
            foreach (Track track in tracks)
                if (track.root != null)
                    foreach (Renderer renderer in track.root.GetComponentsInChildren<Renderer>())
                        if (renderer.enabled) { renderer.enabled = false; hidden.Add(renderer); }

            // Приборы и прицел на экране — тоже: повтор смотрится без интерфейса
            var hiddenCanvases = new List<Canvas>();
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.isRootCanvas && canvas.enabled && canvas.renderMode != RenderMode.WorldSpace)
                {
                    canvas.enabled = false;
                    hiddenCanvases.Add(canvas);
                }

            var ghosts = new List<Ghost>();
            foreach (Track track in tracks)
                if (track.lastFrame >= first && track.firstFrame <= last) ghosts.Add(BuildGhost(track));

            Camera replayCamera = BuildCamera(main);
            GameObject overlay = BuildOverlay();
            Ghost car = ghosts.Find(g => g.track.player);

            float fast = Mathf.Max(0f, (last - first) / recordRate - slowSeconds);
            float recorded = 0f;
            float total = (last - first) / recordRate;
            float orbitStart = car != null ? car.track.rootRotation[last % capacity].eulerAngles.y + 35f : 0f;
            while (recorded < total)
            {
                recorded += Time.deltaTime * (recorded < fast ? 1f : slowSpeed);
                float position = first + Mathf.Min(recorded, total) * recordRate;
                foreach (Ghost ghost in ghosts) Pose(ghost, position);
                if (car != null && replayCamera != null)
                    PlaceCamera(replayCamera.transform, car.root.transform.position, orbitStart + orbitDegrees * recorded / Mathf.Max(0.01f, total));
                yield return null;
            }

            foreach (Ghost ghost in ghosts) Destroy(ghost.root);
            foreach (Mesh mesh in baked) Destroy(mesh);
            baked.Clear();
            foreach (Renderer renderer in hidden) if (renderer != null) renderer.enabled = true;
            foreach (Canvas canvas in hiddenCanvases) if (canvas != null) canvas.enabled = true;
            if (replayCamera != null) Destroy(replayCamera.gameObject);
            if (main != null) main.enabled = true;
            Destroy(overlay);
            Playing = false;
        }

        private Ghost BuildGhost(Track track)
        {
            var ghost = new Ghost { track = track, root = new GameObject("Replay " + (track.root != null ? track.root.name : "wreck")) };
            // Позы деталей записаны в пространстве корня вместе с его масштабом
            ghost.root.transform.localScale = track.rootScale;
            ghost.parts = new Transform[track.parts.Count];
            for (int p = 0; p < track.parts.Count; p++)
            {
                Part part = track.parts[p];
                var go = new GameObject(part.mesh.name);
                go.transform.SetParent(ghost.root.transform, false);
                go.transform.localScale = part.scale;
                Mesh mesh = part.mesh;
                // Живая поза персонажа: снимок на момент гибели
                if (part.renderer is SkinnedMeshRenderer skinned && skinned != null)
                {
                    mesh = new Mesh();
                    baked.Add(mesh);
                    // Масштаб уже в меше: в мире деталь должна остаться с масштабом 1
                    skinned.BakeMesh(mesh, true);
                    go.transform.localScale = new Vector3(1f / track.rootScale.x, 1f / track.rootScale.y, 1f / track.rootScale.z);
                }
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = part.materials;
                renderer.shadowCastingMode = part.shadows;
                ghost.parts[p] = go.transform;
            }
            return ghost;
        }

        // Поза копии между двумя записанными кадрами
        private void Pose(Ghost ghost, float position)
        {
            Track track = ghost.track;
            int a = Mathf.Clamp(Mathf.FloorToInt(position), track.firstFrame, track.lastFrame);
            int b = Mathf.Min(a + 1, track.lastFrame);
            float t = Mathf.Clamp01(position - a);
            bool alive = position >= track.firstFrame && position <= track.lastFrame + 0.5f;
            if (ghost.root.activeSelf != alive) ghost.root.SetActive(alive);
            if (!alive) return;
            int sa = a % capacity, sb = b % capacity;
            ghost.root.transform.SetPositionAndRotation(
                Vector3.Lerp(track.rootPosition[sa], track.rootPosition[sb], t),
                Quaternion.Slerp(track.rootRotation[sa], track.rootRotation[sb], t));
            int count = track.parts.Count;
            for (int p = 0; p < count; p++)
            {
                int ia = sa * count + p, ib = sb * count + p;
                Transform part = ghost.parts[p];
                bool visible = track.partVisible[ia];
                if (part.gameObject.activeSelf != visible) part.gameObject.SetActive(visible);
                if (!visible) continue;
                bool next = track.partVisible[ib];
                part.localPosition = next ? Vector3.Lerp(track.partPosition[ia], track.partPosition[ib], t) : track.partPosition[ia];
                part.localRotation = next ? Quaternion.Slerp(track.partRotation[ia], track.partRotation[ib], t) : track.partRotation[ia];
            }
        }

        // Камера спереди-сбоку машины (видны и погоня сзади), обходит её по кругу; стены и дома не загораживают — камера подъезжает ближе
        private void PlaceCamera(Transform cam, Vector3 target, float angle)
        {
            Vector3 focus = target + Vector3.up * 1.2f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * cameraDistance + Vector3.up * cameraHeight;
            float distance = offset.magnitude;
            if (Physics.SphereCast(focus, 0.4f, offset.normalized, out RaycastHit hit, distance, cameraObstacles, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Renderer>() != null)
                distance = Mathf.Max(2.5f, hit.distance - 0.3f);
            cam.position = focus + offset.normalized * distance;
            cam.rotation = Quaternion.LookRotation(focus - cam.position);
        }

        private static Camera BuildCamera(Camera main)
        {
            var go = new GameObject("ReplayCamera");
            var cam = go.AddComponent<Camera>();
            if (main != null)
            {
                cam.CopyFrom(main);
                cam.fieldOfView = 50f;
                var source = main.GetComponent<UniversalAdditionalCameraData>();
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                if (source != null)
                {
                    data.renderPostProcessing = source.renderPostProcessing;
                    data.antialiasing = source.antialiasing;
                }
                main.enabled = false;
            }
            return cam;
        }

        // Полосы кино сверху и снизу и надпись «ПОВТОР»
        private GameObject BuildOverlay()
        {
            var go = new GameObject("ReplayOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            Bar(go.transform, 0f);
            Bar(go.transform, 1f);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(60f, -28f);
            rect.sizeDelta = new Vector2(600f, 60f);
            var text = label.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = "● ПОВТОР";
            text.fontSize = 38f;
            text.color = new Color(0.9f, 0.25f, 0.2f);
            text.raycastTarget = false;
            return go;
        }

        private static void Bar(Transform parent, float y)
        {
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var rect = (RectTransform)bar.transform;
            rect.anchorMin = new Vector2(0f, y);
            rect.anchorMax = new Vector2(1f, y);
            rect.pivot = new Vector2(0.5f, y);
            rect.sizeDelta = new Vector2(0f, 110f);
            var image = bar.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }
    }
}
