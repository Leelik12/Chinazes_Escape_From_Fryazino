using System;
using System.IO;
using UnityEngine;

public class VRTestLogger : MonoBehaviour
{
    private string logFilePath;
    private float logInterval = 1f; // раз в секунду пишет состояние
    private float timer;
    private int frameCount;
    private float deltaTime;

    private void Awake()
    {
        // путь к файлу логов
        logFilePath = Path.Combine(Application.persistentDataPath, "VR_DebugLog.txt");

        try
        {
            File.WriteAllText(logFilePath, $"=== VR Debug Log Started at {DateTime.Now} ===\n");
        }
        catch (Exception e)
        {
            Debug.LogWarning("Не удалось создать файл логов: " + e.Message);
        }

        // Подписка на ошибки Unity
        Application.logMessageReceived += OnUnityLog;
    }

    private void Update()
    {
        frameCount++;
        deltaTime += Time.unscaledDeltaTime;

        timer += Time.unscaledDeltaTime;
        if (timer >= logInterval)
        {
            float fps = frameCount / deltaTime;
            frameCount = 0;
            deltaTime = 0f;
            timer = 0f;

            LogStatus(fps);
        }
    }

    private void LogStatus(float fps)
    {
        long totalMemory = GC.GetTotalMemory(false) / (1024 * 1024); // в MB
        string logLine = $"[{DateTime.Now:HH:mm:ss}] FPS: {fps:F1}, " +
                         $"PhysicsStep: {Time.fixedDeltaTime:F3}, " +
                         $"Memory: {totalMemory}MB, " +
                         $"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";

        WriteLog(logLine);
    }

    private void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        string logLine = $"[{DateTime.Now:HH:mm:ss}] [{type}] {condition}";
        if (type == LogType.Exception || type == LogType.Error)
            logLine += $"\n{stackTrace}";
        WriteLog(logLine);
    }

    private void WriteLog(string line)
    {
        try
        {
            File.AppendAllText(logFilePath, line + "\n");
        }
        catch { /* игнорируем, если файл временно занят */ }
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnUnityLog;
        WriteLog("=== VR Debug Log Stopped ===");
    }
}
