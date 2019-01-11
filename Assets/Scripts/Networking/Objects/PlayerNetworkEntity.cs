using ENet;
using SoL.Networking.Replication;
using TMPro;
using UnityEngine;

namespace SoL.Networking.Objects
{
    public class PlayerNetworkEntity : DynamicNetworkEntity
    {
        [SerializeField] private GameObject m_camPos = null;
        [SerializeField] private TextMeshPro m_text = null;
        
        private PlayerReplication m_playerReplication = null;
        
        protected override void Update()
        {
            base.Update();
            UpdateLocal();
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
            
                AddStateUpdate(m_buffer);
            
                var packet = m_buffer.GetPacketFromBuffer(PacketFlags.None);
                var command = GameCommandPool.GetGameCommand();
                command.Packet = packet;
                command.Channel = 0;
                command.Source = NetworkId.Peer;
                command.Type = CommandType.Send;

                m_network.AddCommandToQueue(command);

                m_nextUpdate += m_updateRate;
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
        
        private void PlayerNameOnChanged(string obj)
        {
            m_text.SetText(m_playerReplication.PlayerName.Value);
        }
    }
}