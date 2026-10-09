using HarmonyLib;

namespace SwapGear
{
	/// <summary>
	/// Hovering an armor stand with items on it shows the swap key below the game's hover text
	/// </summary>
	/// <remarks>
	/// Every part of an armor stand the player can look at is a Switch - one per slot plus the pose switch
	/// </remarks>
	[HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverText))]
	public static class SwitchHoverPatch
	{
		/// <summary>
		/// Appends the swap key line when the switch belongs to an armor stand that can be swapped with
		/// </summary>
		/// <param name="__instance">The switch being hovered</param>
		/// <param name="__result">The hover text built by the game</param>
		private static void Postfix(Switch __instance, ref string __result)
		{
			if (Swap.Running)
			{
				return;
			}
			ArmorStand stand = __instance.GetComponentInParent<ArmorStand>();
			if (stand == null || !Swap.CanSwap(stand))
			{
				return;
			}
			__result += "\n[<color=yellow><b>" + Plugin.SwapKey.Value + "</b></color>] Swap gear (Esc to cancel)";
		}
	}
}
