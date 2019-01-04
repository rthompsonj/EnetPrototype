using System.Collections.Generic;
using ENet;
using NetStack.Serialization;
using SoL.Networking.Managers;
using SoL.Networking.Objects;
using UnityEngine;

namespace SoL.Networking.Proximity
{
    public class ProximityCoordinator : MonoBehaviour
    {
        private readonly BitBuffer m_buffer = new BitBuffer(128);
        private NetworkedObject m_netObj = null;
        
        struct Observer
        {
            public SensorDistance Flags;
            public NetworkedObject Object;
        }
        
        private const float kNearUpdateRate = 1f/10f;
        private const float kFarUpdateRate = 1f/5f;
        
        private readonly Dictionary<uint, Observer> m_observers = new Dictionary<uint, Observer>();

        private float m_nextUpdateNear = 0f;
        private float m_nextUpdateFar = 0f;

        public int NNear = 0;
        public int NFar = 0;
        
        void Awake()
        {
            m_netObj = gameObject.GetComponentInParent<NetworkedObject>();
            m_nextUpdateNear = Time.time + kNearUpdateRate;
            m_nextUpdateFar = Time.time + kFarUpdateRate;
        }

        public int GetUpdateCount()
        {
            int cnt = 0;

            if (Time.time >= m_nextUpdateNear)
            {
                cnt += NNear;
            }

            if (Time.time >= m_nextUpdateFar)
            {
                cnt += NFar;
            }

            return cnt;
        }

        public IEnumerable<NetworkedObject> GetUpdates()
        {
            bool updateNear = Time.time > m_nextUpdateNear;
            bool updateFar = Time.time > m_nextUpdateFar;

            if (updateNear == false && updateFar == false)
            {
                yield break;   
            }            
            
            foreach (var kvp in m_observers)
            {
                if (updateNear && kvp.Value.Flags.HasFlag(SensorDistance.Near))
                {
                    yield return kvp.Value.Object;
                }
                else if (updateFar && kvp.Value.Flags.HasFlag(SensorDistance.Far) &&
                         kvp.Value.Flags.HasFlag(SensorDistance.Near) == false)
                {
                    yield return kvp.Value.Object;
                }
            }

            if (updateNear)
            {
                m_nextUpdateNear = Time.time + kNearUpdateRate;               
            }

            if (updateFar)
            {
                m_nextUpdateFar = Time.time + kFarUpdateRate;                
            }
        }
        
        public void TriggerEnter(NetworkedObject obj, SensorDistance distance)
        {
            Observer observer;
            if (m_observers.TryGetValue(obj.Peer.ID, out observer))
            {
                var newFlag = observer.Flags.SetFlag(distance);
                m_observers[obj.Peer.ID] = new Observer
                {
                    Flags = newFlag,
                    Object = obj
                };
            }
            else
            {
                m_observers.Add(obj.Peer.ID, new Observer
                {
                    Flags = distance,
                    Object = obj
                });
                
                // SEND SPAWN PACKET
                m_buffer.AddEntityHeader(m_netObj.Peer, OpCodes.Spawn);
                m_buffer.AddInt((int) m_netObj.SpawnType);
                m_buffer.AddInitialState(m_netObj);
                var packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
                var command = GameCommandPool.GetGameCommand();
                command.Type = CommandType.Send;
                command.Target = obj.Peer;
                command.Channel = 1;
                command.Packet = packet;
                m_netObj.Network.AddCommandToQueue(command);
            }
            
            UpdateCounts();
        }
        
        public void TriggerExit(NetworkedObject obj, SensorDistance distance)
        {
            Observer observer;
            if (m_observers.TryGetValue(obj.Peer.ID, out observer))
            {
                var newFlag = observer.Flags.UnsetFlag(distance);
                if (newFlag == SensorDistance.None)
                {
                    m_observers.Remove(obj.Peer.ID);
                    
                    // SEND DESTROY PACKET
                    m_buffer.AddEntityHeader(m_netObj.Peer, OpCodes.Destroy);
                    var packet = m_buffer.GetPacketFromBuffer(PacketFlags.Reliable);
                    var command = GameCommandPool.GetGameCommand();
                    command.Type = CommandType.Send;
                    command.Target = obj.Peer;
                    command.Channel = 1;
                    command.Packet = packet;
                    m_netObj.Network.AddCommandToQueue(command);
                }
                else
                {
                    m_observers[obj.Peer.ID] = new Observer
                    {
                        Flags = newFlag,
                        Object = obj
                    };                    
                }
            }
            
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            int nearCount = 0;
            int farCount = 0;
            
            foreach (var kvp in m_observers)
            {
                if (kvp.Value.Flags.HasFlag(SensorDistance.Near))
                {
                    nearCount += 1;
                }
                else if (kvp.Value.Flags.HasFlag(SensorDistance.Far))
                {
                    farCount += 1;
                }
            }

            NNear = nearCount;
            NFar = farCount;
        }
    }
}