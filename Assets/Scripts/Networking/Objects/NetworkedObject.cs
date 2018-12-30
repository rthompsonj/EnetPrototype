using ENet;
using NetStack.Serialization;
using SoL.Networking.Managers;
using SoL.Networking.Replication;
using UnityEngine;

namespace SoL.Networking.Objects
{
    public abstract class NetworkedObject : MonoBehaviour
    {
        //TODO: uint index for any networked object.  this includes ones that are NOT peers.
        
        #region ABSTRACT_VIRTUAL        
        
        protected abstract IReplicationLayer m_replicationLayer { get; }
        protected virtual void Subscribe() { }
        protected virtual void Unsubscribe() { }
        protected virtual void AddLayers() { }
        protected virtual void ProcessPacketInternal(OpCodes op, BitBuffer buffer) { }
        protected virtual void OnStartServer() { }
        protected virtual void OnStartClient() { }
        protected virtual void OnStartLocalClient() { }
        
        #endregion
        
        #region PROPERTIES
        
        public Peer Peer { get; private set; }
        public uint ID { get; private set; }
        
        #endregion
        
        #region VARS
        
        [SerializeField] protected float m_updateRate = 0.1f;
        protected float m_nextUpdate = 0f;
        
        protected readonly BitBuffer m_buffer = new BitBuffer(128);
        protected BaseNetworkSystem m_network = null;
        
        protected bool m_isServer = false;
        protected bool m_isClient = false;
        protected bool m_isLocal = false;
        
        #endregion
        
        #region MONO
        
        protected virtual void Awake()
        {
            AddLayers();
            Subscribe();
        }

        protected virtual void Update()
        {
            if (m_isServer)
            {
                m_replicationLayer?.UpdateSyncs();
            }
        }

        protected virtual void OnDestroy()
        {
            Unsubscribe();
        }
        
        #endregion
        
        #region INIT

        public void ServerInitialize(BaseNetworkSystem network, Peer localPeer)
        {
            m_network = network;
            Peer = localPeer;
            ID = localPeer.ID;
            m_isServer = true;
            m_replicationLayer?.ServerInitialize(network, localPeer, m_buffer, m_updateRate);            
            OnStartServer();
        }

        public void ClientInitialize(BaseNetworkSystem network, uint localId, BitBuffer initBuffer)
        {
            m_network = network;
            ID = localId;
            m_isClient = true;
            m_isLocal = network.Peer.IsSet && network.Peer.ID == localId;
            m_replicationLayer?.ClientInitialize();
            ReadInitialState(initBuffer);            
            OnStartClient();
            if (m_isLocal)
            {
                OnStartLocalClient();
            }
        }

        public virtual BitBuffer AddInitialState(BitBuffer outBuffer)
        {            
            m_replicationLayer?.WriteAllSyncData(outBuffer);
            return outBuffer;
        }

        protected virtual void ReadInitialState(BitBuffer initBuffer)
        {
            m_replicationLayer?.ReadAllSyncData(initBuffer);
        }
        
        #endregion

        public void ProcessPacket(OpCodes op, BitBuffer inBuffer)
        {
            switch (op)
            {
                case OpCodes.SyncUpdate:
                    m_replicationLayer?.ProcessSyncUpdate(inBuffer);
                    break;
                
                default:
                    ProcessPacketInternal(op, inBuffer);
                    break;
            }
        }
        
        protected virtual bool CanUpdate()
        {
            var peer = m_isLocal ? ClientNetworkSystem.MyPeer : Peer;
            return peer.IsSet && Time.time > m_nextUpdate;
        }
    }
}