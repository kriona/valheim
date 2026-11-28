using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Has a friendly valkyrie's AI pass over her spin for another attack where it would hit buildings - the game asks
	/// this both as she picks an attack and as she makes it (see ValkyrieSpinGuard)
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanUseAttack))]
	internal static class ValkyrieSpinGuardPatch
	{
		/// <summary>
		/// Rules out a valkyrie's spin while it's held back
		/// </summary>
		/// <param name="__instance">The AI</param>
		/// <param name="item">The attack's item</param>
		/// <param name="__result">Whether the AI can use it</param>
		private static void Postfix(BaseAI __instance, ItemDrop.ItemData item, ref bool __result)
		{
			if (!__result || item == null || item.m_shared.m_name != ValkyrieCombat.SpinName || __instance.GetComponent<ValkyrieFlight>() == null)
			{
				return;
			}
			if (ValkyrieSpinGuard.Holds(__instance.m_character, item))
			{
				__result = false;
			}
		}
	}
}
