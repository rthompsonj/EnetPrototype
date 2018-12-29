using System;
using System.Text;
using NetStack.Serialization;

namespace Threaded
{
    public interface ISynchronizedVariable
    {
        bool Dirty { get; }
        int BitFlag { get; }
        void ResetDirty();
        BitBuffer PackVariable(BitBuffer buffer);
        BitBuffer ReadVariable(BitBuffer buffer);
    }
    
    public abstract class SynchronizedVariable<T> : ISynchronizedVariable
    {
        public Action<T> Changed;
        public bool Dirty { get; private set; }        
        public int BitFlag { get; set; }
        
        private T m_value = default(T);
        
        public T Value
        {
            get { return m_value; }
            set
            {
                if (m_value != null && m_value.Equals(value))
                    return;
                m_value = value;
                Dirty = true;
                Changed?.Invoke(m_value);
            }
        }

        public void ResetDirty()
        {
            Dirty = false;
        }

        public abstract BitBuffer PackVariable(BitBuffer buffer);
        public abstract BitBuffer ReadVariable(BitBuffer buffer);
    }

    public class SynchronizedInt : SynchronizedVariable<int>
    {
        public override BitBuffer PackVariable(BitBuffer buffer)
        {
            buffer.AddInt(Value);
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
            buffer.AddUInt(Value);
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
            buffer.AddFloat(Value);
            return buffer;
        }

        public override BitBuffer ReadVariable(BitBuffer buffer)
        {
            Value = buffer.ReadFloat();
            return buffer;
        }
    }

    public class SynchronizedString : SynchronizedVariable<string>
    {
        public override BitBuffer PackVariable(BitBuffer buffer)
        {
            // cannot send nulls
            if (Value == null)
            {
                Value = "";
            }
            
            buffer.AddString(Value);
            return buffer;
        }

        public override BitBuffer ReadVariable(BitBuffer buffer)
        {
            Value = buffer.ReadString();
            return buffer;
        }
    }

    /// <summary>
    /// Avoids limitations of BitBuffer string packing 512 limit.
    /// </summary>
    public class SynchronizedASCII : SynchronizedVariable<string>
    {
        public override BitBuffer PackVariable(BitBuffer buffer)
        {
            // cannot send nulls
            if (Value == null)
            {
                Value = "";
            }
            
            int len = Value.Length;
            buffer.AddInt(len);
            for (int i = 0; i < len; i++)
            {
                buffer.AddByte((byte) Value[i]);
            }
            return buffer;
        }

        public override BitBuffer ReadVariable(BitBuffer buffer)
        {
            int len = buffer.ReadInt();            
            
            StringBuilder sb = new StringBuilder(len);
            
            for (int i = 0; i < len; i++)
            {
                sb.Insert(i, (char)buffer.ReadByte());
            }
            Value = sb.ToString();
            return buffer;
        }
    }
}