using Misc;
using NetStack.Serialization;
using SoL.Networking.Managers;
using SoL.Networking.Replication;
using TMPro;
using UnityEngine;

namespace SoL.Networking.Objects
{
    public class NetworkedCube : NetworkEntity
    {
        #region SERIALIZED_VARS

        [SerializeField] private SpawnType m_spawnType = SpawnType.Cube;
        [SerializeField] private Renderer m_renderer = null;
        [SerializeField] private Material m_serverMat = null;
        [SerializeField] private Material m_remoteMat = null;
        [SerializeField] private Material m_localMat = null;

        [SerializeField] private TextMeshPro m_text = null;

        [SerializeField] private Transform m_toRotate = null;
        
        #endregion

        private PlayerReplication m_playerReplication = null;
        private Vector4? m_newData = null;

        #region MONO
        
        private void Update()
        {
            LerpPositionRotation();
            UpdateLocal();
        }
        
        private void OnMouseDown()
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
        
        protected override void ProcessPacketInternal(OpCodes op, BitBuffer buffer)
        {
            base.ProcessPacketInternal(op, buffer);
            switch (op)
            {
                case OpCodes.StateUpdate:
                    if (m_isLocal)
                    {
                        Debug.LogWarning("Receiving PositionUpdate for myself??");
                        break;   
                    }
                    
                    var pos = buffer.ReadVector3(BaseNetworkSystem.Range);
                    var rot = buffer.ReadFloat();

                    if (m_isServer)
                    {
                        gameObject.transform.position = pos;
                        m_toRotate.transform.rotation = Quaternion.Euler(new Vector3(0f, rot, 0f));
                    }
                    else
                    {
                        m_newData = new Vector4(pos.x, pos.y, pos.z, rot);   
                    }
                    break;
                
                default:
                    Debug.LogWarning($"Received OpCode {op}!  Nothing to do with it...");
                    break;                    
            }
        }
        
        #endregion

        private void PlayerNameOnChanged(string obj)
        {
            if (obj.Length > 5)
            {
                m_text.SetText(m_playerReplication.PlayerName.Value.Substring(0, 5));
            }
            else
            {
                m_text.SetText("PLAYER");
            }
        }
        
        private void LerpPositionRotation()
        {
            if (m_newData.HasValue)
            {
                var targetRot = Quaternion.Euler(new Vector3(0f, m_newData.Value.w, 0f));
                m_toRotate.rotation = Quaternion.Lerp(m_toRotate.rotation, targetRot, Time.deltaTime * 2f);
                gameObject.transform.position = Vector3.Lerp(gameObject.transform.position, m_newData.Value, Time.deltaTime);
                if (Vector3.Distance(gameObject.transform.position, m_newData.Value) < 0.1f)
                {
                    m_newData = null;
                }
            }
        }

        private void UpdateLocal()
        {
            if (m_isServer == false)
                return;
            
            if (m_newData.HasValue == false)
            {
                Vector3 randomPos = new Vector3(
                    Random.Range(-1f, 1f) * 10f,
                    0f,
                    Random.Range(-1f, 1f) * 10f                    
                );
                Vector3 newPos = gameObject.transform.position + randomPos;

                newPos.x = Mathf.Clamp(newPos.x, -BaseNetworkSystem.kMaxRange, BaseNetworkSystem.kMaxRange);
                newPos.z = Mathf.Clamp(newPos.z, -BaseNetworkSystem.kMaxRange, BaseNetworkSystem.kMaxRange);

                m_newData = new Vector4(newPos.x, newPos.y, newPos.z, Random.Range(0f, 1f) * 360f);
            }

            /*  NO LONGER A PEER
            if (Time.time > m_nextUpdate)
            {
                m_buffer.AddEntityHeader(ClientNetworkSystem.MyPeer, OpCodes.StateUpdate);
                m_buffer.AddVector3(gameObject.transform.position, BaseNetworkSystem.Range);
                m_buffer.AddFloat(m_toRotate.eulerAngles.y);

                var command = GameCommandPool.GetGameCommand();
                command.Type = CommandType.Send;
                command.Packet = m_buffer.GetPacketFromBuffer();
                command.Channel = 0;

                m_network.AddCommandToQueue(command);

                m_nextUpdate += m_updateRate;
            }
            */
        }
    }
}