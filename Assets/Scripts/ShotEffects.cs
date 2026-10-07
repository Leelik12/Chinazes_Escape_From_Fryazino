using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RacingProject
{
    // Общие визуальные эффекты выстрела для оружия игрока и врагов
    public static class ShotEffects
    {
        // Вспышка у дула и короткая подсветка
        public static void PlayMuzzle(MonoBehaviour owner, ParticleSystem muzzleFlash, Light muzzleLight, float lightDuration)
        {
            if (muzzleFlash != null)
                muzzleFlash.Play();
            if (muzzleLight != null)
                owner.StartCoroutine(FlashLight(muzzleLight, lightDuration));
        }

        // Пыль и искры в точке попадания, смещённые по нормали, чтобы не утонуть в поверхности
        public static void SpawnImpact(GameObject dustPrefab, GameObject sparksPrefab, Vector3 point, Vector3 normal, float offset, float lifetime)
        {
            Vector3 position = point + normal * offset;
            Quaternion rotation = Quaternion.LookRotation(normal);
            SpawnTimed(dustPrefab, position, rotation, lifetime);
            SpawnTimed(sparksPrefab, position, rotation, lifetime);
        }

        // Эффекты берутся из пула по префабу: при скорострельности пулемёта
        // Instantiate/Destroy на каждое попадание давали лишний мусор для GC
        private static readonly Dictionary<GameObject, Queue<PooledEffect>> pools = new Dictionary<GameObject, Queue<PooledEffect>>();

        private static void SpawnTimed(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
        {
            if (prefab == null) return;

            if (!pools.TryGetValue(prefab, out var pool))
            {
                pool = new Queue<PooledEffect>();
                pools.Add(prefab, pool);
            }

            PooledEffect effect = null;
            // После перезагрузки сцены объекты из пула уничтожены — пропускаем их
            while (pool.Count > 0 && effect == null)
                effect = pool.Dequeue();

            if (effect == null)
            {
                GameObject instance = Object.Instantiate(prefab, position, rotation);
                effect = instance.AddComponent<PooledEffect>();
            }
            else
            {
                effect.transform.SetPositionAndRotation(position, rotation);
            }

            effect.Play(pool, lifetime);
        }

        // --- Трассеры ---

        // Материалы трассеров по цвету: яркость задаётся HDR-цветом материала, чтобы трассер ловил bloom
        private static readonly Dictionary<Color, Material> tracerMaterials = new Dictionary<Color, Material>();
        private static readonly Queue<Tracer> tracers = new Queue<Tracer>();

        // Светящийся отрезок летит от дула к точке попадания (или на дальность выстрела)
        public static void SpawnTracer(Vector3 from, Vector3 to, Color color)
        {
            if (!tracerMaterials.TryGetValue(color, out var material) || material == null)
            {
                var source = Resources.Load<Material>("Fx/Fx_Tracer");
                if (source == null) return;
                material = new Material(source);
                material.SetColor("_BaseColor", color);
                tracerMaterials[color] = material;
            }

            Tracer tracer = null;
            while (tracers.Count > 0 && tracer == null)
                tracer = tracers.Dequeue();
            if (tracer == null)
                tracer = new GameObject("Tracer").AddComponent<Tracer>();

            tracer.Launch(tracers, from, to, material);
        }

        // --- Следы от пуль ---

        // Следы — кольцевой буфер: самый старый след переезжает на место нового
        private const int MaxBulletHoles = 120;
        private static readonly List<Transform> bulletHoles = new List<Transform>();
        private static int nextBulletHole;
        private static Mesh quadMesh;
        private static Material holeMaterial;

        // След остаётся только на неподвижном окружении: коллайдеры машин — упрощённые коробки,
        // и след на них висел бы в воздухе рядом с кузовом
        public static void SpawnBulletHole(RaycastHit hit)
        {
            if (hit.collider == null || hit.rigidbody != null) return;
            if (hit.collider.GetComponentInParent<Unity.Netcode.NetworkObject>() != null) return;

            if (holeMaterial == null)
            {
                holeMaterial = Resources.Load<Material>("Fx/Fx_HoleStone");
                quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                if (holeMaterial == null || quadMesh == null) return;
            }

            if (bulletHoles.Count < MaxBulletHoles)
                bulletHoles.Add(null);
            nextBulletHole %= bulletHoles.Count;
            Transform hole = bulletHoles[nextBulletHole];
            // После перезагрузки сцены следы уничтожены — создаём заново
            if (hole == null)
            {
                var go = new GameObject("BulletHole", typeof(MeshFilter), typeof(MeshRenderer));
                go.GetComponent<MeshFilter>().sharedMesh = quadMesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = holeMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                hole = go.transform;
                bulletHoles[nextBulletHole] = hole;
            }
            nextBulletHole++;

            // Quad смотрит лицом в -Z, поэтому его Z направлен в поверхность
            Quaternion rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            hole.SetPositionAndRotation(hit.point + hit.normal * 0.01f, rotation);
            hole.localScale = Vector3.one * Random.Range(0.1f, 0.16f);
        }

        private static IEnumerator FlashLight(Light light, float duration)
        {
            light.enabled = true;
            yield return new WaitForSeconds(duration);
            light.enabled = false;
        }
    }
}
