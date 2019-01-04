using NetStack.Serialization;
using SoL.Networking.Managers;
using SoL.Networking.Replication;
using TMPro;
using UnityEngine;

namespace SoL.Networking.Objects
{
    public class PlayerObject : NetworkedObject
    {
        [SerializeField] private GameObject m_camPos = null;
        [SerializeField] private TextMeshPro m_text = null;
        private PlayerReplication m_playerReplication = null;
        private Vector4? m_newData = null;
        protected override IReplicationLayer m_replicationLayer => m_playerReplication;

        protected override void Update()
        {
            base.Update();
            LerpPositionRotation();
            UpdateLocal();
        }
        
        protected override void AddLayers()
        {
            m_playerReplication = new PlayerReplication();
        }
        
        protected override void Subscribe()
        {
            base.Subscribe();
            m_playerReplication.PlayerName.Changed += PlayerNameOnChanged;
        }

        protected override void Unsubscribe()
        {
            base.Unsubscribe();            
            m_playerReplication.PlayerName.Changed -= PlayerNameOnChanged;
        }
        
        public override BitBuffer AddInitialState(BitBuffer outBuffer)
        {
            outBuffer = base.AddInitialState(outBuffer);
            outBuffer.AddVector3(gameObject.transform.position, BaseNetworkSystem.Range);
            outBuffer.AddFloat(gameObject.transform.eulerAngles.y);
            return outBuffer;
        }

        protected override void ReadInitialState(BitBuffer initBuffer)
        {
            base.ReadInitialState(initBuffer);
            var pos = initBuffer.ReadVector3(BaseNetworkSystem.Range);
            var rot = Quaternion.Euler(new Vector3(0f, initBuffer.ReadFloat(), 0f));
            gameObject.transform.SetPositionAndRotation(pos, rot);
        }
        
        protected override void OnStartServer()
        {
            gameObject.name = $"{ID} (SERVER)";
            m_playerReplication.PlayerName.Value = ID.ToString();
        }

        protected override void OnStartClient()
        {
            gameObject.name = $"{ID} (CLIENT)";
        }

        protected override void OnStartLocalClient()
        {
            gameObject.name = $"{ID} (LOCAL)";
            Camera.main.gameObject.transform.SetParent(m_camPos.transform);
            Camera.main.gameObject.transform.localPosition = Vector3.zero;
            Camera.main.gameObject.transform.localRotation = Quaternion.identity;
        }
        
        protected override void ProcessPacketInternal(OpCodes op, BitBuffer buffer)
        {
            base.ProcessPacketInternal(op, buffer);
            switch (op)
            {
                case OpCodes.PositionUpdate:
                    if (m_isLocal)
                    {
                        Debug.LogWarning("Receiving PositionUpdate for myself??");
                        break;   
                    }
                    
                    var pos = buffer.ReadVector3(BaseNetworkSystem.Range);
                    var rot = buffer.ReadFloat();

                    if (m_isServer)
                    {
                        gameObject.transform.SetPositionAndRotation(pos, Quaternion.Euler(new Vector3(0f, rot, 0f)));
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

            if (CanUpdate())
            {
                m_buffer.AddEntityHeader(ClientNetworkSystem.MyPeer, OpCodes.PositionUpdate);
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