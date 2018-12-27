using System;
using ENet;
using NetStack.Serialization;
using NextSimple;
using UnityEngine;
using Event = ENet.Event;

namespace Threaded
{
    public class ServerNetworkSystem : BaseNetworkSystem
    {
        private BitBuffer m_buffer = new BitBuffer(128);
        
        #region MONO
        
        protected override void Start()
        {
            base.Start();

            var command = new GameCommand
            {
                Type = GameCommand.CommandType.StartHost,
                Port = 9900,
                ChannelCount = 100,
                PeerLimit = 100,
                UpdateTime = 0
            };

            m_commandQueue.Enqueue(command);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            var command = new GameCommand
            {
                Type = GameCommand.CommandType.StopHost
            };
            m_commandQueue.Enqueue(command);
        }
        
        #endregion
        
        #region OVERRIDES
        
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
            for (int i = 0; i < m_entities.Count; i++)
            {
                if (m_entities[i].Peer.IsSet)
                {
                    m_entities[i].Peer.Disconnect(0);
                }
            }
            host.Flush();
            host.Dispose();
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
            for (int i = 0; i < m_entities.Count; i++)
            {
                if (m_entities[i].Peer.ID != command.Source.ID && m_entities[i].Peer.IsSet)
                {                    
                    m_entities[i].Peer.Send(command.Channel, ref command.Packet);
                }
            }
        }

        protected override void Connect(Event netEvent)
        {
            Debug.Log($"Client connected - ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}");
            SpawnRemotePlayer(netEvent.Peer);
        }

        protected override void Disconnect(Event netEvent)
        {
            Debug.Log($"Client disconnected - ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}  Reason: {netEvent.Data}");
            Peer peer = netEvent.Peer;
            BaseEntity entity = null;

            if (m_entityDict.TryGetValue(peer.ID, out entity))
            {
                uint id = peer.ID;

                m_entityDict.Remove(peer.ID);
                m_entities.Remove(entity);
                    
                Destroy(entity.gameObject);

                // notify everyone else
                if (m_entities.Count > 0)
                {
                    m_buffer.AddEntityHeader(peer, OpCodes.Destroy);
                    var packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
                    var command = new GameCommand
                    {
                        Type = GameCommand.CommandType.BroadcastAll,
                        Channel = 0,
                        Packet = packet
                    };
                    m_commandQueue.Enqueue(command);                    
                }
            }
        }

        protected override void ProcessPacket(Event netEvent)
        {
            //Debug.Log($"Packet received from - ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}, Channel ID: {netEvent.ChannelID}, Data Length: {netEvent.Packet.Length}");

            m_buffer = netEvent.Packet.GetBufferFromPacket(m_buffer);
            var buffer = m_buffer;
            var header = buffer.GetEntityHeader();
            var op = header.OpCode;
            var id = header.ID;

            if (netEvent.Peer.ID != id)
            {
                Debug.LogError($"ID Mismatch! {netEvent.Peer.ID} vs. {id}");
                return;
            }

            switch (op)
            {
                case OpCodes.PositionUpdate:
                    
                    var command = new GameCommand
                    {
                        Type = GameCommand.CommandType.BroadcastOthers,
                        Source = netEvent.Peer,
                        Channel = 1,
                        Packet = netEvent.Packet
                    };

                    m_commandQueue.Enqueue(command);

                    BaseEntity entity = null;
                    if (m_entityDict.TryGetValue(netEvent.Peer.ID, out entity))
                    {
                        var pos = buffer.ReadVector3(SharedStuff.Instance.Range);
                        var h = buffer.ReadFloat();
                        entity.gameObject.transform.position = pos;
                        entity.Renderer.gameObject.transform.rotation = Quaternion.Euler(new Vector3(0f, h, 0f));
                    }
                    break;
                
                default:
                    netEvent.Packet.Dispose();
                    break;
            }
        }
        
        #endregion
        
        
        private void SpawnRemotePlayer(Peer peer)
        {
            BaseEntity entity = SharedStuff.Instance.SpawnPlayer();
            entity.Initialize(peer, peer.ID);

            m_buffer.AddEntityHeader(peer, OpCodes.Spawn);
            m_buffer.AddVector3(entity.gameObject.transform.position, SharedStuff.Instance.Range);
            m_buffer.AddFloat(entity.gameObject.transform.eulerAngles.y);
            Packet packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);

            var command = new GameCommand
            {
                Type = GameCommand.CommandType.Send,
                Target = peer,
                Channel = 0,
                Packet = packet
            };

            m_commandQueue.Enqueue(command);

            command = new GameCommand
            {
                Type = GameCommand.CommandType.BroadcastOthers,
                Source = peer,
                Channel = 1,
                Packet = packet
            };

            m_commandQueue.Enqueue(command);
            
            // must send all of the old data
            for (int i = 0; i < m_entities.Count; i++)
            {
                if (m_entities[i].Peer.ID == peer.ID)
                    continue;
                
                m_buffer.AddEntityHeader(m_entities[i].Peer, OpCodes.Spawn);
                m_buffer.AddVector3(m_entities[i].gameObject.transform.position, SharedStuff.Instance.Range);
                m_buffer.AddFloat(m_entities[i].gameObject.transform.eulerAngles.y);
                var othersPacket = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);

                command = new GameCommand
                {
                    Type = GameCommand.CommandType.Send,
                    Packet = othersPacket,
                    Channel = 1,
                    Target = peer
                };
                
                m_commandQueue.Enqueue(command);
            }

            m_entityDict.Add(peer.ID, entity);
            m_entities.Add(entity);
        }
    }
}