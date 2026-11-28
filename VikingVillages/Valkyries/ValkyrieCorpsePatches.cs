using HarmonyLib;
using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// Names a zombie Fallen Valkyrie a valkyrie, which Zombie Apocalypse calls a Zombie Valkyrie - the Fallen Valkyrie
	/// leaves no corpse to rise from, so every zombie one is a valkyrie risen from hers (see Link.RisesAs)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.GetHoverName))]
	internal static class ZombieValkyrieNamePatch
	{
		/// <summary>
		/// Gives a zombie Fallen Valkyrie a valkyrie's name before the game reads it, which Zombie Apocalypse puts its
		/// zombie word in front of
		/// </summary>
		/// <param name="__instance">The creature being named</param>
		private static void Prefix(Character __instance)
		{
			if (__instance.m_name != ValkyriePrefab.DisplayName && Utils.GetPrefabName(__instance.gameObject) == ValkyrieCombat.FallenPrefab && ZombieState.IsZombie(__instance))
			{
				__instance.m_name = ValkyriePrefab.DisplayName;
			}
		}
	}

	/// <summary>
	/// Poses a valkyrie's corpse the way she was as she died (see ValkyrieCorpse.TakePose)
	/// </summary>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.OnRagdollCreated))]
	internal static class ValkyrieCorpsePosePatch
	{
		/// <summary>
		/// Hands a dying valkyrie's pose to her corpse as the game creates it, on the client that owns her
		/// </summary>
		/// <param name="__instance">The dying creature</param>
		/// <param name="ragdoll">Its corpse</param>
		private static void Postfix(Humanoid __instance, Ragdoll ragdoll)
		{
			if (ragdoll != null && __instance.GetComponent<ValkyrieFlight>() != null && ragdoll.TryGetComponent(out ValkyrieCorpse corpse))
			{
				corpse.TakePose(__instance);
			}
		}
	}
}
