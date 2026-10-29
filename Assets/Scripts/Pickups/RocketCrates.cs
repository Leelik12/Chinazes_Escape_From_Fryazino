using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using RacingProject.Enemy;
using RacingProject.Turret;

namespace RacingProject.Pickups
{
    // Ящики выстрелов к РПГ: проезд через ящик даёт выстрелы гранатомёту стрелка (RocketLauncher), первый ящик
    // приносит и сам гранатомёт. Ящики лежат на карте (объекты сцены, какие на месте — решает сервер и рассылает
    // маской, как у ремкомплектов) и выпадают из подбитых «буханок» с гранатомётчиком, а из босса — всегда.
    // Полному гранатомёту ящик не нужен — он остаётся лежать
    public class RocketCrates : NetworkBehaviour
    {
        [SerializeField] private RocketLauncher launcher;
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private Transform car;
        [Tooltip("Ящики в сцене (не больше 31)")]
        [SerializeField] private GameObject[] crates = new GameObject[0];
        [SerializeField] private int rocketsPerCrate = 2;
        [SerializeField] private float pickupRadius = 4.5f;
        [SerializeField] private float respawnTime = 90f;
        [SerializeField] private AudioSource pickupSound;

        [Header("Выпавшие из врагов")]
        [Tooltip("Шаблон выпавшего ящика (выключенный объект сцены)")]
        [SerializeField] private GameObject dropTemplate;
        [SerializeField] private int rocketsPerDrop = 1;
        [SerializeField] private int rocketsPerBossDrop = 3;
        [Tooltip("Доля остальных врагов, из которых выпадает ящик")]
        [SerializeField, Range(0f, 1f)] private float otherDropChance = 0.06f;
        [SerializeField] private float dropLifetime = 45f;
        [SerializeField] private LayerMask groundMask = ~0;

        private const int All = int.MaxValue;
        private readonly NetworkVariable<int> activeMask = new NetworkVariable<int>(All);
        private float[] respawnAt;

        private struct ServerDrop
        {
            public Vector3 position;
            public float expireAt;
            public int rockets;
        }

        private readonly Dictionary<int, ServerDrop> serverDrops = new Dictionary<int, ServerDrop>();
        private readonly Dictionary<int, GameObject> dropObjects = new Dictionary<int, GameObject>();
        private readonly List<int> finished = new List<int>();
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private int dropCounter;

        public override void OnNetworkSpawn()
        {
            respawnAt = new float[crates.Length];
            if (IsServer)
            {
                activeMask.Value = All;
                if (enemyManager != null) enemyManager.ServerEnemySpawned += OnEnemySpawned;
            }
            activeMask.OnValueChanged += OnMaskChanged;
            ApplyMask(activeMask.Value);
        }

        public override void OnNetworkDespawn()
        {
            if (enemyManager != null) enemyManager.ServerEnemySpawned -= OnEnemySpawned;
            activeMask.OnValueChanged -= OnMaskChanged;
        }

        private void OnEnemySpawned(GameObject enemy)
        {
            EnemyHealth health = enemy != null ? enemy.GetComponent<EnemyHealth>() : null;
            if (health != null) health.OnDeath += OnEnemyDied;
        }

        private void OnEnemyDied(GameObject enemy)
        {
            EnemyHealth health = enemy.GetComponent<EnemyHealth>();
            if (health != null) health.OnDeath -= OnEnemyDied;
            bool boss = enemy.name.Contains("Boss");
            bool rpg = enemy.GetComponentInChildren<EnemyRocketLauncher>(true) != null;
            if (boss) Drop(enemy.transform.position, rocketsPerBossDrop);
            else if (rpg || Random.value < otherDropChance) Drop(enemy.transform.position, rocketsPerDrop);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || car == null || launcher == null) return;
            Vector3 carPosition = car.position;
            int mask = activeMask.Value;
            for (int i = 0; i < crates.Length; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    if (Time.time >= respawnAt[i]) mask |= 1 << i;
                    continue;
                }
                if (crates[i] != null && (crates[i].transform.position - carPosition).sqrMagnitude <= pickupRadius * pickupRadius
                    && launcher.AddAmmo(rocketsPerCrate))
                {
                    mask &= ~(1 << i);
                    respawnAt[i] = Time.time + respawnTime;
                    PickedUpRpc();
                }
            }
            if (mask != activeMask.Value) activeMask.Value = mask;

            finished.Clear();
            foreach (KeyValuePair<int, ServerDrop> drop in serverDrops)
            {
                bool expired = Time.time >= drop.Value.expireAt;
                bool picked = !expired && (drop.Value.position - carPosition).sqrMagnitude <= pickupRadius * pickupRadius
                              && launcher.AddAmmo(drop.Value.rockets);
                if (!expired && !picked) continue;
                finished.Add(drop.Key);
                if (picked) PickedUpRpc();
            }
            foreach (int id in finished)
            {
                serverDrops.Remove(id);
                RemoveDropRpc(id);
            }
        }

        // Сервер: сброс с вертолёта (событие карты MapEvents) — обычный ящик в точке
        public void DropSupply(Vector3 position)
        {
            Drop(position, rocketsPerCrate);
        }

        // Ящик ложится на землю рядом с остовом, чуть в стороне от ремкомплекта
        private void Drop(Vector3 position, int rockets)
        {
            if (!IsServer || dropTemplate == null) return;
            position += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * 2.5f;
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 4f, Vector3.down, groundHits, 30f, groundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i].distance >= nearest || groundHits[i].collider.GetComponentInParent<EnemyHealth>() != null) continue;
                nearest = groundHits[i].distance;
                position = groundHits[i].point;
            }
            int id = ++dropCounter;
            serverDrops[id] = new ServerDrop { position = position, expireAt = Time.time + dropLifetime, rockets = rockets };
            DropRpc(id, position);
        }

        [Rpc(SendTo.Everyone)]
        private void DropRpc(int id, Vector3 position)
        {
            if (dropTemplate == null) return;
            GameObject copy = Instantiate(dropTemplate, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), dropTemplate.transform.parent);
            copy.name = dropTemplate.name + " (выпал)";
            copy.SetActive(true);
            dropObjects[id] = copy;
        }

        [Rpc(SendTo.Everyone)]
        private void RemoveDropRpc(int id)
        {
            if (dropObjects.TryGetValue(id, out GameObject copy))
            {
                if (copy != null) Destroy(copy);
                dropObjects.Remove(id);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void PickedUpRpc()
        {
            if (pickupSound != null) pickupSound.Play();
        }

        private void OnMaskChanged(int previous, int current)
        {
            ApplyMask(current);
        }

        private void ApplyMask(int mask)
        {
            for (int i = 0; i < crates.Length; i++)
                if (crates[i] != null) crates[i].SetActive((mask & (1 << i)) != 0);
        }
    }
}
