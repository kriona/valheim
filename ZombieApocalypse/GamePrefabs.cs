using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Finds the game's prefabs by name and spawns local, unnetworked copies of them
	/// </summary>
	internal static class GamePrefabs
	{
		private static readonly Dictionary<string, GameObject> found = new Dictionary<string, GameObject>();

		/// <summary>
		/// Finds a prefab by name, once
		/// </summary>
		/// <remarks>
		/// Not every prefab is registered with ZNetScene, but one already loaded from the game's files can still be
		/// found among the loaded objects
		/// </remarks>
		/// <param name="name">The prefab's name</param>
		/// <returns>The prefab, or null when it isn't loaded</returns>
		public static GameObject Find(string name)
		{
			if (found.TryGetValue(name, out GameObject prefab))
			{
				return(prefab);
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
			found[name] = prefab;
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
