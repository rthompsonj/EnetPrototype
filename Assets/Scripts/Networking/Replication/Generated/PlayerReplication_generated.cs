namespace SoL.Networking.Replication
{
    public partial class PlayerReplication
    {
        protected override int RegisterSyncs()
        {
            var cnt = base.RegisterSyncs();
            m_syncs.Add(PlayerName);
            PlayerName.BitFlag = 1 << cnt;
            cnt += 1;
            return cnt;
        }
    }
}
