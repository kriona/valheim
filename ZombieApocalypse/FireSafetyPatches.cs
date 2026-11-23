using HarmonyLib;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Notes each fire as it wakes, so the living vikings' paths bend around it (see FireSafety)
	/// </summary>
	[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Awake))]
	public static class FireRegisterPatch
	{
		/// <summary>
		/// Notes the fire
		/// </summary>
		/// <param name="__instance">The fire</param>
		private static void Postfix(Fireplace __instance)
		{
			FireSafety.Register(__instance);
		}
	}

	/// <summary>
	/// Bends a living viking's path around lit fires as the path is found, keeping it clear of their flames (see
	/// FireSafety)
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindPath))]
	public static class VikingFirePathPatch
	{
		/// <summary>
		/// Bends the path when one was just found for a living viking
		/// </summary>
		/// <param name="__instance">The AI finding its way</param>
		/// <param name="__result">Whether it found a path</param>
		private static void Postfix(BaseAI __instance, bool __result)
		{
			// A path found earlier and handed back again was bent when it was found
			if (!__result || __instance.m_lastFindPathTime != Time.time || __instance.m_character == null || !VikingCore.IsLivingViking(__instance.m_character))
			{
				return;
			}
			FireSafety.BendPath(__instance.m_character, __instance.m_path);
		}
	}
}
