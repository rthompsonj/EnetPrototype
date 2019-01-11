using NetStack.Serialization;
using SoL.Networking.Managers;
using SoL.Networking.Replication;
using TMPro;
using UnityEngine;

namespace SoL.Networking.Objects
{
    public class PlayerObject : NetworkEntity
    {
        [SerializeField] private GameObject m_camPos = null;
        [SerializeField] private TextMeshPro m_text = null;

        private Vector4? m_newData = null;
        private PlayerReplication m_playerReplication = null;

        protected void Update()
        {
            LerpPositionRotation();
            UpdateLocal();
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

        protected override void InitReplicationLayer()
        {
            base.InitReplicationLayer();
            if (m_replicationLayer != null)
            {
                m_playerReplication = m_replicationLayer as PlayerReplication;   
            }
        }

        protected override void OnStartServer()
        {
            gameObject.name = $"{NetworkId.Value} (SERVER)";
            m_playerReplication.PlayerName.Value = NetworkId.Value.ToString();
        }

        protected override void OnStartClient()
        {
            gameObject.name = $"{NetworkId.Value} (CLIENT)";
        }

        protected override void OnStartLocalClient()
        {
            gameObject.name = $"{NetworkId.Value} (LOCAL)";
            Camera.main.gameObject.transform.SetParent(m_camPos.transform);
            Camera.main.gameObject.transform.localPosition = Vector3.zero;
            Camera.main.gameObject.transform.localRotation = Quaternion.identity;
        }
        
        protected override void ProcessPacketInternal(OpCodes op, BitBuffer inBuffer)
        {
            base.ProcessPacketInternal(op, inBuffer);
            switch (op)
            {
                case OpCodes.StateUpdate:
                    if (m_isLocal)
                    {
                        Debug.LogWarning("Receiving PositionUpdate for myself??");
                        break;   
                    }
                    
                    var pos = inBuffer.ReadVector3(BaseNetworkSystem.Range);
                    var rot = Quaternion.Euler(new Vector3(0f, inBuffer.ReadFloat(), 0f));

                    if (m_isServer)
                    {
                        gameObject.transform.SetPositionAndRotation(pos, rot);
                    }
                    else
                    {
                        m_newData = new Vector4(pos.x, pos.y, pos.z, rot.eulerAngles.y);   
                    }
                    break;
                
                default:
                    Debug.LogWarning($"Received OpCode {op}!  Nothing to do with it...");
                    break;                    
            }
        }
        
        private void PlayerNameOnChanged(string obj)
        {
            m_text.SetText(m_playerReplication.PlayerName.Value);
        }
        
        private void LerpPositionRotation()
        {
            if (m_newData.HasValue)
            {
                var targetPos = Vector3.Lerp(gameObject.transform.position, m_newData.Value, Time.deltaTime * 2f);
                var targetRot = Quaternion.Euler(new Vector3(0f, m_newData.Value.w, 0f));
                gameObject.transform.SetPositionAndRotation(targetPos, targetRot);
                if (Vector3.Distance(gameObject.transform.position, m_newData.Value) < 0.1f)
                {
                    m_newData = null;
                }
            }
        }
        
        private void UpdateLocal()
        {
            if (m_isLocal == false)
                return;

            var forward = Input.GetKey(KeyCode.W) ? 1f : 0f;
            var backward = Input.GetKey(KeyCode.S) ? -1f : 0f;
            var left = Input.GetKey(KeyCode.A) ? -1f : 0f;
            var right = Input.GetKey(KeyCode.D) ? 1f : 0f;

            var rotateLeft = Input.GetKey(KeyCode.Q) ? -1f : 0f;
            var rotateRight = Input.GetKey(KeyCode.E) ? 1f : 0f;

            Vector2 movement = new Vector2(left + right, forward + backward) * Time.deltaTime * 10f;
            
            var rotation = rotateLeft + rotateRight;
            rotation *= Time.deltaTime * 50f;

            if (rotation != 0f)
            {
                gameObject.transform.RotateAround(gameObject.transform.position, Vector3.up, rotation);
            }
            
            if (movement != Vector2.zero)
            {
                gameObject.transform.Translate(movement.x, 0f, movement.y, Space.Self);
            }

            if (Time.time > m_nextUpdate)
            {
                m_buffer.AddEntityHeader(this, OpCodes.StateUpdate);
                m_buffer.AddVector3(gameObject.transform.position, BaseNetworkSystem.Range);
                m_buffer.AddFloat(gameObject.transform.eulerAngles.y);

                var command = GameCommandPool.GetGameCommand();
                command.Type = CommandType.Send;
                command.Packet = m_buffer.GetPacketFromBuffer();
                command.Channel = 0;

                m_network.AddCommandToQueue(command);

                m_nextUpdate += m_updateRate;
            }
        }
    }
}