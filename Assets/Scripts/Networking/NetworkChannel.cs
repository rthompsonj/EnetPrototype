namespace SoL.Networking
{
    public enum NetworkChannel : byte
    {
        /// <summary>
        /// Invalid Channel, packet rejected
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// Client --> Server
        /// </summary>
        State_Client,
        
        /// <summary>
        /// Server --> Broadcast
        /// </summary>
        State_Server,
        
        /// <summary>
        /// Server --> Broadcast
        /// </summary>
        Replication,

        /// <summary>
        /// Client --> Server
        /// Server --> Broadcast
        /// </summary>
        Spawn_Self,
        
        /// <summary>
        /// Server --> Broadcast
        /// </summary>
        Spawn_Other,
        
        /// <summary>
        /// Client --> Server
        /// Disconnect message
        /// </summary>
        Destroy_Client,
        
        /// <summary>
        /// Server --> Broadcast
        /// </summary>
        Destroy_Server,
       
        /// <summary>
        /// Client --> Server
        /// Rpc Request
        /// </summary>
        Rpc_Client,
        
        /// <summary>
        /// Server --> Client
        /// Rpc response
        /// </summary>
        Rpc_Server        
    }

    public static class NetworkChannelExtensions
    {
        public static byte GetByte(this NetworkChannel channel)
        {
            return (byte) channel;
        }

        public static NetworkChannel GetChannel(byte byteChannel)
        {
            return (NetworkChannel) byteChannel;
        }
    }
}