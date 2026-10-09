using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using RacingProject.Enemy;
using RacingProject.Management;

namespace RacingProject.Network
{
    // Готовность игроков и старт раунда. Хост — водитель, подключившийся клиент — стрелок.
    // Одиночная игра — тот же хост, но без напарника: раунд стартует сразу, игрок и ведёт, и стреляет
    public class RoomController : NetworkBehaviour
    {
        // Игра рассчитана ровно на двоих: водитель и стрелок
        public const int RequiredPlayers = 2;

        [Header("Ссылки на XR Rigs")]
        public GameObject driverRig;       // XR Rig водителя (без рук)
        public GameObject gunnerRig;       // XR Rig пулемётчика (с руками)
        public GameObject car;
        public GameObject Menu;
        public GameObject DriverBody;
        public GameObject GunnerBody;
        public Collider TouchColliderPistol;
        [Tooltip("Риг одиночной игры: камера от третьего лица и экранный HUD")]
        public GameObject soloRig;

        [Header("Перезапуск")]
        [Tooltip("Пауза между гибелью машины и перезапуском: напарник получает последнее здоровье и отдачу гибели, оба видят взрыв и экран гибели")]
        [SerializeField] private float restartDelay = 4f;

        // Пишет только сервер; клиент просит его через SetReadyRpc
        private readonly NetworkVariable<bool> hostReady = new NetworkVariable<bool>();
        private readonly NetworkVariable<bool> clientReady = new NetworkVariable<bool>();
        private readonly NetworkVariable<bool> gameStarted = new NetworkVariable<bool>();

        private bool isLocalReady = false;
        private bool rigsActivated = false;
        private bool restartPending = false;
        private bool soloPending = false;
        private PlayerHealth carHealth;

        // Одиночная игра: хост запущен без напарника, раунд не перезапускается, а возвращает в меню
        public bool Solo { get; private set; }

        // Во время раунда новые подключения не принимаются
        public bool GameStarted => IsSpawned && gameStarted.Value;
        // Для статуса в лобби: нажал ли «Готов» этот игрок и его напарник
        public bool LocalReady => isLocalReady;
        public bool PartnerReady => IsSpawned && (IsServer ? clientReady.Value : hostReady.Value);
        // Готовность водителя (хоста) и стрелка (клиента) для индикаторов меню и пульта
        public bool HostReady => IsSpawned && hostReady.Value;
        public bool ClientReady => IsSpawned && clientReady.Value;

        private void Awake()
        {
            // Статическая роль переживает перезагрузку сцены, а новый раунд начинается в меню
            LocalPlayerRole.Set(PlayerRole.None);
        }

        private void Start()
        {
            if (car != null)
                carHealth = car.GetComponent<PlayerHealth>();
            if (carHealth != null)
                carHealth.OnDeath += OnCarDestroyed;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            if (soloPending)
                StartSolo();
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        public override void OnDestroy()
        {
            if (carHealth != null)
                carHealth.OnDeath -= OnCarDestroyed;
            base.OnDestroy();
        }

        // Вызывает LanLobby сразу после запуска хоста одиночной игры; если сцена ещё не заспавнена, старт — в OnNetworkSpawn
        public void StartSolo()
        {
            Solo = true;
            if (!IsSpawned)
            {
                soloPending = true;
                return;
            }
            soloPending = false;
            if (!IsServer || gameStarted.Value) return;

            isLocalReady = true;
            hostReady.Value = true;
            gameStarted.Value = true;
            StartGameRpc();
        }

        public void OnReadyButtonPressed()
        {
            // Без соединения кнопка ничего не делает
            if (!IsSpawned) return;

            if (isLocalReady) return;
            Debug.Log("Кнопка готовности нажата");
            isLocalReady = true;
            SetReadyRpc();
        }

        [Rpc(SendTo.Server)]
        private void SetReadyRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId)
                hostReady.Value = true;
            else
                clientReady.Value = true;

            TryStartGame();
        }

        // Только на сервере: оба подключены и оба готовы — стартуем у всех
        private void TryStartGame()
        {
            if (gameStarted.Value) return;

            // Без второго игрока не стартуем
            if (NetworkManager.ConnectedClientsIds.Count < RequiredPlayers) return;
            if (!hostReady.Value || !clientReady.Value) return;

            gameStarted.Value = true;
            StartGameRpc();
        }

        // Если в меню есть бункер, сначала открывается гермодверь (MenuExitSequence), и риги включаются в темноте.
        // Длительность одинакова у обоих игроков, поэтому раунд у них стартует одновременно
        [Rpc(SendTo.Everyone)]
        private void StartGameRpc()
        {
            MenuExitSequence exit = Menu != null && Menu.activeInHierarchy ? Menu.GetComponentInChildren<MenuExitSequence>() : null;
            if (exit != null)
                exit.Play(ActivatePlayerRigs);
            else
                ActivatePlayerRigs();
        }

        private void ActivatePlayerRigs()
        {
            if (rigsActivated) return;
            rigsActivated = true;

            LocalPlayerRole.Set(Solo ? PlayerRole.Solo : IsServer ? PlayerRole.Driver : PlayerRole.Gunner);

            if (Solo)
            {
                // Видны оба члена экипажа, камера — снаружи машины
                TouchColliderPistol.enabled = false;
                DriverBody.SetActive(true);
                GunnerBody.SetActive(true);
                if (driverRig != null) driverRig.SetActive(false);
                if (gunnerRig != null) gunnerRig.SetActive(false);
                if (soloRig != null) soloRig.SetActive(true);
                Menu.SetActive(false);
            }
            else if (IsServer)
            {
                // Хост — водитель
                TouchColliderPistol.enabled = false;
                if (driverRig != null)
                {
                    DriverBody.SetActive(true);
                    driverRig.SetActive(true);
                }
                if (gunnerRig != null)
                {
                    gunnerRig.SetActive(false);
                }
                Menu.SetActive(false);
            }
            else
            {
                // Клиент — пулемётчик
                if (driverRig != null)
                {
                    driverRig.SetActive(false);
                }
                if (gunnerRig != null)
                {
                    GunnerBody.SetActive(true);
                    gunnerRig.SetActive(true);
                }
                Menu.SetActive(false);
            }

            EnemyManager enemyManager = FindFirstObjectByType<EnemyManager>();
            if (enemyManager != null)
            {
                enemyManager.StartGame();
            }
        }

        private void OnCarDestroyed()
        {
            if (!IsServer || restartPending) return;
            restartPending = true;
            StartCoroutine(RestartAfterDelay());
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(restartDelay);
            // Одиночная игра закрывает хост, и LanLobby возвращает в меню с выбором режима
            if (Solo)
                GetComponent<LanLobby>().OnLeavePressed();
            else
                RestartRound();
        }

        // Перезапуск раунда: сервер перезагружает сцену у всех. Соединение остаётся,
        // а готовность и роли сбрасываются вместе с новыми объектами сцены
        private void RestartRound()
        {
            if (!IsServer) return;

            NetworkManager.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId) return;

            // Напарник вышел посреди игры — возвращаем оставшегося в меню
            if (gameStarted.Value)
                RestartRound();
            else
                clientReady.Value = false;
        }
    }
}
