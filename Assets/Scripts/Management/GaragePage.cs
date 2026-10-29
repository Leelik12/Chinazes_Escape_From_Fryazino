using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingProject.Management
{
    // Вкладка «Гараж» терминала в бункере: талоны и постоянные улучшения (Garage). Строка — название и деления
    // уровня, справа кнопка с ценой следующего уровня. Строки собирает Tools/Unity/BuildGarageTab.cs
    public class GaragePage : MonoBehaviour
    {
        [Serializable]
        public class Row
        {
            public Garage.Perk perk;
            public TMP_Text label;
            public TMP_Text value;
            public Button button;
        }

        [SerializeField] private Row[] rows = new Row[0];
        [SerializeField] private TMP_Text coins;
        [Tooltip("Строка подсказки внизу экрана: сюда пишется итог покупки")]
        [SerializeField] private TMP_Text hint;

        private void Awake()
        {
            foreach (Row row in rows)
            {
                Row captured = row;
                if (row.button != null) row.button.onClick.AddListener(() => Buy(captured));
            }
        }

        private void OnEnable()
        {
            Garage.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Garage.Changed -= Refresh;
        }

        private void Buy(Row row)
        {
            if (Garage.Maxed(row.perk)) return;
            int cost = Garage.Cost(row.perk);
            bool bought = Garage.TryBuy(row.perk);
            if (hint == null) return;
            hint.text = bought
                ? $"{Garage.Title(row.perk)}: уровень {Garage.Level(row.perk)} — с этого забега"
                : $"Не хватает талонов: нужно {cost}, есть {Garage.Coins}";
        }

        private void Refresh()
        {
            if (coins != null) coins.text = Garage.Coins + " т.";
            foreach (Row row in rows)
            {
                int level = Garage.Level(row.perk);
                int max = Garage.MaxLevel(row.perk);
                if (row.label != null)
                    row.label.text = $"{Garage.Title(row.perk)}  [{new string('#', level)}{new string('·', max - level)}]";
                bool maxed = level >= max;
                if (row.value != null) row.value.text = maxed ? "[ максимум ]" : $"[ {Garage.Cost(row.perk)} т. ]";
                if (row.button != null) row.button.interactable = !maxed;
            }
        }
    }
}
