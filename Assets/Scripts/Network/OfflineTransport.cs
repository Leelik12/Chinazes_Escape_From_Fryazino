using System;
using Unity.Netcode;
using UnityEngine;

namespace RacingProject.Network
{
    // Транспорт без сети для одиночной игры и обучения. Логика игры (прочность, нитро, враги, бочки) построена
    // на Netcode, поэтому и в одиночку нужен хост, но ему не с кем общаться: сообщения своему клиенту
    // Netcode передаёт внутри процесса, а этот транспорт не открывает сокетов и портов и никого не пускает.
    // LanLobby ставит его в NetworkManager перед запуском одиночной игры, а для сетевой возвращает UnityTransport
    public class OfflineTransport : NetworkTransport
    {
        public override ulong ServerClientId => 0;

        public override void Initialize(NetworkManager networkManager = null)
        {
        }

        public override bool StartServer()
        {
            return true;
        }

        // Подключаться некуда
        public override bool StartClient()
        {
            return false;
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            clientId = 0;
            payload = default;
            receiveTime = Time.realtimeSinceStartup;
            return NetworkEvent.Nothing;
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
        }

        public override void DisconnectLocalClient()
        {
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            return 0;
        }

        public override void Shutdown()
        {
        }
    }
}
