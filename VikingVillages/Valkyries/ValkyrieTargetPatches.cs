using System;
using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Has a valkyrie go after the enemies that can strike her at range in the air first (see
	/// ValkyrieSkirmish.RangedTarget)
	/// </summary>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateTarget), new Type[] { typeof(Humanoid), typeof(float), typeof(bool), typeof(bool) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out })]
	internal static class ValkyrieTargetPatch
	{
		/// <summary>
		/// Swaps a valkyrie's target for the nearest enemy she can see that can strike her at range, when the game's
		/// pick can't
		/// </summary>
		/// <param name="__instance">The AI picking a target</param>
		private static void Postfix(MonsterAI __instance)
		{
			if (!__instance.TryGetComponent(out ValkyrieSkirmish skirmish) || __instance.m_character.InAttack())
			{
				return;
			}
			Character current = __instance.m_targetCreature;
			if (current != null && ValkyrieSkirmish.CanStrikeAtRange(current))
			{
				return;
			}
			Character ranged = skirmish.RangedTarget(__instance);
			if (ranged != null)
			{
				__instance.m_targetCreature = ranged;
				__instance.m_targetStatic = null;
			}
		}
	}
}
