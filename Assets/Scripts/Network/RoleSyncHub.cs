using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RacingProject.Network
{
    // Собирает RoleSyncedBehaviour в дочерних объектах и раз в 1/sendRate секунды отправляет напарнику
    // данные компонентов, автор которых — локальный игрок. Заменяет вложенные PhotonView:
    // на машине один NetworkObject, а данные водителя и стрелка идут ненадёжными RPC от каждого из них
    public class RoleSyncHub : NetworkBehaviour
    {
        [Tooltip("Сколько раз в секунду отправлять данные")]
        [SerializeField] private float sendRate = 30f;

        private readonly List<RoleSyncedBehaviour> behaviours = new List<RoleSyncedBehaviour>();
        private readonly SyncStream stream = new SyncStream();
        private float sendTimer;

        public override void OnNetworkSpawn()
        {
            // Иерархия у обоих игроков одинаковая (объекты из одной сцены), поэтому порядок компонентов совпадает
            behaviours.Clear();
            GetComponentsInChildren(true, behaviours);
            sendTimer = 0f;
        }

        private void LateUpdate()
        {
            if (!IsSpawned)
                return;

            PlayerRole role = LocalPlayerRole.Current;
            if (role == PlayerRole.None)
                return;

            sendTimer -= Time.unscaledDeltaTime;
            if (sendTimer > 0f)
                return;
            sendTimer = 1f / Mathf.Max(1f, sendRate);

            stream.BeginWrite();
            bool hasData = false;
            foreach (RoleSyncedBehaviour behaviour in behaviours)
            {
                if (behaviour.Authority != role)
                    continue;
                behaviour.Serialize(stream);
                hasData = true;
            }

            if (hasData)
                SyncRpc((byte)role, stream.ToArray());
        }

        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable)]
        private void SyncRpc(byte role, byte[] data)
        {
            PlayerRole sender = (PlayerRole)role;
            // Свои данные не применяем, даже если роли случайно совпали
            if (sender == PlayerRole.None || sender == LocalPlayerRole.Current)
                return;

            stream.BeginRead(data);
            try
            {
                foreach (RoleSyncedBehaviour behaviour in behaviours)
                {
                    if (behaviour.Authority == sender)
                        behaviour.Serialize(stream);
                }
            }
            catch (System.IO.EndOfStreamException)
            {
                // Пакет короче ожидаемого (например, разные версии сборки) — пропускаем его
            }
        }
    }
}
