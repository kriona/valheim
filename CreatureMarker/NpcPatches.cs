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
	/// Adds each leviathan to the NPC list
	/// </summary>
	/// <remarks>
	/// A leviathan is a mineable rock that floats, with no Character component, and the game gives it no name, so it
	/// is named after its prefab
	/// </remarks>
	[HarmonyPatch(typeof(Leviathan), nameof(Leviathan.Awake))]
	public static class LeviathanAwakePatch
	{
		/// <summary>
		/// Adds the leviathan to the NPC list
		/// </summary>
		/// <param name="__instance">The leviathan that was just created</param>
		private static void Postfix(Leviathan __instance)
		{
			NpcList.Add(__instance, CreatureConfig.GetPrefabNameToken(Utils.GetPrefabName(__instance.gameObject)));
		}
	}

	/// <summary>
	/// Adds Odin to the NPC list each time he appears
	/// </summary>
	/// <remarks>
	/// Odin is a figure with no Character component who watches the player from a distance and vanishes when they get
	/// close, and the game gives him no name
	/// </remarks>
	[HarmonyPatch(typeof(Odin), nameof(Odin.Awake))]
	public static class OdinAwakePatch
	{
		/// <summary>
		/// Adds Odin to the NPC list
		/// </summary>
		/// <param name="__instance">The Odin that was just created</param>
		private static void Postfix(Odin __instance)
		{
			NpcList.Add(__instance, CreatureConfig.GetPrefabNameToken(Utils.GetPrefabName(__instance.gameObject)));
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
