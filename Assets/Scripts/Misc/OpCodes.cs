namespace SoL.Networking
{
    public enum OpCodes : ushort
    {
        None,
        Spawn,
        BulkSpawn,
        Destroy,
        PositionUpdate,
        SyncUpdate
    }
}