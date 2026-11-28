using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Has a player talk to a valkyrie with the use key rather than pet her or rename her, tamed or not - she's a
	/// creature of her own mind, not a pet (see ValkyrieTalk). One angry with them won't be talked to at all
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
	[HarmonyPriority(Priority.First)]
	internal static class ValkyrieTalkPatch
	{
		/// <summary>
		/// Has a valkyrie answer the player, in place of the game's petting and renaming
		/// </summary>
		/// <param name="__instance">The creature's Tameable</param>
		/// <param name="user">The player</param>
		/// <param name="hold">Whether the key is held</param>
		/// <param name="alt">Whether the alternate key is down</param>
		/// <param name="__result">Whether the interaction did anything</param>
		/// <returns>False for a valkyrie, to skip the game's interaction</returns>
		private static bool Prefix(Tameable __instance, Humanoid user, bool hold, bool alt, ref bool __result)
		{
			if (!__instance.TryGetComponent(out ValkyrieTalk talk))
			{
				return(true);
			}
			__result = false;
			if (!hold && !alt && user is Player player && talk.CanTalk(player))
			{
				talk.Talk(player);
				__result = true;
			}
			return(false);
		}
	}

	/// <summary>
	/// Shows a valkyrie with only her name, whether she's hungry, the prompt to talk to her unless she's angry with the
	/// player and, once she's befriended, the prompt to open her pack - not whether she's wild or tamed, since tamed only
	/// means she's willing to carry someone
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
	[HarmonyPriority(Priority.Last)]
	internal static class ValkyrieHoverPatch
	{
		/// <summary>
		/// Replaces a valkyrie's hover text with her name, as the local player knows her, whether she's hungry, and her
		/// prompts
		/// </summary>
		/// <param name="__instance">The creature's Tameable</param>
		/// <param name="__result">Its hover text</param>
		private static void Postfix(Tameable __instance, ref string __result)
		{
			if (!__instance.TryGetComponent(out ValkyrieTalk talk))
			{
				return;
			}
			__result = talk.NameWithHunger();
			if (talk.CanTalk(Player.m_localPlayer))
			{
				__result += "\n" + Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Talk");
			}
			if (ValkyriePackWindow.CanOpen(__instance.m_character, Player.m_localPlayer))
			{
				__result += ValkyriePackWindow.Prompt();
			}
		}
	}

	/// <summary>
	/// Shows only a valkyrie's name over the mount panel while she's ridden, and whether she's hungry - not whether she's
	/// tamed, which the game puts after a mount's name
	/// </summary>
	[HarmonyPatch(typeof(Hud), nameof(Hud.UpdateMount))]
	internal static class ValkyrieMountNamePatch
	{
		/// <summary>
		/// Replaces the mount panel's name with a valkyrie's name, as the local player knows her, and whether she's hungry
		/// </summary>
		/// <param name="__instance">The HUD</param>
		/// <param name="player">The local player</param>
		private static void Postfix(Hud __instance, Player player)
		{
			if (!(player.GetDoodadController() is Sadle saddle) || saddle.GetCharacter() == null || !saddle.GetCharacter().TryGetComponent(out ValkyrieTalk talk))
			{
				return;
			}
			__instance.m_mountNameText.text = talk.NameWithHunger();
		}
	}

	/// <summary>
	/// Keeps a valkyrie from being befriended by being fed - the game tames a creature that's kept fed long enough, but
	/// she's befriended her own way. The game's tame command still befriends her (see ValkyrieTamePatch)
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.TamingUpdate))]
	internal static class ValkyrieTamingPatch
	{
		/// <summary>
		/// Skips the game's taming a fed creature, for a valkyrie
		/// </summary>
		/// <param name="__instance">The creature's Tameable</param>
		/// <returns>False for a valkyrie, to skip taming her</returns>
		private static bool Prefix(Tameable __instance)
		{
			return(__instance.GetComponent<ValkyrieTalk>() == null);
		}
	}

	/// <summary>
	/// Has a valkyrie be befriended rather than tamed, introducing her to the player nearest (see ValkyrieTalk.Befriend)
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.Tame))]
	internal static class ValkyrieTamePatch
	{
		/// <summary>
		/// Befriends a valkyrie in place of the game's taming
		/// </summary>
		/// <param name="__instance">The creature's Tameable</param>
		/// <returns>False for a valkyrie, to skip the game's taming</returns>
		private static bool Prefix(Tameable __instance)
		{
			if (!__instance.TryGetComponent(out ValkyrieTalk talk))
			{
				return(true);
			}
			talk.Befriend(__instance);
			return(false);
		}
	}

	/// <summary>
	/// Names a valkyrie by her own name to the players she's told it to, and plain valkyrie to the rest (see
	/// ValkyrieTalk.ShownName)
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverName))]
	internal static class ValkyrieNamePatch
	{
		/// <summary>
		/// Replaces a valkyrie's name with the one the local player knows her by
		/// </summary>
		/// <param name="__instance">The creature's Tameable</param>
		/// <param name="__result">Its name</param>
		private static void Postfix(Tameable __instance, ref string __result)
		{
			if (__instance.TryGetComponent(out ValkyrieTalk talk))
			{
				__result = talk.ShownName();
			}
		}
	}
}
