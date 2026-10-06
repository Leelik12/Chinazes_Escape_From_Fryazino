namespace RacingProject
{
    // Теги объектов сцены
    public static class Tags
    {
        public const string Car = "Car";
    }

    // ID событий bHaptics из привязанного приложения
    public static class HapticEvents
    {
        public const string Acceleration = "razgon";
        public const string Braking = "remen_bezopasnosty";
        public const string TurnRight = "povorot_pravo";
        public const string TurnLeft = "povorot_levo";
        public const string HandsDamage = "damage_hands";
        public const string SuitLowHealth = "suit_low_hp";
        public const string HandsLowHealth = "hand_low_hp";
    }
}
