using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Notes every corpse as it wakes, so the corpses of the villages' vikings are found without looking through every
	/// loaded object
	/// </summary>
	[HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.Awake))]
	public static class VillageFallenCorpsePatch
	{
		/// <summary>
		/// Hands the corpse that woke to VillageFallen
		/// </summary>
		/// <param name="__instance">The corpse</param>
		private static void Postfix(Ragdoll __instance)
		{
			VillageFallen.NoteCorpse(__instance);
		}
	}

	/// <summary>
	/// Marks a PC viking's corpse with its village and its name, so the vikings near it know whose it is
	/// </summary>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.OnRagdollCreated))]
	public static class VillageFallenMarkPatch
	{
		/// <summary>
		/// Marks the corpse its owner just made
		/// </summary>
		/// <param name="__instance">The creature that died</param>
		/// <param name="ragdoll">Its corpse</param>
		private static void Postfix(Humanoid __instance, Ragdoll ragdoll)
		{
			VillageFallen.MarkCorpse(__instance, ragdoll);
		}
	}
}
