using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RacingProject.Effects
{
    // Дополнение к частицам взрыва: ударная волна кольцом по земле, марево (дрожание картинки сферой) и подпалина
    // на дороге, которая остаётся после взрыва. Создаётся сам при запуске и слушает ExplosionEffect.Exploded,
    // поэтому работает для всех взрывов (машины, ракеты, бочки, РПГ в обучении)
    public class ExplosionFx : MonoBehaviour
    {
        private const float WaveTime = 0.55f;
        private const float HeatTime = 0.9f;
        private const int MaxScorches = 30;
        // Подпалина не исчезает, пока не понадобится место под новую; на старых — плавное выцветание
        private const float ScorchFadeAfter = 90f;

        private class Wave
        {
            public Transform ring;
            public Transform heat;
            public float start;
            public float radius;
        }

        private static ExplosionFx instance;
        private Material ringMaterial;
        private Material heatMaterial;
        private Material scorchMaterial;
        private Mesh quad;
        private Mesh sphere;
        private readonly List<Wave> waves = new List<Wave>();
        private readonly Queue<Wave> freeWaves = new Queue<Wave>();
        private readonly List<Transform> scorches = new List<Transform>();
        private readonly List<float> scorchTimes = new List<float>();
        private int nextScorch;
        private MaterialPropertyBlock block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("ExplosionFx");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ExplosionFx>();
        }

        private void Awake()
        {
            ringMaterial = Resources.Load<Material>("Fx/Fx_Shockwave");
            heatMaterial = Resources.Load<Material>("Fx/Fx_Heat");
            scorchMaterial = Resources.Load<Material>("Fx/Fx_Scorch");
            quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            block = new MaterialPropertyBlock();
        }

        private void OnEnable() => ExplosionEffect.Exploded += OnExploded;
        private void OnDisable() => ExplosionEffect.Exploded -= OnExploded;

        private void OnExploded(Vector3 position, float power)
        {
            Vector3 ground = position;
            Vector3 normal = Vector3.up;
            bool onGround = Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == null;
            if (onGround)
            {
                ground = hit.point;
                normal = hit.normal;
            }
            SpawnWave(position, ground, normal, Mathf.Lerp(6f, 16f, power));
            if (onGround && hit.collider.GetComponentInParent<Unity.Netcode.NetworkObject>() == null)
                SpawnScorch(hit.point, hit.normal, Mathf.Lerp(2.5f, 5.5f, power));
        }

        private void SpawnWave(Vector3 center, Vector3 ground, Vector3 normal, float radius)
        {
            if (ringMaterial == null || quad == null) return;
            Wave wave = freeWaves.Count > 0 ? freeWaves.Dequeue() : CreateWave();
            wave.ring.gameObject.SetActive(true);
            // Квад лицом вверх по нормали земли, чуть выше неё
            wave.ring.SetPositionAndRotation(ground + normal * 0.15f, Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            wave.ring.localScale = Vector3.one * radius * 2f;
            bool heat = wave.heat != null && OpaqueTextureAvailable();
            wave.heat?.gameObject.SetActive(heat);
            if (heat) wave.heat.position = center + Vector3.up * 1f;
            wave.start = Time.time;
            wave.radius = radius;
            waves.Add(wave);
        }

        private Wave CreateWave()
        {
            var wave = new Wave { ring = CreateRenderer("Shockwave", quad, ringMaterial).transform };
            if (heatMaterial != null && sphere != null)
                wave.heat = CreateRenderer("HeatHaze", sphere, heatMaterial).transform;
            return wave;
        }

        private GameObject CreateRenderer(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        // Марево берёт снимок сцены; на слабом качестве (Mobile) его нет, и сфера была бы чёрной
        private static bool OpaqueTextureAvailable()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return asset != null && asset.supportsCameraOpaqueTexture;
        }

        private void Update()
        {
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                Wave wave = waves[i];
                float age = Time.time - wave.start;
                float ringT = Mathf.Clamp01(age / WaveTime);
                // Кольцо быстро вылетает и тормозит
                block.SetFloat("_Progress", 1f - (1f - ringT) * (1f - ringT));
                wave.ring.GetComponent<MeshRenderer>().SetPropertyBlock(block);
                if (wave.heat != null && wave.heat.gameObject.activeSelf)
                {
                    float heatT = Mathf.Clamp01(age / HeatTime);
                    wave.heat.localScale = Vector3.one * wave.radius * Mathf.Lerp(0.3f, 1.1f, Mathf.Sqrt(heatT));
                    block.SetFloat("_Progress", heatT);
                    wave.heat.GetComponent<MeshRenderer>().SetPropertyBlock(block);
                }
                if (age < Mathf.Max(WaveTime, HeatTime)) continue;
                wave.ring.gameObject.SetActive(false);
                wave.heat?.gameObject.SetActive(false);
                waves.RemoveAt(i);
                freeWaves.Enqueue(wave);
            }

            for (int i = 0; i < scorches.Count; i++)
            {
                if (scorches[i] == null) continue;
                float age = Time.time - scorchTimes[i];
                if (age < ScorchFadeAfter) continue;
                float k = 1f - Mathf.Clamp01((age - ScorchFadeAfter) / 20f);
                block.SetColor("_BaseColor", new Color(1f, 1f, 1f, k));
                scorches[i].GetComponent<MeshRenderer>().SetPropertyBlock(block);
                if (k <= 0f) scorches[i].gameObject.SetActive(false);
            }
            block.Clear();
        }

        // Подпалины — кольцевой буфер, как следы пуль
        private void SpawnScorch(Vector3 point, Vector3 normal, float size)
        {
            if (scorchMaterial == null || quad == null) return;
            if (scorches.Count < MaxScorches)
            {
                scorches.Add(null);
                scorchTimes.Add(0f);
            }
            nextScorch %= scorches.Count;
            Transform scorch = scorches[nextScorch];
            if (scorch == null)
            {
                scorch = CreateRenderer("Scorch", quad, scorchMaterial).transform;
                // В сцене, а не под этим объектом: при смене сцены подпалины уходят вместе с ней
                scorch.SetParent(null, true);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(scorch.gameObject, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                scorches[nextScorch] = scorch;
            }
            scorch.gameObject.SetActive(true);
            scorch.GetComponent<MeshRenderer>().SetPropertyBlock(null);
            scorch.SetPositionAndRotation(point + normal * 0.03f, Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            scorch.localScale = Vector3.one * size;
            scorchTimes[nextScorch] = Time.time;
            nextScorch++;
        }
    }
}
