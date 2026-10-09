using TMPro;
using UnityEngine;
using RacingProject.Network;

namespace RacingProject.Management
{
    // Блок «Состояние» на главном экране терминала: связь, водитель и стрелок, отметка «вы».
    // В одиночной игре игрок и водитель, и стрелок
    public class LobbyStatusLines : MonoBehaviour
    {
        [SerializeField] private RoomController room;
        [SerializeField] private LanLobby lobby;
        [SerializeField] private TMP_Text link;
        [SerializeField] private TMP_Text driver;
        [SerializeField] private TMP_Text gunner;
        [SerializeField] private Color good = new Color(0.62f, 0.9f, 0.45f);
        [SerializeField] private Color warn = new Color(0.95f, 0.7f, 0.25f);
        [SerializeField] private Color dim = new Color(0.3f, 0.45f, 0.26f);

        private void Update()
        {
            LobbyState s = LobbyState.Read(room, lobby);

            switch (s.link)
            {
                case LobbyState.Link.Searching: Set(link, "ПОИСК...", warn); break;
                case LobbyState.Link.Connecting: Set(link, "ПОДКЛЮЧЕНИЕ...", warn); break;
                case LobbyState.Link.Online when s.solo:
                    Set(link, "ОДИНОЧНАЯ ИГРА", good);
                    break;
                case LobbyState.Link.Online:
                    Set(link, (s.isHost ? "ХОСТ" : "КЛИЕНТ") + (s.partnerPresent ? "  2/2" : "  1/2"), s.partnerPresent ? good : warn);
                    break;
                default: Set(link, "НЕТ", dim); break;
            }

            if (!s.Online)
            {
                Set(driver, "—", dim);
                Set(gunner, "—", dim);
                return;
            }
            if (s.solo)
            {
                Set(driver, "(ВЫ) ЗА РУЛЁМ", good);
                Set(gunner, "(ВЫ) У ПУЛЕМЁТА", good);
                return;
            }
            Set(driver, Player(s.driverReady, s.isHost), s.driverReady ? good : warn);
            if (!s.partnerPresent)
                Set(gunner, "НЕТ НА СВЯЗИ", dim);
            else
                Set(gunner, Player(s.gunnerReady, !s.isHost), s.gunnerReady ? good : warn);
        }

        private static string Player(bool ready, bool you)
        {
            string state = ready ? "ГОТОВ" : "НЕ ГОТОВ";
            return you ? "(ВЫ) " + state : state;
        }

        private static void Set(TMP_Text target, string text, Color color)
        {
            if (target == null) return;
            if (target.text != text) target.text = text;
            target.color = color;
        }
    }
}
