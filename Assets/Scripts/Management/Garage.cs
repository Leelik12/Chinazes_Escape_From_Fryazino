using System;
using UnityEngine;

namespace RacingProject.Management
{
    // Гараж бункера — постоянные улучшения между забегами. За каждый забег экипаж получает талоны (DeathScreen),
    // в гараже (вкладка терминала, GaragePage) их тратят на уровни: броня, мотор, короб пулемёта, РПГ с собой.
    // Всё хранится в PlayerPrefs у каждого игрока; в забеге действует гараж хоста (GarageLoadout рассылает его уровни)
    public static class Garage
    {
        public enum Perk { Armor, Engine, Belt, Rpg }

        private const string CoinsKey = "GarageCoins";
        private const string LevelKey = "GarageLevel";

        private static readonly string[] Titles = { "Броня кузова", "Расточка мотора", "Короб пулемёта", "РПГ с собой" };
        private static readonly string[] Descriptions =
        {
            "+" + ArmorPerLevel + " к прочности машины за уровень",
            "Тяга мотора +5 % за уровень",
            "Пулемёт перегревается на 8 % медленнее за уровень",
            "Гранатомёт на турели с первой волны: " + RocketsPerLevel + " выстрела за уровень",
        };
        private static readonly int[] MaxLevels = { 5, 5, 5, 3 };
        private static readonly int[] BaseCosts = { 150, 150, 120, 300 };

        public const int ArmorPerLevel = 60;
        public const int RocketsPerLevel = 2;

        // Талоны или уровни изменились — для вкладки гаража
        public static event Action Changed;

        public static int PerkCount => MaxLevels.Length;
        public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);
        public static string Title(Perk perk) => Titles[(int)perk];
        public static string Description(Perk perk) => Descriptions[(int)perk];
        public static int MaxLevel(Perk perk) => MaxLevels[(int)perk];
        public static int Level(Perk perk) => Mathf.Clamp(PlayerPrefs.GetInt(LevelKey + perk, 0), 0, MaxLevel(perk));
        public static bool Maxed(Perk perk) => Level(perk) >= MaxLevel(perk);
        // Следующий уровень дороже: цена растёт с каждым купленным
        public static int Cost(Perk perk) => BaseCosts[(int)perk] * (Level(perk) + 1);

        // Награда за забег: очки и пройденные волны
        public static int RunReward(int score, int wave) => Mathf.Max(0, score / 20 + Mathf.Max(0, wave - 1) * 10);

        public static void AddCoins(int amount)
        {
            if (amount <= 0) return;
            PlayerPrefs.SetInt(CoinsKey, Coins + amount);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool TryBuy(Perk perk)
        {
            if (Maxed(perk) || Coins < Cost(perk)) return false;
            PlayerPrefs.SetInt(CoinsKey, Coins - Cost(perk));
            PlayerPrefs.SetInt(LevelKey + perk, Level(perk) + 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }
    }
}
