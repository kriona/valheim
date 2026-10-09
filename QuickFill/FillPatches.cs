using System;
using HarmonyLib;
using UnityEngine;

namespace QuickFill
{
	internal static class Fill
	{
		/// <summary>
		/// Whether either Ctrl key is held
		/// </summary>
		/// <returns>True when a Ctrl key is held</returns>
		public static bool IsModifierHeld()
		{
			return(ZInput.GetKey(KeyCode.LeftControl, false) || ZInput.GetKey(KeyCode.RightControl, false));
		}

		/// <summary>
		/// Runs addOne up to count times, stopping at the first failure
		/// </summary>
		/// <param name="addOne">Adds a single item, returning whether it was added</param>
		/// <param name="count">Most items to add</param>
		/// <returns>Whether at least one item was added</returns>
		public static bool Repeat(Func<bool> addOne, int count)
		{
			bool added = false;
			for (int i = 0; i < count; i++)
			{
				if (!addOne())
				{
					break;
				}
				added = true;
			}
			return(added);
		}
	}

	/// <summary>
	/// Ctrl + E on the ore / fuel switch of a smelter, kiln, blast furnace, etc. or the fuel switch of an oven fills it
	/// </summary>
	/// <remarks>
	/// Free space is measured before adding anything because the station's state only updates once its owner
	/// handles each RPC - when another player owns it, the per-item "full" check would see stale values
	/// </remarks>
	[HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
	public static class SwitchInteractPatch
	{
		/// <summary>
		/// Fills the station when Ctrl is held on a fillable switch that has space
		/// </summary>
		/// <param name="__instance">The switch being used</param>
		/// <param name="character">The character using it</param>
		/// <param name="hold">Whether the use key is being held down rather than pressed</param>
		/// <param name="__result">Set to whether anything was added when the fill runs</param>
		/// <returns>False to skip the game's single-item interact when the fill runs</returns>
		private static bool Prefix(Switch __instance, Humanoid character, bool hold, ref bool __result)
		{
			if (hold || __instance.m_onUse == null || !Fill.IsModifierHeld())
			{
				return(true);
			}
			int space = GetSpace(__instance);
			if (space <= 0)
			{
				return(true);
			}
			__result = Fill.Repeat(() => __instance.m_onUse(__instance, character, null), space);
			return(false);
		}

		/// <summary>
		/// How many more items the station behind this switch accepts, or 0 if it isn't a fillable switch
		/// </summary>
		/// <param name="sw">The switch being used</param>
		/// <returns>The number of items that fit</returns>
		private static int GetSpace(Switch sw)
		{
			Smelter smelter = sw.GetComponentInParent<Smelter>();
			if (smelter != null)
			{
				if (!smelter.m_nview.IsValid())
				{
					return(0);
				}
				if (sw == smelter.m_addOreSwitch)
				{
					return(smelter.m_maxOre - smelter.GetQueueSize());
				}
				if (sw == smelter.m_addWoodSwitch)
				{
					return(Mathf.FloorToInt(smelter.m_maxFuel - smelter.GetFuel()));
				}
				return(0);
			}
			CookingStation station = sw.GetComponentInParent<CookingStation>();
			if (station != null && station.m_nview.IsValid() && sw == station.m_addFuelSwitch)
			{
				return(Mathf.FloorToInt(station.m_maxFuel - station.GetFuel()));
			}
			return(0);
		}
	}

	/// <summary>
	/// Ctrl + E on a campfire, bonfire, hearth, torch, etc. adds fuel until it is full
	/// </summary>
	/// <remarks>
	/// The fill runs the game's Interact with alt set, which is the path that adds fuel - without alt, fires that
	/// can be turned off are toggled instead
	/// </remarks>
	[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
	public static class FireplaceInteractPatch
	{
		/// <summary>
		/// Set while filling so the repeated Interact calls run the game's original single-fuel logic
		/// </summary>
		private static bool filling;

		/// <summary>
		/// Adds fuel until the fire is full when Ctrl is held
		/// </summary>
		/// <param name="__instance">The fire being used</param>
		/// <param name="user">The character using it</param>
		/// <param name="hold">Whether the use key is being held down rather than pressed</param>
		/// <param name="alt">Whether the alternate action is used, set when Ctrl is held</param>
		/// <param name="__result">Set to whether any fuel was added when the fill runs</param>
		/// <returns>False to skip the game's single-fuel interact when the fill runs</returns>
		private static bool Prefix(Fireplace __instance, Humanoid user, bool hold, ref bool alt, ref bool __result)
		{
			if (filling || hold || !Fill.IsModifierHeld())
			{
				return(true);
			}
			// Keeps Ctrl + E on a full fire from turning it off
			alt = true;
			if (!__instance.m_canRefill || __instance.m_infiniteFuel || !__instance.m_nview.IsValid())
			{
				return(true);
			}
			float fuel = __instance.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel);
			int space = Mathf.FloorToInt(__instance.m_maxFuel) - Mathf.CeilToInt(fuel);
			if (space <= 0)
			{
				return(true);
			}
			filling = true;
			try
			{
				__result = Fill.Repeat(() => __instance.Interact(user, false, true), space);
			}
			finally
			{
				filling = false;
			}
			return(false);
		}
	}
}
