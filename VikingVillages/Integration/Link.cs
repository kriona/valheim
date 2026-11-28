using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What Viking Villages tells Zombie Apocalypse about the world's villages and its creatures (see VillageLink there)
	/// </summary>
	/// <remarks>
	/// Zombie Apocalypse finds these methods by name and parameters, so each is kept for as long as the mod is released -
	/// a new version can add methods, but never remove one or change its name, parameters or return type, since an older
	/// Zombie Apocalypse still calls it
	/// </remarks>
	public static class Link
	{
		/// <summary>
		/// Checks whether the PC vikings take in an abandoned house, as one near an abandoned farm (see
		/// FarmSpawns.FarmNear), so zombie vikings leave it to them
		/// </summary>
		/// <param name="position">Where the house stands</param>
		/// <returns>True when they do</returns>
		public static bool ClaimsHouse(Vector3 position)
		{
			return(ZoneSystem.instance != null && FarmSpawns.FarmNear(position));
		}

		/// <summary>
		/// The prefab one of Viking Villages' creatures rises as when it dies and comes back as a zombie - a valkyrie
		/// rises as a Fallen Valkyrie
		/// </summary>
		/// <param name="prefabName">The dying creature's prefab name</param>
		/// <returns>The prefab name it rises as, or null when it rises as itself</returns>
		public static string RisesAs(string prefabName)
		{
			return(((prefabName == ValkyriePrefab.PrefabName) ? ValkyrieCombat.FallenPrefab : null));
		}
	}
}
