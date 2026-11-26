using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Has a forsaken power that reaches the local player from someone else say who activated it, in place of its own
	/// start message (see PowerGivers)
	/// </summary>
	[HarmonyPatch(typeof(StatusEffect), nameof(StatusEffect.Setup))]
	public static class PowerGiverMessagePatch
	{
		/// <summary>
		/// Swaps the message the status effect shows as it's set up, on its own copy
		/// </summary>
		/// <param name="__instance">The status effect, the character's own copy</param>
		/// <param name="character">Who it's set up on</param>
		private static void Prefix(StatusEffect __instance, Character character)
		{
			string message = PowerGivers.MessageFor(__instance, character);
			if (message != null)
			{
				__instance.m_startMessage = message;
			}
		}
	}
}
