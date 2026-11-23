using HarmonyLib;

namespace ZombieApocalypse
{
	/// <summary>
	/// Notes every corpse as it wakes, so the vikings' hazards are found without looking through every loaded object
	/// </summary>
	[HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.Awake))]
	public static class VillageHazardsRagdollPatch
	{
		/// <summary>
		/// Hands the corpse that woke to VillageHazards
		/// </summary>
		/// <param name="__instance">The corpse</param>
		private static void Postfix(Ragdoll __instance)
		{
			VillageHazards.Register(__instance);
		}
	}
}
