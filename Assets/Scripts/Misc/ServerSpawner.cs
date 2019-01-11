using System.Collections.Generic;
using ENet;
using SoL.Networking.Managers;
using SoL.Networking.Objects;
using UnityEngine;

namespace Networking
{
    public class ServerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject m_toSpawn = null;
        
        private readonly List<NetworkEntity> m_spawns = new List<NetworkEntity>();

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && m_toSpawn != null)
            {
                var go = Instantiate(m_toSpawn);
                var netEntity = go.GetComponent<NetworkEntity>();
                if (netEntity != null)
                {
                    netEntity.ServerInit(BaseNetworkSystem.Instance, default(Peer));
                    m_spawns.Add(netEntity);
                }
            }

            if (Input.GetKeyDown(KeyCode.Backspace) && m_spawns.Count > 0)
            {
                var index = m_spawns.Count - 1;
                var toDelete = m_spawns[index];
                if (toDelete != null)
                {
                    m_spawns.RemoveAt(index);
                    Destroy(toDelete.gameObject);
                }
            }
        }
    }
}