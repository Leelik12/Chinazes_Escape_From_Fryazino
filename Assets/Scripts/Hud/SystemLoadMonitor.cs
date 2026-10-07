using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace RacingProject.Hud
{
    // Загрузка системы для панели HUD: CPU и GPU (всего и игрой), занятая RAM и видеопамять.
    // Берётся из счётчиков производительности Windows (PDH) — тех же, что показывает Диспетчер задач.
    // Опрос идёт в фоновом потоке: сбор счётчиков GPU по всем процессам занимает миллисекунды.
    // Вне Windows данные недоступны (Available = false)
    public sealed class SystemLoadMonitor : IDisposable
    {
        private const float BytesInMb = 1024f * 1024f;

        private readonly int intervalMs;
        private Thread thread;
        private volatile bool running;

        private volatile bool available;
        private volatile float cpuTotal;
        private volatile float cpuProcess;
        private volatile float gpuTotal;
        private volatile float gpuProcess;
        private volatile float ramTotalMb;
        private volatile float ramUsedMb;
        private volatile float ramProcessMb;
        private volatile float vramDedicatedMb;
        private volatile float vramSharedMb;
        private volatile float vramProcessDedicatedMb;
        private volatile float vramProcessSharedMb;

        public bool Available => available;
        public float CpuTotal => cpuTotal;            // %, все процессы
        public float CpuProcess => cpuProcess;        // %, игра (от всех ядер)
        public float GpuTotal => gpuTotal;            // %, 3D-движок GPU, все процессы
        public float GpuProcess => gpuProcess;        // %, 3D-движок GPU, игра
        public float RamTotalMb => ramTotalMb;
        public float RamUsedMb => ramUsedMb;
        public float RamProcessMb => ramProcessMb;    // рабочий набор процесса игры
        public float VramDedicatedMb => vramDedicatedMb;               // выделенная видеопамять, всего
        public float VramSharedMb => vramSharedMb;                     // общая (из RAM), всего
        public float VramProcessDedicatedMb => vramProcessDedicatedMb; // выделенная, игра
        public float VramProcessSharedMb => vramProcessSharedMb;       // общая, игра

        public SystemLoadMonitor(int intervalMs)
        {
            this.intervalMs = intervalMs;
        }

        public void Start()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (running) return;
            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "SystemLoadMonitor" };
            thread.Start();
#endif
        }

        public void Dispose()
        {
            running = false;
            thread = null;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void Run()
        {
            IntPtr query = IntPtr.Zero;
            try
            {
                // System.Diagnostics.Process в Mono Unity отдаёт нули для времени и памяти процесса,
                // поэтому они читаются напрямую из WinAPI
                IntPtr process = GetCurrentProcess();
                string pidTag = "pid_" + GetCurrentProcessId() + "_";
                int cores = Environment.ProcessorCount;

                if (PdhOpenQuery(null, IntPtr.Zero, out query) != 0) return;

                // «% Processor Utility» совпадает с Диспетчером задач, на старых системах его нет
                IntPtr cpuCounter = AddCounter(query, @"\Processor Information(_Total)\% Processor Utility");
                if (cpuCounter == IntPtr.Zero)
                    cpuCounter = AddCounter(query, @"\Processor(_Total)\% Processor Time");

                IntPtr gpuCounter = AddCounter(query, @"\GPU Engine(*engtype_3D)\Utilization Percentage");
                IntPtr dedicatedCounter = AddCounter(query, @"\GPU Adapter Memory(*)\Dedicated Usage");
                IntPtr sharedCounter = AddCounter(query, @"\GPU Adapter Memory(*)\Shared Usage");
                IntPtr processDedicatedCounter = AddCounter(query, @"\GPU Process Memory(" + pidTag + @"*)\Dedicated Usage");
                IntPtr processSharedCounter = AddCounter(query, @"\GPU Process Memory(" + pidTag + @"*)\Shared Usage");

                // Счётчики процентов считаются по разнице двух замеров
                PdhCollectQueryData(query);
                long lastProcessorTime = ProcessorTime100Ns(process);
                Stopwatch stopwatch = Stopwatch.StartNew();

                while (running)
                {
                    Thread.Sleep(intervalMs);
                    if (PdhCollectQueryData(query) != 0) continue;

                    double cpu;
                    if (TryGetValue(cpuCounter, out cpu)) cpuTotal = Clamp100((float)cpu);

                    double gpuAll, gpuOwn;
                    MaxByAdapter(gpuCounter, pidTag, out gpuAll, out gpuOwn);
                    gpuTotal = Clamp100((float)gpuAll);
                    gpuProcess = Clamp100((float)gpuOwn);

                    double bytes, unused;
                    SumArray(dedicatedCounter, null, out bytes, out unused);
                    vramDedicatedMb = (float)(bytes / BytesInMb);
                    SumArray(sharedCounter, null, out bytes, out unused);
                    vramSharedMb = (float)(bytes / BytesInMb);
                    SumArray(processDedicatedCounter, null, out bytes, out unused);
                    vramProcessDedicatedMb = (float)(bytes / BytesInMb);
                    SumArray(processSharedCounter, null, out bytes, out unused);
                    vramProcessSharedMb = (float)(bytes / BytesInMb);

                    // Процессорное время игры за интервал, поделённое на время всех ядер
                    long processorTime = ProcessorTime100Ns(process);
                    double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                    stopwatch.Reset();
                    stopwatch.Start();
                    if (elapsedMs > 0)
                        cpuProcess = Clamp100((float)((processorTime - lastProcessorTime) / 10000.0 / (elapsedMs * cores) * 100.0));
                    lastProcessorTime = processorTime;

                    var counters = new ProcessMemoryCounters { cb = (uint)Marshal.SizeOf(typeof(ProcessMemoryCounters)) };
                    if (K32GetProcessMemoryInfo(process, ref counters, counters.cb))
                        ramProcessMb = counters.WorkingSetSize.ToUInt64() / BytesInMb;

                    var memory = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx)) };
                    if (GlobalMemoryStatusEx(ref memory))
                    {
                        ramTotalMb = memory.ullTotalPhys / BytesInMb;
                        ramUsedMb = (memory.ullTotalPhys - memory.ullAvailPhys) / BytesInMb;
                    }

                    available = true;
                }
            }
            catch (Exception exception)
            {
                // Счётчики могут быть отключены политикой или повреждены — панель тогда пишет «нет данных»
                UnityEngine.Debug.LogWarning("SystemLoadMonitor: счётчики производительности недоступны: " + exception.Message);
                available = false;
            }
            finally
            {
                if (query != IntPtr.Zero) PdhCloseQuery(query);
            }
        }

        // Время процесса в ядре и в пользовательском режиме, в единицах по 100 нс
        private static long ProcessorTime100Ns(IntPtr process)
        {
            long creation, exit, kernel, user;
            return GetProcessTimes(process, out creation, out exit, out kernel, out user) ? kernel + user : 0;
        }

        // Нулевой указатель, если счётчика нет в системе: тогда его значение просто не обновляется
        private static IntPtr AddCounter(IntPtr query, string path)
        {
            IntPtr counter;
            return PdhAddEnglishCounter(query, path, IntPtr.Zero, out counter) == 0 ? counter : IntPtr.Zero;
        }

        private static float Clamp100(float value)
        {
            return value < 0f ? 0f : value > 100f ? 100f : value;
        }

        private static bool TryGetValue(IntPtr counter, out double value)
        {
            value = 0;
            if (counter == IntPtr.Zero) return false;
            PdhFmtCounterValue result;
            if (PdhGetFormattedCounterValue(counter, PdhFmtDouble | PdhFmtNoCap100, IntPtr.Zero, out result) != 0 || result.CStatus != 0)
                return false;
            value = result.DoubleValue;
            return true;
        }

        // Загрузка GPU как в Диспетчере задач: сумма по процессам внутри видеокарты, а из нескольких видеокарт
        // (ноутбук со встроенной и дискретной) — самая загруженная. Видеокарта задаётся частью имени «luid_…»
        private static void MaxByAdapter(IntPtr counter, string ownTag, out double total, out double own)
        {
            total = 0;
            own = 0;
            var adapters = new System.Collections.Generic.Dictionary<string, double[]>();
            ForEachItem(counter, (name, value) =>
            {
                int start = name.IndexOf("luid_", StringComparison.Ordinal);
                int end = start >= 0 ? name.IndexOf("_phys", start, StringComparison.Ordinal) : -1;
                string adapter = start >= 0 && end > start ? name.Substring(start, end - start) : string.Empty;

                double[] sums;
                if (!adapters.TryGetValue(adapter, out sums))
                    adapters[adapter] = sums = new double[2];
                sums[0] += value;
                if (name.Contains(ownTag)) sums[1] += value;
            });

            foreach (double[] sums in adapters.Values)
            {
                if (sums[0] > total) total = sums[0];
                if (sums[1] > own) own = sums[1];
            }
        }

        // Сумма по всем экземплярам счётчика с подстановкой; own — по экземплярам, в имени которых есть ownTag
        private static void SumArray(IntPtr counter, string ownTag, out double total, out double own)
        {
            double sum = 0, ownSum = 0;
            ForEachItem(counter, (name, value) =>
            {
                sum += value;
                if (ownTag != null && name.Contains(ownTag)) ownSum += value;
            });
            total = sum;
            own = ownSum;
        }

        private static void ForEachItem(IntPtr counter, Action<string, double> action)
        {
            if (counter == IntPtr.Zero) return;

            uint bufferSize = 0, itemCount;
            int status = PdhGetFormattedCounterArray(counter, PdhFmtDouble | PdhFmtNoCap100, ref bufferSize, out itemCount, IntPtr.Zero);
            if (status != PdhMoreData || bufferSize == 0) return;

            IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
            try
            {
                if (PdhGetFormattedCounterArray(counter, PdhFmtDouble | PdhFmtNoCap100, ref bufferSize, out itemCount, buffer) != 0) return;

                int itemSize = Marshal.SizeOf(typeof(PdhFmtCounterValueItem));
                for (int i = 0; i < itemCount; i++)
                {
                    var item = (PdhFmtCounterValueItem)Marshal.PtrToStructure(new IntPtr(buffer.ToInt64() + i * itemSize), typeof(PdhFmtCounterValueItem));
                    if (item.CStatus != 0) continue;
                    action(Marshal.PtrToStringUni(item.Name), item.DoubleValue);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private const uint PdhFmtDouble = 0x00000200;
        private const uint PdhFmtNoCap100 = 0x00008000;
        private const int PdhMoreData = unchecked((int)0x800007D2);

        [StructLayout(LayoutKind.Explicit)]
        private struct PdhFmtCounterValue
        {
            [FieldOffset(0)] public uint CStatus;
            [FieldOffset(8)] public double DoubleValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PdhFmtCounterValueItem
        {
            public IntPtr Name;
            public uint CStatus;
            public double DoubleValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCounters
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(IntPtr process, out long creationTime, out long exitTime, out long kernelTime, out long userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool K32GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern int PdhOpenQuery(string dataSource, IntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern int PdhAddEnglishCounter(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);

        [DllImport("pdh.dll")]
        private static extern int PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll")]
        private static extern int PdhGetFormattedCounterValue(IntPtr counter, uint format, IntPtr type, out PdhFmtCounterValue value);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern int PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

        [DllImport("pdh.dll")]
        private static extern int PdhCloseQuery(IntPtr query);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
#endif
    }
}
