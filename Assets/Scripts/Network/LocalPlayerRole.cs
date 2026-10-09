using Unity.Netcode;

namespace RacingProject.Network
{
    // Роль локального игрока в текущем раунде
    public enum PlayerRole
    {
        None,
        Driver,
        Gunner,
        // Одиночная игра на мониторе: один игрок и ведёт машину, и стреляет
        Solo
    }

    // Задаётся RoomController при старте игры и сбрасывается при загрузке сцены.
    // По роли телеметрия решает, на какую платформу отправлять данные с этой машины
    public static class LocalPlayerRole
    {
        public static PlayerRole Current { get; private set; } = PlayerRole.None;

        public static void Set(PlayerRole role)
        {
            Current = role;
        }

        public static bool IsSolo => Current == PlayerRole.Solo;

        // Управляет ли локальный игрок тем, что задаёт эта роль: в одиночной игре — и водителем, и стрелком
        public static bool Controls(PlayerRole role)
        {
            return role != PlayerRole.None && (Current == role || Current == PlayerRole.Solo);
        }

        // Физику машины считает хост (он же водитель), а без сети — сама игра.
        // У клиента машину двигает NetworkTransform
        public static bool SimulatesPhysics
        {
            get
            {
                NetworkManager manager = NetworkManager.Singleton;
                return manager == null || !manager.IsListening || manager.IsServer;
            }
        }
    }
}
