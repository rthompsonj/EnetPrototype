using System.Collections.Generic;
using ENet;
using NetStack.Serialization;
using SoL.Networking.Managers;
using UnityEngine;

namespace SoL.Networking.Replication
{
    public interface IReplicationLayer
    {
        void ServerInitialize(BaseNetworkSystem network, Peer peer, BitBuffer buffer, float updateRate);
        void ClientInitialize();

        void ProcessSyncUpdate(BitBuffer buffer);
        BitBuffer WriteAllSyncData(BitBuffer outBuffer);
        void ReadAllSyncData(BitBuffer inBuffer);

        void UpdateSyncs();
    }
    
    public class ReplicationLayer : IReplicationLayer
    {
        protected readonly List<ISynchronizedVariable> m_syncs = new List<ISynchronizedVariable>();

        protected float m_updateRate = 0.1f;
        protected float m_nextUpdate = 0f;

        private BaseNetworkSystem m_network = null;
        protected Peer m_peer;        
        protected BitBuffer m_buffer = null;

        public void ServerInitialize(BaseNetworkSystem network, Peer peer, BitBuffer buffer, float updateRate)
        {
            m_network = network;
            m_peer = peer;
            m_buffer = buffer;
            m_updateRate = updateRate;
            m_nextUpdate = Time.time + m_updateRate;
            RegisterSyncs();
        }

        public void ClientInitialize()
        {
            RegisterSyncs();
        }
        
        protected virtual int RegisterSyncs()
        {
            return 0;
        }

        /// <summary>
        /// Only called on the server where the peer is present.
        /// </summary>
        public void UpdateSyncs()
        {
            if (CanUpdate() == false)
                return;

            m_nextUpdate += m_updateRate;
            
            int dirtyBits = 0;

            for (int i = 0; i < m_syncs.Count; i++)
            {
                if (m_syncs[i].Dirty)
                {
                    dirtyBits = dirtyBits | m_syncs[i].BitFlag;
                }
            }

            if (dirtyBits == 0)
                return;
           
            m_buffer.AddEntityHeader(m_peer, OpCodes.SyncUpdate);
            m_buffer.AddInt(dirtyBits);

            for (int i = 0; i < m_syncs.Count; i++)
            {
                if (m_syncs[i].Dirty)
                {
                    m_buffer.AddSyncVar(m_syncs[i]);
                }
            }

            var packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
            var command = GameCommandPool.GetGameCommand();
            command.Type = CommandType.BroadcastAll;
            command.Source = m_peer;
            command.Packet = packet;
            command.Channel = 0;

            Debug.Log($"Sending dirtyBits: {dirtyBits}  Length: {packet.Length}");

            m_network.AddCommandToQueue(command);
        }

        public BitBuffer WriteAllSyncData(BitBuffer outBuffer)
        {
            for (int i = 0; i < m_syncs.Count; i++)
            {
                outBuffer.AddSyncVar(m_syncs[i], false);
            }
            return outBuffer;
        }

        public void ReadAllSyncData(BitBuffer inBuffer)
        {
            for (int i = 0; i < m_syncs.Count; i++)
            {
                m_syncs[i].ReadVariable(inBuffer);
            }
        }

        public void ProcessSyncUpdate(BitBuffer inBuffer)
        {
            int dirtyBits = inBuffer.ReadInt();
            
            Debug.Log($"Received dirtyBits: {dirtyBits}");

            if (dirtyBits == 0)
                return;

            for (int i = 0; i < m_syncs.Count; i++)
            {
                if ((dirtyBits & m_syncs[i].BitFlag) == m_syncs[i].BitFlag)
                {
                    m_syncs[i].ReadVariable(inBuffer);
                }
            }
        }

        private bool CanUpdate()
        {
            return Time.time > m_nextUpdate;
        }
    }
}
