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

        private static IEnumerator FlashLight(Light light, float duration)
        {
            light.enabled = true;
            yield return new WaitForSeconds(duration);
            light.enabled = false;
        }
    }
}
