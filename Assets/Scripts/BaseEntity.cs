using System.Collections.Generic;
using System.Text;
using ENet;
using NetStack.Serialization;
using Threaded;
using TMPro;
using UnityEngine;

namespace NextSimple
{
    public class BaseEntity : MonoBehaviour
    {
        private static readonly int[] BitFlags = new int[]
        {
            1 << 0,
            1 << 1,
            1 << 2,
            1 << 3,
            1 << 4,
            1 << 5,
            1 << 6,
            1 << 7,
            1 << 8,
            1 << 9,
            1 << 10,
            1 << 11,
            1 << 12,
        };

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
        private readonly BitBuffer m_buffer = new BitBuffer(128);
        private float m_nextUpdate = 0f;

        private readonly SynchronizedFloat m_randomValue = new SynchronizedFloat();
        private readonly SynchronizedString m_stringValue1 = new SynchronizedString();
        private readonly SynchronizedASCII m_stringValue2 = new SynchronizedASCII();

        private readonly List<ISynchronizedVariable> m_syncs = new List<ISynchronizedVariable>();

        public Renderer Renderer => m_renderer;

        public Vector4? m_newPos = null;

        public uint Id { get; private set; }
        public Peer Peer { get; private set; }

        #region MONO

        void Awake()
        {
            m_syncs.Add(m_randomValue);
            m_syncs.Add(m_stringValue1);
            m_syncs.Add(m_stringValue2);

            m_randomValue.BitFlag = BitFlags[0];
            m_stringValue1.BitFlag = BitFlags[1];
            m_stringValue2.BitFlag = BitFlags[2];
        }

        void Start()
        {
            //TODO: don't actually do this in production; this is me being lazy
            m_client = FindObjectOfType<ClientNetworkSystem>();
            m_server = FindObjectOfType<ServerNetworkSystem>();
            m_randomValue.Changed += RandomValChanged;
            m_stringValue1.Changed += StringValChanged1;
            m_stringValue2.Changed += StringValChanged2;
        }

        void Update()
        {
            LerpPositionRotation();
            UpdateSyncVars();
            UpdateLocal();
        }

        private void OnMouseDown()
        {
            if (m_isServer)
            {
                GenRandomString1();
            }
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
            m_text.SetText(Id.ToString());
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
                m_renderer.gameObject.transform.rotation = Quaternion.Lerp(m_renderer.gameObject.transform.rotation,
                    targetRot, Time.deltaTime * 2f);
                gameObject.transform.position =
                    Vector3.Lerp(gameObject.transform.position, m_newPos.Value, Time.deltaTime * 2f);
                if (Vector3.Distance(gameObject.transform.position, m_newPos.Value) < 0.1f)
                {
                    m_newPos = null;
                }
            }
        }

        private void UpdateSyncVars()
        {
            if (m_isServer == false || CanUpdate() == false)
                return;

            m_nextUpdate += m_updateRate;

            int dirtyBits = 0;

            for (int i = 0; i < m_syncs.Count; i++)
            {
                if (m_syncs[i].Dirty)
                {
                    dirtyBits = dirtyBits | m_syncs[i].BitFlag;
                }
            }

            if (dirtyBits == 0)
                return;

            var buffer = m_buffer;
            buffer.AddEntityHeader(Peer, OpCodes.SyncUpdate);
            buffer.AddInt(dirtyBits);

            for (int i = 0; i < m_syncs.Count; i++)
            {
                if (m_syncs[i].Dirty)
                {
                    buffer.AddSyncVar(m_syncs[i]);
                }
            }

            var packet = buffer.GetPacketFromBuffer(PacketFlags.Reliable);
            var command = GameCommandPool.GetGameCommand();
            command.Type = CommandType.BroadcastAll;
            command.Packet = packet;
            command.Channel = 0;

            Debug.Log($"Sending dirtyBits: {dirtyBits}  Length: {packet.Length}");

            m_server.AddCommandToQueue(command);
        }

        private void UpdateLocal()
        {
            if (m_isLocal == false)
                return;

            if (m_newPos.HasValue == false)
            {
                m_newPos = GetRandomPos();
            }

            if (CanUpdate())
            {
                m_buffer.AddEntityHeader(Peer, OpCodes.PositionUpdate);
                m_buffer.AddVector3(gameObject.transform.position, SharedStuff.Instance.Range);
                m_buffer.AddFloat(m_renderer.gameObject.transform.eulerAngles.y);

                var command = GameCommandPool.GetGameCommand();
                command.Type = CommandType.Send;
                command.Packet = m_buffer.GetPacketFromBuffer();
                command.Channel = 0;

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
                Debug.Log($"RandomVal Changed to {value}!");
                m_randomValue.Value = value;
            }
        }

        private void StringValChanged1(string value)
        {
            if (m_isServer == false)
            {
                Debug.Log($"StringVal1 Changed to {value}!");
                m_stringValue1.Value = value;
                m_text.SetText(m_stringValue1.Value);
            }
        }

        private void StringValChanged2(string value)
        {
            if (m_isServer == false)
            {
                Debug.Log($"StringVal2 Changed to {value}!");
                m_stringValue2.Value = value;
            }
        }

        public void ProcessSyncUpdate(BitBuffer buffer)
        {
            int dirtyBits = buffer.ReadInt();

            Debug.Log($"Received dirtyBits: {dirtyBits}");

            if (dirtyBits == 0)
                return;

            for (int i = 0; i < m_syncs.Count; i++)
            {
                if ((dirtyBits & m_syncs[i].BitFlag) == m_syncs[i].BitFlag)
                {
                    m_syncs[i].ReadVariable(buffer);
                }
            }
        }

        private bool CanUpdate()
        {
            return Peer.IsSet && Time.time > m_nextUpdate;
        }

        #region TEMPORARY_FOR_TESTING

        [ContextMenu("Generate Random Value")]
        private void GenRandomVal()
        {
            if (m_isServer == false)
                return;
            float prev = m_randomValue.Value;
            m_randomValue.Value = Random.Range(0f, 1f);
            Debug.Log($"Generated from {prev} to {m_randomValue.Value}");
        }

        public int stringIterations = 10;

        [ContextMenu("Generate Random String1")]
        private void GenRandomString1()
        {
            if (m_isServer == false)
                return;
            string prev = m_stringValue1.Value;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < stringIterations; i++)
            {
                sb.Append(System.Guid.NewGuid().ToString());
            }

            m_stringValue1.Value = sb.ToString();
            Debug.Log($"Generated from {prev} to {m_stringValue1.Value}");
        }

        [ContextMenu("Generate Random String2")]
        private void GenRandomString2()
        {
            if (m_isServer == false)
                return;
            string prev = m_stringValue2.Value;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < stringIterations; i++)
            {
                sb.Append(System.Guid.NewGuid().ToString());
            }

            m_stringValue2.Value = sb.ToString();
            Debug.Log($"Generated from {prev} to {m_stringValue2.Value}");
        }

        [ContextMenu("Generate Random Value & String")]
        private void GenRandomValStrings()
        {
            GenRandomVal();
            GenRandomString1();
            GenRandomString2();
        }


        #endregion
    }
}