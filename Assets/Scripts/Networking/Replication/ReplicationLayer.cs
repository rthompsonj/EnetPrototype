using System.Collections.Generic;
using ENet;
using NetStack.Serialization;
using SoL.Networking.Managers;
using SoL.Networking.Objects;
using UnityEngine;

namespace SoL.Networking.Replication
{
    public interface IReplicationLayer
    {
        void ServerInit(INetworkManager network, NetworkEntity netEntity, BitBuffer buffer, float updateRate);
        void ClientInit();

        void ProcessSyncUpdate(BitBuffer buffer);
        BitBuffer WriteAllSyncData(BitBuffer outBuffer);
        void ReadAllSyncData(BitBuffer inBuffer);
    }
    
    public class ReplicationLayer : MonoBehaviour, IReplicationLayer
    {
        protected readonly List<ISynchronizedVariable> m_syncs = new List<ISynchronizedVariable>();

        private float m_updateRate = 0.1f;
        private float m_nextUpdate = 0f;

        private INetworkManager m_network = null;
        private NetworkEntity m_networkEntity = null;
        private BitBuffer m_buffer = null;
        private bool m_isServer = false;
        
        #region MONO

        void Update()
        {
            if (m_isServer == false || Time.time < m_nextUpdate ||
                (m_networkEntity.UseProximity && m_networkEntity.NObservers <= 0))
            {
                return;   
            }

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
           
            m_buffer.AddEntityHeader(m_networkEntity, OpCodes.SyncUpdate);
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
            command.Packet = packet;
            command.Channel = 0;
            command.Source = m_networkEntity.NetworkId.Peer;
            
            if (m_networkEntity.UseProximity)
            {
                command.Type = CommandType.BroadcastGroup;
                command.TargetGroup = m_networkEntity.GetObservingPeers(false);
            }
            else
            {
                command.Type = CommandType.BroadcastOthers;
            }

            Debug.Log($"Sending dirtyBits: {dirtyBits}  Length: {packet.Length}");

            m_network.AddCommandToQueue(command);
        }
        
        #endregion
        
        #region INIT

        public void ServerInit(INetworkManager network, NetworkEntity netEntity, BitBuffer buffer, float updateRate)
        {
            m_isServer = true;
            m_network = network;
            m_networkEntity = netEntity;
            m_buffer = buffer;
            m_updateRate = updateRate;
            m_nextUpdate = Time.time + m_updateRate;
            RegisterSyncs();
        }

        public void ClientInit()
        {
            RegisterSyncs();
        }
        
        protected virtual int RegisterSyncs()
        {
            return 0;
        }
        
        #endregion
        
        #region READ_WRITE

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
        
        #endregion

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
    }
}
