using System.Collections.Generic;
using ENet;
using NetStack.Compression;
using NetStack.Serialization;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace NextSimple
{
    public enum OpCodes
    {
        Spawn,
        Destroy,
        PositionUpdate
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
            m_server.Create(address, 500, 100);
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
                    ProcessPacket(evt);
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

            for (int i = 0; i < m_entities.Count; i++)
            {
                m_entities[i].Peer.Send(1, ref packet);
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
                peer.Send(1, ref packet);
            }

            m_entities.Add(entity);
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

        private void ProcessPacket(Event evt)
        {
             byte[] data = new byte[1024];
             evt.Packet.CopyTo(data);
             
             BitBuffer buffer = new BitBuffer(128);
             buffer.FromArray(data, evt.Packet.Length);
    
             OpCodes op = (OpCodes) buffer.ReadInt();
             uint id = buffer.ReadUInt();

             if (evt.Peer.ID != id)
             {
                 Debug.LogError($"ID Mismatch! {evt.Peer.ID} vs. {id}");
                 return;
             }

             Packet packet = evt.Packet;

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
                             m_entities[i].Peer.Send(1, ref packet);
                         }
                     }
                     break;
             }
        }
    }
}
