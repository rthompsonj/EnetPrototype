using NetStack.Serialization;
using SoL.Networking.Managers;
using SoL.Networking.Replication;
using Threaded;
using TMPro;
using UnityEngine;

namespace SoL.Networking.Objects
{
    public class NetworkedCube : NetworkedObject
    {
        [SerializeField] private Renderer m_renderer = null;
        [SerializeField] private Material m_serverMat = null;
        [SerializeField] private Material m_remoteMat = null;
        [SerializeField] private Material m_localMat = null;

        [SerializeField] private TextMeshPro m_text = null;

        [SerializeField] private Transform m_toRotate = null;

        private PlayerReplication m_playerReplication = null;

        protected override IReplicationLayer m_replicationLayer => m_playerReplication;

        private Vector4? m_newData = null;

        protected override void Update()
        {
            base.Update();
            LerpPositionRotation();
            UpdateLocal();
        }
        
        private void OnMouseDown()
        {
            if (m_isServer)
            {
                m_playerReplication.PlayerName.Value = System.Guid.NewGuid().ToString();                
            }
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

        public override BitBuffer AddInitialState(BitBuffer outBuffer)
        {
            outBuffer = base.AddInitialState(outBuffer);
            outBuffer.AddVector3(gameObject.transform.position, BaseNetworkSystem.Range);
            outBuffer.AddFloat(m_toRotate.eulerAngles.y);
            return outBuffer;
        }

        protected override void ReadInitialState(BitBuffer initBuffer)
        {
            base.ReadInitialState(initBuffer);
            var pos = initBuffer.ReadVector3(BaseNetworkSystem.Range);
            var rot = Quaternion.Euler(new Vector3(0f, initBuffer.ReadFloat(), 0f));
            gameObject.transform.position = pos;
            m_toRotate.rotation = rot;
        }

        protected override void OnStartServer()
        {
            m_renderer.material = m_serverMat;
            gameObject.name = $"{ID} (SERVER)";
        }

        protected override void OnStartClient()
        {
            m_renderer.material = m_remoteMat;            
            gameObject.name = $"{ID} (CLIENT)";
        }

        protected override void OnStartLocalClient()
        {
            m_renderer.material = m_localMat;
            gameObject.name = $"{ID} (LOCAL)";
        }
        
        private void LerpPositionRotation()
        {
            if (m_newData.HasValue)
            {
                var targetRot = Quaternion.Euler(new Vector3(0f, m_newData.Value.w, 0f));
                m_toRotate.rotation = Quaternion.Lerp(m_toRotate.rotation, targetRot, Time.deltaTime * 2f);
                gameObject.transform.position = Vector3.Lerp(gameObject.transform.position, m_newData.Value, Time.deltaTime * 2f);
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
            
            if (m_newData.HasValue == false)
            {
                m_newData = new Vector4(
                    Random.Range(-1f, 1f) * BaseNetworkSystem.kMaxRange,
                    Random.Range(-1f, 1f) * BaseNetworkSystem.kMaxRange,
                    Random.Range(-1f, 1f) * BaseNetworkSystem.kMaxRange,
                    Random.Range(0, 1f) * 360f);
            }

            if (CanUpdate())
            {
                m_buffer.AddEntityHeader(ClientNetworkSystem.MyPeer, OpCodes.PositionUpdate);
                m_buffer.AddVector3(gameObject.transform.position, BaseNetworkSystem.Range);
                m_buffer.AddFloat(m_toRotate.eulerAngles.y);

                var command = GameCommandPool.GetGameCommand();
                command.Type = CommandType.Send;
                command.Packet = m_buffer.GetPacketFromBuffer();
                command.Channel = 0;

                m_network.AddCommandToQueue(command);

                m_nextUpdate += m_updateRate;
            }
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
                    m_newData = new Vector4(pos.x, pos.y, pos.z, rot);
                    break;
                
                default:
                    Debug.LogWarning($"Received OpCode {op}!  Nothing to do with it...");
                    break;                    
            }
        }
    }
}