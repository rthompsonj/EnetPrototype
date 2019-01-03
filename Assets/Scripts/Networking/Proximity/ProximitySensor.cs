using SoL.Networking.Objects;
using UnityEngine;

namespace SoL.Networking.Proximity
{
    public enum SensorDistance
    {
        Near,
        Far
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