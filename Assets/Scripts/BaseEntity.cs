using ENet;
using NetStack.Serialization;
using Threaded;
using TMPro;
using UnityEngine;

namespace NextSimple
{
    public class BaseEntity : MonoBehaviour
    {
        private bool m_isServer = false;
        private bool m_isLocal = false;

        private ClientNetworkSystem m_client = null;
        private ServerNetworkSystem m_server = null;

        [SerializeField] private TextMeshPro m_text = null;
        [SerializeField] private Material m_clientRemoteMat = null;
        [SerializeField] private Material m_clientLocalMat = null;
        [SerializeField] private Material m_serverMat = null;
        [SerializeField] private Renderer m_renderer = null;

        private float m_updateRate = 0.1f;
        private readonly SynchronizedFloat m_randomValue = new SynchronizedFloat();
        private readonly BitBuffer m_buffer = new BitBuffer(128);
        private float m_nextUpdate = 0f;

        public Renderer Renderer => m_renderer;
        
        public Vector4? m_newPos = null;
        
        public uint Id { get; private set; }
        public Peer Peer { get; private set; }
        
        #region MONO
        
        void Start()
        {
            //TODO: don't actually do this in production; this is me being lazy
            m_client = FindObjectOfType<ClientNetworkSystem>();
            m_server = FindObjectOfType<ServerNetworkSystem>();
            m_randomValue.Changed += RandomValChanged;
        }
        
        void Update()
        {
            LerpPositionRotation();
            UpdateSyncVars();
            UpdateLocal();
        }
        
        #endregion
        
        #region INIT
        
        public void Initialize(Peer peer, uint id)
        {
            Peer = peer;
            Id = id;
            gameObject.transform.position = GetRandomPos();
            gameObject.name = $"{Id} (SERVER)";
            m_renderer.material = m_serverMat;
            m_isServer = true;
        }

        public void Initialize(uint id, Vector3 pos, Peer peer)
        {
            Peer = peer;
            Id = id;
            gameObject.transform.position = pos;
            gameObject.name = $"{Id} (CLIENT)";
            m_renderer.material = m_clientRemoteMat;
            m_text.SetText("Remote");
        }
                
        #endregion
        
        #region UPDATES

        private void LerpPositionRotation()
        {
            if (m_newPos.HasValue)
            {
                var targetRot = Quaternion.Euler(new Vector3(0f, m_newPos.Value.w, 0f));
                m_renderer.gameObject.transform.rotation = Quaternion.Lerp(m_renderer.gameObject.transform.rotation, targetRot, Time.deltaTime * 2f);
                gameObject.transform.position = Vector3.Lerp(gameObject.transform.position, m_newPos.Value, Time.deltaTime * 2f);
                if (Vector3.Distance(gameObject.transform.position, m_newPos.Value) < 0.1f)
                {
                    m_newPos = null;
                }
            }
        }

        private void UpdateSyncVars()
        {
            if (m_isServer == false)
                return;
            
            if (m_randomValue.Dirty && CanUpdate())
            {
                m_nextUpdate += m_updateRate;

                BitBuffer buffer = m_buffer;
                buffer.AddEntityHeader(Peer, OpCodes.SyncUpdate);
                buffer.AddSyncVar(m_randomValue);
                Packet packet = buffer.GetPacketFromBuffer(PacketFlags.Reliable);
                var command = new BaseNetworkSystem.GameCommand
                {
                    Type = BaseNetworkSystem.GameCommand.CommandType.BroadcastAll,
                    Packet = packet,
                    Channel = 0
                };
                m_server.AddCommandToQueue(command);
                
                m_randomValue.Dirty = false;
            }
        }

        private void UpdateLocal()
        {
            if (m_isLocal == false)
                return;

            if(m_newPos.HasValue == false)
            {
                m_newPos = GetRandomPos();
            }

            if (CanUpdate())
            {
                m_buffer.AddEntityHeader(Peer, OpCodes.PositionUpdate);
                m_buffer.AddVector3(gameObject.transform.position, SharedStuff.Instance.Range);
                m_buffer.AddFloat(m_renderer.gameObject.transform.eulerAngles.y);
                
                
                var command = new BaseNetworkSystem.GameCommand
                {
                    Type = BaseNetworkSystem.GameCommand.CommandType.Send,
                    Packet = m_buffer.GetPacketFromBuffer(),
                    Channel = 0
                };

                m_client.AddCommandToQueue(command);
                
                m_nextUpdate = Time.time + 0.1f;
            }
        }
        
        #endregion
        

        private Vector4 GetRandomPos()
        {
            return new Vector4(
                Random.Range(-1f, 1f) * SharedStuff.Instance.RandomRange,
                Random.Range(-1f, 1f) * SharedStuff.Instance.RandomRange,
                Random.Range(-1f, 1f) * SharedStuff.Instance.RandomRange,
                Random.Range(0, 1f) * SharedStuff.Instance.RandomRange * 360f);
        }

        public void AssumeOwnership()
        {
            m_isLocal = true;
            gameObject.name = $"{gameObject.name} OWNER";
            m_renderer.material = m_clientLocalMat;
            m_text.SetText("Local");
        }

        private void RandomValChanged(float value)
        {
            if (m_isServer == false)
            {
                float prev = m_randomValue.Value;
                Debug.Log($"Value Changed from {prev} to {value}!");
                m_randomValue.Value = value;   
            }
        }

        public void ProcessSyncUpdate(BitBuffer buffer)
        {
            m_randomValue.ReadVariable(buffer);
        }

        private bool CanUpdate()
        {
            return Peer.IsSet && Time.time > m_nextUpdate;
        }

        [ContextMenu("Generate Random Value")]
        private void GenRandomVal()
        {
            if (m_isServer == false)
                return;
            float prev = m_randomValue.Value;
            m_randomValue.Value = Random.Range(0f, 1f);
            Debug.Log($"Generated from {prev} to {m_randomValue.Value}");
            m_randomValue.Dirty = true;
        }
    }
}