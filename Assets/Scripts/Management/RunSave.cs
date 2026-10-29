using System;
using UnityEngine;

namespace RacingProject.Management
{
    // Сохранение забега одиночной игры: в каждой передышке, после выбора карточки, EnemyManager записывает волну,
    // счёт, улучшения, прочность и выстрелы РПГ в PlayerPrefs. Из меню можно выйти посреди волны и потом
    // «Продолжить» — забег начнётся со следующей после сохранённой волны. Гибель машины и новый забег стирают запись
    public static class RunSave
    {
        [Serializable]
        public class Data
        {
            // Последняя зачищенная волна
            public int wave;
            public int score;
            public int kills;
            // Уровни карточек RunUpgrades по порядку Card
            public int[] cards = new int[0];
            public int health;
            public int rockets;
        }

        private const string Key = "RunSave";
        // Меню спрашивает номер волны каждый кадр: запись читается один раз до следующего изменения
        private static Data cached;
        private static bool cacheValid;

        // «Продолжить» в меню: флаг переживает перезагрузку сцены, его снимает EnemyManager при старте забега
        public static bool ContinueRequested { get; set; }

        public static bool Exists => Load() != null;

        // Номер волны, с которой продолжится забег (0 — сохранения нет)
        public static int NextWave
        {
            get
            {
                Data data = Load();
                return data != null ? data.wave + 1 : 0;
            }
        }

        public static Data Load()
        {
            if (cacheValid) return cached;
            cached = null;
            if (PlayerPrefs.HasKey(Key))
            {
                try
                {
                    cached = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key));
                }
                catch (ArgumentException)
                {
                    PlayerPrefs.DeleteKey(Key);
                }
            }
            cacheValid = true;
            return cached;
        }

        public static void Save(Data data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            cacheValid = false;
        }

        public static void Clear()
        {
            cached = null;
            cacheValid = true;
            if (!PlayerPrefs.HasKey(Key)) return;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
