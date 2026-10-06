using System.IO;
using UnityEngine;

namespace RacingProject.Network
{
    // Поток сетевых данных в стиле OnPhotonSerializeView: один и тот же метод Serialize и пишет, и читает,
    // поэтому порядок полей у отправителя и получателя совпадает автоматически
    public sealed class SyncStream
    {
        private readonly MemoryStream buffer = new MemoryStream(256);
        private readonly BinaryWriter writer;
        private readonly BinaryReader reader;

        public bool IsWriting { get; private set; }

        public SyncStream()
        {
            writer = new BinaryWriter(buffer);
            reader = new BinaryReader(buffer);
        }

        public void BeginWrite()
        {
            IsWriting = true;
            buffer.SetLength(0);
        }

        public byte[] ToArray()
        {
            writer.Flush();
            return buffer.ToArray();
        }

        public void BeginRead(byte[] data)
        {
            IsWriting = false;
            buffer.SetLength(0);
            buffer.Write(data, 0, data.Length);
            buffer.Position = 0;
        }

        public void Serialize(ref float value)
        {
            if (IsWriting) writer.Write(value);
            else value = reader.ReadSingle();
        }

        public void Serialize(ref bool value)
        {
            if (IsWriting) writer.Write(value);
            else value = reader.ReadBoolean();
        }

        public void Serialize(ref byte value)
        {
            if (IsWriting) writer.Write(value);
            else value = reader.ReadByte();
        }

        public void Serialize(ref Vector3 value)
        {
            Serialize(ref value.x);
            Serialize(ref value.y);
            Serialize(ref value.z);
        }

        public void Serialize(ref Quaternion value)
        {
            Serialize(ref value.x);
            Serialize(ref value.y);
            Serialize(ref value.z);
            Serialize(ref value.w);
        }
    }
}
