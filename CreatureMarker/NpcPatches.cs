using HarmonyLib;

namespace CreatureMarker
{
	/// <summary>
	/// Adds each trader to the NPC list once it has snapped to the ground
	/// </summary>
	[HarmonyPatch(typeof(Trader), nameof(Trader.Start))]
	public static class TraderStartPatch
	{
		/// <summary>
		/// Adds the trader to the NPC list
		/// </summary>
		/// <param name="__instance">The trader that just started</param>
		private static void Postfix(Trader __instance)
		{
			NpcList.Add(__instance, __instance.m_name);
		}
	}

	/// <summary>
	/// Adds the pets in each location, like Halstein in Hildir's camp, to the NPC list
	/// </summary>
	/// <remarks>
	/// Petable has no Awake or Start to patch, and pets are part of the location that places them
	/// </remarks>
	[HarmonyPatch(typeof(Location), nameof(Location.Awake))]
	public static class LocationAwakePatch
	{
		/// <summary>
		/// Adds every pet under the location to the NPC list
		/// </summary>
		/// <param name="__instance">The location that was just created</param>
		private static void Postfix(Location __instance)
		{
			foreach (Petable petable in __instance.GetComponentsInChildren<Petable>(true))
			{
				NpcList.Add(petable, petable.m_name);
			}
		}
	}
}
