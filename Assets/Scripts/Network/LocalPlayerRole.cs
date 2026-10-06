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
    }
}
