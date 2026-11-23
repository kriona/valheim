using HarmonyLib;

namespace ZombieApocalypse
{
	/// <summary>
	/// Keeps track of the doors in the world, for living vikings to walk through
	/// </summary>
	[HarmonyPatch(typeof(Door), nameof(Door.Awake))]
	public static class VikingDoorsPatch
	{
		/// <summary>
		/// Adds the door that woke, leaving out a door with no ZDO, such as the ghost of one being placed, which the game
		/// sets up nothing for either
		/// </summary>
		/// <param name="__instance">The door</param>
		private static void Postfix(Door __instance)
		{
			if (__instance.m_nview == null || __instance.m_nview.GetZDO() == null)
			{
				return;
			}
			VikingDoors.Register(__instance);
		}
	}
}
