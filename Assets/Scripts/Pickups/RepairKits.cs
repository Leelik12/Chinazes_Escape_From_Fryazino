using Unity.Netcode;
using UnityEngine;

namespace RacingProject.Pickups
{
    // Ремкомплекты на дороге: проезд через них чинит машину игроков. Сами ремкомплекты — обычные
    // объекты сцены, а какие из них на месте, решает сервер и рассылает маской в NetworkVariable.
    // Подобранный ремкомплект появляется снова через respawnTime
    public class RepairKits : NetworkBehaviour
    {
        [SerializeField] private PlayerHealth carHealth;
        [Tooltip("Ремкомплекты в сцене (не больше 31)")]
        [SerializeField] private GameObject[] kits;
        [SerializeField] private int healAmount = 300;
        [Tooltip("Расстояние от центра машины, на котором ремкомплект подбирается, м")]
        [SerializeField] private float pickupRadius = 4f;
        [SerializeField] private float respawnTime = 45f;
        [Tooltip("Звук подбора, слышат оба игрока")]
        [SerializeField] private AudioSource pickupSound;

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
                    && carHealth.Heal(healAmount))
                {
                    mask &= ~(1 << i);
                    respawnAt[i] = Time.time + respawnTime;
                    PickedUpRpc();
                }
            }

            if (mask != activeMask.Value)
                activeMask.Value = mask;
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
