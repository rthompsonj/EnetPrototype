using System.Text;
using System.Threading;
using DisruptorUnity3d;
using ENet;
using Misc;
using NetStack.Compression;
using SoL.Networking.Objects;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace SoL.Networking.Managers
{
    public abstract class BaseNetworkSystem : MonoBehaviour, INetworkManager
    {
        //TODO: temp
        public static BaseNetworkSystem Instance;
        
        public const float kMaxRange = 32f;

        private static uint m_nextNetworkId = 1;
        public static NetworkId GetNetworkId(Peer peer)
        {
            var value = m_nextNetworkId;
            m_nextNetworkId += 1;
            return new NetworkId(value, peer);
        }
        
        public static BoundedRange[] Range = new BoundedRange[]
        {
            new BoundedRange(-kMaxRange, kMaxRange, 0.05f),
            new BoundedRange(-kMaxRange, kMaxRange, 0.05f),
            new BoundedRange(-kMaxRange, kMaxRange, 0.05f)
        };

        [SerializeField] protected SpawnParameters m_params = null;
        [SerializeField] protected SpawnType m_type = SpawnType.None;

        [SerializeField] private TextMeshProUGUI m_stats = null;
        private Host m_host;
        protected Peer m_peer;
        
        public Peer Peer => m_peer;

        [SerializeField] protected string m_targetHost = "127.0.0.1";
        [SerializeField] protected ushort m_targetPort = 9900;
        
        protected abstract void Func_StartHost(Host host, GameCommand command);
        protected abstract void Func_StopHost(Host host, GameCommand command);
        protected abstract void Func_Send(Host host, GameCommand command);
        protected abstract void Func_BroadcastAll(Host host, GameCommand command);
        protected abstract void Func_BroadcastOthers(Host host, GameCommand command);
        protected abstract void Func_BroadcastGroup(Host host, GameCommand command);

        protected abstract void Connect(Event netEvent);
        protected abstract void Disconnect(Event netEvent);
        protected abstract void ProcessPacket(Event netEvent);

        protected virtual void UpdateStates() { }

        private Thread m_logicThread = null;
        private Thread m_networkThread = null;

        private volatile bool m_logicThreadActive = false;
        private volatile bool m_networkThreadActive = false;
        
        //        CommandQueue: game thread writes,    logic thread reads
        //       FunctionQueue: logic thread writes,   network thread reads
        //     LogicEventQueue: logic thread writes,   game thread reads
        // TransportEventQueue: network thread writes, logic thread reads
        
        protected readonly RingBuffer<GameCommand> m_commandQueue = new RingBuffer<GameCommand>(64);
        protected readonly RingBuffer<GameCommand> m_functionQueue = new RingBuffer<GameCommand>(64);
        protected readonly RingBuffer<Event> m_logicEventQueue = new RingBuffer<Event>(64);
        protected readonly RingBuffer<Event> m_transportEventQueue = new RingBuffer<Event>(64);
        
        //protected readonly ListDictCollection<uint, NetworkedObject> m_peers = new ListDictCollection<uint, NetworkedObject>(true);        
        
        protected readonly ListDictCollection<uint, NetworkEntity> m_networkEntities = new ListDictCollection<uint, NetworkEntity>(true);        

        public virtual void RegisterEntity(NetworkEntity entity)
        {
            m_networkEntities.Add(entity.NetworkId.Value, entity);
        }

        public virtual void DeregisterEntity(NetworkEntity entity)
        {
            m_networkEntities.Remove(entity.NetworkId.Value);
        }
        
        #region MONO

        //TODO: temp
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        protected virtual void Start()
        {
            m_logicThread = LogicThread();
            m_networkThread = NetworkThread();

            m_logicThreadActive = true;
            m_networkThreadActive = true;
            
            m_logicThread.Start();
            m_networkThread.Start();
        }

        private void OnDestroy()
        {
            var command = GameCommandPool.GetGameCommand();
            command.Type = CommandType.StopHost;
            m_commandQueue.Enqueue(command);
        }

        protected void TerminateThreads()
        {
            m_logicThreadActive = false;
            m_networkThreadActive = false;            
        }
        
        private void Update()
        {
            Event netEvent;
            while (m_logicEventQueue.TryDequeue(out netEvent))
            {
                switch (netEvent.Type)
                {
                    case EventType.None:
                        break;
                    
                    case EventType.Connect:
                        Connect(netEvent);
                        break;
                    
                    case EventType.Disconnect:
                        Disconnect(netEvent);
                        break;
                    
                    case EventType.Timeout:
                        Disconnect(netEvent);
                        break;
                    
                    case EventType.Receive:
                        ProcessPacket(netEvent);
                        break;                    
                }
            }
            
            Profiler.BeginSample("Update States");
            UpdateStates();
            Profiler.EndSample();
            
            Profiler.BeginSample("Update Stats");
            UpdateStats();
            Profiler.EndSample();
        }
        
        #endregion

        #region THREADS
        
        private Thread LogicThread()
        {
            return new Thread(() =>
            {
                while (m_logicThreadActive)
                {
                    GameCommand command = null;
                    Event netEvent;
                    
                    // --> to network thread
                    while (m_commandQueue.TryDequeue(out command))
                    {
                        m_functionQueue.Enqueue(command);
                    }
                    
                    // --> to game thread
                    while (m_transportEventQueue.TryDequeue(out netEvent))
                    {
                        switch (netEvent.Type)
                        {
                            case EventType.None:
                                break;
                            
                            default:
                                m_logicEventQueue.Enqueue(netEvent);
                                break;
                        }
                    }

                    Thread.Sleep(1);
                }
            });
        }

        private Thread NetworkThread()
        {
            return new Thread(() =>
            {
                int updateTime = (int)(1000f / 30f);

                using (Host host = new Host())
                {
                    m_host = host;
                    
                    while (m_networkThreadActive)
                    {
                        GameCommand command = null;
                        
                        while (m_functionQueue.TryDequeue(out command))
                        {
                            switch (command.Type)
                            {
                                case CommandType.StartHost:
                                    updateTime = command.UpdateTime;
                                    Func_StartHost(host, command);
                                    break;

                                case CommandType.StopHost:
                                    Func_StopHost(host, command);
                                    break;

                                case CommandType.Send:
                                    Func_Send(host, command);
                                    break;

                                case CommandType.BroadcastAll:
                                    Func_BroadcastAll(host, command);
                                    break;

                                case CommandType.BroadcastOthers:
                                    Func_BroadcastOthers(host, command);
                                    break;
                                
                                case CommandType.BroadcastGroup:
                                    Func_BroadcastGroup(host, command);
                                    break;
                            }

                            if (command.Packet.IsSet)
                            {
                                command.Packet.Dispose();
                            }
                            
                            GameCommandPool.ReturnGameCommand(command);
                        }

                        if (host.IsSet)
                        {
                            Event netEvent;
                            host.Service(updateTime, out netEvent);
                            if (netEvent.Type != EventType.None)
                            {
                                // --> to logic thread
                                m_transportEventQueue.Enqueue(netEvent);
                            }
                        }
                    }
                    
                    host.Flush();
                    host.Dispose();
                }
            });           
        }
        
        #endregion
        
        public void AddCommandToQueue(GameCommand command)
        {
            m_commandQueue.Enqueue(command);   
        }

        private StringBuilder m_sb = new StringBuilder();
        
        private void UpdateStats()
        {
            if (m_stats == null || m_host == null || m_host.IsSet == false)
                return;

            m_sb.Clear();
            m_sb.AppendLine($"{GetValueUnit(m_host.BytesSent)}\tSent");
            m_sb.AppendLine($"{GetValueUnit(m_host.BytesReceived)}\tReceived");
            m_sb.AppendLine("");
            m_sb.AppendLine($"{m_host.PacketsSent}\tPackets Sent");
            m_sb.AppendLine($"{m_host.PacketsReceived}\tPackets Received");
            m_sb.AppendLine("");
            if (m_peer.IsSet)
            {
                m_sb.AppendLine($"{m_peer.PacketsLost}\tPackets Lost");
                m_sb.AppendLine($"{m_peer.RoundTripTime}\tRTT");
                m_sb.AppendLine($"{m_peer.State}\tState");
            }
            else
            {
                m_sb.AppendLine($"{m_host.PeersCount}\tPeer Count");   
            }
            m_stats.SetText(m_sb.ToString());
        }

        private string GetValueUnit(uint b)
        {
            // mb
            if (b > 1e6)
            {
                return $"{b / 1e6f} mb";
            }
            
            // kb
            if (b > 1000)
            {
                return $"{b / 1000f} kb";
            }

            return $"{b} bytes";
        }
    }
}