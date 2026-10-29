using Unity.Netcode;
using UnityEngine;
using RacingProject.Car;

namespace RacingProject.Pickups
{
    // Баллоны закиси азота на дороге: проезд через баллон пополняет нитро машины игроков.
    // Как и ремкомплекты, баллоны — объекты сцены, а какие на месте, решает сервер и рассылает маской.
    // Подобранный баллон появляется снова через respawnTime; при полном нитро баллон остаётся лежать
    public class NitroPickups : NetworkBehaviour
    {
        [SerializeField] private CarNitro carNitro;
        [Tooltip("Баллоны в сцене (не больше 31)")]
        [SerializeField] private GameObject[] canisters = new GameObject[0];
        [Tooltip("Доля заряда, которую даёт баллон")]
        [SerializeField, Range(0f, 1f)] private float refill = 1f;
        [Tooltip("Расстояние от центра машины, на котором баллон подбирается, м")]
        [SerializeField] private float pickupRadius = 4f;
        [SerializeField] private float respawnTime = 40f;
        [Tooltip("Звук подбора, слышат оба игрока")]
        [SerializeField] private AudioSource pickupSound;

        private const int All = int.MaxValue;

        private readonly NetworkVariable<int> activeMask = new NetworkVariable<int>(All);
        private float[] respawnAt;

        public override void OnNetworkSpawn()
        {
            respawnAt = new float[canisters.Length];
            if (IsServer)
                activeMask.Value = All;
            activeMask.OnValueChanged += OnMaskChanged;
            ApplyMask(activeMask.Value);
        }

        public override void OnNetworkDespawn()
        {
            activeMask.OnValueChanged -= OnMaskChanged;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || carNitro == null) return;

            int mask = activeMask.Value;
            Vector3 carPosition = carNitro.transform.position;
            for (int i = 0; i < canisters.Length; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    if (Time.time >= respawnAt[i])
                        mask |= 1 << i;
                    continue;
                }
                if (canisters[i] != null
                    && (canisters[i].transform.position - carPosition).sqrMagnitude <= pickupRadius * pickupRadius
                    && carNitro.Refill(refill))
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
            for (int i = 0; i < canisters.Length; i++)
            {
                if (canisters[i] != null)
                    canisters[i].SetActive((mask & (1 << i)) != 0);
            }
        }
    }
}
