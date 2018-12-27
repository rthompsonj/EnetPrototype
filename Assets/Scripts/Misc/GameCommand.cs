using ENet;

namespace Threaded
{
    public enum CommandType
    {
        None,
        StartHost,
        StopHost,
        Send,
        BroadcastAll,
        BroadcastOthers
    }
    
    public class GameCommand
    {
        public CommandType Type;

        public string Host;
        public ushort Port;
        public int ChannelCount;
        public int PeerLimit;
        public int UpdateTime;

        public Peer Source;
        public Peer Target;

        public Packet Packet;
        public byte Channel;

        public void Reset()
        {
            Source = default(Peer);
            Target = default(Peer);
            Packet = default(Packet);
            Channel = 0;
        }
    }
}