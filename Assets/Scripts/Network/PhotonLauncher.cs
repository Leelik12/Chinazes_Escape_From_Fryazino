using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonLauncher : MonoBehaviourPunCallbacks
{
    [Header("Настройки комнаты")]
    public string roomName = "Room1";
    public byte maxPlayers = 2;

    void Awake()
    {
        PhotonNetwork.SendRate = 120;
        PhotonNetwork.SerializationRate = 120;
        PhotonNetwork.AutomaticallySyncScene = true;
    }
    void Start()
    {

        // Подключаемся к серверу Photon
        Debug.Log("Connecting to Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }

    // Вызывается при успешном соединении с Photon Master Server
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master");
        PhotonNetwork.JoinLobby(); // присоединяемся к лобби
    }

    // Вызывается при присоединении к лобби
    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby, creating or joining room...");

        // Пытаемся подключиться к комнате, если нет — создаем
        PhotonNetwork.JoinOrCreateRoom(roomName,
            new RoomOptions { MaxPlayers = maxPlayers },
            TypedLobby.Default);
    }

    // Вызывается после успешного присоединения к комнате
    public override void OnJoinedRoom()
    {
        Debug.Log("Joined Room. Players in room: " + PhotonNetwork.CurrentRoom.PlayerCount);
    }

    // Вызывается, если подключение к серверу не удалось
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning("Disconnected from Photon: " + cause);
    }
}
