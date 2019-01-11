using System;
using ENet;
using NetStack.Serialization;
using SoL.Networking.Objects;
using UnityEngine;
using Event = ENet.Event;

namespace SoL.Networking.Managers
{
    public sealed class ClientNetworkSystem : BaseNetworkSystem
    {        
        private BitBuffer m_buffer = new BitBuffer(128);

        public static Peer MyPeer;
        
        #region MONO
        
        protected override void Start()
        {
            base.Start();
            var command = GameCommandPool.GetGameCommand();
            command.Type = CommandType.StartHost;
            command.Host = m_targetHost;
            command.Port = m_targetPort;
            command.UpdateTime = 0;
            command.ChannelCount = 100;
            m_commandQueue.Enqueue(command);
        }
        
        #endregion
        
        #region OVERRIDES

        protected override void Func_StartHost(Host host, GameCommand command)
        {
            Debug.Log("STARTING CLIENT FROM NETWORK THREAD");
            try
            {
                Address addy = new Address
                {
                    Port = command.Port
                };
                addy.SetHost(command.Host);
        
                host.Create();
                m_peer = host.Connect(addy, command.ChannelCount);
                MyPeer = m_peer;
                Debug.Log($"Client started on port: {command.Port} with {command.ChannelCount} channels.");
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }   
        }

        protected override void Func_StopHost(Host host, GameCommand command)
        {
            Debug.Log("STOPPING CLIENT FROM NETWORK THREAD");
            if (Peer.IsSet && Peer.State == PeerState.Connected)
            {
                Peer.Disconnect((uint)OpCodes.Destroy);
            }
            TerminateThreads();
        }
        
        protected override void Func_Send(Host host, GameCommand command)
        {
            if (Peer.IsSet)
            {
                Peer.Send(command.Channel, ref command.Packet);
            }
        }
        
        protected override void Func_BroadcastAll(Host host, GameCommand command)
        {

        }
        
        protected override void Func_BroadcastOthers(Host host, GameCommand command)
        {

        }
        
        protected override void Func_BroadcastGroup(Host host, GameCommand command)
        {

        }

        protected override void Connect(Event netEvent)
        {
            //throw new NotImplementedException();
        }

        protected override void Disconnect(Event netEvent)
        {
            Debug.Log("Disconnect detected!");
            if (Application.isEditor == false)
            {
                Application.Quit();
            }
        }

        protected override void ProcessPacket(Event netEvent)
        {
            byte channel = netEvent.ChannelID;
            m_buffer = netEvent.Packet.GetBufferFromPacket(m_buffer);
            var buffer = m_buffer;
            var header = m_buffer.GetEntityHeader();
            var op = header.OpCode;
            var id = header.Id;

            NetworkEntity netEntity = null;

            switch (op)
            {
                case OpCodes.ConnectionEvent:
                    var result = (OpCodes)m_buffer.ReadUInt();
                    switch (result)
                    {
                        case OpCodes.Ok:
                            m_buffer.AddEntityHeader(m_peer, OpCodes.Spawn);
                            m_buffer.AddInt((int)m_type);
                            var packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
                            var command = GameCommandPool.GetGameCommand();
                            command.Type = CommandType.Send;
                            command.Packet = packet;
                            command.Channel = 0;
                            m_commandQueue.Enqueue(command);
                            break;
                    }
                    break;
                
                case OpCodes.Spawn:
                    var spawnType = (Misc.SpawnType)m_buffer.ReadInt();
                    SpawnEntity(id, buffer, spawnType, netEvent.ChannelID);
                    break;
                
                case OpCodes.BulkSpawn:
                    var cnt = m_buffer.ReadInt();
                    for (int i = 0; i < cnt; i++)
                    {
                        header = m_buffer.GetEntityHeader();
                        var bulkSpawnType = (Misc.SpawnType) m_buffer.ReadInt();
                        SpawnEntity(header.Id, buffer, bulkSpawnType, netEvent.ChannelID);
                    }
                    break;
		        
                case OpCodes.Destroy:
                    if (m_networkEntities.TryGetValue(id, out netEntity) && netEntity.NetworkId.Value == id)
                    {
                        Destroy(netEntity.gameObject);
                    }
                    break;
		        
                case OpCodes.StateUpdate:
                case OpCodes.SyncUpdate:
                    if (m_networkEntities.TryGetValue(id, out netEntity))
                    {
                        netEntity.ProcessPacket(op, buffer);
                    }
                    else
                    {
                        Debug.LogWarning($"No entity found for Id: {id}!");
                    }
                    break;
            }
        }

        private NetworkEntity SpawnEntity(uint id, BitBuffer inBuffer, Misc.SpawnType spawnType, byte channel)
        {
            var go = m_params.InstantiateSpawn(spawnType);
            var netEntity = go.GetComponent<NetworkEntity>();
            if (netEntity != null)
            {
                netEntity.ClientInit(this, id, inBuffer, channel);   
            }
            return netEntity;
        }
        
        #endregion
    }
}