using ENet;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

public enum EventCodes
{
	Exit=2,
	Lala=3
}

public class TestClient : MonoBehaviour
{
	private Host m_client = null;
	private Peer m_peer;
	private Address m_address;

	public PeerState State;

	void Start()
	{
		m_address = new Address {Port = 9900};
		m_address.SetHost("127.0.0.1");
		m_client.Create();
		m_peer = m_client.Connect(m_address);
	}

	void OnDestroy()
	{
		switch (m_peer.State)
		{
			case PeerState.Connected:
				m_peer.Disconnect((uint)EventCodes.Exit);
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
				Debug.Log($"Packet received from server - Channel ID: {evt.ChannelID}, Data Length: {evt.Packet.Length}");
				evt.Packet.Dispose();
				break;
		}

		if (m_peer.IsSet)
		{
			State = m_peer.State;
		}
	}
    
}
