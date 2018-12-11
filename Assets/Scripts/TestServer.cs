using ENet;
using UnityEngine;
using UnityEngine.UI;
using Event = ENet.Event;
using EventType = ENet.EventType;

public class TestServer : MonoBehaviour
{
    [SerializeField] private Text m_nConnected = null;
    
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
                Debug.Log($"Client connected - ID: {evt.Peer.ID}, IP: {evt.Peer.IP}");
                break;
            
            case EventType.Disconnect:
                Debug.Log($"Client disconnected - ID: {evt.Peer.ID}, IP: {evt.Peer.IP}  Reason: {evt.Data}");
                break;
            
            case EventType.Timeout:
                Debug.Log($"Client timeout - ID: {evt.Peer.ID}, IP: {evt.Peer.IP}");
                break;
            
            case EventType.Receive:
                Debug.Log($"Packet received from - ID: {evt.Peer.ID}, IP: {evt.Peer.IP}, Channel ID: {evt.ChannelID}, Data Length: {evt.Packet.Length}");
                evt.Packet.Dispose();
                break;
        }

        m_nConnected.text = m_server.PeersCount.ToString();
    }
    
}
