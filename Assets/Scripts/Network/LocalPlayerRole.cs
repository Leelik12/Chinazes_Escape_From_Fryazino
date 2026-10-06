using Unity.Netcode;

namespace RacingProject.Network
{
    // Роль локального игрока в текущем раунде
    public enum PlayerRole
    {
        None,
        Driver,
        Gunner
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
