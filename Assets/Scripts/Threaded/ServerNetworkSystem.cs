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
            
            for (int i = 0; i < m_entities.Count; i++)
            {
                if (m_entities[i].Id == peer.ID)
                {
                    byte[] data = new byte[8];
                    BitBuffer buffer = new BitBuffer(128);
                    buffer.AddUShort((ushort) OpCodes.Destroy)
                        .AddUInt(peer.ID)
                        .ToArray(data);
                    Packet packet = default(Packet);
                    packet.Create(data);
                    var command = new GameCommand
                    {
                        Type = GameCommand.CommandType.BroadcastAll,
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

            OpCodes op = (OpCodes) buffer.ReadUShort();
            uint id = buffer.ReadUInt();

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
                        var posUpdate = PackerUnpacker.DeserializePositionUpdate(buffer, SharedStuff.Instance.Range);
                        entity.gameObject.transform.position = posUpdate.Position;
                        entity.gameObject.transform.rotation = Quaternion.Euler(new Vector3(0f, posUpdate.Heading, 0f));
                        
                        //entity.gameObject.transform.position = SharedStuff.ReadAndGetPositionFromCompressed(buffer, SharedStuff.Instance.Range);
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
            buffer.AddUShort((ushort)OpCodes.Spawn)
                .AddUInt(entity.Id)
                .AddUInt(pos.x)
                .AddUInt(pos.y)
                .AddUInt(pos.z)
                .ToArray(data);            

            Packet packet = default(Packet);
            packet.Create(data);

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
                command = PackerUnpacker.GetPositionUpdate(OpCodes.Spawn, m_entities[i].Peer.ID, m_entities[i].gameObject, SharedStuff.Instance.Range, 1);
                command.Target = peer;
                
                /*
                pos = BoundedRange.Compress(m_entities[i].gameObject.transform.position, SharedStuff.Instance.Range);
                buffer.Clear();
                buffer.AddUShort((ushort) OpCodes.Spawn).AddUInt(m_entities[i].Id).AddUInt(pos.x).AddUInt(pos.y)
                    .AddUInt(pos.z).ToArray(data);
                packet.Create(data);
                
                command = new GameCommand
                {
                    Type = GameCommand.CommandType.Send,
                    Target = peer,
                    Channel = 1,
                    Packet = packet
                };
                */
                
                m_commandQueue.Enqueue(command);
            }

            m_entityDict.Add(peer.ID, entity);
            m_entities.Add(entity);
        }
    }
}