using NetStack.Buffers;

namespace SoL.Networking
{
    public static class ByteArrayPool
    {
        private static ArrayPool<byte> m_pool = null;
        private static ArrayPool<byte> Pool
        {
            get
            {
                if (m_pool == null)
                {
                    m_pool = ArrayPool<byte>.Create(1024, 50);
                }
                return m_pool;
            }
        }

        public static byte[] GetByteArray(int size)
        {
            return Pool.Rent(size);
        }

        public static void ReturnByteArray(byte[] data)
        {
            Pool.Return(data);
        }
    }
}