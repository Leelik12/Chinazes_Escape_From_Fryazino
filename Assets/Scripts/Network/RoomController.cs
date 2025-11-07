using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;
using UnityEngine.UI;

public class RoomController : MonoBehaviourPunCallbacks
{
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
    private float connectionCheckTimer = 0f;
    private float connectionCheckInterval = 1f;

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
        if (isLocalReady) return;
        Debug.Log("Кнопка готовности нажата");
        isLocalReady = true;

        // Устанавливаем CustomProperty "IsReady"
        Hashtable props = new Hashtable { { "IsReady", true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        UpdateReadyUI();
        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!player.CustomProperties.ContainsKey("IsReady") || !(bool)player.CustomProperties["IsReady"])
                return;
        }

        // Все игроки готовы — активируем риги
        ActivatePlayerRigs();
    }

    private void ActivatePlayerRigs()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonView carView = car.GetComponent<PhotonView>();
            PhotonView bodyView = DriverBody.GetComponent<PhotonView>();
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

        EnemyManager enemyManager = FindObjectOfType<EnemyManager>();
        if (enemyManager != null)
        {
            enemyManager.StartGame();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey("IsReady"))
        {
            UpdateReadyUI();
            CheckAllPlayersReady();
        }
    }

    private void UpdateReadyUI()
    {
        if (PhotonNetwork.PlayerList.Length < 2) return;

        Player first = PhotonNetwork.PlayerList[0];
        firstPlayerReadyCircle.color = (first.CustomProperties.ContainsKey("IsReady") && (bool)first.CustomProperties["IsReady"])
            ? readyColor : notReadyColor;

        if (PhotonNetwork.PlayerList.Length > 1)
        {
            Player second = PhotonNetwork.PlayerList[1];
            secondPlayerReadyCircle.color = (second.CustomProperties.ContainsKey("IsReady") && (bool)second.CustomProperties["IsReady"])
                ? readyColor : notReadyColor;
        }
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
