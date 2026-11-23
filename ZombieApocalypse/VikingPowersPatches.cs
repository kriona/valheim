using HarmonyLib;

namespace ZombieApocalypse
{
	/// <summary>
	/// Has a living viking learn a forsaken power it's given - by a player near it using theirs, or another viking - on
	/// the client that owns it, where every status effect added by name ends up
	/// </summary>
	[HarmonyPatch(typeof(SEMan), nameof(SEMan.Internal_AddStatusEffect))]
	public static class VikingPowerLearnPatch
	{
		/// <summary>
		/// Notes the status effect on the viking it was added to (see VikingPowers.Received)
		/// </summary>
		/// <param name="__instance">The status effects of whoever was given it</param>
		/// <param name="nameHash">The status effect's name hash</param>
		private static void Postfix(SEMan __instance, int nameHash)
		{
			if (__instance.m_character is LivingVikingBody viking && viking.Powers != null)
			{
				viking.Powers.Received(nameHash);
			}
		}
	}
}
