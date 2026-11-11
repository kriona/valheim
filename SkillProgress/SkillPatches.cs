using HarmonyLib;

namespace SkillProgress
{
	/// <summary>
	/// Shows a skill's row each time the local player trains it - every skill gain goes through RaiseSkill
	/// </summary>
	/// <remarks>
	/// The game's level cap is a literal inside Skill.Raise rather than a value that can be read, and mods that raise it
	/// patch that method, so whether a skill can still level up is judged by whether the call changed its level or
	/// progress
	/// </remarks>
	[HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
	public static class RaiseSkillPatch
	{
		/// <summary>
		/// Notes the skill's level and progress before it's raised, when it belongs to the local player
		/// </summary>
		/// <param name="__instance">The skills of the player who trained</param>
		/// <param name="skillType">The skill being trained</param>
		/// <param name="__state">The skill's level and progress, or null when the skill isn't the local player's</param>
		private static void Prefix(Skills __instance, Skills.SkillType skillType, out (float level, float accumulator)? __state)
		{
			__state = null;
			if (skillType == Skills.SkillType.None || __instance.m_player == null || __instance.m_player != Player.m_localPlayer)
			{
				return;
			}
			Skills.Skill skill = __instance.GetSkill(skillType);
			__state = (skill.m_level, skill.m_accumulator);
		}

		/// <summary>
		/// Shows the skill's row when it gained, or removes it when it couldn't level up any further
		/// </summary>
		/// <param name="__instance">The skills of the player who trained</param>
		/// <param name="skillType">The skill that was trained</param>
		/// <param name="__state">The skill's level and progress before it was raised</param>
		private static void Postfix(Skills __instance, Skills.SkillType skillType, (float level, float accumulator)? __state)
		{
			if (__state == null)
			{
				return;
			}
			Skills.Skill skill = __instance.GetSkill(skillType);
			if (skill.m_level == __state.Value.level && skill.m_accumulator == __state.Value.accumulator)
			{
				SkillBars.Maxed(skillType);
				return;
			}
			SkillBars.Trained(__instance, skillType);
		}
	}
}
