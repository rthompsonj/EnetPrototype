using System;
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
                Start,
                Stop,
                Send,
                Broadcast
            }

            public CommandType Type;

            public string Host;
            public ushort Port;
            public int ChannelCount;
            public int PeerLimit;
            public int UpdateTime;

            public Peer Peer;
            public byte Channel;
            public Packet Packet;
        }

        protected abstract Thread LogicThread();
        protected abstract Thread NetworkThread();
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

        protected virtual void Update()
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
                        netEvent.Packet.Dispose();
                        break;                    
                }
            }
        }
    }
}