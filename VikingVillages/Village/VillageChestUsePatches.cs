using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Adds the key for keeping the vikings out of a chest in their village, or letting them use it again (see
	/// VillageChestUse), to the chest's hover text, below its own name and keys - unless a ward keeps the player out of
	/// the chest
	/// </summary>
	[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
	public static class VillageChestUseHoverPatch
	{
		/// <summary>
		/// Puts the line below the chest's own hover text
		/// </summary>
		/// <param name="__instance">The chest looked at</param>
		/// <param name="__result">Its hover text</param>
		private static void Postfix(Container __instance, ref string __result)
		{
			if (__instance == null || (__instance.m_checkGuardStone && !PrivateArea.CheckAccess(__instance.transform.position, 0f, false)))
			{
				return;
			}
			string line = VillageChestUse.HoverLine(__instance);
			if (line == null)
			{
				return;
			}
			__result = (string.IsNullOrEmpty(__result) ? line : __result + "\n" + line);
		}
	}
}
