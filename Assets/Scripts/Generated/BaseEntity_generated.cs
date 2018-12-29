namespace NextSimple
{
    public partial class BaseEntity
    {
        protected override int RegisterSyncs()
        {
            var cnt = base.RegisterSyncs();
            m_syncs.Add(m_randomValue);
            m_randomValue.BitFlag = 1 << cnt;
            cnt += 1;
            m_syncs.Add(m_stringValue1);
            m_stringValue1.BitFlag = 1 << cnt;
            cnt += 1;
            m_syncs.Add(m_stringValue2);
            m_stringValue2.BitFlag = 1 << cnt;
            cnt += 1;
            return cnt;
        }
    }
}
