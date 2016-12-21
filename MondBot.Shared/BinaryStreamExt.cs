using System.IO;
using System.Threading.Tasks;

namespace MondBot
{
    public static class BinaryStreamExt
    {
        public static Task<int> ReadInt32Async(this BinaryReader reader)
        {
            return Task.Run(() => reader.ReadInt32());
        }

        public static Task WriteInt32Async(this BinaryWriter writer, int value)
        {
            return Task.Run(() => writer.Write(value));
        }

        public static Task<string> ReadStringAsync(this BinaryReader reader)
        {
            return Task.Run(() => reader.ReadString());
        }

        public static Task WriteStringAsync(this BinaryWriter writer, string value)
        {
            return Task.Run(() => writer.Write(value));
        }

        public static Task<byte[]> ReadBytesAsync(this BinaryReader reader)
        {
            return Task.Run(() =>
            {
                var length = reader.ReadInt32();
                return reader.ReadBytes(length);
            });
        }

        public static Task WriteBytesAsync(this BinaryWriter writer, byte[] data)
        {
            return Task.Run(() =>
            {
                writer.Write(data.Length);
                writer.Write(data);
            });
        }
    }
}
