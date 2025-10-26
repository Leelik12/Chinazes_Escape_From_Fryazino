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
        // Количество сетевых обновлений в секунду (по умолчанию 10)
        PhotonNetwork.SendRate = 60;

        // Сколько раз в секунду PUN сериализует данные объектов (по умолчанию 10)
        PhotonNetwork.SerializationRate = 60;
    }
    void Start()
    {
        // Автоматическая синхронизация сцен (по желанию)
        PhotonNetwork.AutomaticallySyncScene = true;

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

        // Здесь можно сразу вызывать спавн префабов (Car/Turret) через отдельный скрипт RoomController
    }

    // Вызывается, если подключение к серверу не удалось
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning("Disconnected from Photon: " + cause);
    }
}
