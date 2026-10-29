using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RacingProject.Enemy;

namespace RacingProject.Effects
{
    // Силуэты врагов за стенами: у каждой вражеской машины крупные части кузова получают копию-рендерер с шейдером
    // RacingProject/Silhouette, который рисуется только там, где машину что-то закрывает. Копии делят сетку
    // с кузовом, поэтому вмятины и оторванные детали видны и в силуэте. Создаётся сам при запуске
    public class EnemySilhouettes : MonoBehaviour
    {
        // Мелочь (зеркала, ручки, фары) силуэт не рисует: меньше вызовов отрисовки
        private const float MinPartSize = 0.8f;
        // Дальше этого силуэт гаснет, м
        private const float MaxDistance = 160f;

        private class Entry
        {
            public EnemyHealth health;
            public List<Renderer> copies = new List<Renderer>();
            public bool shown = true;
        }

        private static EnemySilhouettes instance;
        private readonly Dictionary<EnemyCarController, Entry> entries = new Dictionary<EnemyCarController, Entry>();
        private readonly List<EnemyCarController> pending = new List<EnemyCarController>();
        private Material material;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("EnemySilhouettes");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<EnemySilhouettes>();
        }

        private void Awake()
        {
            material = Resources.Load<Material>("Fx/Fx_Silhouette");
        }

        private void Update()
        {
            if (material == null || Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.5f;

            // Новая машина ждёт одну проверку: CarDeformation успевает подменить сетку кузова на свою копию
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                EnemyCarController car = pending[i];
                pending.RemoveAt(i);
                if (car != null) entries[car] = Build(car);
            }
            foreach (EnemyCarController car in FindObjectsByType<EnemyCarController>(FindObjectsSortMode.None))
                if (!entries.ContainsKey(car) && !pending.Contains(car))
                    pending.Add(car);

            Vector3 eye = Audio.ActiveListener.Position;
            List<EnemyCarController> dead = null;
            foreach (KeyValuePair<EnemyCarController, Entry> pair in entries)
            {
                if (pair.Key == null)
                {
                    (dead ??= new List<EnemyCarController>()).Add(pair.Key);
                    continue;
                }
                bool alive = pair.Value.health == null || pair.Value.health.HealthFraction > 0f;
                bool show = alive && (pair.Key.transform.position - eye).sqrMagnitude < MaxDistance * MaxDistance;
                if (show == pair.Value.shown) continue;
                pair.Value.shown = show;
                foreach (Renderer copy in pair.Value.copies)
                    if (copy != null) copy.enabled = show;
            }
            if (dead != null)
                foreach (EnemyCarController car in dead)
                    entries.Remove(car);
        }

        private Entry Build(EnemyCarController car)
        {
            var entry = new Entry { health = car.GetComponent<EnemyHealth>() };
            foreach (MeshRenderer renderer in car.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || renderer.bounds.size.magnitude < MinPartSize) continue;
                // Упрощённые копии только для теней (ShadowProxy) не видны
                if (renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                // Пропускаем прозрачное (стёкла, ореолы): в силуэте они дали бы лишнюю пелену
                Material main = renderer.sharedMaterial;
                if (main != null && main.renderQueue >= (int)RenderQueue.Transparent) continue;

                var go = new GameObject("Silhouette", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(renderer.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = go.GetComponent<MeshRenderer>();
                var materials = new Material[filter.sharedMesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                copy.sharedMaterials = materials;
                copy.shadowCastingMode = ShadowCastingMode.Off;
                copy.receiveShadows = false;
                copy.lightProbeUsage = LightProbeUsage.Off;
                copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
                entry.copies.Add(copy);
            }
            return entry;
        }
    }
}
