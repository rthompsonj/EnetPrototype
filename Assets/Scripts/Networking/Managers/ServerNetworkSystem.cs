using System;
using ENet;
using NetStack.Serialization;
using SoL.Networking.Objects;
using UnityEngine;
using UnityEngine.Profiling;
using Event = ENet.Event;

namespace SoL.Networking.Managers
{
    public class ServerNetworkSystem : BaseNetworkSystem
    {
        private BitBuffer m_buffer = new BitBuffer(128);
        
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

        protected override void OnDestroy()
        {
            base.OnDestroy();
            var command = new GameCommand
            {
                Type = CommandType.StopHost
            };
            m_commandQueue.Enqueue(command);
        }

        protected override void Update()
        {
            base.Update();

            for (int i = 0; i < m_peers.Count; i++)
            {
                if (m_peers[i].Proximity == null)
                    continue;
                
                //TODO: this may always return 1 as the box is inside of the triggers?
                int observers = m_peers[i].Proximity.GetUpdateCount();
                if (observers <= 0)
                {
                    continue;
                }

                var peerGroup = PeerArrayPool.GetArray(observers);
                int cnt = 0;
                foreach (var obs in m_peers[i].Proximity.GetUpdates())
                {
                    peerGroup[cnt] = obs.Peer;
                    cnt += 1;
                }

                if (cnt <= 0)
                {
                    continue;   
                }

                m_buffer.AddEntityHeader(m_peers[i].Peer, OpCodes.PositionUpdate);
                m_buffer.AddVector3(m_peers[i].gameObject.transform.position, Range);
                m_buffer.AddFloat(0f);  //TODO: lazy
                var packet = m_buffer.GetPacketFromBuffer(PacketFlags.None);
                var command = GameCommandPool.GetGameCommand();
                command.Type = CommandType.BroadcastGroup;
                command.Packet = packet;
                command.TargetGroup = peerGroup;
                command.Channel = 2;
                m_commandQueue.Enqueue(command);
            }
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
            for (int i = 0; i < m_peers.Count; i++)
            {
                if (m_peers[i].Peer.IsSet)
                {
                    m_peers[i].Peer.Disconnect(0);
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
            for (int i = 0; i < m_peers.Count; i++)
            {
                if (m_peers[i].Peer.ID != command.Source.ID && m_peers[i].Peer.IsSet)
                {                    
                    m_peers[i].Peer.Send(command.Channel, ref command.Packet);
                }
            }
        }
        
        protected override void Func_BroadcastGroup(Host host, GameCommand command)
        {
            //TODO: stop-gap solution until dlls are updated with recent ENET changes.
            for (int i = 0; i < command.TargetGroup.Length; i++)
            {
                if (command.TargetGroup[i].IsSet)
                {
                    command.TargetGroup[i].Send(command.Channel, ref command.Packet);
                }
            }
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
            NetworkedObject nobj = null;

            if (m_peers.TryGetValue(peer.ID, out nobj))
            {
                uint id = peer.ID;

                m_peers.Remove(peer.ID);
                    
                Destroy(nobj.gameObject);

                // notify everyone else
                if (m_peers.Count > 0)
                {
                    m_buffer.AddEntityHeader(peer, OpCodes.Destroy);
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
            var id = header.ID;

            if (netEvent.Peer.ID != id)
            {
                Debug.LogError($"ID Mismatch! {netEvent.Peer.ID} vs. {id}");
                Profiler.EndSample();
                return;
            }

            switch (op)
            {
                case OpCodes.Spawn:
                    var spawnType = (Misc.SpawnType)m_buffer.ReadInt();
                    SpawnRemotePlayer(netEvent.Peer, spawnType);
                    break;
                
                case OpCodes.PositionUpdate:
                    Profiler.BeginSample("Process Packet - Position Update");
                    /*
                    var command = GameCommandPool.GetGameCommand();
                    command.Type = CommandType.BroadcastOthers;
                    command.Source = netEvent.Peer;
                    command.Channel = 1;
                    command.Packet = netEvent.Packet;

                    m_commandQueue.Enqueue(command);
                    */

                    NetworkedObject nobj = null;
                    if (m_peers.TryGetValue(netEvent.Peer.ID, out nobj))
                    {
                        nobj.ProcessPacket(op, buffer);

                        // player TODO: (for now)
                        if (nobj.Proximity == null)
                        {
                            var command = GameCommandPool.GetGameCommand();
                            command.Type = CommandType.BroadcastOthers;
                            command.Source = netEvent.Peer;
                            command.Channel = 1;
                            command.Packet = netEvent.Packet;

                            m_commandQueue.Enqueue(command);                            
                        }
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
        
        
        private void SpawnRemotePlayer(Peer peer, Misc.SpawnType spawnType)
        {
            var go = m_params.InstantiateSpawn(spawnType);
            NetworkedObject nobj = go.GetComponent<NetworkedObject>();
            nobj.SpawnType = spawnType;
            nobj.ServerInitialize(this, peer);

            m_buffer.AddEntityHeader(peer, OpCodes.Spawn);
            m_buffer.AddInt((int)spawnType);
            m_buffer.AddInitialState(nobj);
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

            int packetSize = 0;
            
            // individual packets
            /*
            // must send all of the old data
            for (int i = 0; i < m_entities.Count; i++)
            {
                if (m_entities[i].Peer.ID == peer.ID)
                    continue;
                
                m_buffer.AddEntityHeader(m_entities[i].Peer, OpCodes.Spawn);
                m_buffer.AddVector3(m_entities[i].gameObject.transform.position, SharedStuff.Instance.Range);
                m_buffer.AddFloat(m_entities[i].gameObject.transform.eulerAngles.y);
                m_buffer.AddEntitySyncData(m_entities[i]);
                var spawnOthersPacket = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);

                var spawnOthersCommand = GameCommandPool.GetGameCommand();
                spawnOthersCommand.Type = CommandType.Send;
                spawnOthersCommand.Packet = spawnOthersPacket;
                spawnOthersCommand.Channel = 1;
                spawnOthersCommand.Target = peer;
                
                packetSize += spawnOthersPacket.Length;
                
                m_commandQueue.Enqueue(spawnOthersCommand);
            }
            */

            var cnt = 0;
            for (int i = 0; i < m_peers.Count; i++)
            {
                // do not send ourselves
                if (m_peers[i].Peer.ID == peer.ID)
                    continue;
                
                // do not send NPCs
                if (m_peers[i].Proximity != null)
                    continue;

                cnt += 1;
            }
            
            // one large packet
            m_buffer.AddEntityHeader(peer, OpCodes.BulkSpawn);
            m_buffer.AddInt(cnt);
            for (int i = 0; i < m_peers.Count; i++)
            {
                // do not send ourselves
                if (m_peers[i].Peer.ID == peer.ID)
                    continue;
                
                // do not send NPCs
                if (m_peers[i].Proximity != null)
                    continue;

                m_buffer.AddEntityHeader(m_peers[i].Peer, OpCodes.Spawn, false);
                m_buffer.AddInt((int)m_peers[i].SpawnType);
                m_buffer.AddInitialState(m_peers[i]);
            }
            var spawnOthersPacket = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
            var spawnOthersCommand = GameCommandPool.GetGameCommand();
            spawnOthersCommand.Type = CommandType.Send;
            spawnOthersCommand.Packet = spawnOthersPacket;
            spawnOthersCommand.Channel = 1;
            spawnOthersCommand.Target = peer;

            packetSize += spawnOthersPacket.Length;
            
            m_commandQueue.Enqueue(spawnOthersCommand);

            m_peers.Add(peer.ID, nobj);
            
            Debug.Log($"Sent SpawnOthersPacket of size {packetSize.ToString()}");
        }
    }
}