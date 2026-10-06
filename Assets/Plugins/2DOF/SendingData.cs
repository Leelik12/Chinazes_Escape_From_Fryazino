using System.IO.MemoryMappedFiles;
using System.Threading;

namespace _2DOF
{
    /// <summary>
    /// Этот класс используется для отправки данных
    /// </summary>
    public sealed class SendingData
    {
        /// <summary>
        /// Время ожидания между отправками данных.
        /// В миллисекундах.
        /// </summary>
        public const int WAIT_TIME = 20;

        /// <summary>
        /// Данные телеметрии объекта.
        /// </summary>
        public readonly ObjectTelemetryData ObjectTelemetryData = new();

        private const string MAP_NAME = "2DOFMemoryDataGrabber";
        private const int VALUES_COUNT = 6;
        private Thread _thread;
        private volatile bool _running;

        /// <summary>
        /// Запуск отправки данных.
        /// </summary>
        public void SendingStart()
        {
            if (_running) return;
            _running = true;
            // Фоновый поток не держит процесс, если остановку не вызвали
            _thread = new Thread(HandlerData) { IsBackground = true };
            _thread.Start();
        }

        /// <summary>
        /// Остановка отправки данных.
        /// Поток завершается сам после текущей записи, вместо Thread.Abort посреди записи в файл.
        /// </summary>
        public void SendingStop()
        {
            if (!_running) return;
            _running = false;
            _thread?.Join(WAIT_TIME * 5);
            _thread = null;
        }

        private void HandlerData()
        {
            // Размер в байтах. Раньше передавалось число значений (6 байт) при записи 6 double;
            // работало только потому, что Windows округляет отображение до страницы памяти
            using var memoryMappedFile = MemoryMappedFile.CreateOrOpen(MAP_NAME, VALUES_COUNT * sizeof(double));
            using var accessor = memoryMappedFile.CreateViewAccessor();

            while (_running)
            {
                accessor.WriteArray(0, ObjectTelemetryData.DataArray, 0, VALUES_COUNT);
                Thread.Sleep(WAIT_TIME);
            }

            // Нули возвращают платформу в нейтраль, иначе она осталась бы в последнем наклоне
            accessor.WriteArray(0, new double[VALUES_COUNT], 0, VALUES_COUNT);
        }
    }
}