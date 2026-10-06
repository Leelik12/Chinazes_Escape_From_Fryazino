using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using RacingProject.Enemy;

namespace RacingProject.Network
{
    // Готовность игроков и старт раунда. Хост — водитель, подключившийся клиент — стрелок
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
        [Header("UI Готовности")]
        public Image firstPlayerReadyCircle;
        public Image secondPlayerReadyCircle;
        public Color notReadyColor = Color.red;
        public Color readyColor = Color.green;

        [Header("UI Состояния подключения")]
        public Image connectionStatusCircle;
        public Color connectedColor = Color.green;
        public Color disconnectedColor = Color.red;

        [Header("Перезапуск")]
        [Tooltip("Пауза между гибелью машины и перезапуском: за это время напарник получает последнее здоровье и отдачу гибели")]
        [SerializeField] private float restartDelay = 1.5f;

        // Пишет только сервер; клиент просит его через SetReadyRpc
        private readonly NetworkVariable<bool> hostReady = new NetworkVariable<bool>();
        private readonly NetworkVariable<bool> clientReady = new NetworkVariable<bool>();
        private readonly NetworkVariable<bool> gameStarted = new NetworkVariable<bool>();

        private bool isLocalReady = false;
        private bool rigsActivated = false;
        private bool restartPending = false;
        private PlayerHealth carHealth;

        // Во время раунда новые подключения не принимаются
        public bool GameStarted => IsSpawned && gameStarted.Value;

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

        private void Update()
        {
            UpdateConnectionStatus();
            UpdateReadyUI();
        }

        private void UpdateConnectionStatus()
        {
            if (connectionStatusCircle == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            bool connected = manager != null && (manager.IsServer ? manager.IsListening : manager.IsConnectedClient);
            connectionStatusCircle.color = connected ? connectedColor : disconnectedColor;
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

        [Rpc(SendTo.Everyone)]
        private void StartGameRpc()
        {
            ActivatePlayerRigs();
        }

        private void ActivatePlayerRigs()
        {
            if (rigsActivated) return;
            rigsActivated = true;

            LocalPlayerRole.Set(IsServer ? PlayerRole.Driver : PlayerRole.Gunner);

            if (IsServer)
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

        private void UpdateReadyUI()
        {
            SetReadyCircle(firstPlayerReadyCircle, IsSpawned && hostReady.Value);
            SetReadyCircle(secondPlayerReadyCircle, IsSpawned && clientReady.Value);
        }

        private void SetReadyCircle(Image circle, bool ready)
        {
            if (circle == null) return;
            circle.color = ready ? readyColor : notReadyColor;
        }
    }
}
