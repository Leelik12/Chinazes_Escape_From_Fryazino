using Unity.Netcode;

namespace RacingProject.Network
{
    // Снимок состояния лобби для меню и пульта бункера: связь, кто подключён и кто нажал «Готов».
    // Игрок 1 — хост (водитель), игрок 2 — клиент (стрелок)
    public struct LobbyState
    {
        public enum Link { Offline, Searching, Connecting, Online }

        public Link link;
        public bool isHost;
        public bool partnerPresent;
        public bool driverReady;
        public bool gunnerReady;
        // Одиночная игра: игрок и водитель, и стрелок, напарника нет
        public bool solo;

        public bool Online => link == Link.Online;
        public int ReadyCount => (driverReady ? 1 : 0) + (gunnerReady ? 1 : 0);

        public static LobbyState Read(RoomController room, LanLobby lobby)
        {
            var state = new LobbyState();
            NetworkManager manager = NetworkManager.Singleton;
            bool listening = manager != null && manager.IsListening;
            bool inSession = listening && (manager.IsServer || manager.IsConnectedClient);

            if (inSession)
                state.link = Link.Online;
            else if (listening)
                state.link = Link.Connecting;
            else if (lobby != null && lobby.Searching)
                state.link = Link.Searching;
            else
                state.link = Link.Offline;

            if (!inSession) return state;

            state.isHost = manager.IsServer;
            if (room != null && room.Solo)
            {
                state.solo = true;
                state.partnerPresent = true;
                state.driverReady = state.gunnerReady = true;
                return state;
            }
            // Список подключённых есть только на сервере; клиент в сессии всегда видит хоста
            state.partnerPresent = !manager.IsServer || manager.ConnectedClientsIds.Count >= RoomController.RequiredPlayers;
            if (room != null)
            {
                state.driverReady = room.HostReady;
                state.gunnerReady = room.ClientReady;
            }
            return state;
        }
    }
}
