using System;
using System.Threading;
using ENet;
using NetStack.Compression;
using NetStack.Serialization;
using NextSimple;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace Threaded
{
    public class ClientNetworkSystem : BaseNetworkSystem
    {
        public Peer Peer { get; private set; }
        
        protected override void Start()
        {
            base.Start();
            var command = new GameCommand
            {
                Type = GameCommand.CommandType.Start,
                Host = "127.0.0.1",
                Port = 9900,
                UpdateTime = 0,
                ChannelCount = 100
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

        public void AddCommandToQueue(GameCommand command)
        {
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
                
                using (Host client = new Host())
                {
                    while (true)
                    {
                        while (m_functionQueue.TryDequeue(out GameCommand command))
                        {
                            switch (command.Type)
                            {
                                case GameCommand.CommandType.Start:
                                    Debug.Log("STARTING CLIENT FROM NETWORK THREAD");
                                    try
                                    {
                                        Address addy = new Address
                                        {
                                            Port = command.Port
                                        };
                                        addy.SetHost(command.Host);
                                        updateTime = command.UpdateTime;
                                
                                        client.Create();
                                        Peer = client.Connect(addy, command.ChannelCount);
                                        Debug.Log($"Client started on port: {command.Port} with {command.ChannelCount} channels.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogError(ex);
                                    }
                                    break;
                                
                                case GameCommand.CommandType.Stop:
                                    Debug.Log("STOPPING CLIENT FROM NETWORK THREAD");

                                    /*
                                    switch (peer.State)
                                    {
                                        case PeerState.Connected:
                                            peer.Disconnect((uint)EventCodes.Exit);
                                            break;
                                    }
                                    */
                            
                                    client.Flush();
                                    client.Dispose();
                                    break;
                                
                                case GameCommand.CommandType.Send:
                                    if (client.IsSet && Peer.IsSet)
                                    {
                                        Peer.Send(command.Channel, ref command.Packet);   
                                    }
                                    break;
                                
                                default:
                                    throw new ArgumentException($"Invalid CommandType: {command.Type}");
                            }
                        }

                        if (client.IsSet)
                        {
                            ENet.Event netEvent;
                            client.Service(updateTime, out netEvent);
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
            //throw new NotImplementedException();
        }

        protected override void Disconnect(Event netEvent)
        {
            //throw new NotImplementedException();
        }

        protected override void ProcessPacket(Event netEvent)
        {
            Packet packet = netEvent.Packet;
            byte channel = netEvent.ChannelID;
            
            byte[] data = new byte[1024];
            packet.CopyTo(data);
	        
            BitBuffer buffer = new BitBuffer(128);
            buffer.FromArray(data, packet.Length);

            OpCodes op = (OpCodes) buffer.ReadInt();
            uint id = buffer.ReadUInt();

            BaseEntity entity = null;
            CompressedVector3 compressedPos;

            switch (op)
            {
                case OpCodes.Spawn:
                    entity = SharedStuff.Instance.SpawnPlayer();
                    entity.Initialize(id, SharedStuff.ReadAndGetPositionFromCompressed(buffer, SharedStuff.Instance.Range), Peer);
                    if (channel == 0)
                    {
                        entity.AssumeOwnership();
                    }
                    m_entityDict.Add(id, entity);
                    m_entities.Add(entity);
                    break;
		        
                case OpCodes.Destroy:
                    if (m_entityDict.TryGetValue(id, out entity) && entity.Id == id)
                    {
                        m_entityDict.Remove(id);
                        m_entities.Remove(entity);
                        Destroy(entity.gameObject);                        
                    }
                    break;
		        
                case OpCodes.PositionUpdate:
                    if (m_entityDict.TryGetValue(id, out entity))
                    {
                        entity.m_newPos = SharedStuff.ReadAndGetPositionFromCompressed(buffer, SharedStuff.Instance.Range);
                    }
                    break;
            }
        }
    }
}