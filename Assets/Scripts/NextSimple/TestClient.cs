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

        void Start()
        {
        	m_address = new Address {Port = 9900};
        	m_address.SetHost("127.0.0.1");
        	m_client = new Host();
        	m_client.Create();
        	m_peer = m_client.Connect(m_address);
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
        			ProcessPacket(evt.Packet);
        			Debug.Log($"Packet received from server - Channel ID: {evt.ChannelID}, Data Length: {evt.Packet.Length}");
        			evt.Packet.Dispose();
        			break;
        	}

        	if (m_peer.IsSet)
        	{
        		State = m_peer.State;
        	}
        }

        void ProcessPacket(Packet packet)
        {
	        byte[] data = new byte[1024];
	        packet.CopyTo(data);
	        
	        BitBuffer buffer = new BitBuffer(128);
	        buffer.FromArray(data, packet.Length);

	        OpCodes op = (OpCodes) buffer.ReadInt();
	        uint id = buffer.ReadUInt();

	        switch (op)
	        {
		        case OpCodes.Spawn:
			        var entity = SharedStuff.Instance.SpawnPlayer();
			        var x = buffer.ReadUInt();
			        var y = buffer.ReadUInt();
			        var z = buffer.ReadUInt();
			        var compressedPos = new CompressedVector3(x, y, z);
			        entity.Initialize(id, BoundedRange.Decompress(compressedPos, SharedStuff.Instance.Range), ClientId);
			        break;
		        
		        case OpCodes.Destroy:
			        var entities = GameObject.FindObjectsOfType<BaseEntity>();
			        foreach (var e in entities)
			        {
				        if (e.Id == id)
				        {
					        Destroy(e.gameObject);
				        }
			        }
			        break;
	        }
        }
    }
}