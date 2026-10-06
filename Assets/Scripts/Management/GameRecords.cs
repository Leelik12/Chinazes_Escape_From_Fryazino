using UnityEngine;

namespace RacingProject.Management
{
    // Рекорды очков и убийств: хранятся в PlayerPrefs у каждого игрока на его компьютере
    public static class GameRecords
    {
        private const string BestKillsKey = "BestKills";
        private const string BestScoreKey = "BestScore";

        public static int BestKills => PlayerPrefs.GetInt(BestKillsKey, 0);
        public static int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);

        // Возвращают true, если это новый рекорд
        public static bool SubmitKills(int kills)
        {
            return Submit(BestKillsKey, kills);
        }

        public static bool SubmitScore(int score)
        {
            return Submit(BestScoreKey, score);
        }

        private static bool Submit(string key, int value)
        {
            if (value <= PlayerPrefs.GetInt(key, 0)) return false;

            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            return true;
        }
    }
}
