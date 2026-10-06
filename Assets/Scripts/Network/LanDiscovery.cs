using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace RacingProject.Network
{
    // Поиск игры в локальной сети: хост раз в секунду рассылает UDP broadcast с портом игры,
    // клиент слушает порт обнаружения и берёт адрес хоста из отправителя пакета
    public sealed class LanDiscovery : IDisposable
    {
        public const int DiscoveryPort = 47777;
        // Чужие программы на том же порту не примем за хост
        private const string Signature = "RACINGPROJECT_LAN_1";
        private const float BroadcastInterval = 1f;

        private UdpClient socket;
        private List<IPEndPoint> broadcastTargets;
        private byte[] announcement;
        private float nextBroadcastTime;

        public bool IsBroadcasting => socket != null && announcement != null;
        public bool IsListening => socket != null && announcement == null;

        public void StartBroadcasting(ushort gamePort)
        {
            Stop();
            try
            {
                socket = new UdpClient { EnableBroadcast = true };
                broadcastTargets = GetBroadcastTargets();
                announcement = Encoding.UTF8.GetBytes(Signature + "|" + gamePort);
                nextBroadcastTime = 0f;
            }
            catch (Exception e)
            {
                Debug.LogWarning("LAN discovery: не удалось начать рассылку: " + e.Message);
                Stop();
            }
        }

        public bool StartListening()
        {
            Stop();
            try
            {
                socket = new UdpClient();
                // Порт можно делить с другим экземпляром игры на этом же компьютере
                socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("LAN discovery: не удалось открыть порт " + DiscoveryPort + ": " + e.Message);
                Stop();
                return false;
            }
        }

        // Хост вызывает каждый кадр, рассылка идёт раз в BroadcastInterval
        public void Tick(float time)
        {
            if (!IsBroadcasting || time < nextBroadcastTime)
                return;
            nextBroadcastTime = time + BroadcastInterval;

            foreach (IPEndPoint target in broadcastTargets)
            {
                try
                {
                    socket.Send(announcement, announcement.Length, target);
                }
                catch (SocketException)
                {
                    // Отдельный адаптер может быть недоступен — остальным всё равно отправляем
                }
            }
        }

        // Клиент вызывает каждый кадр; true, если пришло объявление хоста
        public bool TryReceive(out string address, out ushort port)
        {
            address = null;
            port = 0;
            if (!IsListening)
                return false;

            try
            {
                while (socket.Available > 0)
                {
                    IPEndPoint sender = null;
                    byte[] data = socket.Receive(ref sender);
                    string[] parts = Encoding.UTF8.GetString(data).Split('|');
                    if (parts.Length == 2 && parts[0] == Signature && ushort.TryParse(parts[1], out port))
                    {
                        address = sender.Address.ToString();
                        return true;
                    }
                }
            }
            catch (SocketException)
            {
                // Windows сообщает так о недоставленных пакетах — просто ждём следующих
            }
            return false;
        }

        public void Stop()
        {
            if (socket != null)
                socket.Close();
            socket = null;
            announcement = null;
        }

        public void Dispose()
        {
            Stop();
        }

        // Общий broadcast Windows отправляет только через один адаптер, поэтому шлём ещё и в broadcast
        // каждой IPv4-подсети (Wi-Fi, Ethernet), а также на loopback для запуска двух копий на одном ПК
        private static List<IPEndPoint> GetBroadcastTargets()
        {
            var targets = new List<IPEndPoint>
            {
                new IPEndPoint(IPAddress.Broadcast, DiscoveryPort),
                new IPEndPoint(IPAddress.Loopback, DiscoveryPort)
            };

            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up
                        || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    foreach (UnicastIPAddressInformation info in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily != AddressFamily.InterNetwork || info.IPv4Mask == null)
                            continue;

                        byte[] ip = info.Address.GetAddressBytes();
                        byte[] mask = info.IPv4Mask.GetAddressBytes();
                        for (int i = 0; i < ip.Length; i++)
                            ip[i] |= (byte)~mask[i];

                        var target = new IPEndPoint(new IPAddress(ip), DiscoveryPort);
                        if (!targets.Contains(target))
                            targets.Add(target);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("LAN discovery: не удалось получить список адаптеров: " + e.Message);
            }
            return targets;
        }
    }
}
