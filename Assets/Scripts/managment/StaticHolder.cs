using UnityEngine;

public static class StaticHolder
{ // значит обнул€ть при старте игры
    public static bool PlayerWin; //
    public static bool PlayerLose; //
    public static string LastTime;
    public static float LastTimeFloat;
    public static string BestTime;
    public static float BestTimeFloat;
    public static bool Record; //

    //настройки и звук
    public static float MusikVolume = 0f;
    public static float EngineVolume = 0f;
}
