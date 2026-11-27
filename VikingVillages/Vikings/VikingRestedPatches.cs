using HarmonyLib;
using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// Heals a PC viking by the food it has eaten, and boosts its healing while it rests by a fire or is rested,
	/// by as much as the game's Resting and Rested effects boost a player's, and slows it while it's wet or cold, by as
	/// much as those effects slow a player's - giving it the weather's effects as it goes (see VikingWeather)
	/// </summary>
	/// <remarks>
	/// A creature heals in its AI's regeneration without reading its status effects, so the extra is added after the
	/// game's own heal, in proportion to it (see PCVikingBody.AfterRegeneration)
	/// </remarks>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.UpdateRegeneration))]
	public static class VikingRegenPatch
	{
		/// <summary>
		/// Notes the viking's health before the game heals it
		/// </summary>
		/// <param name="__instance">The AI healing its creature</param>
		/// <param name="__state">The creature's health, or -1 when it isn't a PC viking</param>
		private static void Prefix(BaseAI __instance, out float __state)
		{
			__state = ((__instance.m_character is PCVikingBody viking && VikingCore.IsPCViking(viking)) ? viking.GetHealth() : -1f);
		}

		/// <summary>
		/// Gives the viking the weather's effects and its rest's and food's healing
		/// </summary>
		/// <param name="__instance">The AI healing its creature</param>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="__state">The creature's health before, or -1 when it isn't a PC viking</param>
		private static void Postfix(BaseAI __instance, float dt, float __state)
		{
			if (__state >= 0f && __instance.m_character is PCVikingBody viking)
			{
				viking.AfterRegeneration(dt, __state);
			}
		}
	}
}
