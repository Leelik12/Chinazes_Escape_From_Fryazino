using System.Collections.Generic;
using UnityEngine;
using RacingProject.Audio;
using RacingProject.Enemy;

namespace RacingProject.Effects
{
    // Фары врагов — настоящие прожекторы с тенями, но каждый такой свет дорог. Горят и отбрасывают тени только
    // у ближайших машин, следующие светят без теней, у дальних прожекторы гаснут (светятся сами фары-стёкла
    // и точечные огни). Пересчёт пару раз в секунду, создаётся сам при запуске
    public class EnemyLightBudget : MonoBehaviour
    {
        // Сколько ближайших врагов светят с тенями и сколько — без теней
        private const int ShadowedCars = 2;
        private const int LitCars = 6;
        private const float Interval = 0.4f;

        private class Entry
        {
            public EnemyCarController car;
            public Light[] spots;
            public LightShadows[] shadows;
            public float sqrDistance;
        }

        private static EnemyLightBudget instance;
        private readonly Dictionary<EnemyCarController, Entry> entries = new Dictionary<EnemyCarController, Entry>();
        private readonly List<Entry> sorted = new List<Entry>();
        private float nextUpdate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("EnemyLightBudget");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<EnemyLightBudget>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + Interval;

            Vector3 eye = ActiveListener.Position;
            sorted.Clear();
            foreach (EnemyCarController car in FindObjectsByType<EnemyCarController>(FindObjectsSortMode.None))
            {
                if (!entries.TryGetValue(car, out Entry entry))
                {
                    entry = CreateEntry(car);
                    entries[car] = entry;
                }
                entry.sqrDistance = (car.transform.position - eye).sqrMagnitude;
                sorted.Add(entry);
            }
            sorted.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));
            for (int i = 0; i < sorted.Count; i++)
            {
                Entry entry = sorted[i];
                for (int s = 0; s < entry.spots.Length; s++)
                {
                    if (entry.spots[s] == null) continue;
                    entry.spots[s].enabled = i < LitCars;
                    entry.spots[s].shadows = i < ShadowedCars ? entry.shadows[s] : LightShadows.None;
                }
            }

            // Уничтоженные машины
            if (entries.Count > sorted.Count)
            {
                var dead = new List<EnemyCarController>();
                foreach (EnemyCarController car in entries.Keys)
                    if (car == null) dead.Add(car);
                foreach (EnemyCarController car in dead)
                    entries.Remove(car);
            }
        }

        private static Entry CreateEntry(EnemyCarController car)
        {
            var spots = new List<Light>();
            foreach (Light light in car.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Spot) spots.Add(light);
            var entry = new Entry { car = car, spots = spots.ToArray(), shadows = new LightShadows[spots.Count] };
            for (int i = 0; i < spots.Count; i++)
            {
                entry.shadows[i] = spots[i].shadows;
                // Свет фар не освещает сам кузов изнутри и не тратит тени на мелочь
                spots[i].shadowNearPlane = Mathf.Max(spots[i].shadowNearPlane, 0.5f);
            }
            return entry;
        }
    }
}
