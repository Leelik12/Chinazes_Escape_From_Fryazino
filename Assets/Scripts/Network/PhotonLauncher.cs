using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;

namespace RacingProject.Network
{
    public class PhotonLauncher : MonoBehaviourPunCallbacks
    {
        [Header("Настройки комнаты")]
        public string roomName = "Room1";
        public byte maxPlayers = 2;

        [Header("Частота сети")]
        [Tooltip("Пакетов в секунду. 120 перегружало канал без заметной пользы, 60 хватает для машины и рук")]
        [SerializeField] private int sendRate = 60;
        [Tooltip("Вызовов OnPhotonSerializeView в секунду, не больше Send Rate")]
        [SerializeField] private int serializationRate = 60;

        void Awake()
        {
            PhotonNetwork.SendRate = sendRate;
            PhotonNetwork.SerializationRate = Mathf.Min(serializationRate, sendRate);
            PhotonNetwork.AutomaticallySyncScene = true;
        }
        void Start()
        {
            // После перезапуска раунда сцена грузится заново, а соединение и комната остаются
            if (PhotonNetwork.InRoom) return;

            // Флаг готовности хранится у локального игрока и иначе уехал бы в новую комнату
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { RoomController.ReadyKey, false } });

            if (PhotonNetwork.InLobby)
            {
                OnJoinedLobby();
                return;
            }
            if (PhotonNetwork.IsConnectedAndReady)
            {
                OnConnectedToMaster();
                return;
            }

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

        // Например, комната уже заполнена двумя игроками
        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"Join room failed ({returnCode}): {message}");
        }

        // Вызывается, если подключение к серверу не удалось
        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning("Disconnected from Photon: " + cause);
        }
    }
}
