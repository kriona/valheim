using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Shows a living or dvergr viking's helmet as a Yule hat while the hats show (see YuleHats) - the game compares the
	/// helmet it's given with the one it shows each time it updates the equipment, so the hats come and go as soon as
	/// they're turned on or off
	/// </summary>
	[HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetHelmetEquipped))]
	public static class YuleHatsHelmetPatch
	{
		/// <summary>
		/// Swaps the helmet to show before the game shows it
		/// </summary>
		/// <param name="__instance">The character's equipment</param>
		/// <param name="hash">The item hash of the helmet worn, or 0 for none</param>
		private static void Prefix(VisEquipment __instance, ref int hash)
		{
			hash = YuleHats.Swap(__instance, hash);
		}
	}
}
