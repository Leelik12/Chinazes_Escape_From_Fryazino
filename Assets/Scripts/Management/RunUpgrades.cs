using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingProject.Management
{
    // Улучшения на один забег: между волнами экипажу предлагают три карточки, выбранная действует до конца раунда.
    // Выбор рассылает сервер (EnemyManager), и у обоих игроков уровни одинаковы: каждый компонент сам читает
    // свой множитель там, где он работает (пулемёт — у стрелка, мотор и бампер — у водителя, ремонт — на сервере)
    public static class RunUpgrades
    {
        public enum Card
        {
            Engine,
            Armor,
            FireRate,
            Cooling,
            Damage,
            Nitro,
            Regen,
            Bumper,
        }

        public const int MaxLevel = 3;
        // Прибавка прочности за уровень «Бронелистов»
        public const int ArmorPerLevel = 150;

        private static readonly string[] Titles =
        {
            "Форсированный мотор", "Бронелисты", "Скорострельность", "Охлаждение ствола",
            "Бронебойные пули", "Баллон побольше", "Ремонт на ходу", "Усиленный бампер",
        };

        private static readonly string[] Descriptions =
        {
            "Тяга мотора +15 %", "Прочность +" + ArmorPerLevel + ", сразу починена", "Пулемёт стреляет на 15 % чаще",
            "Ствол греется на 20 % медленнее", "Урон пулемёта +20 %", "Нитро заряжается на 30 % быстрее",
            "Машина чинится сама: +4 в секунду", "Урон от столкновений −25 %, таран +25 %",
        };

        private static readonly int[] Levels = new int[Enum.GetValues(typeof(Card)).Length];

        // Выбрана карточка (у обоих игроков): для звука и надписи
        public static event Action<Card> Applied;

        public static int CardCount => Levels.Length;

        public static string Title(Card card) => Titles[(int)card];
        public static string Description(Card card) => Descriptions[(int)card];
        public static int Level(Card card) => Levels[(int)card];

        // Множители для компонентов машины
        // Мотор и короб пулемёта ещё и из гаража бункера (GarageLoadout)
        public static float EngineTorque => (1f + 0.15f * Level(Card.Engine)) * GarageLoadout.EngineTorque;
        public static float FireInterval => 1f / (1f + 0.15f * Level(Card.FireRate));
        public static float HeatPerShot => Mathf.Pow(0.8f, Level(Card.Cooling)) * GarageLoadout.HeatPerShot;
        public static float GunDamage => 1f + 0.2f * Level(Card.Damage);
        public static float NitroRecharge => 1f + 0.3f * Level(Card.Nitro);
        public static float RegenPerSecond => 4f * Level(Card.Regen);
        public static float CrashDamageTaken => Mathf.Pow(0.75f, Level(Card.Bumper));
        public static float RamDamage => 1f + 0.25f * Level(Card.Bumper);

        // Новый раунд: всё с нуля
        public static void Reset()
        {
            Array.Clear(Levels, 0, Levels.Length);
        }

        public static bool CanUpgrade(Card card) => Level(card) < MaxLevel;

        // Три разные карточки из ещё не прокачанных до предела; если таких меньше трёх — сколько есть
        public static List<Card> Roll(System.Random random)
        {
            var pool = new List<Card>();
            foreach (Card card in Enum.GetValues(typeof(Card)))
                if (CanUpgrade(card)) pool.Add(card);
            var offer = new List<Card>();
            while (offer.Count < 3 && pool.Count > 0)
            {
                int index = random.Next(pool.Count);
                offer.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return offer;
        }

        public static void Apply(Card card)
        {
            if (!CanUpgrade(card)) return;
            Levels[(int)card]++;
            Applied?.Invoke(card);
        }
    }
}
