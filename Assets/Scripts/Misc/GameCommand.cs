using ENet;
using NetStack.Threading;

namespace SoL.Networking
{
    public enum CommandType
    {
        None,
        StartHost,
        StopHost,
        Send,
        BroadcastAll,
        BroadcastOthers,
        BroadcastGroup
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
        public Peer[] TargetGroup;

        public Packet Packet;
        public byte Channel;

        public void Reset()
        {
            Host = null;
            Port = 0;
            ChannelCount = 1;
            PeerLimit = 64;
            UpdateTime = 0;
            
            Source = default(Peer);
            Target = default(Peer);
            TargetGroup = null;
            
            Packet = default(Packet);
            Channel = 0;
        }
    }
    
    public static class GameCommandPool
    {
        private static ConcurrentPool<GameCommand> m_pool = null;
        private static ConcurrentPool<GameCommand> Pool
        {
            get
            {
                if (m_pool == null)
                {
                    m_pool = new ConcurrentPool<GameCommand>(8, () => new GameCommand());
                }
                return m_pool;
            }
        }

        public static GameCommand GetGameCommand()
        {
            return Pool.Acquire();
        }

        public static void ReturnGameCommand(GameCommand command)
        {
            command.Reset();
            Pool.Release(command);
        }
    }
}