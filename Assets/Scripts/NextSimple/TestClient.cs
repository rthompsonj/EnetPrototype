using System.Collections.Generic;
using ENet;
using NetStack.Compression;
using NetStack.Serialization;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace NextSimple
{
    public class TestClient : MonoBehaviour
    {
	    private static uint m_currentClient = 0;
	    
        private Host m_client = null;
        private Peer m_peer;
        private Address m_address;

        public PeerState State;

        public uint ClientId;
        
        private List<BaseEntity> m_entities = new List<BaseEntity>();

        void Start()
        {
        	m_address = new Address {Port = 9900};
        	m_address.SetHost("127.0.0.1");
        	m_client = new Host();
        	m_client.Create();
        	m_peer = m_client.Connect(m_address, 100);
            ClientId = m_currentClient;
            m_currentClient += 1;
        }

        void OnDestroy()
        {
        	switch (m_peer.State)
        	{
        		case PeerState.Connected:
        			m_peer.Disconnect((uint) EventCodes.Exit);
        			break;
        	}

            foreach (var e in m_entities)
            {
	            if (e != null)
	            {
		            Destroy(e);
	            }
            }

        	m_client.Flush();
        	m_client.Dispose();
        }

        void Update()
        {
        	if (m_client == null || m_client.IsSet == false)
        		return;

        	Event evt;
        	m_client.Service(0, out evt);

        	switch (evt.Type)
        	{
        		case EventType.None:
        			break;

        		case EventType.Connect:
        			Debug.Log($"Client connected to server - ID: {m_peer.ID}");
        			break;

        		case EventType.Disconnect:
        			Debug.Log($"Client disconnected from server");
        			break;

        		case EventType.Timeout:
        			Debug.Log($"Client connection timeout");
        			break;

        		case EventType.Receive:
        			ProcessPacket(evt.Packet, evt.ChannelID);
        			Debug.Log($"Packet received from server - Channel ID: {evt.ChannelID}, Data Length: {evt.Packet.Length}");
        			evt.Packet.Dispose();
        			break;
        	}

        	if (m_peer.IsSet)
        	{
        		State = m_peer.State;
        	}
        }

        void ProcessPacket(Packet packet, byte channel)
        {
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
			        entity.Initialize(id, SharedStuff.ReadAndGetPositionFromCompressed(buffer, SharedStuff.Instance.Range), m_peer);
			        if (channel == 0)
			        {
				        entity.AssumeOwnership();
			        }
			        m_entities.Add(entity);
			        break;
		        
		        case OpCodes.Destroy:
			        entity = GetEntityForId(id);			        			        
			        if (entity != null && entity.Id == id)
			        {
				        m_entities.Remove(entity);
				        Destroy(entity.gameObject);
			        }
			        break;
		        
		        case OpCodes.PositionUpdate:
			        entity = GetEntityForId(id);
			        if (entity != null)
			        {
				        entity.m_newPos = SharedStuff.ReadAndGetPositionFromCompressed(buffer, SharedStuff.Instance.Range);
			        }
			        break;
	        }
        }

        private BaseEntity GetEntityForId(uint id)
        {
	        for (int i = 0; i < m_entities.Count; i++)
	        {
		        if (m_entities[i].Id == id)
		        {
			        return m_entities[i];
		        }
	        }

	        return null;
        }
    }
}