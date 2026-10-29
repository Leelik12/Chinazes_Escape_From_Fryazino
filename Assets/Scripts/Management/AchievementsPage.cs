using System;
using TMPro;
using UnityEngine;

namespace RacingProject.Management
{
    // Вкладка «Награды» терминала в бункере: по строке на достижение (Achievements) — название и отметка
    // «получено», у «Сотни» — счётчик. Описание показывает строка подсказки при наведении (TerminalSelector).
    // Строки собирает Tools/Unity/BuildAchievements.cs
    public class AchievementsPage : MonoBehaviour
    {
        [Serializable]
        public class Row
        {
            public Achievements.Id id;
            public TMP_Text label;
            public TMP_Text value;
        }

        [SerializeField] private Row[] rows = new Row[0];
        [Tooltip("Строка «получено N из M»")]
        [SerializeField] private TMP_Text total;

        private void OnEnable()
        {
            Achievements.Unlocked += OnUnlocked;
            Refresh();
        }

        private void OnDisable()
        {
            Achievements.Unlocked -= OnUnlocked;
        }

        private void OnUnlocked(Achievements.Id id)
        {
            Refresh();
        }

        private void Refresh()
        {
            int got = 0;
            foreach (Row row in rows)
            {
                bool has = Achievements.Has(row.id);
                if (has) got++;
                if (row.label != null)
                    row.label.text = (has ? "[x] " : "[ ] ") + Achievements.Title(row.id);
                if (row.value == null) continue;
                if (has) row.value.text = "получено";
                else if (row.id == Achievements.Id.Kills100)
                    row.value.text = $"{Mathf.Min(Achievements.TotalKills, Achievements.KillsGoal)} / {Achievements.KillsGoal}";
                else row.value.text = "—";
            }
            if (total != null) total.text = $"{got} из {Achievements.Count}";
        }
    }
}
