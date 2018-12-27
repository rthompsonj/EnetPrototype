using System.Collections.Generic;
using System.Threading;
using DisruptorUnity3d;
using ENet;
using NextSimple;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace Threaded
{
    public abstract class BaseNetworkSystem : MonoBehaviour
    {
        public class GameCommand
        {
            public enum CommandType
            {
                StartHost,
                StopHost,
                Send,
                BroadcastAll,
                BroadcastOthers
            }

            public CommandType Type;

            public string Host;
            public ushort Port;
            public int ChannelCount;
            public int PeerLimit;
            public int UpdateTime;

            public Peer Source;
            public Peer Target;

            public byte Channel;
            public Packet Packet;            
        }

        protected abstract void Func_StartHost(Host host, GameCommand command);
        protected abstract void Func_StopHost(Host host, GameCommand command);
        protected abstract void Func_Send(Host host, GameCommand command);
        protected abstract void Func_BroadcastAll(Host host, GameCommand command);
        protected abstract void Func_BroadcastOthers(Host host, GameCommand command);
        
        
        //protected abstract Thread LogicThread();
        //protected abstract Thread NetworkThread();
        protected abstract void Connect(Event netEvent);
        protected abstract void Disconnect(Event netEvent);
        protected abstract void ProcessPacket(Event netEvent);

        private Thread m_logicThread = null;
        private Thread m_networkThread = null;
        
        //        CommandQueue: game thread writes,    logic thread reads
        //       FunctionQueue: logic thread writes,   network thread reads
        //     LogicEventQueue: logic thread writes,   game thread reads
        // TransportEventQueue: network thread writes, logic thread reads
        
        protected readonly RingBuffer<GameCommand> m_commandQueue = new RingBuffer<GameCommand>(64);
        protected readonly RingBuffer<GameCommand> m_functionQueue = new RingBuffer<GameCommand>(64);
        protected readonly RingBuffer<Event> m_logicEventQueue = new RingBuffer<Event>(64);
        protected readonly RingBuffer<Event> m_transportEventQueue = new RingBuffer<Event>(64);
        
        protected readonly List<BaseEntity> m_entities = new List<BaseEntity>();
        protected readonly Dictionary<uint, BaseEntity> m_entityDict = new Dictionary<uint, BaseEntity>();
        
        protected virtual void Start()
        {
            m_logicThread = LogicThread();
            m_networkThread = NetworkThread();   
            
            m_logicThread.Start();
            m_networkThread.Start();
        }

        protected virtual void OnDestroy()
        {
            
        }

        private Thread LogicThread()
        {
            return new Thread(() =>
            {
                while (true)
                {
                    // --> to network thread
                    while (m_commandQueue.TryDequeue(out GameCommand command))
                    {
                        m_functionQueue.Enqueue(command);
                    }
                    
                    // --> to game thread
                    while (m_transportEventQueue.TryDequeue(out Event netEvent))
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
                }
            });
        }

        private Thread NetworkThread()
        {
            return new Thread(() =>
            {
                int updateTime = 0;

                using (Host host = new Host())
                {
                    while (true)
                    {
                        while (m_functionQueue.TryDequeue(out GameCommand command))
                        {
                            switch (command.Type)
                            {
                                case GameCommand.CommandType.StartHost:
                                    updateTime = command.UpdateTime;
                                    Func_StartHost(host, command);
                                    break;

                                case GameCommand.CommandType.StopHost:
                                    Func_StopHost(host, command);
                                    break;

                                case GameCommand.CommandType.Send:
                                    Func_Send(host, command);
                                    break;

                                case GameCommand.CommandType.BroadcastAll:
                                    Func_BroadcastAll(host, command);
                                    break;

                                case GameCommand.CommandType.BroadcastOthers:
                                    Func_BroadcastOthers(host, command);
                                    break;
                            }

                            if (command.Packet.IsSet)
                            {
                                command.Packet.Dispose();
                            }
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
                }
            });           
        }

        protected void Update()
        {
            while (m_logicEventQueue.TryDequeue(out Event netEvent))
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
        }
        
        public void AddCommandToQueue(GameCommand command)
        {
            m_commandQueue.Enqueue(command);   
        }
    }
}