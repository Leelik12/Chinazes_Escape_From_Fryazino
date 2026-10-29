using Unity.Netcode;
using UnityEngine;

namespace RacingProject.Management
{
    // Уровни гаража на этот забег: их берёт сервер (хост) из своего Garage и рассылает остальным, поэтому у обоих
    // игроков одна броня, один мотор и один короб. Без сети (обучение) — свой гараж.
    // Броню прибавляет EnemyManager.StartGame (у обоих), РПГ выдаёт сервер там же; мотор и короб читает RunUpgrades
    public class GarageLoadout : NetworkBehaviour
    {
        private static GarageLoadout instance;

        // По 4 бита на улучшение
        private readonly NetworkVariable<int> packed = new NetworkVariable<int>();

        private void Awake()
        {
            instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            int value = 0;
            for (int i = 0; i < Garage.PerkCount; i++)
                value |= Garage.Level((Garage.Perk)i) << (i * 4);
            packed.Value = value;
        }

        public override void OnDestroy()
        {
            if (instance == this) instance = null;
            base.OnDestroy();
        }

        public static int Level(Garage.Perk perk)
        {
            if (instance == null || !instance.IsSpawned) return Garage.Level(perk);
            return (instance.packed.Value >> ((int)perk * 4)) & 0xF;
        }

        public static int ArmorBonus => Garage.ArmorPerLevel * Level(Garage.Perk.Armor);
        public static int StartRockets => Garage.RocketsPerLevel * Level(Garage.Perk.Rpg);
        public static float EngineTorque => 1f + 0.05f * Level(Garage.Perk.Engine);
        public static float HeatPerShot => Mathf.Pow(0.92f, Level(Garage.Perk.Belt));
    }
}
