using Misc;
using SoL.Networking.Managers;
using SoL.Networking.Replication;
using TMPro;
using UnityEngine;

namespace SoL.Networking.Objects
{
    public class NPCNetworkEntity : DynamicNetworkEntity
    {
        #region SERIALIZED_VARS

        [SerializeField] private SpawnType m_spawnType = SpawnType.Cube;
        [SerializeField] private Renderer m_renderer = null;
        [SerializeField] private Material m_serverMat = null;
        [SerializeField] private Material m_remoteMat = null;
        [SerializeField] private Material m_localMat = null;

        [SerializeField] private TextMeshPro m_text = null;
        
        #endregion
        
        private PlayerReplication m_playerReplication = null;
        
        #region MONO
        
        protected override void Update()
        {
            base.Update();
            UpdateLocal();
        }
        
        private void OnMouseDown()
        {
            GenerateName();
        }

        [ContextMenu("Generate Name")]
        private void GenerateName()
        {
            if (m_isServer && m_playerReplication != null)
            {
                m_playerReplication.PlayerName.Value = System.Guid.NewGuid().ToString();                
            }            
        }
        
        #endregion

        #region OVERRIDES     
        
        protected override void InitReplicationLayer()
        {
            base.InitReplicationLayer();
            if (m_replicationLayer != null)
            {
                m_playerReplication = m_replicationLayer as PlayerReplication;   
            }
        }

        protected override void Subscribe()
        {
            base.Subscribe();
            if (m_playerReplication != null)
            {
                m_playerReplication.PlayerName.Changed += PlayerNameOnChanged;   
            }
        }

        protected override void Unsubscribe()
        {
            base.Unsubscribe();
            if (m_playerReplication != null)
            {
                m_playerReplication.PlayerName.Changed -= PlayerNameOnChanged;   
            }
        }

        protected override void OnStartServer()
        {
            SpawnType = m_spawnType;
            m_renderer.material = m_serverMat;
            gameObject.name = $"{NetworkId.Value} (SERVER)";
            m_text.SetText(NetworkId.Value.ToString());
        }

        protected override void OnStartClient()
        {
            m_renderer.material = m_remoteMat;            
            gameObject.name = $"{NetworkId.Value} (CLIENT)";            
        }

        protected override void OnStartLocalClient()
        {
            m_renderer.material = m_localMat;
            gameObject.name = $"{NetworkId.Value} (LOCAL)";
        }
        
        #endregion
        
        private void PlayerNameOnChanged(string obj)
        {
            if (obj.Length > 5)
            {
                var newName = m_playerReplication.PlayerName.Value.Substring(0, 5);
                m_text.SetText(newName);
            }
            else
            {
                m_text.SetText("PLAYER");
            }
        }
        
        private void UpdateLocal()
        {
            if (m_isServer == false)
                return;
            
            if (m_targetPosRot.HasValue == false)
            {
                Vector3 randomPos = new Vector3(
                    Random.Range(-1f, 1f) * 10f,
                    0f,
                    Random.Range(-1f, 1f) * 10f                    
                );
                Vector3 newPos = gameObject.transform.position + randomPos;

                newPos.x = Mathf.Clamp(newPos.x, -BaseNetworkSystem.kMaxRange, BaseNetworkSystem.kMaxRange);
                newPos.z = Mathf.Clamp(newPos.z, -BaseNetworkSystem.kMaxRange, BaseNetworkSystem.kMaxRange);

                m_targetPosRot = new Vector4(newPos.x, newPos.y, newPos.z, Random.Range(0f, 1f) * 360f);
            }
        }
    }
}