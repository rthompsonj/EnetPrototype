using NetStack.Buffers;

namespace Threaded
{
    public static class ByteArrayPool
    {
        private static ArrayPool<byte> m_buffers = null;
        private static ArrayPool<byte> Buffers
        {
            get
            {
                if (m_buffers == null)
                {
                    m_buffers = ArrayPool<byte>.Create(1024, 50);
                }
                return m_buffers;
            }
        }

        public static byte[] GetByteArray(int size)
        {
            return Buffers.Rent(size);
        }

        public static void ReturnByteArray(byte[] data)
        {
            Buffers.Return(data);
        }
    }
}