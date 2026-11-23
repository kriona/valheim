using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// Finds the game's prefabs by name and spawns local, unnetworked copies of them
	/// </summary>
	internal static class GamePrefabs
	{
		/// <summary>
		/// Seconds before a prefab that wasn't found is looked for again, since looking through every loaded object is
		/// slow
		/// </summary>
		private const float RetryInterval = 60f;

		private static readonly Dictionary<string, GameObject> found = new Dictionary<string, GameObject>();

		/// <summary>
		/// When each prefab that wasn't found was last looked for, as a Time.realtimeSinceStartup value
		/// </summary>
		private static readonly Dictionary<string, float> missedAt = new Dictionary<string, float>();

		/// <summary>
		/// Finds a prefab by name, keeping it for later calls while it exists
		/// </summary>
		/// <remarks>
		/// Not every prefab is registered with ZNetScene, but one already loaded from the game's files can still be
		/// found among the loaded objects. A prefab that wasn't found is looked for again at most once every
		/// RetryInterval seconds, since it may only be loaded later
		/// </remarks>
		/// <param name="name">The prefab's name</param>
		/// <returns>The prefab, or null when it isn't loaded</returns>
		public static GameObject Find(string name)
		{
			if (found.TryGetValue(name, out GameObject prefab))
			{
				if (prefab != null)
				{
					return(prefab);
				}
				found.Remove(name);
			}
			if (missedAt.TryGetValue(name, out float at) && Time.realtimeSinceStartup - at < RetryInterval)
			{
				return(null);
			}
			prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(name) : null);
			if (prefab == null)
			{
				foreach (GameObject loaded in Resources.FindObjectsOfTypeAll<GameObject>())
				{
					if (loaded.name == name && loaded.transform.parent == null)
					{
						prefab = loaded;
						break;
					}
				}
			}
			if (prefab != null)
			{
				found[name] = prefab;
				missedAt.Remove(name);
			}
			else
			{
				missedAt[name] = Time.realtimeSinceStartup;
			}
			return(prefab);
		}

		/// <summary>
		/// Spawns a copy of a prefab on this client only, without registering its network view
		/// </summary>
		/// <param name="prefab">The prefab</param>
		/// <param name="position">Where to spawn it</param>
		/// <param name="rotation">Which way it faces</param>
		/// <returns>The copy</returns>
		public static GameObject SpawnLocal(GameObject prefab, Vector3 position, Quaternion rotation)
		{
			ZNetView.m_forceDisableInit = true;
			try
			{
				return(Object.Instantiate(prefab, position, rotation));
			}
			finally
			{
				ZNetView.m_forceDisableInit = false;
			}
		}
	}
}
