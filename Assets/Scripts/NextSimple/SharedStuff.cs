using NetStack.Compression;
using NetStack.Serialization;
using UnityEngine;

namespace NextSimple
{
    public class SharedStuff : MonoBehaviour
    {
        public static SharedStuff Instance = null;
        
        public float RandomRange = 5f;
        
        [SerializeField] private GameObject m_playerPrefab = null;

        public BoundedRange[] Range = new BoundedRange[3];
        
        void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            Range[0] = new BoundedRange(-RandomRange, RandomRange, 0.05f);
            Range[1] = new BoundedRange(-RandomRange, RandomRange, 0.05f);
            Range[2] = new BoundedRange(-RandomRange, RandomRange, 0.05f);
        }
        
        public BaseEntity SpawnPlayer()
        {
            var go = Instantiate(m_playerPrefab);
            BaseEntity entity = go.GetComponent<BaseEntity>();
            return entity;
        }

        public static Vector3 ReadAndGetPositionFromCompressed(BitBuffer buffer, BoundedRange[] range)
        {
	        var x = buffer.ReadUInt();
            var y = buffer.ReadUInt();
            var z = buffer.ReadUInt();
            var compressedPos = new CompressedVector3(x, y, z);
            return BoundedRange.Decompress(compressedPos, range);
        }
    }
}