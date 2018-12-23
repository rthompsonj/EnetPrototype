using ENet;
using UnityEngine;

namespace NextSimple
{
    public class BaseEntity : MonoBehaviour
    {        
        public uint Id { get; private set; }
        public Peer Peer { get; private set; }

        public void Initialize(Peer peer, uint id)
        {
            Peer = peer;
            Id = id;
            gameObject.transform.position = new Vector3(
                Random.Range(-1f, 1f) * SharedStuff.Instance.RandomRange,                
                Random.Range(-1f, 1f) * SharedStuff.Instance.RandomRange,
                Random.Range(-1f, 1f) * SharedStuff.Instance.RandomRange);
            gameObject.name = $"{Id} (SERVER)";
        }

        public void Initialize(uint id, Vector3 pos, uint clientId)
        {
            Id = id;
            gameObject.transform.position = pos;
            gameObject.name = $"{Id} (CLIENT {clientId})";
        }
    }
}