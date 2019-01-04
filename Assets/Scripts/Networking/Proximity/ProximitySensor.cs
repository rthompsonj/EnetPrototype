using System;
using SoL.Networking.Objects;
using UnityEngine;

namespace SoL.Networking.Proximity
{
    [Flags]
    public enum SensorDistance
    {
        None = 0,
        Near = 1 << 0,        
        Far  = 1 << 1
    }

    public static class SensorDistanceExtensions
    {
        public static SensorDistance SetFlag(this SensorDistance a, SensorDistance b)
        {
            return a | b;
        }

        public static SensorDistance UnsetFlag(this SensorDistance a, SensorDistance b)
        {
            return a & (~b);
        }

        public static bool HasFlag(this SensorDistance a, SensorDistance b)
        {
            return (a & b) == b;
        }
    }
    
    public class ProximitySensor : MonoBehaviour
    {
        [SerializeField] private SensorDistance m_distance = SensorDistance.Near;
        [SerializeField] private ProximityCoordinator m_coordinator = null;
        
        private void OnTriggerEnter(Collider other)
        {
            if (m_coordinator == null)
                return;
            
            var obj = other.gameObject.GetComponent<NetworkedObject>();
            if (obj != null)
            {
                m_coordinator.TriggerEnter(obj, m_distance);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (m_coordinator == null)
                return;
            
            var obj = other.gameObject.GetComponent<NetworkedObject>();
            if (obj != null)
            {
                m_coordinator.TriggerExit(obj, m_distance);
            }
        }
    }
}