using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Keeps a farm's piece from wearing as it wakes, until its farm has been put right (see FarmSupport)
	/// </summary>
	[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Awake))]
	public static class FarmSupportAwakePatch
	{
		/// <summary>
		/// Notes the piece once it has woken
		/// </summary>
		/// <param name="__instance">The piece</param>
		private static void Postfix(WearNTear __instance)
		{
			FarmSupport.OnAwake(__instance);
		}
	}

	/// <summary>
	/// Skips a farm's piece's wear - breaking for want of support among it - while it's kept from wearing (see
	/// FarmSupport)
	/// </summary>
	[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.UpdateWear))]
	public static class FarmSupportWearPatch
	{
		/// <summary>
		/// Skips the update for a piece kept from wearing
		/// </summary>
		/// <param name="__instance">The piece</param>
		/// <returns>False to skip it</returns>
		private static bool Prefix(WearNTear __instance)
		{
			return(!FarmSupport.Holds(__instance));
		}
	}
}
