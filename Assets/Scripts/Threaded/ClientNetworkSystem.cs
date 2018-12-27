using System;
using ENet;
using NetStack.Serialization;
using NextSimple;
using UnityEngine;
using Event = ENet.Event;

namespace Threaded
{
    public class ClientNetworkSystem : BaseNetworkSystem
    {
        public Peer Peer { get; private set; }
        
        private BitBuffer m_buffer = new BitBuffer(128);
        
        protected override void Start()
        {
            base.Start();
            var command = new GameCommand
            {
                Type = GameCommand.CommandType.StartHost,
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
                Type = GameCommand.CommandType.StopHost
            };
            m_commandQueue.Enqueue(command);
        }

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
                Peer = host.Connect(addy, command.ChannelCount);
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
            host.Flush();
            host.Dispose();
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

        protected override void Connect(Event netEvent)
        {
            //throw new NotImplementedException();
        }

        protected override void Disconnect(Event netEvent)
        {
            Debug.Log("Disconnect detected!");
        }

        protected override void ProcessPacket(Event netEvent)
        {
            byte channel = netEvent.ChannelID;
            m_buffer = netEvent.Packet.GetBufferFromPacket(m_buffer);
            var buffer = m_buffer;
            var header = m_buffer.GetEntityHeader();
            var op = header.OpCode;
            var id = header.ID;
            
            BaseEntity entity = null;
            Vector3 pos;
            float h;

            switch (op)
            {
                case OpCodes.Spawn:
                    entity = SharedStuff.Instance.SpawnPlayer();
                    pos = buffer.ReadVector3(SharedStuff.Instance.Range);
                    h = buffer.ReadFloat();
                    entity.Initialize(id, pos, Peer);
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
                        pos = buffer.ReadVector3(SharedStuff.Instance.Range);
                        h = buffer.ReadFloat();

                        entity.m_newPos = new Vector4(pos.x, pos.y, pos.z, h);
                    }
                    else
                    {
                        Debug.LogWarning($"Unable to locate entity ID: {id}");
                    }
                    break;
                
                case OpCodes.SyncUpdate:
                    if (m_entityDict.TryGetValue(id, out entity))
                    {
                        entity.ProcessSyncUpdate(buffer);
                    }
                    break;
            }
        }
    }
}