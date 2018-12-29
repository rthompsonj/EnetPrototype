using System.Collections.Generic;
using UnityEngine;

namespace Threaded
{
    public abstract class NetworkedEntity : MonoBehaviour
    {
        protected readonly List<ISynchronizedVariable> m_syncs = new List<ISynchronizedVariable>();

        protected virtual void Awake()
        {
            RegisterSyncs();
        }
        
        protected virtual int RegisterSyncs()
        {
            return 0;
        }
    }
}