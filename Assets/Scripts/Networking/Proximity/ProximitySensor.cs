using SoL.Networking.Objects;
using UnityEngine;

namespace SoL.Networking.Proximity
{
    public class ProximitySensor : MonoBehaviour
    {
        private const string kLayerName = "Proximity";
        
        [SerializeField] private SensorBand m_band = SensorBand.None;
        private Collider m_collider = null;
        
        public SensorBand SensorBand => m_band;
        public NetworkEntity NetworkEntity { private get; set; }
        public bool CanUpdate { get; private set; }
        private float m_timeOfNextUpdate = 0f;

        public void SetUpdateFlag()
        {
            if (m_band == SensorBand.None)
                return;
            
            CanUpdate = Time.time > m_timeOfNextUpdate;

            if (CanUpdate)
            {
                m_timeOfNextUpdate = Time.time + m_band.GetUpdateTime();
            }
        }

        #region MONO
        
        /// <summary>
        /// Check for collider and turn it off until we are initialized.
        /// </summary>
        private void Awake()
        {
            m_collider = gameObject.GetComponent<Collider>();
            if (m_collider == null)
            {
                Debug.LogWarning($"No collider on {gameObject.name}!");
                Destroy(gameObject);
                return;
            }
            m_collider.enabled = false;
            m_collider.isTrigger = true;
            gameObject.layer = LayerMask.NameToLayer(kLayerName);
        }

        /// <summary>
        /// Should be initialized so lets turn the collider back on.
        /// </summary>
        private void Start()
        {
            m_collider.enabled = true;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (NetworkEntity == null)
                return;
            
            var obj = other.gameObject.GetComponent<NetworkEntity>();
            if (obj != null)
            {
                NetworkEntity.ProximitySensorEnter(this, obj);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (NetworkEntity == null)
                return;
            
            var obj = other.gameObject.GetComponent<NetworkEntity>();
            if (obj != null)
            {
                NetworkEntity.ProximitySensorExit(this, obj);
            }
        }
        
        #endregion
    }
}