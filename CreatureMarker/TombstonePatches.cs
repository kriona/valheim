using HarmonyLib;

namespace CreatureMarker
{
	/// <summary>
	/// Adds each tombstone to the tombstone list
	/// </summary>
	[HarmonyPatch(typeof(TombStone), nameof(TombStone.Awake))]
	public static class TombStoneAwakePatch
	{
		/// <summary>
		/// Adds the tombstone to the tombstone list
		/// </summary>
		/// <param name="__instance">The tombstone that was just created</param>
		private static void Postfix(TombStone __instance)
		{
			TombstoneList.Add(__instance);
		}
	}
}
