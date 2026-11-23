using HarmonyLib;

namespace ZombieApocalypse
{
	/// <summary>
	/// Widens a farm being generated and gives it more farmhouses when its village would have too few houses
	/// </summary>
	/// <remarks>
	/// The generator places its rooms here with its room prefabs loaded and its random numbers seeded, and saves them
	/// right after, so farmhouses added here are saved with the rest
	/// </remarks>
	[HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.GenerateRooms))]
	public static class VillageFarmhousesPatch
	{
		/// <summary>
		/// Widens the farm before its rooms are placed
		/// </summary>
		/// <param name="__instance">The generator</param>
		/// <param name="mode">How its location is being placed</param>
		/// <param name="__state">The generator's own settings, put back after</param>
		private static void Prefix(DungeonGenerator __instance, ZoneSystem.SpawnMode mode, out VillageFarmhouses.FarmSettings __state)
		{
			__state = VillageFarmhouses.Widen(__instance, mode);
		}

		/// <summary>
		/// Places the extra farmhouses once the generator's own rooms are placed, then puts its settings back
		/// </summary>
		/// <param name="__instance">The generator</param>
		/// <param name="mode">How its location is being placed</param>
		/// <param name="__state">The generator's own settings</param>
		private static void Postfix(DungeonGenerator __instance, ZoneSystem.SpawnMode mode, VillageFarmhouses.FarmSettings __state)
		{
			VillageFarmhouses.EnsureHouses(__instance, mode);
			VillageFarmhouses.Unwiden(__instance, __state);
		}
	}
}
