using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RacingProject.Audio;

namespace RacingProject.Effects
{
    // Уличные фонари: большинство горит (натрий — тёплый, ртуть — холодный), часть мигает, часть разбита.
    // Состояние фонаря зависит только от его позиции, поэтому у обоих игроков одно и то же.
    // У горящего — светящийся плафон (виден сквозь туман) и слабый конус света в воздухе; настоящий свет
    // (прожектор вниз, без теней) — только у maxLights ближайших к слушателю, остальные дорого и издалека не видны.
    // Сбитый машиной фонарь (наклон больше 35°) гаснет со снопом искр
    public class StreetLights : MonoBehaviour
    {
        [SerializeField] private Transform[] lamps = new Transform[0];
        [Tooltip("Плафон в координатах фонаря")]
        [SerializeField] private Vector3 headOffset = new Vector3(0f, 15.6f, 4.0f);
        [SerializeField] private Material glowMaterial;
        [SerializeField] private Material beamMaterial;
        [SerializeField] private ParticleSystem sparks;

        [Header("Свет")]
        [SerializeField] private int maxLights = 32;
        [SerializeField] private float intensity = 450f;
        [SerializeField] private float range = 34f;
        [SerializeField] private float spotAngle = 120f;
        [SerializeField] private Color sodium = new Color(1f, 0.68f, 0.36f);
        [SerializeField] private Color mercury = new Color(0.72f, 0.86f, 1f);
        [SerializeField, Range(0f, 1f)] private float mercuryShare = 0.3f;
        [SerializeField, Range(0f, 1f)] private float flickerShare = 0.15f;
        [SerializeField, Range(0f, 1f)] private float brokenShare = 0.15f;
        [SerializeField] private float glowSize = 2.2f;
        [SerializeField] private float beamLength = 15f;
        [SerializeField] private float beamRadius = 6f;
        [SerializeField, Range(0f, 1f)] private float beamAlpha = 0.1f;

        private enum State { On, Flicker, Broken }

        private class Lamp
        {
            public Transform root;
            public State state;
            public Color color;
            public float seed;
            public Renderer glow;
            public Renderer beam;
            public Light light;
            public float level = 1f;
            public bool knocked;
        }

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BeamColorId = Shader.PropertyToID("_Color");
        private readonly List<Lamp> all = new List<Lamp>();
        private readonly List<Light> pool = new List<Light>();
        private readonly List<Lamp> nearest = new List<Lamp>();
        private MaterialPropertyBlock block;
        private Mesh quad;
        private Mesh cone;
        private float nextSort;

        private void Start()
        {
            block = new MaterialPropertyBlock();
            quad = BuildQuad();
            cone = Searchlight.BuildCone(beamLength, beamRadius, beamAlpha);
            foreach (Transform root in lamps)
            {
                if (root == null) continue;
                Vector3 p = root.position;
                // Детерминированный «случай» по позиции: одинаков у обоих игроков
                float h = Hash(p.x * 0.731f + p.z * 1.379f);
                var lamp = new Lamp
                {
                    root = root,
                    seed = Hash(p.z * 0.517f - p.x * 0.913f) * 100f,
                    state = h < brokenShare ? State.Broken : h < brokenShare + flickerShare ? State.Flicker : State.On,
                    color = Hash(p.x * 2.17f + p.z * 0.29f) < mercuryShare ? mercury : sodium,
                };
                if (lamp.state != State.Broken) CreateVisuals(lamp);
                all.Add(lamp);
            }
            for (int i = 0; i < maxLights; i++)
            {
                var go = new GameObject("StreetLight " + i);
                go.transform.SetParent(transform, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.shadows = LightShadows.None;
                light.range = range;
                light.spotAngle = spotAngle;
                light.innerSpotAngle = spotAngle * 0.4f;
                light.enabled = false;
                pool.Add(light);
            }
        }

        private static float Hash(float x)
        {
            float s = Mathf.Sin(x * 12.9898f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        private void CreateVisuals(Lamp lamp)
        {
            var head = new GameObject("LampGlow");
            head.transform.SetParent(lamp.root, false);
            head.transform.localPosition = headOffset;
            head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (glowMaterial != null)
            {
                head.AddComponent<MeshFilter>().sharedMesh = quad;
                var renderer = head.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = glowMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                head.transform.localScale = Vector3.one * glowSize;
                lamp.glow = renderer;
            }
            if (beamMaterial != null)
            {
                var beam = new GameObject("LampBeam", typeof(MeshFilter), typeof(MeshRenderer));
                beam.transform.SetParent(lamp.root, false);
                beam.transform.localPosition = headOffset - Vector3.up * 0.2f;
                beam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                beam.GetComponent<MeshFilter>().sharedMesh = cone;
                var renderer = beam.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = beamMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                lamp.beam = renderer;
            }
        }

        // Квадрат 1×1, смотрит вниз после поворота на 90° по X
        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "LampGlowQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Update()
        {
            float time = Time.time;
            foreach (Lamp lamp in all)
            {
                if (lamp.root == null || lamp.state == State.Broken) continue;
                if (!lamp.knocked && Vector3.Angle(lamp.root.up, Vector3.up) > 35f)
                {
                    lamp.knocked = true;
                    Vector3 head = lamp.root.TransformPoint(headOffset);
                    if (sparks != null)
                    {
                        sparks.transform.position = head;
                        sparks.Emit(60);
                    }
                    SoundLibrary.Play(SoundLibrary.Sfx.Glass, head, 0.8f);
                }
                lamp.level = lamp.knocked ? 0f : lamp.state == State.Flicker ? Flicker(time, lamp.seed) : 1f;
                SetVisual(lamp);
            }

            if (time >= nextSort)
            {
                nextSort = time + 0.5f;
                SortNearest();
            }
            for (int i = 0; i < pool.Count; i++)
            {
                Light light = pool[i];
                if (i >= nearest.Count || nearest[i].level <= 0.01f)
                {
                    light.enabled = false;
                    continue;
                }
                Lamp lamp = nearest[i];
                light.enabled = true;
                light.transform.SetPositionAndRotation(lamp.root.TransformPoint(headOffset - Vector3.up * 0.3f), lamp.root.rotation * Quaternion.Euler(90f, 0f, 0f));
                light.color = lamp.color;
                light.intensity = intensity * lamp.level;
            }
        }

        // Неисправная лампа: подолгу горит, потом трещит сериями вспышек и гаснет на секунду-две
        private static float Flicker(float time, float seed)
        {
            float slow = Mathf.PerlinNoise(time * 0.35f, seed);
            if (slow > 0.55f) return 1f;
            if (slow < 0.3f) return 0.04f;
            return Mathf.PerlinNoise(time * 18f, seed + 7f) > 0.5f ? 1f : 0.1f;
        }

        private void SetVisual(Lamp lamp)
        {
            if (lamp.glow != null)
            {
                block.Clear();
                block.SetColor(ColorId, lamp.color * (4f * lamp.level));
                lamp.glow.SetPropertyBlock(block);
                lamp.glow.enabled = lamp.level > 0.01f;
            }
            if (lamp.beam != null)
            {
                block.Clear();
                block.SetColor(BeamColorId, lamp.color * lamp.level);
                lamp.beam.SetPropertyBlock(block);
                lamp.beam.enabled = lamp.level > 0.05f;
            }
        }

        private void SortNearest()
        {
            Vector3 eye = ActiveListener.Position;
            nearest.Clear();
            foreach (Lamp lamp in all)
                if (lamp.root != null && lamp.state != State.Broken && !lamp.knocked)
                    nearest.Add(lamp);
            nearest.Sort((a, b) => (a.root.position - eye).sqrMagnitude.CompareTo((b.root.position - eye).sqrMagnitude));
            if (nearest.Count > pool.Count) nearest.RemoveRange(pool.Count, nearest.Count - pool.Count);
        }
    }
}
