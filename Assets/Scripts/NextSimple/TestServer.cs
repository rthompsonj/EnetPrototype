using System.Collections.Generic;
using ENet;
using NetStack.Compression;
using NetStack.Serialization;
using UnityEngine;
using UnityEngine.UI;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace NextSimple
{
    public enum OpCodes
    {
        Spawn,
        Destroy
    }
    
    public class TestServer : MonoBehaviour
    {
        [SerializeField] private Text m_nConnected = null;

        private List<BaseEntity> m_entities = new List<BaseEntity>();
        
        private Host m_server = null;

        void Start()
        {
            m_server = new Host();
            Address address = new Address {Port = 9900};
            m_server.Create(address, 500);
        }

        void OnDestroy()
        {
            m_server.Flush();
            m_server.Dispose();
        }

        void Update()
        {
            if (m_server == null || m_server.IsSet == false)
                return;

            Event evt;
            m_server.Service(0, out evt);

            switch (evt.Type)
            {
                case EventType.None:
                    break;

                case EventType.Connect:
                    SpawnRemotePlayer(evt.Peer);
                    Debug.Log($"Client connected - ID: {evt.Peer.ID}, IP: {evt.Peer.IP}");
                    break;

                case EventType.Disconnect:
                    ServerDisconnectPlayer(evt.Peer);
                    Debug.Log($"Client disconnected - ID: {evt.Peer.ID}, IP: {evt.Peer.IP}  Reason: {evt.Data}");
                    break;

                case EventType.Timeout:
                    ServerDisconnectPlayer(evt.Peer);
                    Debug.Log($"Client timeout - ID: {evt.Peer.ID}, IP: {evt.Peer.IP}");
                    break;

                case EventType.Receive:
                    Debug.Log($"Packet received from - ID: {evt.Peer.ID}, IP: {evt.Peer.IP}, Channel ID: {evt.ChannelID}, Data Length: {evt.Packet.Length}");
                    evt.Packet.Dispose();
                    break;
            }

            //m_nConnected.text = m_server.PeersCount.ToString();
        }

        private void SpawnRemotePlayer(Peer peer)
        {
            BaseEntity entity = SharedStuff.Instance.SpawnPlayer();
            entity.Initialize(peer, peer.ID);
            m_entities.Add(entity);

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
            
            for (int i = 0; i < m_entities.Count; i++)
            {
                if (m_entities[i] == entity)
                    continue;
                m_entities[i].Peer.Send(0, ref packet);
            }
        }

        private void ServerDisconnectPlayer(Peer peer)
        {
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
                    m_server.Broadcast(0, ref packet);
                    m_entities.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
