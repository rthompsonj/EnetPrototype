namespace SoL.Networking.Replication
{
    public partial class PlayerReplication : ReplicationLayer
    {
        public readonly SynchronizedString PlayerName = new SynchronizedString();
    }
}