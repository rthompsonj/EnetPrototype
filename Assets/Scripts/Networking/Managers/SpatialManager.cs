using System;
using System.Collections;
using System.Collections.Generic;
using SoL.Networking.Objects;
using Supercluster.KDTree;
using UnityEngine;
using UnityEngine.Profiling;

namespace SoL.Networking.Managers
{
    public class SpatialManager : MonoBehaviour
    {
        private ServerNetworkSystem m_server = null;
        
        private const float kBuildTime = 1f;
        private const int kDimensions = 3;
        private const float kObeserverDistance = 5f;
        
        private KDTree<float, NetworkedObject> m_tree = null;

        private readonly List<float[]> m_positionCache = new List<float[]>();
        private Func<float[], float[], double> m_metric = null;
        private readonly float[] m_searchPos = new float[kDimensions];

        private WaitForSeconds m_wait = null;
        private IEnumerator m_rebuildCo = null;

        
        void Awake()
        {
            m_wait = new WaitForSeconds(kBuildTime);
            m_metric = MetricCalculation;
            m_server = gameObject.GetComponent<ServerNetworkSystem>();
        }

        void Start()
        {
            m_rebuildCo = RebuildTreeCo();
            StartCoroutine(m_rebuildCo);
        }

        private IEnumerator RebuildTreeCo()
        {
            while (true)
            {
                yield return m_wait;

                if (m_server.Peers.Count <= 1)
                {
                    m_tree = null;
                }
                else
                {                    
                    PrepTreeData();
                    m_tree = new KDTree<float, NetworkedObject>(kDimensions, m_positionCache.ToArray(), m_server.Peers.Values.ToArray(), m_metric);                    
                }                
                
                RebuildObservers();
            }
        }

        private void PrepTreeData()
        {
            Profiler.BeginSample("[KDTree] Fill Entity Position Cache");
            m_positionCache.Clear();
            for (int i = 0; i < m_server.Peers.Count; i++)
            {
                Vector3 pos = m_server.Peers[i].gameObject.transform.position;
                m_positionCache.Add(new float[] { pos.x, pos.y, pos.z });                
            }
            Profiler.EndSample();
        }
        
        private double MetricCalculation(float[] x, float[] y)
        {
            double dist = 0;
            for (int i = 0; i < x.Length; i++)
            {
                dist = dist + (x[i] - y[i]) * (x[i] - y[i]);
            }
            return dist;
        }

        public IEnumerable<NetworkedObject> GetObjectsWithinRange(Vector3 pos, float range)
        {
            if (m_server.Peers.Count == 1)
            {
                foreach (var no in m_server.Peers.GetValues())
                {
                    yield return no;
                }
                yield break;
            }
            
            if (m_tree == null)
                yield break;

            for (int i = 0; i < kDimensions; i++)
            {
                
                m_searchPos[i] = pos[i];
            }

            Profiler.BeginSample("[KDTree] Search");
            var results = m_tree.RadialSearch(m_searchPos, range * range);
            Profiler.EndSample();
            
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].Item2 == null)
                    continue;
                yield return results[i].Item2;
            }
        }
        
        private readonly HashSet<NetworkedObject> m_nearby = new HashSet<NetworkedObject>();

        public void RebuildObservers()
        {
            for (int i = 0; i < m_server.Peers.Count; i++)
            {                
                m_nearby.Clear();
                foreach (var o in GetObjectsWithinRange(m_server.Peers[i].gameObject.transform.position, kObeserverDistance))
                {
                    if (o != null && o.Peer.IsSet && o.Peer.ID != m_server.Peers[i].ID)
                    {
                        m_nearby.Add(o);
                    }
                }
                Debug.Log($"Found {m_nearby.Count} within range!");
                m_server.Peers[i].RebuildObservers(m_nearby);
                m_server.UpdateObservers(m_server.Peers[i]);
            }
        }
    }
}