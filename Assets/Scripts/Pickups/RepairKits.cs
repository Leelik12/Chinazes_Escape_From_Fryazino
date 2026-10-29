using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RacingProject.Pickups
{
    // Ремкомплекты на дороге: проезд через них чинит машину игроков. Сами ремкомплекты — обычные
    // объекты сцены, а какие из них на месте, решает сервер и рассылает маской в NetworkVariable.
    // Подобранный ремкомплект появляется снова через respawnTime.
    // Ещё ремкомплекты выпадают из подбитых врагов (Drop, вызывает EnemyManager): копия первого ремкомплекта сцены
    // появляется у обоих игроков на месте взрыва и лежит dropLifetime секунд
    public class RepairKits : NetworkBehaviour
    {
        [SerializeField] private PlayerHealth carHealth;
        [Tooltip("Ремкомплект чинит и поломки: колёса, радиатор, фары, стекло")]
        [SerializeField] private RacingProject.Car.CarDamageState carDamage;
        [Tooltip("Ремкомплекты в сцене (не больше 31)")]
        [SerializeField] private GameObject[] kits;
        [SerializeField] private int healAmount = 300;
        [Tooltip("Расстояние от центра машины, на котором ремкомплект подбирается, м")]
        [SerializeField] private float pickupRadius = 4f;
        [SerializeField] private float respawnTime = 45f;
        [Tooltip("Звук подбора, слышат оба игрока")]
        [SerializeField] private AudioSource pickupSound;

        [Header("Выпавшие из врагов")]
        [SerializeField] private int dropHealAmount = 150;
        [SerializeField] private float dropLifetime = 40f;
        [Tooltip("Высота ремкомплекта над землёй, м")]
        [SerializeField] private float dropHeight = 1f;
        [Tooltip("Слои земли, на которую ложится выпавший ремкомплект")]
        [SerializeField] private LayerMask groundMask = ~0;

        private struct ServerDrop
        {
            public Vector3 position;
            public float expireAt;
        }

        private readonly Dictionary<int, ServerDrop> serverDrops = new Dictionary<int, ServerDrop>();
        private readonly Dictionary<int, GameObject> dropObjects = new Dictionary<int, GameObject>();
        private readonly List<int> finishedDrops = new List<int>();
        private int dropCounter;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];

        private const int AllKits = int.MaxValue;

        private readonly NetworkVariable<int> activeMask = new NetworkVariable<int>(AllKits);
        private float[] respawnAt;

        public override void OnNetworkSpawn()
        {
            respawnAt = new float[kits.Length];
            if (IsServer)
                activeMask.Value = AllKits;
            activeMask.OnValueChanged += OnMaskChanged;
            ApplyMask(activeMask.Value);
        }

        public override void OnNetworkDespawn()
        {
            activeMask.OnValueChanged -= OnMaskChanged;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || carHealth == null) return;

            int mask = activeMask.Value;
            Vector3 carPosition = carHealth.transform.position;
            for (int i = 0; i < kits.Length; i++)
            {
                bool active = (mask & (1 << i)) != 0;
                if (!active)
                {
                    if (Time.time >= respawnAt[i])
                        mask |= 1 << i;
                    continue;
                }

                // Целой машине ремкомплект не нужен — он остаётся на дороге
                if ((kits[i].transform.position - carPosition).sqrMagnitude <= pickupRadius * pickupRadius
                    && Repair(healAmount))
                {
                    mask &= ~(1 << i);
                    respawnAt[i] = Time.time + respawnTime;
                    PickedUpRpc();
                }
            }

            if (mask != activeMask.Value)
                activeMask.Value = mask;

            UpdateDrops(carPosition);
        }

        // Ремонт прочности и поломок; false — чинить нечего, ремкомплект остаётся лежать
        private bool Repair(int amount)
        {
            bool healed = carHealth.Heal(amount);
            bool fixedParts = carDamage != null && carDamage.Repair();
            return healed || fixedParts;
        }

        // Сервер: ремкомплект выпал из врага в этой точке
        // Ближайший к точке ремкомплект, который лежит на месте (для подсказки по рации); null — нет ни одного
        public Vector3? NearestAvailable(Vector3 from)
        {
            int mask = activeMask.Value;
            Vector3? best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < kits.Length; i++)
            {
                if (kits[i] == null || (mask & (1 << i)) == 0) continue;
                float d = (kits[i].transform.position - from).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = kits[i].transform.position; }
            }
            return best;
        }

        public void Drop(Vector3 position)
        {
            if (!IsServer || !IsSpawned || kits.Length == 0) return;
            // Земля под точкой: корпус гибнущего врага ещё на месте, его пропускаем
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 4f, Vector3.down, groundHits, 30f, groundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i].distance >= nearest || groundHits[i].collider.GetComponentInParent<Enemy.EnemyHealth>() != null) continue;
                nearest = groundHits[i].distance;
                position = groundHits[i].point;
            }
            position += Vector3.up * dropHeight;
            int id = ++dropCounter;
            serverDrops[id] = new ServerDrop { position = position, expireAt = Time.time + dropLifetime };
            DropRpc(id, position);
        }

        private void UpdateDrops(Vector3 carPosition)
        {
            finishedDrops.Clear();
            foreach (KeyValuePair<int, ServerDrop> drop in serverDrops)
            {
                bool expired = Time.time >= drop.Value.expireAt;
                bool picked = !expired && (drop.Value.position - carPosition).sqrMagnitude <= pickupRadius * pickupRadius
                              && Repair(dropHealAmount);
                if (!expired && !picked) continue;
                finishedDrops.Add(drop.Key);
                if (picked) PickedUpRpc();
            }
            foreach (int id in finishedDrops)
            {
                serverDrops.Remove(id);
                RemoveDropRpc(id);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void DropRpc(int id, Vector3 position)
        {
            GameObject template = kits[0];
            if (template == null) return;
            GameObject copy = Instantiate(template, position, template.transform.rotation, template.transform.parent);
            copy.name = template.name + " (выпал)";
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
            if (pickupSound != null)
                pickupSound.Play();
        }

        private void OnMaskChanged(int previous, int current)
        {
            ApplyMask(current);
        }

        private void ApplyMask(int mask)
        {
            for (int i = 0; i < kits.Length; i++)
            {
                if (kits[i] != null)
                    kits[i].SetActive((mask & (1 << i)) != 0);
            }
        }
    }
}
