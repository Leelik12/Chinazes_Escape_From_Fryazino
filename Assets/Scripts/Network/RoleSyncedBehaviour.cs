using UnityEngine;

namespace RacingProject.Network
{
    // Компонент, данные которого задаёт игрок определённой роли (водитель или стрелок),
    // а у напарника они применяются по сети. Отправку собирает RoleSyncHub на родительском объекте
    public abstract class RoleSyncedBehaviour : MonoBehaviour
    {
        [Tooltip("Чей компьютер задаёт эти данные: у этого игрока они считаются локально, у напарника приходят по сети")]
        [SerializeField] private PlayerRole authority = PlayerRole.Driver;

        public PlayerRole Authority => authority;

        // До старта раунда ролей нет, и никто ничего не задаёт
        public bool HasAuthority => authority != PlayerRole.None && LocalPlayerRole.Current == authority;
        // Управляет ли этим локальный ввод: в одиночной игре — у обеих ролей. Позы тел (их задаёт VR-трекинг)
        // в одиночной игре по-прежнему берут HasAuthority и стоят в позе по умолчанию
        public bool IsLocallyControlled => LocalPlayerRole.Controls(authority);

        // Пишет данные у автора и читает их у напарника
        public abstract void Serialize(SyncStream stream);
    }
}
