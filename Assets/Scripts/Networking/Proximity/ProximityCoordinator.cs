using System.Collections.Generic;
using SoL.Networking.Objects;
using UnityEngine;

namespace SoL.Networking.Proximity
{
    public class ProximityCoordinator : MonoBehaviour
    {
        private const float kNearUpdateRate = 1f/10f;
        private const float kFarUpdateRate = 1f/5f;
        
        private readonly HashSet<NetworkedObject> m_nearSet = new HashSet<NetworkedObject>();
        private readonly HashSet<NetworkedObject> m_farSet = new HashSet<NetworkedObject>();

        private float m_nextUpdateNear = 0f;
        private float m_nextUpdateFar = 0f;

        public int NNear = 0;
        public int NFar = 0;
        
        void Awake()
        {
            m_nextUpdateNear = Time.time + kNearUpdateRate;
            m_nextUpdateFar = Time.time + kFarUpdateRate;
        }

        public int GetUpdateCount()
        {
            int cnt = 0;

            if (Time.time >= m_nextUpdateNear)
            {
                cnt += m_nearSet.Count;
            }

            if (Time.time >= m_nextUpdateFar)
            {
                cnt += m_farSet.Count;
            }

            return cnt;
        }

        public IEnumerable<NetworkedObject> GetUpdates()
        {
            if (Time.time >= m_nextUpdateNear)
            {
                foreach (var no in m_nearSet)
                {
                    yield return no;
                }
                m_nextUpdateNear = Time.time + kNearUpdateRate;
            }

            if (Time.time >= m_nextUpdateFar)
            {
                foreach (var no in m_farSet)
                {
                    yield return no;
                }
                m_nextUpdateFar = Time.time + kFarUpdateRate;
            }
        }
        
        public void TriggerEnter(NetworkedObject obj, SensorDistance distance)
        {
            switch (distance)
            {
                case SensorDistance.Near:
                    m_farSet.Remove(obj);
                    m_nearSet.Add(obj);
                    break;
                
                case SensorDistance.Far:
                    if (m_nearSet.Contains(obj) == false)
                    {
                        m_farSet.Add(obj);
                    }
                    break;
            }
            
            UpdateCounts();
        }
        
        public void TriggerExit(NetworkedObject obj, SensorDistance distance)
        {
            switch (distance)
            {
                case SensorDistance.Near:
                    m_farSet.Add(obj);
                    m_nearSet.Remove(obj);
                    break;
                
                case SensorDistance.Far:
                    m_farSet.Remove(obj);
                    m_nearSet.Remove(obj);
                    break;
            }
            
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            NNear = m_nearSet.Count;
            NFar = m_farSet.Count;
        }
    }
}