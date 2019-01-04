using UnityEngine;

namespace Misc
{
    public enum SpawnType
    {
        None = 0,
        Cube = 1,
        Player = 2
    }
    
    [CreateAssetMenu(menuName = "SpawnParameters", fileName = "SpawnParam", order = 5)]
    public class SpawnParameters : ScriptableObject
    {
        [SerializeField] private GameObject m_cube = null;
        [SerializeField] private GameObject m_player = null;

        public GameObject InstantiateSpawn(SpawnType st)
        {
            switch (st)
            {
                case SpawnType.Cube:
                    return Instantiate(m_cube);

                case SpawnType.Player:
                    return Instantiate(m_player);

                default:
                    Debug.LogError($"Cannot spawn type {st.ToString()}!");
                    return null;
            }
        }
    }
}