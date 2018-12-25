using ENet;
using NetStack.Compression;
using NetStack.Serialization;
using UnityEngine;

namespace Threaded
{
    public struct PositionUpdateData
    {
        public Vector3 Position;
        public float Heading;
    }
    
    public static class PackerUnpacker
    {
        public static BaseNetworkSystem.GameCommand GetPositionUpdate(OpCodes op, uint id, GameObject obj, BoundedRange[] range, byte channel)
        {
            var pos = BoundedRange.Compress(obj.transform.position, range);            
            var heading = HalfPrecision.Compress(obj.transform.eulerAngles.y);

            byte[] data = new byte[14];            
            BitBuffer buffer = new BitBuffer(128);
            buffer
                .AddUShort((ushort)op)
                .AddUInt(id)
                .AddUInt(pos.x)
                .AddUInt(pos.y)
                .AddUInt(pos.z)
                .AddUShort(heading)
                .ToArray(data);

            Packet packet = default(Packet);
            packet.Create(data);

            return new BaseNetworkSystem.GameCommand
            {
                Type = BaseNetworkSystem.GameCommand.CommandType.Send,
                Packet = packet,
                Channel = channel
            };
        }

        public static PositionUpdateData DeserializePositionUpdate(BitBuffer buffer, BoundedRange[] range)
        {
            var x = buffer.ReadUInt();
            var y = buffer.ReadUInt();
            var z = buffer.ReadUInt();
            var cpos = new CompressedVector3(x, y, z);
            var pos = BoundedRange.Decompress(cpos, range);
            
            var ch = buffer.ReadUShort();
            var h = HalfPrecision.Decompress(ch);

            return new PositionUpdateData
            {
                Position = pos,
                Heading = h
            };
        }
        
    }
}