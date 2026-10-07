using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using RacingProject.Car;
using RacingProject.Enemy;
using RacingProject.Hud;
using RacingProject.Turret;

namespace RacingProject.EditorTools
{
    // Приборы в машине игроков: собирает в сцене три экрана world-space UGUI и рисует для них спрайты.
    //  - DriverCluster — дисплей водителя на торпедо: спидометр со стрелкой, шкала прочности, передача, нитро и нагрев пулемёта;
    //  - TacticalDisplay — экран на консоли: волна, очки с комбо, убитые, прочность босса;
    //  - GunnerPanel и GunnerHeat — табло на внутренней стороне щита пулемёта: прочность машины, счёт, нагрев.
    // Старые экраны (Priborka, тексты и полосы CarParametrs) выключаются, но остаются: на них ссылаются скрипты.
    // Повторный запуск пересобирает экраны. Координаты — в локальных единицах объекта VolgaCar (масштаб 2)
    public static class CockpitHudBuilder
    {
        private const string SpriteFolder = "Assets/Content/UI/Cockpit";
        private const string FontPath = "Assets/Content/BlackOpsOne-Regular.asset";
        private const string HousingMaterialPath = "Assets/Models/Turret/TurretArmor.mat";

        private static readonly Color PanelColor = new Color(0.025f, 0.04f, 0.055f, 0.93f);
        private static readonly Color OutlineColor = new Color(0.3f, 0.85f, 1f, 0.35f);
        private static readonly Color Accent = new Color(0.35f, 0.88f, 1f);
        private static readonly Color LabelColor = new Color(0.58f, 0.68f, 0.76f);
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color Warm = new Color(1f, 0.62f, 0.2f);
        private static readonly Color Danger = new Color(1f, 0.25f, 0.2f);

        private static Sprite disk, ring, ticks, rounded, outline, glow;
        private static TMP_FontAsset font;

        [MenuItem("RacingProject/Собрать приборы")]
        public static void Build()
        {
            GenerateSprites();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            GameObject carObject = GameObject.Find("VolgaCar");
            if (carObject == null)
            {
                Debug.LogError("CockpitHudBuilder: в открытой сцене нет VolgaCar");
                return;
            }
            Transform car = carObject.transform;

            var sources = new Sources
            {
                health = carObject.GetComponent<PlayerHealth>(),
                car = carObject.GetComponent<CarControllerSample>(),
                nitro = carObject.GetComponent<CarNitro>(),
                gun = car.Find("RoofTurret/RoofGun") != null ? car.Find("RoofTurret/RoofGun").GetComponent<VRGun>() : null,
                enemies = Object.FindFirstObjectByType<EnemyManager>()
            };

            BuildDriverCluster(car, sources);
            BuildTacticalDisplay(car, sources);
            BuildGunnerPanels(car, sources);
            HideOldDisplays(car);

            EditorSceneManager.MarkSceneDirty(carObject.scene);
            Debug.Log("Приборы собраны");
        }

        private struct Sources
        {
            public PlayerHealth health;
            public CarControllerSample car;
            public CarNitro nitro;
            public VRGun gun;
            public EnemyManager enemies;
        }

        // --- Экраны ---

        // Приборка водителя — навесной дисплей на торпедо над ободом руля: штатное место приборов за рулём
        // почти целиком закрывают обод и спицы. 920×400 пикселей на 0,28 единицы машины, повёрнут к глазам водителя
        private static void BuildDriverCluster(Transform car, Sources sources)
        {
            Vector3 eye = new Vector3(-0.385f, 0.891f, 0.335f);
            Vector3 position = new Vector3(-0.386f, 0.725f, 0.84f);
            RectTransform canvas = CreateCanvas(car, "DriverCluster", position, Quaternion.LookRotation(position - eye), new Vector2(920f, 400f), 0.28f);
            Housing(car, canvas);
            Panel(canvas, Vector2.zero, new Vector2(920f, 400f));
            var display = canvas.gameObject.AddComponent<CockpitDisplay>();

            HudArcGauge speed = ArcGauge(canvas, "Speed", new Vector2(-275f, -5f), 340f, Accent, null, true, "КМ/Ч", 92f);
            HudArcGauge hp = ArcGauge(canvas, "Health", new Vector2(275f, -5f), 340f, Color.white, HealthGradient(), false, "ПРОЧНОСТЬ", 64f);

            Label(canvas, "ПЕРЕДАЧА", new Vector2(0f, 170f), 28f);
            AddImage(canvas, "GearFrame", outline, OutlineColor, new Vector2(0f, 88f), new Vector2(130f, 130f), UnityEngine.UI.Image.Type.Sliced);
            TMP_Text gear = Text(canvas, "Gear", "N", 104f, Accent, new Vector2(0f, 84f), new Vector2(130f, 130f));

            Label(canvas, "НИТРО", new Vector2(0f, -16f), 28f);
            HudBar nitro = Bar(canvas, "Nitro", new Vector2(0f, -50f), new Vector2(180f, 22f), new Color(0.3f, 0.6f, 1f), null, false);
            Label(canvas, "ПУЛЕМЁТ", new Vector2(0f, -98f), 28f);
            HudBar heat = Bar(canvas, "GunHeat", new Vector2(0f, -132f), new Vector2(180f, 22f), Color.white, HeatGradient(), false);

            Assign(display, sources, so =>
            {
                so.FindProperty("speedGauge").objectReferenceValue = speed;
                so.FindProperty("healthGauge").objectReferenceValue = hp;
                so.FindProperty("gearText").objectReferenceValue = gear;
                so.FindProperty("nitroBar").objectReferenceValue = nitro;
                so.FindProperty("heatBar").objectReferenceValue = heat;
            });
        }

        // Экран на консоли, на месте прежнего CarParametrs: 900×300 пикселей на 0,32 единицы, наклонён к водителю
        private static void BuildTacticalDisplay(Transform car, Sources sources)
        {
            RectTransform canvas = CreateCanvas(car, "TacticalDisplay", new Vector3(0.01f, 0.608f, 0.80f), Quaternion.Euler(20.7f, 0f, 0f), new Vector2(900f, 300f), 0.32f);
            Panel(canvas, Vector2.zero, new Vector2(900f, 300f));
            var display = canvas.gameObject.AddComponent<CockpitDisplay>();

            Label(canvas, "ВОЛНА", new Vector2(-300f, 102f), 34f);
            TMP_Text wave = Text(canvas, "Wave", "0", 96f, Accent, new Vector2(-300f, 30f), new Vector2(260f, 110f));
            Label(canvas, "ОЧКИ", new Vector2(0f, 102f), 34f);
            TMP_Text score = Text(canvas, "Score", "0", 80f, Color.white, new Vector2(0f, 30f), new Vector2(320f, 110f));
            TMP_Text combo = Text(canvas, "Combo", "", 40f, Warm, new Vector2(0f, -30f), new Vector2(200f, 50f));
            Label(canvas, "УБИТО", new Vector2(300f, 102f), 34f);
            TMP_Text kills = Text(canvas, "Kills", "0", 96f, Color.white, new Vector2(300f, 30f), new Vector2(260f, 110f));

            GameObject boss;
            HudBar bossBar = BossBar(canvas, new Vector2(0f, -100f), 800f, out boss);

            Assign(display, sources, so =>
            {
                so.FindProperty("waveText").objectReferenceValue = wave;
                so.FindProperty("scoreText").objectReferenceValue = score;
                so.FindProperty("comboText").objectReferenceValue = combo;
                so.FindProperty("killsText").objectReferenceValue = kills;
                so.FindProperty("bossRoot").objectReferenceValue = boss;
                so.FindProperty("bossBar").objectReferenceValue = bossBar;
            });
        }

        // Табло стрелка на внутренней стороне переднего щита, по обе стороны от пулемёта. Поворачиваются вместе
        // со станком и смотрят на глаза стрелка; верх — на уровне края щита, чтобы не загораживать дорогу
        private static void BuildGunnerPanels(Transform car, Sources sources)
        {
            Transform mount = car.Find("RoofTurret/TurretMount");
            if (mount == null)
            {
                Debug.LogWarning("CockpitHudBuilder: нет RoofTurret/TurretMount, табло стрелка не собрано");
                return;
            }

            Vector3 eye = new Vector3(0f, 0.328f, 0.035f);

            Vector3 leftPosition = new Vector3(-0.25f, 0.07f, 0.6f);
            RectTransform left = CreateCanvas(mount, "GunnerPanel", leftPosition, Quaternion.LookRotation(leftPosition - eye), new Vector2(600f, 300f), 0.24f);
            Housing(mount, left);
            Panel(left, Vector2.zero, new Vector2(600f, 300f));
            var leftDisplay = left.gameObject.AddComponent<CockpitDisplay>();

            Label(left, "МАШИНА", new Vector2(-200f, 115f), 28f);
            HudBar hp = Bar(left, "Health", new Vector2(75f, 115f), new Vector2(330f, 28f), Color.white, HealthGradient(), true);
            Label(left, "ВОЛНА", new Vector2(-190f, 62f), 28f);
            TMP_Text wave = Text(left, "Wave", "0", 64f, Accent, new Vector2(-190f, 10f), new Vector2(180f, 70f));
            Label(left, "ОЧКИ", new Vector2(10f, 62f), 28f);
            TMP_Text score = Text(left, "Score", "0", 52f, Color.white, new Vector2(10f, 10f), new Vector2(220f, 70f));
            TMP_Text combo = Text(left, "Combo", "", 30f, Warm, new Vector2(10f, -32f), new Vector2(160f, 36f));
            Label(left, "УБИТО", new Vector2(200f, 62f), 28f);
            TMP_Text kills = Text(left, "Kills", "0", 64f, Color.white, new Vector2(200f, 10f), new Vector2(180f, 70f));
            GameObject boss;
            HudBar bossBar = BossBar(left, new Vector2(0f, -100f), 540f, out boss);

            Assign(leftDisplay, sources, so =>
            {
                so.FindProperty("healthBar").objectReferenceValue = hp;
                so.FindProperty("waveText").objectReferenceValue = wave;
                so.FindProperty("scoreText").objectReferenceValue = score;
                so.FindProperty("comboText").objectReferenceValue = combo;
                so.FindProperty("killsText").objectReferenceValue = kills;
                so.FindProperty("bossRoot").objectReferenceValue = boss;
                so.FindProperty("bossBar").objectReferenceValue = bossBar;
            });

            Vector3 rightPosition = new Vector3(0.24f, 0.07f, 0.6f);
            RectTransform right = CreateCanvas(mount, "GunnerHeat", rightPosition, Quaternion.LookRotation(rightPosition - eye), new Vector2(300f, 300f), 0.12f);
            Housing(mount, right);
            Panel(right, Vector2.zero, new Vector2(300f, 300f));
            var rightDisplay = right.gameObject.AddComponent<CockpitDisplay>();
            HudArcGauge heat = ArcGauge(right, "Heat", new Vector2(0f, 0f), 260f, Color.white, HeatGradient(), false, "НАГРЕВ", 56f);
            TMP_Text warning = Text(right, "Overheat", "ПЕРЕГРЕВ", 34f, Danger, new Vector2(0f, -112f), new Vector2(280f, 44f));
            warning.gameObject.SetActive(false);

            Assign(rightDisplay, sources, so =>
            {
                so.FindProperty("heatGauge").objectReferenceValue = heat;
                so.FindProperty("overheatWarning").objectReferenceValue = warning;
            });
        }

        private static void HideOldDisplays(Transform car)
        {
            foreach (string path in new[] { "Priborka/Canvas", "Priborka/Canvas (1)" })
                SetActive(car.Find(path), false);

            // Сам CarParametrs остаётся: на нём DeathScreen и DamageFlash
            Transform parameters = car.Find("CarParametrs");
            if (parameters == null) return;
            foreach (Transform child in parameters)
                SetActive(child, false);
        }

        private static void SetActive(Transform target, bool active)
        {
            if (target == null || target.gameObject.activeSelf == active) return;
            Undo.RecordObject(target.gameObject, "Hide old display");
            target.gameObject.SetActive(active);
        }

        private static void Assign(CockpitDisplay display, Sources sources, System.Action<SerializedObject> assignWidgets)
        {
            var so = new SerializedObject(display);
            so.FindProperty("health").objectReferenceValue = sources.health;
            so.FindProperty("enemies").objectReferenceValue = sources.enemies;
            so.FindProperty("car").objectReferenceValue = sources.car;
            so.FindProperty("nitro").objectReferenceValue = sources.nitro;
            so.FindProperty("gun").objectReferenceValue = sources.gun;
            assignWidgets(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Элементы ---

        private static RectTransform CreateCanvas(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector2 sizePixels, float widthUnits)
        {
            Transform old = parent.Find(name);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            Undo.RegisterCreatedObjectUndo(go, "Build cockpit");
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = sizePixels;
            rect.localPosition = localPosition;
            rect.localRotation = localRotation;
            float scale = widthUnits / sizePixels.x;
            rect.localScale = new Vector3(scale, scale, scale);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            return rect;
        }

        // Корпус дисплея — тёмная коробка за экраном: без неё экран снаружи просвечивал бы зеркально
        private static void Housing(Transform parent, RectTransform canvas)
        {
            string name = canvas.name + "Housing";
            Transform old = parent.Find(name);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(box, "Build cockpit");
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.layer = parent.gameObject.layer;
            box.transform.SetParent(parent, false);

            const float depth = 0.014f;
            Vector2 size = canvas.sizeDelta * canvas.localScale.x;
            box.transform.localRotation = canvas.localRotation;
            box.transform.localPosition = canvas.localPosition + canvas.localRotation * new Vector3(0f, 0f, depth / 2f + 0.001f);
            box.transform.localScale = new Vector3(size.x + 0.012f, size.y + 0.012f, depth);
            box.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(HousingMaterialPath);
        }

        private static void Panel(RectTransform parent, Vector2 position, Vector2 size)
        {
            AddImage(parent, "Background", rounded, PanelColor, position, size, UnityEngine.UI.Image.Type.Sliced);
            AddImage(parent, "Outline", outline, OutlineColor, position, size, UnityEngine.UI.Image.Type.Sliced);
        }

        private static HudArcGauge ArcGauge(RectTransform parent, string name, Vector2 position, float diameter, Color fillColor, Gradient gradient,
            bool withNeedle, string label, float valueSize)
        {
            RectTransform root = Node(parent, name, position, new Vector2(diameter, diameter));
            Vector2 size = new Vector2(diameter, diameter);

            AddImage(root, "Glow", glow, new Color(fillColor.r, fillColor.g, fillColor.b, 0.08f), Vector2.zero, size * 1.1f, UnityEngine.UI.Image.Type.Simple);
            AddImage(root, "Face", disk, new Color(0f, 0f, 0f, 0.45f), Vector2.zero, size * 0.9f, UnityEngine.UI.Image.Type.Simple);
            Image track = AddImage(root, "Track", ring, TrackColor, Vector2.zero, size, UnityEngine.UI.Image.Type.Filled);
            SetupArc(track, 0.75f);
            Image fill = AddImage(root, "Fill", ring, fillColor, Vector2.zero, size, UnityEngine.UI.Image.Type.Filled);
            SetupArc(fill, 0f);
            AddImage(root, "Ticks", ticks, new Color(1f, 1f, 1f, 0.55f), Vector2.zero, size, UnityEngine.UI.Image.Type.Simple);

            RectTransform needle = null;
            if (withNeedle)
            {
                needle = Node(root, "Needle", Vector2.zero, new Vector2(10f, diameter * 0.42f));
                needle.pivot = new Vector2(0.5f, 0f);
                needle.anchoredPosition = Vector2.zero;
                AddImage(needle, "Shape", rounded, Warm, new Vector2(0f, diameter * 0.21f), new Vector2(8f, diameter * 0.42f), UnityEngine.UI.Image.Type.Sliced);
                needle.localRotation = Quaternion.Euler(0f, 0f, 135f);
                AddImage(root, "Hub", disk, new Color(0.12f, 0.14f, 0.16f), Vector2.zero, new Vector2(diameter * 0.12f, diameter * 0.12f), UnityEngine.UI.Image.Type.Simple);
            }

            // При стрелке число ниже центра, иначе по центру
            float valueY = withNeedle ? -diameter * 0.2f : diameter * 0.02f;
            TMP_Text value = Text(root, "Value", "0", valueSize, Color.white, new Vector2(0f, valueY), new Vector2(diameter * 0.7f, valueSize * 1.2f));
            Label(root, label, new Vector2(0f, withNeedle ? -diameter * 0.36f : -diameter * 0.2f), diameter * 0.085f);

            var gauge = root.gameObject.AddComponent<HudArcGauge>();
            var so = new SerializedObject(gauge);
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("needle").objectReferenceValue = needle;
            so.FindProperty("valueText").objectReferenceValue = value;
            if (gradient != null)
            {
                so.FindProperty("useGradient").boolValue = true;
                so.FindProperty("colors").gradientValue = gradient;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return gauge;
        }

        // Дуга 270° от левого нижнего угла по часовой стрелке: начало заполнения снизу, повёрнутое на 45° по часовой
        private static void SetupArc(Image image, float amount)
        {
            image.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            image.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Bottom;
            image.fillClockwise = true;
            image.fillAmount = amount;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        }

        private static HudBar Bar(RectTransform parent, string name, Vector2 position, Vector2 size, Color color, Gradient gradient, bool withValue)
        {
            RectTransform root = Node(parent, name, position, size);
            AddImage(root, "Track", rounded, TrackColor, Vector2.zero, size, UnityEngine.UI.Image.Type.Sliced);
            Image fill = AddImage(root, "Fill", rounded, color, Vector2.zero, size, UnityEngine.UI.Image.Type.Filled);
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;

            TMP_Text value = null;
            if (withValue)
                value = Text(root, "Value", "", size.y * 0.8f, Color.white, Vector2.zero, size);

            var bar = root.gameObject.AddComponent<HudBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("valueText").objectReferenceValue = value;
            if (gradient != null)
            {
                so.FindProperty("useGradient").boolValue = true;
                so.FindProperty("colors").gradientValue = gradient;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return bar;
        }

        private static HudBar BossBar(RectTransform parent, Vector2 position, float width, out GameObject root)
        {
            RectTransform node = Node(parent, "Boss", position, new Vector2(width, 60f));
            Text(node, "Title", "БОСС", 24f, Danger, new Vector2(-width / 2f + 50f, 0f), new Vector2(100f, 40f));
            HudBar bar = Bar(node, "Bar", new Vector2(50f, 0f), new Vector2(width - 120f, 28f), Danger, null, true);
            node.gameObject.SetActive(false);
            root = node.gameObject;
            return bar;
        }

        private static RectTransform Node(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image AddImage(RectTransform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size, Image.Type type)
        {
            RectTransform rect = Node(parent, name, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = type;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(RectTransform parent, string name, string content, float size, Color color, Vector2 position, Vector2 box)
        {
            RectTransform rect = Node(parent, name, position, box);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Label(RectTransform parent, string content, Vector2 position, float size)
        {
            TMP_Text text = Text(parent, "Label " + content, content, size, LabelColor, position, new Vector2(size * 12f, size * 1.4f));
            text.characterSpacing = 6f;
        }

        private static Gradient HealthGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Danger, 0f), new GradientColorKey(new Color(1f, 0.78f, 0.2f), 0.45f), new GradientColorKey(new Color(0.35f, 0.95f, 0.55f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private static Gradient HeatGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Accent, 0f), new GradientColorKey(Warm, 0.6f), new GradientColorKey(Danger, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        // --- Спрайты ---

        // Рисуются по формулам со сглаживанием краёв и сохраняются в PNG
        private static void GenerateSprites()
        {
            Directory.CreateDirectory(SpriteFolder);
            disk = SaveSprite("Disk", 256, Vector4.zero, (x, y) => Coverage(126f - Length(x, y, 128f)));
            ring = SaveSprite("Ring", 512, Vector4.zero, (x, y) =>
            {
                float d = Length(x, y, 256f);
                return Coverage(Mathf.Min(250f - d, d - 216f));
            });
            ticks = SaveSprite("Ticks", 512, Vector4.zero, TickCoverage);
            rounded = SaveSprite("Rounded", 64, new Vector4(20f, 20f, 20f, 20f), (x, y) => Coverage(-RoundedDistance(x, y, 64f, 16f)));
            outline = SaveSprite("Outline", 64, new Vector4(20f, 20f, 20f, 20f), (x, y) =>
            {
                float d = RoundedDistance(x, y, 64f, 16f);
                return Coverage(Mathf.Min(-d, d + 2.5f));
            });
            glow = SaveSprite("Glow", 128, Vector4.zero, (x, y) =>
            {
                float t = Mathf.Clamp01(1f - Length(x, y, 64f) / 63f);
                return t * t;
            });
        }

        // Риски шкалы на 270°: 21 риска, каждая вторая длиннее (деления по 10%)
        private static float TickCoverage(float x, float y)
        {
            float dx = x - 256f, dy = y - 256f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg; // 0 — вверх, по часовой
            float best = 0f;
            for (int i = 0; i <= 20; i++)
            {
                float tickAngle = -135f + 13.5f * i;
                if (Mathf.Abs(Mathf.DeltaAngle(angle, tickAngle)) > 6f) continue;
                bool major = i % 2 == 0;
                float inner = major ? 168f : 186f;
                float halfWidth = major ? 2.6f : 1.5f;
                float across = radius * Mathf.Sin(Mathf.DeltaAngle(angle, tickAngle) * Mathf.Deg2Rad);
                float along = Mathf.Min(radius - inner, 208f - radius);
                best = Mathf.Max(best, Coverage(Mathf.Min(halfWidth - Mathf.Abs(across), along)));
            }
            return best;
        }

        private static float Length(float x, float y, float center)
        {
            float dx = x - center, dy = y - center;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float RoundedDistance(float x, float y, float size, float radius)
        {
            float half = size / 2f - 1f;
            float qx = Mathf.Abs(x - size / 2f) - (half - radius);
            float qy = Mathf.Abs(y - size / 2f) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        // Плотность пикселя по расстоянию до края: сглаживание в один пиксель
        private static float Coverage(float distanceInside)
        {
            return Mathf.Clamp01(distanceInside + 0.5f);
        }

        private static Sprite SaveSprite(string name, int size, Vector4 border, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha(x + 0.5f, y + 0.5f) * 255f));
            texture.SetPixels32(pixels);
            texture.Apply();

            string path = SpriteFolder + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
