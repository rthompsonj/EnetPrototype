using System;
using System.Collections.Generic;
using ENet;
using Misc;
using NetStack.Serialization;
using SoL.Networking.Managers;
using SoL.Networking.Proximity;
using SoL.Networking.Replication;
using UnityEngine;

namespace SoL.Networking.Objects
{
    public abstract class NetworkEntity : MonoBehaviour
    {
        public NetworkId NetworkId { get; private set; }
        
        //TODO: temp       
        public SpawnType SpawnType { get; set; }
        //TODO: temp

        protected BitBuffer m_buffer = null;
        protected INetworkManager m_network = null;
        protected IReplicationLayer m_replicationLayer = null; 
        
        protected bool m_isServer = false;
        protected bool m_isClient = false;
        protected bool m_isLocal = false;
        
        protected virtual void OnStartAll() { }
        protected virtual void OnStartServer() { }
        protected virtual void OnStartClient() { }
        protected virtual void OnStartLocalClient() { }
        
        protected virtual void Subscribe() { }
        protected virtual void Unsubscribe() { }

        [SerializeField] private GameObject m_proximityPrefab = null;
        [SerializeField] protected float m_updateRate = 0.1f;        
        protected float m_nextUpdate = 0f;        

        private ProximitySensor[] m_proximitySensors = null;
        private Dictionary<NetworkId, Observer> m_observers = null;

        public int NObservers => m_observers?.Count ?? 0;
        public bool UseProximity => m_proximitySensors != null && m_proximitySensors.Length > 0;
        
        #region MONO
        
        protected virtual void OnDestroy()
        {
            Unsubscribe();
            DeregisterEntity();
            SendBulkDestroy();
        }
        
        #endregion
        
        #region INIT
        
        public void ServerInit(INetworkManager network, Peer peer)
        {
            NetworkId = BaseNetworkSystem.GetNetworkId(peer);
            m_network = network;
            m_buffer = new BitBuffer(128);
            m_isServer = true;
            InitReplicationLayer();
            Subscribe();
            InitProximity();
            RegisterEntity();
            OnStartServer();
        }

        public void ClientInit(INetworkManager network, uint networkId, BitBuffer inBuffer, byte channel)
        {
            NetworkId = new NetworkId(networkId);
            m_network = network;
            
            m_isClient = true;
            m_isLocal = channel == 0;            
            
            InitReplicationLayer();

            Subscribe();

            ReadInitialState(inBuffer);
            
            RegisterEntity();

            OnStartClient();
            
            if (m_isLocal)
            {
                m_buffer = new BitBuffer(128);
                OnStartLocalClient();
            }            
        }

        protected virtual void InitReplicationLayer()
        {
            m_replicationLayer = gameObject.GetComponent<IReplicationLayer>();
            
            if (m_isServer)
            {
                m_replicationLayer?.ServerInit(m_network, this, m_buffer, m_updateRate);
            }
            else
            {
                m_replicationLayer?.ClientInit();
            }
        }
        
        private void RegisterEntity()
        {
            BaseNetworkSystem.Instance.RegisterEntity(this);
        }

        private void DeregisterEntity()
        {
            BaseNetworkSystem.Instance.DeregisterEntity(this);            
        }
        
        #endregion

        #region NETWORK
        
        public void ProcessPacket(OpCodes op, BitBuffer inBuffer)
        {
            switch (op)
            {
                case OpCodes.SyncUpdate:
                    m_replicationLayer?.ProcessSyncUpdate(inBuffer);
                    break;
                
                case OpCodes.StateUpdate:
                    ReadStateUpdate(inBuffer);
                    break;                    
                
                default:
                    ProcessPacketInternal(op, inBuffer);
                    break;                
            }
        }
        
        protected virtual void ProcessPacketInternal(OpCodes op, BitBuffer buffer) { }

        // SERVER ONLY
        public void UpdateState()
        {
            if (HasStateUpdate() == false)
                return;
            
            m_buffer.AddEntityHeader(this, OpCodes.StateUpdate);
            
            AddStateUpdate(m_buffer);
            
            var packet = m_buffer.GetPacketFromBuffer(PacketFlags.None);
            var command = GameCommandPool.GetGameCommand();
            command.Packet = packet;
            command.Channel = 2;
            command.Source = NetworkId.Peer;

            if (UseProximity)
            {
                command.Type = CommandType.BroadcastGroup;
                command.TargetGroup = GetObservingPeers();
            }
            else
            {
                command.Type = CommandType.BroadcastOthers;
            }

            m_network.AddCommandToQueue(command);
        }

        protected virtual void ReadStateUpdate(BitBuffer inBuffer)
        {
            
        }

        protected virtual bool HasStateUpdate()
        {
            return false;
        }

        protected virtual BitBuffer AddStateUpdate(BitBuffer outBuffer)
        {
            return outBuffer;            
        }        

        public virtual BitBuffer AddInitialState(BitBuffer outBuffer)
        {
            m_replicationLayer?.WriteAllSyncData(outBuffer);
            return outBuffer;
        }

        protected virtual BitBuffer ReadInitialState(BitBuffer inBuffer)
        {
            m_replicationLayer?.ReadAllSyncData(inBuffer);
            return inBuffer;
        }        

        public Peer[] GetObservingPeers(bool considerProximityBands = true)
        {
            int cnt = 0;
            var peerGroup = PeerArrayPool.GetArray(m_observers.Count);

            if (considerProximityBands == false)
            {
                foreach (var kvp in m_observers)
                {
                    if (kvp.Value.NetworkEntity != null && kvp.Value.NetworkEntity.NetworkId.HasPeer)
                    {
                        peerGroup[cnt] = kvp.Value.NetworkEntity.NetworkId.Peer;
                        cnt += 1;
                    }
                }

                return peerGroup;
            }


            for (int i = 0; i < m_proximitySensors.Length; i++)
            {
                m_proximitySensors[i].SetUpdateFlag();
            }   
            
            foreach (var kvp in m_observers)
            {
                foreach (var sensor in m_proximitySensors)
                {                    
                    if (sensor.CanUpdate && kvp.Value.Band.HasFlag(sensor.SensorBand) && 
                        kvp.Value.NetworkEntity != null && kvp.Value.NetworkEntity.NetworkId.HasPeer)
                    {
                        peerGroup[cnt] = kvp.Value.NetworkEntity.NetworkId.Peer;
                        cnt += 1;
                        break;
                    }
                }
            }

            return peerGroup;
        }
        
        #endregion

        private void SendBulkDestroy()
        {
            if (m_isServer)
            {
                m_buffer.AddEntityHeader(this, OpCodes.Destroy);
                var packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
                var command = GameCommandPool.GetGameCommand();
                command.Type = CommandType.BroadcastAll;
                command.Channel = 1;
                command.Packet = packet;
                m_network.AddCommandToQueue(command);
            }            
        }
        
        #region PROXIMITY
        
        struct Observer
        {
            public SensorBand Band;
            public NetworkEntity NetworkEntity;
        }
        
        private void InitProximity()
        {
            if (m_proximityPrefab == null)
                return;
            
            var go = Instantiate(m_proximityPrefab, gameObject.transform);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_proximitySensors = go.GetComponentsInChildren<ProximitySensor>();

            for (int i = 0; i < m_proximitySensors.Length; i++)
            {
                m_proximitySensors[i].NetworkEntity = this;
            }
            
            if (m_proximitySensors.Length > 0)
            {                
                m_observers = new Dictionary<NetworkId, Observer>();
                Array.Sort(m_proximitySensors, new ProximitySensorComparer());   
            }
        }

        public void ProximitySensorEnter(ProximitySensor sensor, NetworkEntity netEntity)
        {
            if (m_isServer == false)
                return;
            
            Observer observer;
            if (m_observers.TryGetValue(netEntity.NetworkId, out observer))
            {
                var newBand = observer.Band.SetFlag(sensor.SensorBand);
                m_observers[netEntity.NetworkId] = new Observer
                {
                    Band = newBand,
                    NetworkEntity = netEntity
                };
            }
            else
            {
                observer = new Observer
                {
                    Band = sensor.SensorBand,
                    NetworkEntity = netEntity
                };
                m_observers.Add(netEntity.NetworkId, observer);

                // SEND SPAWN PACKET
                m_buffer.AddEntityHeader(this, OpCodes.Spawn);
                m_buffer.AddInt((int)SpawnType);
                m_buffer.AddInitialState(this);
                var packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
                var command = GameCommandPool.GetGameCommand();
                command.Type = CommandType.Send;
                command.Target = netEntity.NetworkId.Peer;
                command.Channel = 1;
                command.Packet = packet;
                m_network.AddCommandToQueue(command);
            }
        }

        public void ProximitySensorExit(ProximitySensor sensor, NetworkEntity netEntity)
        {
            if (m_isServer == false)
                return;
            
            Observer observer;
            if (m_observers.TryGetValue(netEntity.NetworkId, out observer))
            {
                var newBand = observer.Band.UnsetFlag(sensor.SensorBand);
                if (newBand == SensorBand.None)
                {
                    m_observers.Remove(netEntity.NetworkId);

                    // SEND DESTROY PACKET
                    m_buffer.AddEntityHeader(this, OpCodes.Destroy);
                    var packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
                    var command = GameCommandPool.GetGameCommand();
                    command.Type = CommandType.Send;
                    command.Target = netEntity.NetworkId.Peer;
                    command.Channel = 1;
                    command.Packet = packet;
                    m_network.AddCommandToQueue(command);
                }
                else
                {
                    m_observers[netEntity.NetworkId] = new Observer
                    {
                        Band = newBand,
                        NetworkEntity = netEntity
                    };
                }
            }
        }
        
        #endregion
    }
}