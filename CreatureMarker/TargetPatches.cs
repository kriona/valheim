using System.Collections.Generic;
using HarmonyLib;

namespace CreatureMarker
{
	/// <summary>
	/// Stores the creature each monster is targeting in its ZDO, so every player with the mod can tell whether a
	/// monster run by another player's game is after them
	/// </summary>
	/// <remarks>
	/// Only the game that owns a monster runs its AI, and the game itself only shares whether it has a target, not
	/// which one. Setting a ZDO value to what it already holds doesn't sync anything, so this only adds traffic when
	/// the target changes
	/// </remarks>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateTarget))]
	public static class MonsterTargetPatch
	{
		/// <summary>
		/// The ZDO keys holding the ZDOID of the creature the monster is targeting
		/// </summary>
		public static readonly KeyValuePair<int, int> TargetHash = ZDO.GetHashZDOID(Plugin.PluginGuid + ".target");

		/// <summary>
		/// Stores the monster's target creature, or none when it has no target creature
		/// </summary>
		/// <param name="__instance">The monster whose target was just updated</param>
		private static void Postfix(MonsterAI __instance)
		{
			if (!__instance.m_nview.IsValid())
			{
				return;
			}
			ZDOID target = ((__instance.m_targetCreature != null) ? __instance.m_targetCreature.GetZDOID() : ZDOID.None);
			__instance.m_nview.GetZDO().Set(TargetHash, target);
		}
	}
}
