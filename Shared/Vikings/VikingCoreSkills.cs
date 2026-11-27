using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// A viking's skills - its level in each, raising them the way a player's are raised, and what they add to its
	/// running speed
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Gets the viking's level in a skill, with what its effects add, like a piece of gear that raises a skill, and
		/// rounded down to a whole level, the way the game's Skills.GetSkillLevel gives a player's for everything it
		/// goes into (see ExactSkillLevel)
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>The level, or a negative number before the viking has data</returns>
		public float SkillLevel(Skills.SkillType skill)
		{
			float level = ExactSkillLevel(skill);
			return((level >= 0f) ? Mathf.Floor(level) : level);
		}

		/// <summary>
		/// Gets the viking's level in a skill, with what its effects add, including how far it has come toward the next
		/// level, for the skills dialog's bars (see OwnSkillLevel)
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>The level, or a negative number before the viking has data</returns>
		public float ExactSkillLevel(Skills.SkillType skill)
		{
			float level = OwnSkillLevel(skill);
			if (level >= 0f)
			{
				humanoid.GetSEMan().ModifySkillLevel(skill, ref level);
			}
			return(level);
		}

		/// <summary>
		/// Gets the viking's own level in a skill, before what its effects add - a viking of the world's own level in it,
		/// as rolled (see VikingGear.RollData) and raised as a PC viking practices it (see RaiseSkill), or its one
		/// level for every skill when it was rolled before each skill had its own, or the dead player's level for a
		/// tombstone viking
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>The level from 0 to 100, or a negative number before the viking has data</returns>
		private float OwnSkillLevel(Skills.SkillType skill)
		{
			if (GetData() == null)
			{
				return(-1f);
			}
			// Every viking is at least SkillFloor in every skill, a tombstone viking whose player never used one included -
			// but an NPC viking, who was never taught to fight, has none at all in its combat skills (see
			// Viking Villages' NPCVikings.Skill)
			if (!data.Tombstone)
			{
				float floor = (IsNPCViking(humanoid) ? 0f : SkillFloor);
				return(Mathf.Max(floor, data.Skills.TryGetValue((int)skill, out float own) ? own : data.Skill));
			}
			// A tombstone viking from before vikings had skills fights at its biome's middle skill
			if (data.Skills.Count == 0)
			{
				BiomeGear gear = VikingGear.Get(BiomeIndex());
				return(Mathf.Max(SkillFloor, (gear.SkillMin + gear.SkillMax) / 2f));
			}
			return(Mathf.Max(SkillFloor, data.Skills.TryGetValue((int)skill, out float level) ? level : 0f));
		}

		/// <summary>
		/// The lowest level a viking has in any skill
		/// </summary>
		public const float SkillFloor = 1f;

		/// <summary>
		/// Raises a living viking's skill the way a player's rises, for the same things, like a hit with a
		/// weapon or a block - by the skill's step, the world's skill gain rate, its status effects and being rested,
		/// toward the next level the way a player's is, from its own level in everything - glowing around its head on
		/// every client as it reaches a new level, the way a player does
		/// </summary>
		/// <remarks>
		/// Its levels are kept in its data as it rises, rather than a player's whole levels and progress toward the next,
		/// so it rises a bit at a time at the rate a player's fills up
		/// </remarks>
		/// <param name="skill">The skill</param>
		/// <param name="factor">How much the action raises it, as the game gives it</param>
		public void RaiseSkill(Skills.SkillType skill, float factor)
		{
			if (skill == Skills.SkillType.None || !IsLivingViking(humanoid) || nview == null || !nview.IsValid() || !nview.IsOwner())
			{
				return;
			}
			// Read again, since another client may have raised its skills while it owned the viking
			data = VikingData.FromBytes(nview.GetZDO().GetByteArray(DataKey));
			if (data == null || data.Tombstone)
			{
				return;
			}
			float level = OwnSkillLevel(skill);
			if (level >= 100f)
			{
				return;
			}
			float multiplier = 1f;
			humanoid.GetSEMan().ModifyRaiseSkill(skill, ref multiplier);
			ModifySkillGain(skill, ref multiplier);
			float gain = IncreaseStep(skill) * factor * multiplier * Game.m_skillGainRate;
			float raised = RaisedLevel(level, gain);
			data.Skills[(int)skill] = raised;
			nview.GetZDO().Set(DataKey, data.ToBytes());
			if (!LeveledUp(level, raised))
			{
				return;
			}
			OnLevelUp(skill, raised);
			if (Player.m_debugMode)
			{
				string label = data.Name;
				NameForLog(ref label);
				VikingMod.Log.LogInfo("Skill: " + label + " at " + transform.position.ToString("F0") + " reached " + skill + " " + Mathf.Floor(raised));
			}
		}

		/// <summary>
		/// Changes how much a skill rises by what only the viking's kind has, like a PC viking being rested
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="multiplier">How much it rises, changed here</param>
		partial void ModifySkillGain(Skills.SkillType skill, ref float multiplier);

		/// <summary>
		/// Shows the viking reaching a new level in a skill, the way its kind does
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="raised">Its new level</param>
		partial void OnLevelUp(Skills.SkillType skill, float raised);

		/// <summary>
		/// Works out how much a player has to gain in a skill to reach the next level from a level, the game's
		/// Skills.Skill.GetNextLevelRequirement
		/// </summary>
		/// <param name="level">The level, from 0 to 100</param>
		/// <returns>The gain the next level takes</returns>
		internal static float LevelRequirement(float level)
		{
			return(Mathf.Pow(Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f);
		}

		/// <summary>
		/// Works out a PC viking's level in a skill after a gain - rising by the share of what the next level takes
		/// (see LevelRequirement) that the gain is, so a player's gain to a whole level takes the viking there too, up to
		/// 100
		/// </summary>
		/// <param name="level">Its level, from 0 to 100</param>
		/// <param name="gain">What it gains, as a player's skill would</param>
		/// <returns>The raised level</returns>
		internal static float RaisedLevel(float level, float gain)
		{
			return(Mathf.Min(100f, level + gain / LevelRequirement(level)));
		}

		/// <summary>
		/// Checks whether a skill rising reached a new whole level
		/// </summary>
		/// <param name="level">The level before</param>
		/// <param name="raised">The level after</param>
		/// <returns>True when it did</returns>
		internal static bool LeveledUp(float level, float raised)
		{
			return(Mathf.Floor(raised) > Mathf.Floor(level));
		}

		/// <summary>
		/// Gets how fast a skill rises for a player, which a viking's rises at too
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>Its step, 1 when the player has none for it</returns>
		private static float IncreaseStep(Skills.SkillType skill)
		{
			if (increaseSteps == null)
			{
				Skills skills = ((Game.instance != null && Game.instance.m_playerPrefab != null) ? Game.instance.m_playerPrefab.GetComponent<Skills>() : null);
				if (skills == null)
				{
					return(1f);
				}
				increaseSteps = new Dictionary<Skills.SkillType, float>();
				foreach (Skills.SkillDef def in skills.m_skills)
				{
					increaseSteps[def.m_skill] = def.m_increseStep;
				}
			}
			return(increaseSteps.TryGetValue(skill, out float step) ? step : 1f);
		}
	}
}
