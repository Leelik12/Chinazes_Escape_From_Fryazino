using UnityEngine;

namespace RacingProject.Management
{
    // Рекорд убийств: хранится в PlayerPrefs у каждого игрока на его компьютере
    public static class GameRecords
    {
        private const string BestKillsKey = "BestKills";

        public static int BestKills => PlayerPrefs.GetInt(BestKillsKey, 0);

        // Возвращает true, если это новый рекорд
        public static bool SubmitKills(int kills)
        {
            if (kills <= BestKills) return false;

            PlayerPrefs.SetInt(BestKillsKey, kills);
            PlayerPrefs.Save();
            return true;
        }
    }
}
