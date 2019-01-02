using System;
using System.Collections;
using System.Collections.Generic;

namespace Misc
{
    public class ListDictCollection<TKey, TValue>
    {
        private readonly List<TValue> m_list = null;
        private readonly Dictionary<TKey, TValue> m_dict = null;

        private readonly bool m_replaceWhenPresent = false;
        
        public ListDictCollection(bool replace = false)
        {
            m_list = new List<TValue>();
            m_dict = new Dictionary<TKey, TValue>();
            m_replaceWhenPresent = replace;
        }
        
        #region ADD_REMOVE

        public void Add(TKey key, TValue value)
        {
            if (m_replaceWhenPresent)
            {
                Remove(key);
            }
            m_list.Add(value);
            m_dict.Add(key, value);   
        }

        public bool Remove(TKey key)
        {
            TValue value;
            if (m_dict.TryGetValue(key, out value))
            {
                m_dict.Remove(key);
                return m_list.Remove(value);
            }
            return false;
        }
        
        #endregion
        
        #region LIST_LIKE

        public int Count => m_list.Count;

        public TValue this[int index]
        {
            get
            {
                if (index < 0 || index >= m_list.Count)
                {
                    throw new IndexOutOfRangeException();
                }
                return m_list[index];
            }
        }

        public IEnumerator<TValue> Values()
        {
            for (int i = 0; i < m_list.Count; i++)
            {
                yield return m_list[i];
            }
        }
        
        #endregion

        #region DICT_LIKE

        public bool ContainsKey(TKey key)
        {
            return m_dict.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return m_dict.TryGetValue(key, out value);
        }

        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (m_dict.TryGetValue(key, out value) == false)
                {
                    throw new KeyNotFoundException(key.ToString());
                }
                return value;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> Items()
        {
            foreach (var kvp in m_dict)
            {
                yield return kvp;
            }
        }

        #endregion       
    }
}