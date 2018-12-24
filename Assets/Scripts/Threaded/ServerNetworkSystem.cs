using System;
using System.Threading;
using DisruptorUnity3d;
using ENet;
using NetStack.Compression;
using NetStack.Serialization;
using NextSimple;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace Threaded
{
    public class ServerNetworkSystem : BaseNetworkSystem
    {
        protected override void Start()
        {
            base.Start();

            var command = new GameCommand
            {
                Type = GameCommand.CommandType.Start,
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
                Type = GameCommand.CommandType.Stop
            };
            m_commandQueue.Enqueue(command);
        }

        protected override Thread LogicThread()
        {
            return new Thread(() =>
            {
                while (true)
                {
                    // --> to network thread
                    while (m_commandQueue.TryDequeue(out GameCommand command))
                    {
                        m_functionQueue.Enqueue(command);
                    }

                    // --> to game thread
                    while (m_transportEventQueue.TryDequeue(out Event netEvent))
                    {
                        switch (netEvent.Type)
                        {
                            case EventType.None:
                                break;
                            
                            default:
                                m_logicEventQueue.Enqueue(netEvent);
                                break;
                        }
                    }
                }
            });
        }

        protected override Thread NetworkThread()
        {
            return new Thread(() =>
            {
                int updateTime = 0;
                
                using (Host server = new Host())
                {
                    while (true)
                    {
                        while (m_functionQueue.TryDequeue(out GameCommand command))
                        {
                            switch (command.Type)
                            {
                                case GameCommand.CommandType.Start:
                                    Debug.Log("STARTING SERVER FROM NETWORK THREAD");
                                    try
                                    {
                                        Address addy = new Address
                                        {
                                            Port = command.Port
                                        };
                                        updateTime = command.UpdateTime;
                                
                                        server.Create(addy, command.PeerLimit, command.ChannelCount);
                                        Debug.Log($"Server started on port: {command.Port} for {command.PeerLimit} users with {command.ChannelCount} channels.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogError(ex);
                                    }                                    
                                    break;
                                
                                case GameCommand.CommandType.Stop:
                                    Debug.Log("STOPPING SERVER FROM NETWORK THREAD");
                                    server.Flush();
                                    server.Dispose();
                                    break;
                                
                                case GameCommand.CommandType.Send:
                                    if (command.Peer.IsSet)
                                    {
                                        Debug.Log($"SENDING PACKET TO {command.Peer.ID} with packet length {command.Packet.Length} and IsSet: {command.Packet.IsSet}");
                                        command.Peer.Send(command.Channel, ref command.Packet);   
                                    }
                                    break;
                                
                                case GameCommand.CommandType.Broadcast:
                                    server.Broadcast(command.Channel, ref command.Packet);
                                    break;
                            }

                            if (command.Packet.IsSet)
                            {
                                command.Packet.Dispose();
                            }
                        }

                        if (server.IsSet)
                        {
                            ENet.Event netEvent;
                            server.Service(updateTime, out netEvent);
                            if (netEvent.Type != EventType.None)
                            {
                                // --> to logic thread
                                m_transportEventQueue.Enqueue(netEvent);
                            }
                        }
                    }
                }
            });
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
            
            for (int i = 0; i < m_entities.Count; i++)
            {
                if (m_entities[i].Id == peer.ID)
                {
                    byte[] data = new byte[8];
                    BitBuffer buffer = new BitBuffer(128);
                    buffer.AddInt((int) OpCodes.Destroy)
                        .AddUInt(peer.ID)
                        .ToArray(data);
                    Packet packet = default(Packet);
                    packet.Create(data);
                    var command = new GameCommand
                    {
                        Type = GameCommand.CommandType.Broadcast,
                        Channel = 0,
                        Packet = packet
                    };
                    m_commandQueue.Enqueue(command);
                    m_entityDict.Remove(peer.ID);
                    m_entities.RemoveAt(i);
                    return;
                }
            }
        }

        protected override void ProcessPacket(Event netEvent)
        {
            Debug.Log($"Packet received from - ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}, Channel ID: {netEvent.ChannelID}, Data Length: {netEvent.Packet.Length}");
            
            byte[] data = new byte[1024];
            netEvent.Packet.CopyTo(data);
             
            BitBuffer buffer = new BitBuffer(128);
            buffer.FromArray(data, netEvent.Packet.Length);
    
            OpCodes op = (OpCodes) buffer.ReadInt();
            uint id = buffer.ReadUInt();

            if (netEvent.Peer.ID != id)
            {
                Debug.LogError($"ID Mismatch! {netEvent.Peer.ID} vs. {id}");
                return;
            }

            switch (op)
            {
                case OpCodes.PositionUpdate:
                    for (int i = 0; i < m_entities.Count; i++)
                    {
                        if (m_entities[i].Id == id)
                        {
                            m_entities[i].gameObject.transform.position = SharedStuff.ReadAndGetPositionFromCompressed(buffer, SharedStuff.Instance.Range);
                        }
                        else
                        {
                            var command = new GameCommand
                            {
                                Type = GameCommand.CommandType.Send,
                                Peer = m_entities[i].Peer,
                                Channel = 1,
                                Packet = netEvent.Packet
                            };
                            m_commandQueue.Enqueue(command);
                        }
                    }
                    break;
                
                default:
                    netEvent.Packet.Dispose();
                    break;
            }
        }
        
        
        
        private void SpawnRemotePlayer(Peer peer)
        {
            BaseEntity entity = SharedStuff.Instance.SpawnPlayer();
            entity.Initialize(peer, peer.ID);

            CompressedVector3 pos = BoundedRange.Compress(entity.gameObject.transform.position, SharedStuff.Instance.Range);

            byte[] data = new byte[16];
            BitBuffer buffer = new BitBuffer(128);
            buffer.AddInt((int)OpCodes.Spawn)
                .AddUInt(entity.Id)
                .AddUInt(pos.x)
                .AddUInt(pos.y)
                .AddUInt(pos.z)
                .ToArray(data);            

            Packet packet = default(Packet);
            packet.Create(data);

            Debug.LogWarning(peer.Send(0, ref packet));

            var command = new GameCommand
            {
                Type = GameCommand.CommandType.Send,
                Channel = 1,
                Packet = packet
            };

            for (int i = 0; i < m_entities.Count; i++)
            {
                //m_entities[i].Peer.Send(1, ref packet);
                m_commandQueue.Enqueue(command);
            }            
            
            /*
            m_server.Broadcast(0, ref packet);
                         
            buffer.Clear();
            buffer.AddInt((int) OpCodes.AssumeOwnership).AddUInt(entity.Id).ToArray(data);
            packet.Create(data);
            peer.Send(0, ref packet);
            */
            
            // must send all of the old data
            for (int i = 0; i < m_entities.Count; i++)
            {
                if (m_entities[i].Id == peer.ID)
                    continue;
                pos = BoundedRange.Compress(m_entities[i].gameObject.transform.position, SharedStuff.Instance.Range);
                buffer.Clear();
                buffer.AddInt((int) OpCodes.Spawn).AddUInt(m_entities[i].Id).AddUInt(pos.x).AddUInt(pos.y)
                    .AddUInt(pos.z).ToArray(data);
                packet.Create(data);                
                //peer.Send(1, ref packet);
                var old_command = new GameCommand
                {
                    Type = GameCommand.CommandType.Send,
                    Channel = 1,
                    Packet = packet
                };
                m_commandQueue.Enqueue(old_command);
            }

            m_entityDict.Add(peer.ID, entity);
            m_entities.Add(entity);
        }
    }
}