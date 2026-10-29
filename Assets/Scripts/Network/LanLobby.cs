using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using RacingProject.Management;

namespace RacingProject.Network
{
    // Лобби в главном меню: одиночная игра (только на мониторе) и её продолжение с сохранённой волны (RunSave), создать игру (хост, водитель)
    // или найти её в локальной сети (клиент, стрелок). Поиск собирает объявления хостов: одна подходящая игра —
    // подключение сразу, несколько — список, игра выбирается цифрой; игры другой версии видны, но к ним не подключиться.
    // После обрыва связи появляется «Переподключиться» к тому же адресу.
    // Состояние кнопок берётся из NetworkManager каждый кадр, поэтому переживает перезагрузку сцены
    [RequireComponent(typeof(RoomController))]
    public class LanLobby : MonoBehaviour
    {
        // Аргумент командной строки: -connect 192.168.1.10 — подключиться по адресу без поиска
        public const string ConnectArgument = "-connect";

        [Header("Кнопки")]
        [Tooltip("Одиночная игра: видна только в режиме монитора")]
        [SerializeField] private GameObject soloButton;
        [Tooltip("Продолжить сохранённый одиночный забег: видна, если сохранение есть")]
        [SerializeField] private GameObject continueButton;
        [SerializeField] private TMP_Text continueButtonLabel;
        [Tooltip("Обучение: как и одиночная игра, видно только в режиме монитора")]
        [SerializeField] private GameObject tutorialButton;
        [SerializeField] private GameObject hostButton;
        [SerializeField] private GameObject joinButton;
        [SerializeField] private TMP_Text joinButtonLabel;
        [SerializeField] private GameObject readyButton;
        [SerializeField] private GameObject leaveButton;
        [Tooltip("Переподключиться к последнему хосту: видна после обрыва связи")]
        [SerializeField] private GameObject reconnectButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Сеть")]
        [SerializeField] private ushort gamePort = 7777;
        [Tooltip("Сколько секунд искать игру в сети")]
        [SerializeField] private float searchTimeout = 15f;
        [Tooltip("После первой найденной игры ещё столько секунд ждём другие, чтобы предложить выбор")]
        [SerializeField] private float collectTime = 1.5f;

        // Сообщение о разрыве соединения: показывается после перезагрузки сцены
        private static string pendingStatus;
        // Уведомление хосту «напарник отключился»: переживает перезапуск раунда, гаснет с подключением нового
        private static string hostNotice;
        // Адрес из -connect используется только при запуске игры, а не после каждого возврата в меню
        private static bool commandLineUsed;
        // «Начать игру» в конце обучения: после возврата в меню сразу запускается одиночная игра
        private static bool soloAfterReload;
        // Последний хост, к которому подключались, и нужно ли предложить переподключение (после обрыва связи)
        private static string lastAddress;
        private static ushort lastPort;
        private static bool offerReconnect;

        private readonly LanDiscovery discovery = new LanDiscovery();
        private RoomController room;
        private string idleStatus = "";
        private string connectAddress;
        private string localAddresses;
        private float searchEndTime;
        private bool wasConnected;
        private bool reloading;
        // Найденные при поиске игры и момент, когда выбирать между ними (0 — подходящих ещё нет)
        private readonly List<LanDiscovery.Announcement> foundGames = new List<LanDiscovery.Announcement>();
        private float decideTime;
        private bool choosing;
        // Сообщение о запуске раунда: кнопки прячутся, пока бункер провожает экипаж к выходу
        private string launchStatus;

        // Идёт поиск игры в сети (для индикаторов меню)
        public bool Searching => discovery.IsListening;

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

            if (soloAfterReload)
            {
                soloAfterReload = false;
                OnSoloPressed();
                return;
            }

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

        public void ShowLaunch(string text)
        {
            launchStatus = text;
        }

        // Одиночная игра — хост только для этого компьютера, без сети (OfflineTransport): портов не открывает и никого не пускает
        public void OnSoloPressed()
        {
            if (StartLocalHost())
                room.StartSolo();
        }

        // Одиночная игра с сохранённой волны: сохранение применит EnemyManager при старте забега
        public void OnContinuePressed()
        {
            if (!RunSave.Exists) return;
            RunSave.ContinueRequested = true;
            if (StartLocalHost())
                room.StartSolo();
            else
                RunSave.ContinueRequested = false;
        }

        // Обучение — тоже одиночная игра, но на полигоне и без врагов
        public void OnTutorialPressed()
        {
            if (StartLocalHost())
                room.StartTutorial();
        }

        public static void StartSoloAfterReload()
        {
            soloAfterReload = true;
        }

        private bool StartLocalHost()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsListening || ViewModeService.IsVR) return false;

            discovery.Stop();
            hostNotice = null;
            UseOfflineTransport(manager);
            if (!manager.StartHost())
            {
                idleStatus = "Не удалось начать игру";
                return false;
            }
            wasConnected = true;
            return true;
        }

        public void OnHostPressed()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsListening) return;

            discovery.Stop();
            hostNotice = null;
            UseLanTransport(manager).SetConnectionData("127.0.0.1", gamePort, "0.0.0.0");
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

            foundGames.Clear();
            decideTime = 0f;
            choosing = false;
            if (discovery.StartListening())
                searchEndTime = Time.unscaledTime + searchTimeout;
            else
                idleStatus = "Порт поиска " + LanDiscovery.DiscoveryPort + " занят";
        }

        // Снова к хосту, с которым пропала связь
        public void OnReconnectPressed()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsListening || lastAddress == null) return;

            offerReconnect = false;
            idleStatus = "";
            Connect(lastAddress, lastPort);
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
            bool shouldBroadcast = manager.IsServer && manager.IsListening && !room.Solo
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

            while (discovery.TryReceive(out LanDiscovery.Announcement game))
                AddFound(game);

            List<LanDiscovery.Announcement> compatible = CompatibleGames();
            if (compatible.Count > 0 && decideTime <= 0f)
                decideTime = Time.unscaledTime + collectTime;

            if (choosing)
            {
                // Игра выбирается цифрой; в VR клавиатуры нет — подключаемся к первой
                int picked = ViewModeService.IsVR ? 0 : PressedDigit();
                if (picked >= 0 && picked < compatible.Count)
                    Connect(compatible[picked].address, compatible[picked].port);
                return;
            }

            if (decideTime > 0f && Time.unscaledTime >= decideTime)
            {
                if (compatible.Count == 1 || ViewModeService.IsVR)
                    Connect(compatible[0].address, compatible[0].port);
                else
                    choosing = true;
            }
            else if (Time.unscaledTime > searchEndTime)
            {
                discovery.Stop();
                idleStatus = foundGames.Count > 0
                    ? "Найдена игра другой версии: " + Describe(foundGames[0]) + ", у вас v" + Application.version + ". Нужна одинаковая версия"
                    : "Игра не найдена. Создана ли она на другом ПК в этой сети?";
            }
        }

        // Хост шлёт объявление каждую секунду и через все адаптеры (и loopback): одну игру храним один раз,
        // с первым адресом, с которого она пришла
        private void AddFound(LanDiscovery.Announcement game)
        {
            for (int i = 0; i < foundGames.Count; i++)
            {
                LanDiscovery.Announcement known = foundGames[i];
                if (known.port == game.port && (known.address == game.address || known.hostName == game.hostName))
                    return;
            }
            foundGames.Add(game);
        }

        private List<LanDiscovery.Announcement> CompatibleGames()
        {
            var compatible = new List<LanDiscovery.Announcement>();
            foreach (LanDiscovery.Announcement game in foundGames)
                if (game.Compatible) compatible.Add(game);
            return compatible;
        }

        private static string Describe(LanDiscovery.Announcement game)
        {
            string version = string.IsNullOrEmpty(game.version) ? "старая версия" : "v" + game.version;
            return game.hostName + " (" + game.address + ", " + version + ")";
        }

        // Нажатая цифра 1–9 как номер в списке (0-based), -1 — не нажата
        private static int PressedDigit()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return -1;
            for (int i = 0; i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                    return i;
            }
            return -1;
        }

        private void Connect(string address, ushort port)
        {
            discovery.Stop();
            choosing = false;
            connectAddress = address;
            lastAddress = address;
            lastPort = port;
            UseLanTransport(NetworkManager.Singleton).SetConnectionData(address, port);
            if (!NetworkManager.Singleton.StartClient())
                idleStatus = "Не удалось подключиться к " + address;
        }

        private void UpdateUi(NetworkManager manager)
        {
            bool online = manager.IsListening;
            bool searching = discovery.IsListening;
            bool inSession = manager.IsServer || manager.IsConnectedClient;

            if (launchStatus != null)
            {
                SetActive(soloButton, false);
                SetActive(continueButton, false);
                SetActive(tutorialButton, false);
                SetActive(hostButton, false);
                SetActive(joinButton, false);
                SetActive(readyButton, false);
                SetActive(leaveButton, false);
                SetText(statusText, launchStatus);
                return;
            }

            // Переподключение занимает строку «Обучения», чтобы меню не выросло
            bool reconnect = !online && !searching && offerReconnect && lastAddress != null;
            SetActive(soloButton, !online && !searching && !ViewModeService.IsVR);
            bool canContinue = !online && !searching && !ViewModeService.IsVR && RunSave.Exists;
            SetActive(continueButton, canContinue);
            if (canContinue) SetText(continueButtonLabel, $"Продолжить: волна {RunSave.NextWave}");
            SetActive(tutorialButton, !online && !searching && !ViewModeService.IsVR && !reconnect);
            SetActive(hostButton, !online && !searching);
            SetActive(joinButton, !online);
            SetActive(reconnectButton, reconnect);
            SetActive(readyButton, online && inSession);
            SetActive(leaveButton, online);
            SetText(joinButtonLabel, searching ? "Отмена" : "Найти игру");

            string status;
            if (searching && choosing)
                status = BuildChoiceStatus();
            else if (searching)
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

        // Несколько игр в сети: список с номерами
        private string BuildChoiceStatus()
        {
            var lines = new System.Text.StringBuilder("Найдено игр: " + foundGames.Count + ". Нажмите цифру, чтобы подключиться");
            int number = 0;
            foreach (LanDiscovery.Announcement game in foundGames)
            {
                lines.Append('\n');
                lines.Append(game.Compatible ? (++number) + " — " + Describe(game) : "× " + Describe(game) + " — другая версия");
            }
            return lines.ToString();
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
            response.Approved = isHost || (!full && !room.GameStarted && !room.Solo);
            if (!response.Approved)
                response.Reason = room.Solo ? "Это одиночная игра" : full ? "В игре уже два игрока" : "Игра уже началась";
        }

        // Соединение закрыто: вышли сами, хост закрыл игру или связь пропала
        private void OnClientStopped(bool wasHost)
        {
            if (reloading) return;

            if (!wasConnected)
            {
                // До игры дело не дошло — достаточно сообщения
                // Отказ хоста приходит текстом («В игре уже два игрока»), служебные сообщения NGO о таймауте — нет
                string reason = NetworkManager.Singleton.DisconnectReason;
                idleStatus = string.IsNullOrEmpty(reason) || reason.StartsWith("[Disconnect Event]")
                    ? "Не удалось подключиться к " + connectAddress
                    : reason;
                Debug.Log("LanLobby: " + idleStatus);
                // Не вышло переподключиться — кнопка остаётся, хост может ещё подняться
                if (!wasHost && lastAddress != null && connectAddress == lastAddress)
                    offerReconnect = true;
                return;
            }

            if (pendingStatus == null)
            {
                // NGO при штатном закрытии хоста присылает «Disconnected due to host shutting down.»
                string reason = NetworkManager.Singleton.DisconnectReason;
                pendingStatus = !string.IsNullOrEmpty(reason) && reason.Contains("shutting down")
                    ? "Хост закрыл игру"
                    : "Соединение с хостом потеряно";
                // Вышли не сами: у клиента в меню будет «Переподключиться»
                offerReconnect = !wasHost && lastAddress != null;
            }
            Debug.Log("LanLobby: соединение закрыто, возврат в меню. " + pendingStatus);

            // Сцена могла уйти далеко от меню — начинаем с чистой
            reloading = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // Сетевая игра — по UDP через UnityTransport из префаба NetworkManager
        private static UnityTransport UseLanTransport(NetworkManager manager)
        {
            UnityTransport transport = manager.GetComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            return transport;
        }

        // Одиночная игра и обучение — без сети: OfflineTransport не открывает сокетов и портов
        private static void UseOfflineTransport(NetworkManager manager)
        {
            OfflineTransport transport = manager.GetComponent<OfflineTransport>();
            if (transport == null)
                transport = manager.gameObject.AddComponent<OfflineTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
        }

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
