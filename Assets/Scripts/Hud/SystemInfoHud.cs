using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.XR;
using RacingProject.Management;
using RacingProject.Network;

namespace RacingProject.Hud
{
    // Системная информация: FPS и время кадра, память, экран, режим просмотра, роль и сеть.
    // Статистика кадров копится за refreshInterval и обновляет текст раз в этот интервал
    public class SystemInfoHud : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.F4;
        [SerializeField] private bool expanded = true;
        [Tooltip("Период обновления текста, с")]
        [SerializeField] private float refreshInterval = 0.5f;
        [Tooltip("Высота графиков загрузки CPU и GPU в пикселях экрана высотой 1080")]
        [SerializeField] private float graphHeight = 56f;
        [Tooltip("Сколько замеров помнит график; при интервале 0,5 с 120 замеров — минута")]
        [SerializeField] private int graphSamples = 120;

        private const float BytesInMb = 1024f * 1024f;

        private readonly StringBuilder builder = new StringBuilder(512);
        private HudPanel panel;
        private string text = string.Empty;

        private float elapsed;
        private int frames;
        private float minFrameTime = float.MaxValue;
        private float maxFrameTime;

        private SystemLoadMonitor monitor;
        private HudGraph cpuGraph;
        private HudGraph gpuGraph;
        private System.Action<Rect> drawGraphs;

        private void Awake()
        {
            panel = new HudPanel("СИСТЕМА", toggleKey, HudCorner.TopLeft, 0, 480f, expanded);
            // Синий — загрузка всей системы, оранжевый — доля игры
            cpuGraph = new HudGraph(graphSamples, new Color(0.35f, 0.65f, 1f, 0.75f), new Color(1f, 0.6f, 0.2f, 0.9f));
            gpuGraph = new HudGraph(graphSamples, new Color(0.35f, 0.65f, 1f, 0.75f), new Color(1f, 0.6f, 0.2f, 0.9f));
            drawGraphs = DrawGraphs;
        }

        private void OnEnable()
        {
            panel.Show();
            monitor = new SystemLoadMonitor(Mathf.RoundToInt(refreshInterval * 1000f));
            monitor.Start();
        }

        private void OnDisable()
        {
            panel.Hide();
            monitor.Dispose();
        }

        private void Update()
        {
            panel.HandleInput();

            float frameTime = Time.unscaledDeltaTime;
            elapsed += frameTime;
            frames++;
            minFrameTime = Mathf.Min(minFrameTime, frameTime);
            maxFrameTime = Mathf.Max(maxFrameTime, frameTime);

            if (elapsed < refreshInterval) return;

            if (monitor.Available)
            {
                cpuGraph.Push(monitor.CpuTotal, monitor.CpuProcess);
                gpuGraph.Push(monitor.GpuTotal, monitor.GpuProcess);
            }
            text = BuildText(frames / elapsed, elapsed / frames);
            elapsed = 0f;
            frames = 0;
            minFrameTime = float.MaxValue;
            maxFrameTime = 0f;
        }

        private void OnGUI()
        {
            float extraHeight = monitor.Available ? graphHeight * 2f + 6f : 0f;
            panel.Draw(text, extraHeight, drawGraphs);
        }

        private void DrawGraphs(Rect area)
        {
            Rect cpuRect = new Rect(area.x, area.y, area.width, graphHeight);
            Rect gpuRect = new Rect(area.x, area.y + graphHeight + 6f, area.width, graphHeight);
            cpuGraph.Draw(cpuRect, $"CPU {monitor.CpuTotal:F0}%  <color=#ffb060>игра {monitor.CpuProcess:F0}%</color>", HudPanel.TextStyle);
            gpuGraph.Draw(gpuRect, $"GPU {monitor.GpuTotal:F0}%  <color=#ffb060>игра {monitor.GpuProcess:F0}%</color>", HudPanel.TextStyle);
        }

        private string BuildText(float fps, float averageFrameTime)
        {
            builder.Clear();

            string fpsColor = fps >= 60f ? "#7dff7d" : fps >= 30f ? "#ffd27a" : "#ff7d7d";
            builder.Append($"FPS: <color={fpsColor}><b>{fps:F0}</b></color>   кадр {averageFrameTime * 1000f:F1} мс ")
                   .Append($"(мин {minFrameTime * 1000f:F1}, макс {maxFrameTime * 1000f:F1})\n");

            string vSync = QualitySettings.vSyncCount > 0 ? "вкл" : "выкл";
            string targetFps = Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "нет";
            builder.Append($"VSync: {vSync}   лимит FPS: {targetFps}   физика: {1f / Time.fixedDeltaTime:F0} Гц\n");

            builder.Append($"Экран: {Screen.width}×{Screen.height} @ {Screen.currentResolution.refreshRateRatio.value:F0} Гц, {Screen.fullScreenMode}\n");
            builder.Append($"Графика: {GraphicsQuality.PresetName}, масштаб {GraphicsQuality.OptionLabels(GraphicsQuality.Option.RenderScale)[GraphicsQuality.Get(GraphicsQuality.Option.RenderScale)]}, качество «{QualitySettings.names[QualitySettings.GetQualityLevel()]}»\n");

            float allocated = Profiler.GetTotalAllocatedMemoryLong() / BytesInMb;
            float reserved = Profiler.GetTotalReservedMemoryLong() / BytesInMb;
            float managed = System.GC.GetTotalMemory(false) / BytesInMb;
            builder.Append($"Память Unity: {allocated:F0} / {reserved:F0} МБ, managed {managed:F0} МБ\n");

            if (monitor.Available)
            {
                builder.Append($"RAM: {monitor.RamUsedMb / 1024f:F1} / {monitor.RamTotalMb / 1024f:F1} ГБ, игра {monitor.RamProcessMb:F0} МБ\n");
                // У дискретной видеокарты своя память (graphicsMemorySize — её объём),
                // у встроенной выделенной нет: она берёт общую из RAM
                if (monitor.VramDedicatedMb > 0f)
                    builder.Append($"VRAM: {monitor.VramDedicatedMb:F0} / {SystemInfo.graphicsMemorySize} МБ, игра {monitor.VramProcessDedicatedMb:F0} МБ\n");
                else
                    builder.Append($"VRAM (встроенная, общая с RAM): {monitor.VramSharedMb:F0} МБ, игра {monitor.VramProcessSharedMb:F0} МБ\n");
            }
            else
            {
                builder.Append($"RAM: {SystemInfo.systemMemorySize} МБ   VRAM: {SystemInfo.graphicsMemorySize} МБ (загрузка недоступна)\n");
            }

            builder.Append($"CPU: {SystemInfo.processorType}\n");
            builder.Append($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})\n");

            string view = ViewModeService.IsVR
                ? $"VR ({(XRSettings.isDeviceActive ? XRSettings.loadedDeviceName : "устройство не активно")})"
                : "монитор";
            builder.Append($"Режим: {view}   роль: {RoleName(LocalPlayerRole.Current)}\n");
            builder.Append(NetworkStatus());

            return builder.ToString();
        }

        private static string RoleName(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Driver: return "водитель";
                case PlayerRole.Gunner: return "стрелок";
                case PlayerRole.Solo: return "одиночная игра";
                default: return "нет";
            }
        }

        // Пинг — время пакета туда и обратно по данным транспорта
        private static string NetworkStatus()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
                return "Сеть: не подключено";

            NetworkTransport transport = manager.NetworkConfig.NetworkTransport;
            if (manager.IsServer)
            {
                int clients = manager.ConnectedClientsIds.Count - (manager.IsHost ? 1 : 0);
                string ping = string.Empty;
                foreach (ulong clientId in manager.ConnectedClientsIds)
                {
                    if (clientId == manager.LocalClientId) continue;
                    ping = $", пинг до клиента {transport.GetCurrentRtt(clientId)} мс";
                    break;
                }
                return $"Сеть: хост, клиентов {clients}{ping}";
            }

            string state = manager.IsConnectedClient ? "подключён" : "подключение…";
            return $"Сеть: клиент, {state}, пинг {transport.GetCurrentRtt(NetworkManager.ServerClientId)} мс";
        }
    }
}
