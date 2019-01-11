using System;
using ENet;
using Misc;
using NetStack.Serialization;
using SoL.Networking.Objects;
using UnityEngine;
using UnityEngine.Profiling;
using Event = ENet.Event;

namespace SoL.Networking.Managers
{
    public sealed class ServerNetworkSystem : BaseNetworkSystem
    {
        private BitBuffer m_buffer = new BitBuffer(128);

        private readonly PlayerCollection m_peers = new PlayerCollection(true);
        
        #region MONO
        
        protected override void Start()
        {
            base.Start();

            var command = GameCommandPool.GetGameCommand();
            command.Type = CommandType.StartHost;
            command.Port = m_targetPort;
            command.ChannelCount = 100;
            command.PeerLimit = 100;
            command.UpdateTime = 0;          

            m_commandQueue.Enqueue(command);
        }

        protected override void UpdateStates()
        {
            base.UpdateStates();

            if (m_peers.Count <= 0)
                return;

            for (int i = 0; i < m_networkEntities.Count; i++)
            {
                m_networkEntities[i].UpdateState();
            }
        }

        #endregion
        
        #region OVERRIDES

        public override void RegisterEntity(NetworkEntity entity)
        {
            base.RegisterEntity(entity);
            if (entity.NetworkId.HasPeer)
            {
                m_peers.Add(entity.NetworkId.Value, entity);
            }
        }

        public override void DeregisterEntity(NetworkEntity entity)
        {
            base.DeregisterEntity(entity);
            m_peers.Remove(entity.NetworkId.Value);
        }
        
        protected override void Func_StartHost(Host host, GameCommand command)
        {
            Debug.Log("STARTING SERVER FROM NETWORK THREAD");
            try
            {
                Address addy = new Address
                {
                    Port = command.Port
                };
        
                host.Create(addy, command.PeerLimit, command.ChannelCount);
                Debug.Log($"Server started on port: {command.Port} for {command.PeerLimit} users with {command.ChannelCount} channels.");
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }   
        }

        protected override void Func_StopHost(Host host, GameCommand command)
        {
            Debug.Log("STOPPING SERVER FROM NETWORK THREAD");
            for (int i = 0; i < m_peers.Count; i++)
            {
                if (m_peers[i].NetworkId.Peer.IsSet)
                {
                    m_peers[i].NetworkId.Peer.Disconnect(0);
                }
            }
            TerminateThreads();
        }
        
        protected override void Func_Send(Host host, GameCommand command)
        {
            if (command.Target.IsSet)
            {
                command.Target.Send(command.Channel, ref command.Packet);   
            }
        }
        
        protected override void Func_BroadcastAll(Host host, GameCommand command)
        {
            host.Broadcast(command.Channel, ref command.Packet);
        }
        
        protected override void Func_BroadcastOthers(Host host, GameCommand command)
        {
            for (int i = 0; i < m_peers.Count; i++)
            {
                if (m_peers[i].NetworkId.Peer.ID != command.Source.ID && m_peers[i].NetworkId.Peer.IsSet)
                {                    
                    m_peers[i].NetworkId.Peer.Send(command.Channel, ref command.Packet);
                }
            }
        }
        
        protected override void Func_BroadcastGroup(Host host, GameCommand command)
        {
            //TODO: stop-gap solution until dlls are updated with recent ENET changes.
            /*
            for (int i = 0; i < command.TargetGroup.Length; i++)
            {
                if (command.TargetGroup[i].IsSet)
                {
                    command.TargetGroup[i].Send(command.Channel, ref command.Packet);
                }
            }
            */
            host.Broadcast(command.Channel, ref command.Packet, ref command.TargetGroup);
            PeerArrayPool.ReturnArray(command.TargetGroup);
        }

        protected override void Connect(Event netEvent)
        {
            Debug.Log($"Client connected - ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}");            
            m_buffer.AddEntityHeader(netEvent.Peer, OpCodes.ConnectionEvent);
            m_buffer.AddUInt((uint)OpCodes.Ok);
            var packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
            var command = GameCommandPool.GetGameCommand();
            command.Type = CommandType.Send;
            command.Packet = packet;
            command.Channel = 0;
            command.Target = netEvent.Peer;
            m_commandQueue.Enqueue(command);
            //SpawnRemotePlayer(netEvent.Peer);
        }

        protected override void Disconnect(Event netEvent)
        {
            Debug.Log($"Client disconnected - ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}  Reason: {netEvent.Data}");
            Peer peer = netEvent.Peer;
            NetworkEntity netEntity = m_peers.GetNetworkEntityForPeerId(netEvent.Peer.ID);

            if (netEntity != null)
            {                    
                Destroy(netEntity.gameObject);

                // notify everyone else
                if (m_peers.Count > 0)
                {
                    m_buffer.AddEntityHeader(netEntity, OpCodes.Destroy);
                    var packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
                    var command = GameCommandPool.GetGameCommand();
                    command.Type = CommandType.BroadcastAll;
                    command.Packet = packet;
                    command.Channel = 0;
                    m_commandQueue.Enqueue(command);                    
                }
            }
        }

        protected override void ProcessPacket(Event netEvent)
        {
            Profiler.BeginSample("Process Packet");
            //Debug.Log($"Packet received from - ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}, Channel ID: {netEvent.ChannelID}, Data Length: {netEvent.Packet.Length}");
            
            m_buffer = netEvent.Packet.GetBufferFromPacket(m_buffer);
            var buffer = m_buffer;
            var header = buffer.GetEntityHeader();
            var op = header.OpCode;
            var id = header.Id;

            /*  this will not validate if we are sending NetworkId values.
            if (netEvent.Peer.ID != id)
            {
                Debug.LogError($"ID Mismatch! {netEvent.Peer.ID} vs. {id}");
                Profiler.EndSample();
                return;
            }
            */

            switch (op)
            {
                case OpCodes.Spawn:
                    var spawnType = (Misc.SpawnType)m_buffer.ReadInt();
                    SpawnRemoteEntity(netEvent.Peer, spawnType);
                    break;
                
                case OpCodes.StateUpdate:
                    Profiler.BeginSample("Process Packet - Position Update");

                    NetworkEntity netEntity = null;
                    if (m_networkEntities.TryGetValue(id, out netEntity))
                    {
                        netEntity.ProcessPacket(op, buffer);
                    }

                    Profiler.EndSample();
                    break;
                
                default:
                    netEvent.Packet.Dispose();
                    break;
            }
            Profiler.EndSample();
        }
        
        #endregion

        private void SpawnRemoteEntity(Peer peer, Misc.SpawnType spawnType)
        {
            var go = m_params.InstantiateSpawn(spawnType);
            NetworkEntity netEntity = go.GetComponent<NetworkEntity>();
            netEntity.SpawnType = spawnType;
            netEntity.ServerInit(this, peer);

            m_buffer.AddEntityHeader(netEntity, OpCodes.Spawn);
            m_buffer.AddInt((int)spawnType);
            m_buffer.AddInitialState(netEntity);
            Packet spawnPlayerPacket = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);

            var spawnPlayerCommand = GameCommandPool.GetGameCommand();
            spawnPlayerCommand.Type = CommandType.Send;
            spawnPlayerCommand.Target = peer;
            spawnPlayerCommand.Channel = 0;
            spawnPlayerCommand.Packet = spawnPlayerPacket;

            m_commandQueue.Enqueue(spawnPlayerCommand);

            var spawnPlayerForOthersCommand = GameCommandPool.GetGameCommand();
            spawnPlayerForOthersCommand.Type = CommandType.BroadcastOthers;
            spawnPlayerForOthersCommand.Source = peer;
            spawnPlayerForOthersCommand.Channel = 1;
            spawnPlayerForOthersCommand.Packet = spawnPlayerPacket;

            m_commandQueue.Enqueue(spawnPlayerForOthersCommand);
            
            SpawnOthersForRemoteEntity(netEntity);
        }

        private void SpawnOthersForRemoteEntity(NetworkEntity netEntity)
        {
            var cnt = 0;
            for (int i = 0; i < m_peers.Count; i++)
            {
                // do not send to ourselves
                if (m_peers[i].NetworkId == netEntity.NetworkId)
                    continue;

                // do not send NPCs
                if (m_peers[i].NetworkId.HasPeer == false)
                    continue;

                cnt += 1;
            }
            
            // one large packet
            m_buffer.AddEntityHeader(netEntity, OpCodes.BulkSpawn);
            m_buffer.AddInt(cnt);
            for (int i = 0; i < m_peers.Count; i++)
            {
                // do not send to ourselves
                if (m_peers[i].NetworkId == netEntity.NetworkId)
                    continue;

                // do not send NPCs
                if (m_peers[i].NetworkId.HasPeer == false)
                    continue;

                m_buffer.AddEntityHeader(m_peers[i], OpCodes.Spawn, false);                
                m_buffer.AddInt((int)m_peers[i].SpawnType);
                m_buffer.AddInitialState(m_peers[i]);
            }
            var spawnOthersPacket = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
            var spawnOthersCommand = GameCommandPool.GetGameCommand();
            spawnOthersCommand.Type = CommandType.Send;
            spawnOthersCommand.Packet = spawnOthersPacket;
            spawnOthersCommand.Channel = 1;
            spawnOthersCommand.Target = netEntity.NetworkId.Peer;
            
            m_commandQueue.Enqueue(spawnOthersCommand);
            
            Debug.Log($"Sent SpawnOthersPacket of size {spawnOthersPacket.Length.ToString()}");
        }
    }
}