using System.Collections;
using UnityEngine;

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

    private static void SpawnTimed(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
    {
        if (prefab == null) return;
        Object.Destroy(Object.Instantiate(prefab, position, rotation), lifetime);
    }

    private static IEnumerator FlashLight(Light light, float duration)
    {
        light.enabled = true;
        yield return new WaitForSeconds(duration);
        light.enabled = false;
    }
}
