using HarmonyLib;

namespace ZombieApocalypse
{
	/// <summary>
	/// Turns a creature marked as a zombie in its ZDO into one as it loads - on another client, or after a reload - and
	/// every zombie viking, which is always one
	/// </summary>
	/// <remarks>
	/// A zombie raised on this client isn't marked yet when its Awake runs, so Zombies.Raise sets it up instead
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.Awake))]
	public static class ZombieAwakePatch
	{
		/// <summary>
		/// Applies a zombie's faction, loot, look and getting up
		/// </summary>
		/// <param name="__instance">The creature loading</param>
		private static void Postfix(Character __instance)
		{
			if (!__instance.IsPlayer() && (Zombies.IsZombie(__instance) || ZombieViking.IsViking(__instance)))
			{
				Zombies.Apply(__instance);
			}
		}
	}

	/// <summary>
	/// Holds a zombie's AI still while it gets up
	/// </summary>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
	public static class ZombieAIPatch
	{
		/// <summary>
		/// Skips the AI's update and keeps the zombie in place while RiseBlend holds it
		/// </summary>
		/// <param name="__instance">The AI updating</param>
		/// <param name="__result">Whether the AI handled the update, set to true when it's held</param>
		/// <returns>False to skip the update</returns>
		private static bool Prefix(MonsterAI __instance, ref bool __result)
		{
			if (!RiseBlend.IsHeld(__instance))
			{
				return(true);
			}
			__instance.StopMoving();
			__result = true;
			return(false);
		}
	}

	/// <summary>
	/// Makes a zombie immune to damage while it gets up
	/// </summary>
	/// <remarks>
	/// Every hit, arrow, fire and poison reaches a creature through this RPC, so skipping it also skips the hit's
	/// blood, sounds and stagger
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
	public static class ZombieDamagePatch
	{
		/// <summary>
		/// Skips the damage while the zombie is still getting up
		/// </summary>
		/// <param name="__instance">The creature being hit</param>
		/// <returns>False to skip the damage</returns>
		private static bool Prefix(Character __instance)
		{
			return(__instance.IsPlayer() || !Zombies.IsRising(__instance));
		}
	}

	/// <summary>
	/// Keeps zombies from being tamed, so they stay hostile to players
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.SetTamed))]
	public static class ZombieTamePatch
	{
		/// <summary>
		/// Skips taming a zombie
		/// </summary>
		/// <param name="__instance">The creature being tamed or untamed</param>
		/// <param name="tamed">True when it's being tamed</param>
		/// <returns>False to skip taming a zombie</returns>
		private static bool Prefix(Character __instance, bool tamed)
		{
			return(!tamed || !Zombies.IsZombie(__instance));
		}
	}

	/// <summary>
	/// Stops a fed zombie making progress toward being tamed and showing the soothed effect
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.TamingUpdate))]
	public static class ZombieTamingPatch
	{
		/// <summary>
		/// Skips the taming update for a zombie
		/// </summary>
		/// <param name="__instance">The creature's Tameable</param>
		/// <returns>False to skip the update for a zombie</returns>
		private static bool Prefix(Tameable __instance)
		{
			return(__instance.m_character == null || !Zombies.IsZombie(__instance.m_character));
		}
	}

	/// <summary>
	/// Puts ZombieName in front of a zombie's name
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.GetHoverName))]
	public static class ZombieNamePatch
	{
		/// <summary>
		/// Adds the word to a zombie's name
		/// </summary>
		/// <param name="__instance">The creature being named</param>
		/// <param name="__result">Its name</param>
		private static void Postfix(Character __instance, ref string __result)
		{
			string word = Plugin.ZombieName.Value;
			if (!string.IsNullOrEmpty(word) && Zombies.IsZombie(__instance))
			{
				__result = word + " " + __result;
			}
		}
	}
}
