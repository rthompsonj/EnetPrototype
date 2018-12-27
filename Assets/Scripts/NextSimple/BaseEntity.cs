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
        [SerializeField] private Renderer m_renderer;

        private float m_updateRate = 0.1f;
        private SynchronizedFloat m_randomValue = new SynchronizedFloat();
        
        public bool HasPeer = false;
        
        public uint Id { get; private set; }
        public Peer Peer { get; private set; }

        private Vector3 GetRandomPos()
        {
            return new Vector3(
                Random.Range(-1f, 1f) * SharedStuff.Instance.RandomRange,                
                Random.Range(-1f, 1f) * SharedStuff.Instance.RandomRange,
                Random.Range(-1f, 1f) * SharedStuff.Instance.RandomRange);            
        }

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

        public void AssumeOwnership()
        {
            m_isLocal = true;
            gameObject.name = $"{gameObject.name} OWNER";
            m_renderer.material = m_clientLocalMat;
            m_text.SetText("Local");
        }

        public Vector3? m_newPos = null;

        private float m_nextUpdate = 2f;

        void Start()
        {
            m_client = FindObjectOfType<ClientNetworkSystem>();
            m_server = FindObjectOfType<ServerNetworkSystem>();
            m_randomValue.Changed += Changed;
        }

        private void Changed(float value)
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

        void Update()
        {
            HasPeer = Peer.IsSet;
            
            if (m_newPos.HasValue)
            {
                gameObject.transform.position = Vector3.Lerp(gameObject.transform.position, m_newPos.Value, Time.deltaTime * 2f);
                if (Vector3.Distance(gameObject.transform.position, m_newPos.Value) < 0.1f)
                {
                    m_newPos = null;
                }
            }

            if (m_isLocal == false)
            {
                if (m_randomValue.Dirty && CanUpdate())
                {
                    m_nextUpdate += m_updateRate;

                    BitBuffer buffer = new BitBuffer(128);
                    buffer.AddUShort((ushort) Threaded.OpCodes.SyncUpdate).AddUInt(Peer.ID);
                    buffer = m_randomValue.PackVariable(buffer);
                    byte[] data = new byte[buffer.Length+4];
                    buffer.ToArray(data);
                    Packet packet = default(Packet);
                    packet.Create(data, PacketFlags.Reliable);
                    var command = new BaseNetworkSystem.GameCommand
                    {
                        Type = BaseNetworkSystem.GameCommand.CommandType.BroadcastAll,
                        Packet = packet,
                        Channel = 0
                    };
                    m_server.AddCommandToQueue(command);
                    
                    m_randomValue.Dirty = false;
                }
                return;   
            }

            if(m_newPos.HasValue == false)
            {
                m_newPos = GetRandomPos();
            }

            if (CanUpdate())
            {
                
                /*
                var h = HalfPrecision.Compress(gameObject.transform.eulerAngles.y);
                var pos = BoundedRange.Compress(gameObject.transform.position, SharedStuff.Instance.Range);
                byte[] data = new byte[16];
                BitBuffer buffer = new BitBuffer(128);
                buffer.AddInt((int) OpCodes.PositionUpdate).
                    AddUInt(Id).AddUInt(pos.x).AddUInt(pos.y).AddUInt(pos.z).AddUShort(h)
                    .ToArray(data);
                Packet packet = default(Packet);
                packet.Create(data);
                //Peer.Send(0, ref packet);                
                
                var command = new BaseNetworkSystem.GameCommand
                {
                    Type = BaseNetworkSystem.GameCommand.CommandType.Send,
                    Packet = packet,
                    Channel = 0
                };
                */

                var command = PackerUnpacker.GetPositionUpdate(Threaded.OpCodes.PositionUpdate, Peer.ID, gameObject, SharedStuff.Instance.Range, 0);
                m_client.AddCommandToQueue(command);
                
                m_nextUpdate = Time.time + 0.1f;
            }
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