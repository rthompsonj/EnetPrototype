using System;
using ENet;
using NetStack.Serialization;
using SoL.Networking.Objects;
using UnityEngine;
using Event = ENet.Event;

namespace SoL.Networking.Managers
{
    public class ClientNetworkSystem : BaseNetworkSystem
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

        protected override void OnDestroy()
        {
            base.OnDestroy();
            var command = GameCommandPool.GetGameCommand();
            command.Type = CommandType.StopHost;
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
            var id = header.ID;

            NetworkedObject nobj = null;

            switch (op)
            {
                case OpCodes.Spawn:
                    nobj = SpawnNetworkedObject(id, buffer);
                    break;
                
                case OpCodes.BulkSpawn:
                    var cnt = m_buffer.ReadInt();
                    for (int i = 0; i < cnt; i++)
                    {
                        header = m_buffer.GetEntityHeader();
                        SpawnNetworkedObject(header.ID, buffer);
                    }
                    break;
		        
                case OpCodes.Destroy:
                    if (m_actorDict.TryGetValue(id, out nobj) && nobj.ID == id)
                    {
                        m_actorDict.Remove(id);
                        m_actors.Remove(nobj);
                        Destroy(nobj.gameObject);                        
                    }
                    break;
		        
                case OpCodes.PositionUpdate:
                case OpCodes.SyncUpdate:
                    if (m_actorDict.TryGetValue(id, out nobj))
                    {
                        nobj.ProcessPacket(op, buffer);
                    }
                    else
                    {
                        Debug.LogWarning($"Unable to locate entity ID: {id}");
                    }
                    break;
            }
        }

        private NetworkedObject SpawnNetworkedObject(uint id, BitBuffer buffer)
        {
            var go = Instantiate(m_playerGo);
            var nobj = go.GetComponent<NetworkedObject>();
            nobj.ClientInitialize(this, id, buffer);
            m_actorDict.Add(id, nobj);
            m_actors.Add(nobj);
            return nobj;
        }
        
        #endregion
    }
}