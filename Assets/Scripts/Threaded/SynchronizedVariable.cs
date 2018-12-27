using System;
using NetStack.Compression;
using NetStack.Serialization;
using UnityEngine;

namespace Threaded
{
    public abstract class SynchronizedVariable<T>
    {
        public Action<T> Changed;
        public bool Dirty { get; set; }        

        private T m_value = default(T);
        
        public T Value
        {
            get { return m_value; }
            set
            {
                if (m_value.Equals(value))
                    return;
                m_value = value;
                Changed?.Invoke(m_value);
            }
        }

        public abstract BitBuffer PackVariable(BitBuffer buffer);
        public abstract BitBuffer ReadVariable(BitBuffer buffer);
    }

    public class SynchronizedInt : SynchronizedVariable<int>
    {
        public override BitBuffer PackVariable(BitBuffer buffer)
        {
            if (Dirty == false)
            {
                return buffer;                
            }

            buffer.AddInt(Value);
            Dirty = false;
            return buffer;
        }

        public override BitBuffer ReadVariable(BitBuffer buffer)
        {
            Value = buffer.ReadInt();
            return buffer;
        }
    }
    
    public class SynchronizedUInt : SynchronizedVariable<uint>
    {
        public override BitBuffer PackVariable(BitBuffer buffer)
        {
            if (Dirty == false)
            {
                return buffer;                
            }

            buffer.AddUInt(Value);
            Dirty = false;
            return buffer;
        }

        public override BitBuffer ReadVariable(BitBuffer buffer)
        {
            Value = buffer.ReadUInt();
            return buffer;
        }
    }

    public class SynchronizedFloat : SynchronizedVariable<float>
    {
        public override BitBuffer PackVariable(BitBuffer buffer)
        {
            if (Dirty == false)
            {
                return buffer;                
            }

            buffer.AddFloat(Value);
            /*
            ushort compressed = HalfPrecision.Compress(Value);
            Debug.Log($"Compressed {Value} to {compressed}");
            buffer.AddUShort(compressed);
            */
            Dirty = false;
            return buffer;
        }

        public override BitBuffer ReadVariable(BitBuffer buffer)
        {
            Value = buffer.ReadFloat();
            /*
            ushort compressed = buffer.ReadUShort();
            Value = HalfPrecision.Decompress(compressed);
            Debug.Log($"Uncompressed {compressed} to {Value}");
            */
            return buffer;
        }
    }

    public class SynchronizedString : SynchronizedVariable<string>
    {
        public override BitBuffer PackVariable(BitBuffer buffer)
        {
            if (Dirty == false)
            {
                return buffer;                
            }

            buffer.AddString(Value);
            Dirty = false;
            return buffer;
        }

        public override BitBuffer ReadVariable(BitBuffer buffer)
        {
            Value = buffer.ReadString();
            return buffer;
        }
    }
}