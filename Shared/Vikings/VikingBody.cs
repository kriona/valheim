using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// Works out a viking's skill factors from its level, the same as a player's
	/// </summary>
	internal static class VikingSkillFactors
	{
		/// <summary>
		/// How far a viking's hits stray either side of what its skill gives, as a share of the weapon's damage - the
		/// same as a player's
		/// </summary>
		private const float SkillDamageSpread = 0.15f;

		/// <summary>
		/// Works out a skill as a fraction of full skill
		/// </summary>
		/// <param name="skill">The skill, where None has no level</param>
		/// <param name="level">The viking's level in it, from 0 to 100</param>
		/// <returns>Its skill from 0 to 1</returns>
		internal static float Factor(Skills.SkillType skill, float level)
		{
			return((skill == Skills.SkillType.None) ? 0f : Mathf.Clamp01(level / 100f));
		}

		/// <summary>
		/// Works out the range a viking's damage factor is rolled in - from VikingCore.SkillDamageBase of a weapon's
		/// damage with no skill to all of it with full skill, give or take SkillDamageSpread
		/// </summary>
		/// <param name="skill">The skill the attack uses</param>
		/// <param name="level">The viking's level in it, from 0 to 100</param>
		/// <param name="min">The least the factor can be</param>
		/// <param name="max">The most the factor can be</param>
		internal static void DamageRange(Skills.SkillType skill, float level, out float min, out float max)
		{
			float factor = Mathf.Lerp(VikingCore.SkillDamageBase, 1f, Factor(skill, level));
			min = Mathf.Clamp01(factor - SkillDamageSpread);
			max = Mathf.Clamp01(factor + SkillDamageSpread);
		}
	}

	/// <summary>
	/// A viking's body - the game's Humanoid, in place of the Player its prefab is built from (see
	/// VikingCore.BuildPrefab), doing what living and zombie vikings share where the game only does it for a player
	/// </summary>
	/// <remarks>
	/// Each override calls the game's own method where the viking adds to it, so other patches of that method still
	/// run for a viking
	/// </remarks>
	internal abstract class VikingBody : Humanoid
	{
		/// <summary>
		/// The viking's looks, gear and skills
		/// </summary>
		protected VikingCore viking;

		/// <summary>
		/// Finds the viking's looks, gear and skills as the body wakes
		/// </summary>
		public override void Awake()
		{
			base.Awake();
			viking = GetComponent<VikingCore>();
			// The player's animator starts in the pose of waking up on the ground after a respawn, which only the Player
			// component the viking doesn't have turns off (see Player.SetupAwake), so a new viking stands from the start
			if (m_animator != null)
			{
				m_animator.SetBool(WakeupParameter, false);
			}
		}

		/// <summary>
		/// The animator's parameter for the pose of waking up on the ground after a respawn
		/// </summary>
		private const string WakeupParameter = "wakeup";

		/// <summary>
		/// Loads the viking's things as it starts
		/// </summary>
		/// <remarks>
		/// Start runs after a viking raised on this client has been handed its data, which its Awake runs before
		/// </remarks>
		public override void Start()
		{
			base.Start();
			if (viking != null)
			{
				viking.OnStart();
			}
		}

		/// <summary>
		/// Checks whether the viking has a beard
		/// </summary>
		/// <returns>True when it has, false when it has none or no data yet</returns>
		public bool HasBeard()
		{
			VikingData data = ((viking != null) ? viking.GetData() : null);
			return(data != null && data.HasBeard());
		}

		/// <summary>
		/// Gets the viking's skill as a fraction, the same as a player's, which a monster doesn't have
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>Its skill from 0 to 1</returns>
		public override float GetSkillFactor(Skills.SkillType skill)
		{
			float level = ((viking != null) ? viking.SkillLevel(skill) : -1f);
			if (level < 0f)
			{
				return(base.GetSkillFactor(skill));
			}
			return(VikingSkillFactors.Factor(skill, level));
		}

		/// <summary>
		/// Rolls the viking's damage factor from its skill, the same as a player's, in place of a monster's near-full
		/// damage (see VikingSkillFactors.DamageRange)
		/// </summary>
		/// <param name="skill">The skill the attack uses</param>
		/// <returns>The damage factor</returns>
		public override float GetRandomSkillFactor(Skills.SkillType skill)
		{
			float level = ((viking != null) ? viking.SkillLevel(skill) : -1f);
			if (level < 0f)
			{
				return(base.GetRandomSkillFactor(skill));
			}
			VikingSkillFactors.DamageRange(skill, level, out float min, out float max);
			return(Mathf.Lerp(min, max, Random.value));
		}

		/// <summary>
		/// Raises the viking's skill for the same things a player's rise for (see VikingCore.RaiseSkill), in place of a
		/// creature's, which only levels up a tamed one
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="value">How much the action raises it</param>
		public override void RaiseSkill(Skills.SkillType skill, float value = 1f)
		{
			if (viking != null)
			{
				viking.RaiseSkill(skill, value);
			}
		}

		/// <summary>
		/// Adds the resistances of the armor the viking wears to its own, the same as a player's
		/// </summary>
		/// <param name="mods">Its damage modifiers</param>
		public override void ApplyArmorDamageMods(ref HitData.DamageModifiers mods)
		{
			base.ApplyArmorDamageMods(ref mods);
			if (viking != null)
			{
				viking.ApplyArmorMods(ref mods);
			}
		}

		/// <summary>
		/// Adds up the armor the viking wears, with what its effects add, the way the game adds up a player's (see
		/// VikingCore.BodyArmor)
		/// </summary>
		/// <returns>Its armor</returns>
		public override float GetBodyArmor()
		{
			return((viking != null) ? viking.BodyArmor() : base.GetBodyArmor());
		}

		/// <summary>
		/// Wears down the viking's armor by a hit, the way the game wears down a player's (see VikingCore.WearArmor)
		/// </summary>
		/// <param name="hit">The hit, after resistances and armor</param>
		public override void DamageArmorDurability(HitData hit)
		{
			base.DamageArmorDurability(hit);
			if (viking != null)
			{
				viking.WearArmor(hit);
			}
		}

		/// <summary>
		/// Counts the viking's crossbow as always loaded, since only a player reloads - its AI waits between shots instead
		/// </summary>
		/// <returns>True</returns>
		public override bool IsWeaponLoaded()
		{
			return(true);
		}

		/// <summary>
		/// Switches the viking off a broken or empty weapon before it attacks, and wears its weapon down as the attack
		/// starts, since the game only wears down a player's weapons
		/// </summary>
		/// <param name="target">What it attacks</param>
		/// <param name="secondaryAttack">Whether it's the weapon's secondary attack</param>
		/// <returns>Whether the attack started</returns>
		public override bool StartAttack(Character target, bool secondaryAttack)
		{
			if (viking != null)
			{
				viking.CheckWeapon();
			}
			bool started = base.StartAttack(target, secondaryAttack);
			if (started && viking != null)
			{
				viking.WearWeapon();
			}
			return(started);
		}

		/// <summary>
		/// Gives the viking's corpse the player's looks, since a monster's corpse only gets its equipment
		/// </summary>
		/// <param name="ragdoll">The corpse, just made by this client</param>
		public override void OnRagdollCreated(Ragdoll ragdoll)
		{
			base.OnRagdollCreated(ragdoll);
			if (viking != null)
			{
				viking.OnRagdollCreated(ragdoll);
			}
		}

		/// <summary>
		/// Gets the viking's hover text, with what it shows a player looking at it, and a summary of its state and AI
		/// after it in debug mode
		/// </summary>
		/// <returns>The hover text</returns>
		public override string GetHoverText()
		{
			string text = HoverText(base.GetHoverText());
			if (!Player.m_debugMode || viking == null)
			{
				return(text);
			}
			string summary = viking.DebugSummary();
			return(string.IsNullOrEmpty(text) ? summary : text + "\n" + summary);
		}

		/// <summary>
		/// Adds what the viking shows a player looking at it to the game's hover text, ahead of the debug summary
		/// </summary>
		/// <param name="text">The game's hover text</param>
		/// <returns>The hover text</returns>
		protected virtual string HoverText(string text)
		{
			return(text);
		}
	}
}
