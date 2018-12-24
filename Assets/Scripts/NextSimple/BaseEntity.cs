using System.Runtime.Serialization;
using ENet;
using NetStack.Compression;
using NetStack.Serialization;
using Threaded;
using UnityEngine;

namespace NextSimple
{
    public class BaseEntity : MonoBehaviour
    {
        private bool m_isServer = false;
        private bool m_isLocal = false;

        private ClientNetworkSystem m_client = null;

        [SerializeField] private Material m_clientRemoteMat = null;
        [SerializeField] private Material m_clientLocalMat = null;
        [SerializeField] private Material m_serverMat = null;
        [SerializeField] private Renderer m_renderer;

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
        }

        public void Initialize(uint id, Vector3 pos, Peer peer)
        {
            Peer = peer;
            Id = id;
            gameObject.transform.position = pos;
            gameObject.name = $"{Id} (CLIENT)";
            m_renderer.material = m_clientRemoteMat;
        }

        public void AssumeOwnership()
        {
            m_isLocal = true;
            gameObject.name = $"{gameObject.name} OWNER";
            m_renderer.material = m_clientLocalMat;
        }

        public Vector3? m_newPos = null;

        private float m_nextUpdate = 2f;

        void Start()
        {
            m_client = FindObjectOfType<ClientNetworkSystem>();
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
                return;

            if(m_newPos.HasValue == false)
            {
                m_newPos = GetRandomPos();
            }

            if (Time.time > m_nextUpdate)
            {
                var pos = BoundedRange.Compress(gameObject.transform.position, SharedStuff.Instance.Range);
                byte[] data = new byte[16];
                BitBuffer buffer = new BitBuffer(128);
                buffer.AddInt((int) OpCodes.PositionUpdate).
                    AddUInt(Id).AddUInt(pos.x).AddUInt(pos.y).AddUInt(pos.z)
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

                m_client.AddCommandToQueue(command);
                
                m_nextUpdate = Time.time + 0.1f;
            }
        }
    }
}