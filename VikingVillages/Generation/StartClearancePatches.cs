using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Leaves an abandoned farm out of the world's plan of its locations as the world's locations are generated, when it
	/// would stand too close to the world's start location (see StartClearance)
	/// </summary>
	/// <remarks>
	/// The start location is prioritized, so it's always planned before any farm. The game counts the farm as placed
	/// all the same, so the world has one fewer farm for each one left out
	/// </remarks>
	[HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.RegisterLocation))]
	public static class StartClearancePatch
	{
		/// <summary>
		/// Skips planning a farm too close to the start location while the world's locations are being generated - not
		/// while a saved or sent plan is being loaded
		/// </summary>
		/// <param name="__instance">The zone system</param>
		/// <param name="location">The location being planned</param>
		/// <param name="pos">Where it would stand</param>
		/// <param name="generated">Whether it's already placed</param>
		/// <returns>False to leave it out</returns>
		private static bool Prefix(ZoneSystem __instance, ZoneSystem.ZoneLocation location, Vector3 pos, bool generated)
		{
			if (generated || __instance.m_generateLocationsCoroutine == null || __instance.LocationsGenerated || !FarmSpawns.IsFarm(location) || !StartClearance.NearStart(pos))
			{
				return(true);
			}
			Plugin.Log.LogInfo("Left the farm " + location.m_prefabName + " at " + pos.ToString("F0") + " out of the world, too close to its start location");
			return(false);
		}
	}
}
