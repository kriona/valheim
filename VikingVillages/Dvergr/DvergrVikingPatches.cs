using HarmonyLib;
using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// Keeps the dvergr, the dvergr vikings and the PC vikings from hurting each other - a dvergr mage's fireball
	/// landing among PC vikings, or a viking's sledge swung among the dvergr - while they aren't enemies, which as
	/// one faction they never are
	/// </summary>
	/// <remarks>
	/// The game leaves a creature's own side out of an attack only when the attack says so, and some of the dvergr's
	/// spells hit everyone in reach. Hits between the dvergr themselves are left as the game has them
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
	public static class DvergrFriendlyFirePatch
	{
		/// <summary>
		/// Skips a hit from one of the dvergr's side on another, when one of them is a viking and they aren't enemies
		/// </summary>
		/// <param name="__instance">The creature hit</param>
		/// <param name="hit">The hit</param>
		/// <returns>False to skip the hit</returns>
		private static bool Prefix(Character __instance, HitData hit)
		{
			if (hit == null || __instance == null)
			{
				return(true);
			}
			Character attacker = hit.GetAttacker();
			if (attacker == null || attacker == __instance || attacker.IsPlayer())
			{
				return(true);
			}
			bool victimSide = (__instance.GetFaction() == Character.Faction.Dverger && !__instance.IsTamed());
			bool attackerSide = (attacker.GetFaction() == Character.Faction.Dverger && !attacker.IsTamed());
			bool viking = (VikingCore.IsPCOrDvergr(__instance) || VikingCore.IsPCOrDvergr(attacker));
			return(!DvergrFriendlyFireRules.Spared(victimSide, attackerSide, viking, BaseAI.IsEnemy(attacker, __instance)));
		}
	}

	/// <summary>
	/// What decides whether a hit between creatures of the dvergr's side is kept from landing
	/// </summary>
	internal static class DvergrFriendlyFireRules
	{
		/// <summary>
		/// Checks whether a hit is kept from landing - one of the dvergr's side on another, one of them a PC or dvergr
		/// viking, while they aren't enemies
		/// </summary>
		/// <param name="victimSide">Whether the creature hit is of the dvergr's faction, and not tamed</param>
		/// <param name="attackerSide">Whether the one who hit it is</param>
		/// <param name="viking">Whether either is a PC or dvergr viking</param>
		/// <param name="enemies">Whether the one who hit it takes it for an enemy</param>
		/// <returns>True to keep it from landing</returns>
		internal static bool Spared(bool victimSide, bool attackerSide, bool viking, bool enemies)
		{
			return(victimSide && attackerSide && viking && !enemies);
		}
	}
}
