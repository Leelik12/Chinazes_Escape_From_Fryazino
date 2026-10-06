using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RoomController : MonoBehaviourPunCallbacks
{
    // Ключ свойства игрока «готов к старту»
    public const string ReadyKey = "IsReady";
    // Игра рассчитана ровно на двоих: водитель и стрелок
    private const int RequiredPlayers = 2;

    [Header("Ссылки на XR Rigs")]
    public GameObject driverRig;       // XR Rig водителя (без рук)
    public GameObject gunnerRig;       // XR Rig пулемётчика (с руками)
    public GameObject car;
    public GameObject Turret;
    public GameObject MachineGun;
    public GameObject Menu;
    public GameObject DriverBody;
    public GameObject GunnerBody;
    public GameObject LeftProxyHand;
    public GameObject RightProxyHand;
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

    private bool isLocalReady = false;
    private bool gameStarted = false;
    private PlayerHealth carHealth;
    private float connectionCheckTimer = 0f;
    private float connectionCheckInterval = 1f;

    private void Start()
    {
        if (car != null)
            carHealth = car.GetComponent<PlayerHealth>();
        if (carHealth != null)
            carHealth.OnDeath += OnCarDestroyed;

        // После перезапуска раунда игроки уже в комнате
        UpdateReadyUI();
    }

    private void OnDestroy()
    {
        if (carHealth != null)
            carHealth.OnDeath -= OnCarDestroyed;
    }

    private void Update()
    {
        // Проверяем подключение раз в секунду
        connectionCheckTimer += Time.deltaTime;
        if (connectionCheckTimer >= connectionCheckInterval)
        {
            connectionCheckTimer = 0f;
            UpdateConnectionStatus();
        }
    }

    private void UpdateConnectionStatus()
    {
        if (connectionStatusCircle == null) return;

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            connectionStatusCircle.color = connectedColor;
        }
        else
        {
            connectionStatusCircle.color = disconnectedColor;
        }
    }

    public void OnReadyButtonPressed()
    {
        //Фикс легендарного теста RIP
        if (!PhotonNetwork.InRoom) return;

        if (isLocalReady) return;
        Debug.Log("Кнопка готовности нажата");
        isLocalReady = true;

        // Устанавливаем CustomProperty "IsReady"
        Hashtable props = new Hashtable { { ReadyKey, true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        UpdateReadyUI();
        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        if (gameStarted) return;

        // Без второго игрока не стартуем
        if (PhotonNetwork.PlayerList.Length < RequiredPlayers) return;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!IsReady(player))
                return;
        }

        // Все игроки готовы — активируем риги
        ActivatePlayerRigs();
    }

    private static bool IsReady(Player player)
    {
        return player != null
            && player.CustomProperties.TryGetValue(ReadyKey, out object value)
            && value is bool ready && ready;
    }

    private void ActivatePlayerRigs()
    {
        gameStarted = true;

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonView carView = car.GetComponent<PhotonView>();
            PhotonView bodyView = DriverBody.GetComponent<PhotonView>();
            TouchColliderPistol.enabled = false;
            // Мастер-клиент — водитель
            if (driverRig != null)
            {
                DriverBody.SetActive(true);
                driverRig.SetActive(true);
                carView.TransferOwnership(PhotonNetwork.LocalPlayer);
                bodyView.TransferOwnership(PhotonNetwork.LocalPlayer);
                Debug.Log("Права на машину выданы");
            }
            if (gunnerRig != null)
            {
                gunnerRig.SetActive(false);
            }
            Menu.SetActive(false);
        }
        else
        {
            PhotonView TurretView = Turret.GetComponent<PhotonView>();
            PhotonView MachineGunView = MachineGun.GetComponent<PhotonView>();
            PhotonView GunnerView = GunnerBody.GetComponent<PhotonView>();
            PhotonView LeftHand = LeftProxyHand.GetComponent<PhotonView>();
            PhotonView RightHand = RightProxyHand.GetComponent<PhotonView>();
            // Второй игрок — пулемётчик
            if (driverRig != null)
            {
                driverRig.SetActive(false);
            }
            if (gunnerRig != null)
            {
                GunnerBody.SetActive(true);
                gunnerRig.SetActive(true);
                TurretView.TransferOwnership(PhotonNetwork.LocalPlayer);
                MachineGunView.TransferOwnership(PhotonNetwork.LocalPlayer);
                GunnerView.TransferOwnership(PhotonNetwork.LocalPlayer);
                LeftHand.TransferOwnership(PhotonNetwork.LocalPlayer);
                RightHand.TransferOwnership(PhotonNetwork.LocalPlayer);
                Debug.Log("Права на туррель и пулемет выданы");
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
        RestartRound();
    }

    // Перезапуск раунда: мастер сбрасывает готовность всех игроков и перезагружает сцену у всех.
    // Сброс отправляется раньше загрузки, иначе после рестарта старые флаги сразу запустили бы игру
    private void RestartRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        foreach (var player in PhotonNetwork.PlayerList)
            player.SetCustomProperties(new Hashtable { { ReadyKey, false } });

        PhotonNetwork.LoadLevel(SceneManager.GetActiveScene().name);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(ReadyKey))
        {
            UpdateReadyUI();
            CheckAllPlayersReady();
        }
    }

    public override void OnJoinedRoom()
    {
        UpdateReadyUI();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateReadyUI();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateReadyUI();

        // Напарник вышел посреди игры — возвращаем оставшегося в меню
        if (gameStarted)
            RestartRound();
    }

    private void UpdateReadyUI()
    {
        Player[] players = PhotonNetwork.PlayerList;
        SetReadyCircle(firstPlayerReadyCircle, players.Length > 0 ? players[0] : null);
        SetReadyCircle(secondPlayerReadyCircle, players.Length > 1 ? players[1] : null);
    }

    private void SetReadyCircle(Image circle, Player player)
    {
        if (circle == null) return;
        circle.color = IsReady(player) ? readyColor : notReadyColor;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (connectionStatusCircle != null)
            connectionStatusCircle.color = disconnectedColor;
    }

    public override void OnConnectedToMaster()
    {
        if (connectionStatusCircle != null)
            connectionStatusCircle.color = connectedColor;
    }
}
