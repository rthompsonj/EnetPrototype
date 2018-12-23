using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Simple
{
	public class ClientSpawner : MonoBehaviour
	{
		[SerializeField] private GameObject m_clientPrefab = null;
		[SerializeField] private Text m_nPlayers = null;

		private readonly List<GameObject> m_clients = new List<GameObject>();

		void Update()
		{
			if (m_clientPrefab != null && Input.GetKeyDown(KeyCode.Space))
			{
				GameObject g = Instantiate(m_clientPrefab);
				m_clients.Add(g);
			}

			if (m_clients.Count > 0 && Input.GetKeyDown(KeyCode.Backspace))
			{
				int index = m_clients.Count - 1;
				GameObject obj = m_clients[index];
				if (obj != null)
				{
					m_clients.RemoveAt(index);
					Destroy(obj);
				}
			}

			m_nPlayers.text = m_clients.Count.ToString();
		}
	}
}
