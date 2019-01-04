namespace SoL.Networking
{
    public enum OpCodes : ushort
    {
        None,
        ConnectionEvent,
        Ok,        
        Spawn,
        BulkSpawn,
        Destroy,
        PositionUpdate,
        SyncUpdate
    }
}