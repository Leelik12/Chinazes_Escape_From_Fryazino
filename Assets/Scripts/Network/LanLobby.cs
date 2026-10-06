using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RacingProject.Network
{
    // Лобби в главном меню: создать игру (хост, водитель) или найти её в локальной сети (клиент, стрелок).
    // Состояние кнопок берётся из NetworkManager каждый кадр, поэтому переживает перезагрузку сцены
    [RequireComponent(typeof(RoomController))]
    public class LanLobby : MonoBehaviour
    {
        // Аргумент командной строки: -connect 192.168.1.10 — подключиться по адресу без поиска
        public const string ConnectArgument = "-connect";

        [Header("Кнопки")]
        [SerializeField] private GameObject hostButton;
        [SerializeField] private GameObject joinButton;
        [SerializeField] private TMP_Text joinButtonLabel;
        [SerializeField] private GameObject readyButton;
        [SerializeField] private GameObject leaveButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Сеть")]
        [SerializeField] private ushort gamePort = 7777;
        [Tooltip("Сколько секунд искать игру в сети")]
        [SerializeField] private float searchTimeout = 15f;

        // Сообщение о разрыве соединения: показывается после перезагрузки сцены
        private static string pendingStatus;
        // Уведомление хосту «напарник отключился»: переживает перезапуск раунда, гаснет с подключением нового
        private static string hostNotice;
        // Адрес из -connect используется только при запуске игры, а не после каждого возврата в меню
        private static bool commandLineUsed;

        private readonly LanDiscovery discovery = new LanDiscovery();
        private RoomController room;
        private string idleStatus = "";
        private string connectAddress;
        private string localAddresses;
        private float searchEndTime;
        private bool wasConnected;
        private bool reloading;

        private void Awake()
        {
            room = GetComponent<RoomController>();
        }

        private void Start()
        {
            if (pendingStatus != null)
            {
                idleStatus = pendingStatus;
                pendingStatus = null;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("LanLobby: нет NetworkManager, проверьте NetworkBootstrap в сцене");
                enabled = false;
                return;
            }

            manager.OnClientStopped += OnClientStopped;
            manager.OnClientDisconnectCallback += OnPartnerDisconnected;
            manager.ConnectionApprovalCallback = ApproveConnection;
            wasConnected = manager.IsListening;

            string address = commandLineUsed ? null : GetCommandLineAddress();
            commandLineUsed = true;
            if (address != null && !manager.IsListening)
                Connect(address, gamePort);
        }

        private void OnDestroy()
        {
            discovery.Stop();

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null) return;
            manager.OnClientStopped -= OnClientStopped;
            manager.OnClientDisconnectCallback -= OnPartnerDisconnected;
            if (manager.ConnectionApprovalCallback == ApproveConnection)
                manager.ConnectionApprovalCallback = null;
        }

        public void OnHostPressed()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsListening) return;

            discovery.Stop();
            hostNotice = null;
            Transport.SetConnectionData("127.0.0.1", gamePort, "0.0.0.0");
            if (!manager.StartHost())
            {
                idleStatus = "Не удалось создать игру: порт " + gamePort + " занят?";
                return;
            }
            wasConnected = true;
        }

        // Повторное нажатие во время поиска отменяет его
        public void OnJoinPressed()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsListening) return;

            if (discovery.IsListening)
            {
                discovery.Stop();
                idleStatus = "";
                return;
            }

            if (discovery.StartListening())
                searchEndTime = Time.unscaledTime + searchTimeout;
            else
                idleStatus = "Порт поиска " + LanDiscovery.DiscoveryPort + " занят";
        }

        public void OnLeavePressed()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening) return;

            discovery.Stop();
            pendingStatus = "";
            manager.Shutdown();
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null) return;

            if (manager.IsConnectedClient)
                wasConnected = true;

            UpdateBroadcast(manager);
            UpdateSearch();
            UpdateUi(manager);
        }

        // Хост объявляет игру, пока не подключился второй игрок
        private void UpdateBroadcast(NetworkManager manager)
        {
            bool shouldBroadcast = manager.IsServer && manager.IsListening
                && manager.ConnectedClientsIds.Count < RoomController.RequiredPlayers
                && !room.GameStarted;

            if (shouldBroadcast && !discovery.IsBroadcasting)
                discovery.StartBroadcasting(gamePort);
            else if (!shouldBroadcast && discovery.IsBroadcasting)
                discovery.Stop();

            discovery.Tick(Time.unscaledTime);
        }

        private void UpdateSearch()
        {
            if (!discovery.IsListening) return;

            if (discovery.TryReceive(out string address, out ushort port))
            {
                Connect(address, port);
            }
            else if (Time.unscaledTime > searchEndTime)
            {
                discovery.Stop();
                idleStatus = "Игра не найдена. Создана ли она на другом ПК в этой сети?";
            }
        }

        private void Connect(string address, ushort port)
        {
            discovery.Stop();
            connectAddress = address;
            Transport.SetConnectionData(address, port);
            if (!NetworkManager.Singleton.StartClient())
                idleStatus = "Не удалось подключиться к " + address;
        }

        private void UpdateUi(NetworkManager manager)
        {
            bool online = manager.IsListening;
            bool searching = discovery.IsListening;
            bool inSession = manager.IsServer || manager.IsConnectedClient;

            SetActive(hostButton, !online && !searching);
            SetActive(joinButton, !online);
            SetActive(readyButton, online && inSession);
            SetActive(leaveButton, online);
            SetText(joinButtonLabel, searching ? "Отмена" : "Найти игру");

            string status;
            if (searching)
                status = "Поиск игры в сети... " + Mathf.CeilToInt(Mathf.Max(0f, searchEndTime - Time.unscaledTime)) + " с";
            else if (online && manager.IsServer && manager.ConnectedClientsIds.Count < RoomController.RequiredPlayers)
                status = (string.IsNullOrEmpty(hostNotice) ? "" : hostNotice + "\n")
                    + "Игра создана (IP: " + GetLocalAddresses() + "). Ждём второго игрока...";
            else if (online && manager.IsServer)
            {
                hostNotice = null;
                status = "Второй игрок подключился. " + GetReadyStatus();
            }
            else if (online && manager.IsConnectedClient)
                status = "Подключено к " + connectAddress + ". " + GetReadyStatus();
            else if (online)
                status = "Подключение к " + connectAddress + "...";
            else
                status = idleStatus;
            SetText(statusText, status);
        }

        private string GetReadyStatus()
        {
            if (!room.LocalReady)
                return room.PartnerReady ? "Напарник готов, нажмите «Готов»" : "Нажмите «Готов»";
            return "Ждём готовности напарника...";
        }

        // Адреса не меняются за время игры, поэтому берутся один раз
        private string GetLocalAddresses()
        {
            if (localAddresses == null)
            {
                var addresses = LanDiscovery.GetLocalAddresses();
                localAddresses = addresses.Count > 0 ? string.Join(", ", addresses) : "нет сети";
            }
            return localAddresses;
        }

        // На хосте: напарник вышел или пропала связь
        private void OnPartnerDisconnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (!manager.IsServer || manager.ShutdownInProgress || clientId == NetworkManager.ServerClientId) return;

            hostNotice = room.GameStarted ? "Напарник отключился, раунд остановлен" : "Напарник отключился";
            Debug.Log("LanLobby: " + hostNotice);
        }

        // Хост пускает только одного напарника и только до старта раунда
        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            NetworkManager manager = NetworkManager.Singleton;
            bool isHost = request.ClientNetworkId == NetworkManager.ServerClientId;
            bool full = manager.ConnectedClientsIds.Count >= RoomController.RequiredPlayers;

            response.CreatePlayerObject = false;
            response.Approved = isHost || (!full && !room.GameStarted);
            if (!response.Approved)
                response.Reason = full ? "В игре уже два игрока" : "Игра уже началась";
        }

        // Соединение закрыто: вышли сами, хост закрыл игру или связь пропала
        private void OnClientStopped(bool wasHost)
        {
            if (reloading) return;

            if (!wasConnected)
            {
                // До игры дело не дошло — достаточно сообщения
                string reason = NetworkManager.Singleton.DisconnectReason;
                idleStatus = string.IsNullOrEmpty(reason) ? "Не удалось подключиться к " + connectAddress : reason;
                Debug.Log("LanLobby: " + idleStatus);
                return;
            }

            if (pendingStatus == null)
            {
                // NGO при штатном закрытии хоста присылает «Disconnected due to host shutting down.»
                string reason = NetworkManager.Singleton.DisconnectReason;
                pendingStatus = !string.IsNullOrEmpty(reason) && reason.Contains("shutting down")
                    ? "Хост закрыл игру"
                    : "Соединение с хостом потеряно";
            }
            Debug.Log("LanLobby: соединение закрыто, возврат в меню. " + pendingStatus);

            // Сцена могла уйти далеко от меню — начинаем с чистой
            reloading = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private static UnityTransport Transport =>
            (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

        private static string GetCommandLineAddress()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == ConnectArgument)
                    return args[i + 1];
            }
            return null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null && target.text != text)
                target.text = text;
        }
    }
}
