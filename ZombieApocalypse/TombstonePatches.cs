using HarmonyLib;
using Kriona.Vikings;

namespace ZombieApocalypse
{
	/// <summary>
	/// Turns the local player into a zombie viking when a zombie kills them, unless they have corpse run
	/// </summary>
	/// <remarks>
	/// OnDeath makes the player's corpse and tombstone, so the equipment they're wearing is read first, and their
	/// things are split between the tombstone and the viking once both are made
	/// </remarks>
	[HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
	public static class VikingPlayerDeathPatch
	{
		/// <summary>
		/// Records the player's looks and equipment when a zombie dealt the killing blow and they don't have corpse
		/// run
		/// </summary>
		/// <remarks>
		/// Corpse run is checked before the game's OnDeath runs, while the player still has their status effects
		/// </remarks>
		/// <param name="__instance">The dying player</param>
		private static void Prefix(Player __instance)
		{
			VikingCore.Pending = null;
			if (!Plugin.TombstoneZombies.Value || !ServerCheck.Active || __instance != Player.m_localPlayer || !__instance.m_nview.IsOwner())
			{
				return;
			}
			Character killer = __instance.m_lastHit?.GetAttacker();
			if (killer != null && ZombieState.IsZombie(killer) && !CorpseRun.Protects(__instance))
			{
				VikingCore.Pending = VikingCore.BeginPlayerDeath(__instance);
			}
		}

		/// <summary>
		/// Sets the player's corpse to rise as a viking carrying their things
		/// </summary>
		private static void Postfix()
		{
			PlayerDeath death = VikingCore.Pending;
			VikingCore.Pending = null;
			if (death != null)
			{
				VikingCore.FinishPlayerDeath(death);
			}
		}
	}

	/// <summary>
	/// Notes the tombstone a player's death makes while they die by a zombie's hand
	/// </summary>
	[HarmonyPatch(typeof(TombStone), nameof(TombStone.Setup))]
	public static class VikingGravePatch
	{
		/// <summary>
		/// Records the tombstone for the death in progress
		/// </summary>
		/// <param name="__instance">The tombstone</param>
		private static void Postfix(TombStone __instance)
		{
			PlayerDeath death = VikingCore.Pending;
			if (death != null && death.Grave == null)
			{
				death.Grave = __instance;
			}
		}
	}
}
