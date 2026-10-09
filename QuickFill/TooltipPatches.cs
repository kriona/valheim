using HarmonyLib;

namespace QuickFill
{
	internal static class Tooltip
	{
		/// <summary>
		/// Builds the hover line for the fill shortcut, formatted like the game's own key hints
		/// </summary>
		/// <param name="action">Text after the key, may contain $ localization tokens</param>
		/// <returns>The localized line, starting with a line break</returns>
		public static string FillLine(string action)
		{
			return(Localization.instance.Localize("\n[<color=yellow><b>Ctrl + $KEY_Use</b></color>] " + action));
		}
	}

	/// <summary>
	/// Adds the fill shortcut to the hover text of a smelter's ore switch
	/// </summary>
	[HarmonyPatch(typeof(Smelter), nameof(Smelter.OnHoverAddOre))]
	public static class SmelterOreTooltipPatch
	{
		/// <summary>
		/// Appends the fill line
		/// </summary>
		/// <param name="__result">The hover text built by the game</param>
		private static void Postfix(ref string __result)
		{
			__result += Tooltip.FillLine("Fill");
		}
	}

	/// <summary>
	/// Adds the fill shortcut to the hover text of a smelter's fuel switch
	/// </summary>
	[HarmonyPatch(typeof(Smelter), nameof(Smelter.OnHoverAddFuel))]
	public static class SmelterFuelTooltipPatch
	{
		/// <summary>
		/// Appends the fill line naming the smelter's fuel
		/// </summary>
		/// <param name="__instance">The smelter being hovered</param>
		/// <param name="__result">The hover text built by the game</param>
		private static void Postfix(Smelter __instance, ref string __result)
		{
			__result += Tooltip.FillLine("Fill with " + __instance.m_fuelItem.m_itemData.m_shared.m_name);
		}
	}

	/// <summary>
	/// Adds the fill shortcut to the hover text of an oven's fuel switch
	/// </summary>
	[HarmonyPatch(typeof(CookingStation), nameof(CookingStation.OnHoverFuelSwitch))]
	public static class CookingStationFuelTooltipPatch
	{
		/// <summary>
		/// Appends the fill line naming the station's fuel
		/// </summary>
		/// <param name="__instance">The cooking station being hovered</param>
		/// <param name="__result">The hover text built by the game</param>
		private static void Postfix(CookingStation __instance, ref string __result)
		{
			__result += Tooltip.FillLine("Fill with " + __instance.m_fuelItem.m_itemData.m_shared.m_name);
		}
	}

	/// <summary>
	/// Adds the fill shortcut to the hover text of a fire that takes fuel
	/// </summary>
	[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
	public static class FireplaceTooltipPatch
	{
		/// <summary>
		/// Appends the fill line naming the fire's fuel, unless the fire can't be refilled
		/// </summary>
		/// <param name="__instance">The fire being hovered</param>
		/// <param name="__result">The hover text built by the game</param>
		private static void Postfix(Fireplace __instance, ref string __result)
		{
			if (!__instance.m_canRefill || __instance.m_infiniteFuel || !__instance.m_nview.IsValid())
			{
				return;
			}
			__result += Tooltip.FillLine("Fill with " + __instance.m_fuelItem.m_itemData.m_shared.m_name);
		}
	}
}
