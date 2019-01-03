using ENet;
using NetStack.Buffers;

namespace SoL.Networking
{
    public static class PeerArrayPool
    {
        private static ArrayPool<Peer> m_pool = null;
        private static ArrayPool<Peer> Pool
        {
            get
            {
                if (m_pool == null)
                {
                    m_pool = ArrayPool<Peer>.Create(1024, 50);
                }
                return m_pool;
            }
        }

        public static Peer[] GetArray(int size)
        {
            return Pool.Rent(size);
        }

        public static void ReturnArray(Peer[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = default(Peer);
            }
            Pool.Return(data);
        }
    }
}