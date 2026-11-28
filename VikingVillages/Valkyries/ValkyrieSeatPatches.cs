using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Lets a player get on a valkyrie only once she's befriended and while she's friendly with them, and never take her
	/// seat off, since it's part of her (see ValkyrieSeat)
	/// </summary>
	[HarmonyPatch(typeof(Sadle), nameof(Sadle.Interact))]
	internal static class ValkyrieSeatInteractPatch
	{
		/// <summary>
		/// Skips getting on a valkyrie that isn't befriended or is angry with the player, and taking off her seat
		/// </summary>
		/// <param name="__instance">The saddle</param>
		/// <param name="character">The player</param>
		/// <param name="alt">Whether the alternate key is down</param>
		/// <param name="__result">Whether the interaction did anything</param>
		/// <returns>False to skip the game's interaction</returns>
		private static bool Prefix(Sadle __instance, Humanoid character, bool alt, ref bool __result)
		{
			if (__instance.m_character == null || __instance.m_character.GetComponent<ValkyrieFlight>() == null)
			{
				return(true);
			}
			if (alt || !ValkyrieSeat.CanMount(__instance.m_character, character as Player))
			{
				__result = false;
				return(false);
			}
			return(true);
		}
	}

	/// <summary>
	/// Shows a valkyrie's seat as the prompts to ride her and open her inventory, by her name as the local player knows
	/// her and whether she's hungry, and nothing while she can't be ridden by them
	/// </summary>
	[HarmonyPatch(typeof(Sadle), nameof(Sadle.GetHoverText))]
	internal static class ValkyrieSeatHoverPatch
	{
		/// <summary>
		/// Replaces a valkyrie's saddle prompts with the prompts to ride her and open her inventory
		/// </summary>
		/// <param name="__instance">The saddle</param>
		/// <param name="__result">The hover text</param>
		private static void Postfix(Sadle __instance, ref string __result)
		{
			if (__instance.m_character == null || !__instance.m_character.TryGetComponent(out ValkyrieTalk talk))
			{
				return;
			}
			if (!__instance.InUseDistance(Player.m_localPlayer))
			{
				return;
			}
			if (!ValkyrieSeat.CanMount(__instance.m_character, Player.m_localPlayer))
			{
				__result = "";
				return;
			}
			__result = talk.NameWithHunger() + "\n" + Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Ride");
			if (ValkyriePackWindow.CanOpen(__instance.m_character, Player.m_localPlayer))
			{
				__result += ValkyriePackWindow.Prompt();
			}
		}
	}
}
