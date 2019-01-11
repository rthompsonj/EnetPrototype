using System.Collections.Generic;
using SoL.Networking.Objects;

namespace Misc
{
    public class PlayerCollection : ListDictCollection<uint, NetworkEntity>
    {
        private readonly Dictionary<uint, uint> m_peerIdToNetworkId = null;

        public PlayerCollection(bool replace = false) : base(replace)
        {
            m_peerIdToNetworkId = new Dictionary<uint, uint>();
        }

        public NetworkEntity GetNetworkEntityForPeerId(uint peerId)
        {
            uint networkId;
            NetworkEntity netEntity = null;
            if (m_peerIdToNetworkId.TryGetValue(peerId, out networkId) && 
                TryGetValue(networkId, out netEntity))
            {
                return netEntity;
            }
            return null;
        }
        
        #region ADD_REMOVE

        public override void Add(uint key, NetworkEntity value)
        {
            base.Add(key, value);
            if (m_replaceWhenPresent)
            {
                m_peerIdToNetworkId.Remove(value.NetworkId.Peer.ID);
            }
            m_peerIdToNetworkId.Add(value.NetworkId.Peer.ID, value.NetworkId.Value);
        }

        // key = NetworkId.Value
        public override bool Remove(uint key)
        {
            bool removed = false;
            NetworkEntity netEntity;
            
            if (TryGetValue(key, out netEntity))
            {
                var peerId = netEntity.NetworkId.Peer.ID;
                removed = m_peerIdToNetworkId.Remove(peerId);
            }

            return removed && base.Remove(key);
        }
        
        #endregion 
    }
}