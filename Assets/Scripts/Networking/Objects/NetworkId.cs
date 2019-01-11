using System;
using ENet;

namespace SoL.Networking.Objects
{
    public struct NetworkId : IEquatable<NetworkId>
    {
        private readonly uint m_value;
        public uint Value => m_value;

        private readonly Peer m_peer;
        public Peer Peer => m_peer;

        public NetworkId(uint value)
        {
            m_value = value;
            m_peer = default(Peer);
        }

        public NetworkId(uint value, Peer peer)
        {
            m_value = value;
            m_peer = peer;
        }

        public bool IsEmpty => m_value == 0;
        public bool HasPeer => m_peer.IsSet;
        public bool IsPlayer => HasPeer;
        
        public override int GetHashCode()
        {
            return (int) m_value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is NetworkId && Equals((NetworkId) obj);
        }

        public static bool operator ==(NetworkId c1, NetworkId c2)
        {
            return c1.m_value == c2.m_value;
        }

        public static bool operator !=(NetworkId c1, NetworkId c2)
        {
            return c1.m_value != c2.m_value;
        }

        public override string ToString()
        {
            return m_value.ToString();
        }
        
        public static NetworkId Invalid = new NetworkId(uint.MaxValue);
        internal static NetworkId Zero = new NetworkId(0);

        public bool Equals(NetworkId other)
        {
            return m_value == other.m_value;
        }
    }
}