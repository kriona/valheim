using System;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The rules for a PC viking's power attacks - its melee weapon's secondary attack: which ones stagger, which
	/// hit harder than the weapon's normal blow, when each is a smart move and how often it can use one - worked out
	/// apart from the game so they can be tested
	/// </summary>
	internal static class PowerAttackRules
	{
		/// <summary>
		/// What a power attack is good for - staggering, with at least StaggerEdge times the stagger of the weapon's
		/// normal blow, and hitting hard, with at least DamageEdge times its damage. A mace's does both
		/// </summary>
		[Flags]
		internal enum Kind
		{
			None = 0,
			Staggers = 1,
			HitsHard = 2
		}

		/// <summary>
		/// How many times the stagger and the damage of the weapon's normal blow a power attack has to have to count as
		/// staggering or hitting hard
		/// </summary>
		internal const float StaggerEdge = 2f;
		internal const float DamageEdge = 1.5f;

		/// <summary>
		/// How wide a power attack swings, in degrees, to catch every enemy around the viking - an atgeir's spin
		/// </summary>
		internal const float WideAngle = 180f;

		/// <summary>
		/// How many enemies within reach make a wide stagger worth it with no one there to help
		/// </summary>
		internal const int CrowdSize = 2;

		/// <summary>
		/// The least time between two of a viking's power attacks, in seconds
		/// </summary>
		internal const float RestMin = 3f;

		/// <summary>
		/// Works out what a weapon's power attack is good for, against its normal blow. A thrown or shot one - a spear's
		/// throw, which leaves the spear on the ground - is never used, nor a weapon with none
		/// </summary>
		/// <param name="normal">The weapon's normal attack</param>
		/// <param name="power">Its secondary attack</param>
		/// <returns>What it's good for, or Kind.None</returns>
		internal static Kind Classify(Attack normal, Attack power)
		{
			if (normal == null || power == null || !Usable(power.m_attackType, power.m_consumeItem, !string.IsNullOrEmpty(power.m_attackAnimation)))
			{
				return(Kind.None);
			}
			return(Classify(normal.m_damageMultiplier, normal.m_staggerMultiplier, power.m_damageMultiplier, power.m_staggerMultiplier));
		}

		/// <summary>
		/// Checks whether a secondary attack is one a viking uses at all - a blow, not thrown or shot, that doesn't use up
		/// the weapon, on a weapon that has one
		/// </summary>
		/// <param name="type">How it attacks</param>
		/// <param name="consumes">Whether it uses up the weapon</param>
		/// <param name="animated">Whether it has an animation, which a weapon with no secondary attack doesn't</param>
		/// <returns>True when it is</returns>
		internal static bool Usable(Attack.AttackType type, bool consumes, bool animated)
		{
			if (!animated || consumes)
			{
				return(false);
			}
			return(type != Attack.AttackType.Projectile && type != Attack.AttackType.TriggerProjectile && type != Attack.AttackType.None);
		}

		/// <summary>
		/// Works out what a power attack is good for from its multipliers against the normal blow's
		/// </summary>
		/// <param name="normalDamage">The normal blow's damage multiplier</param>
		/// <param name="normalStagger">The normal blow's stagger multiplier</param>
		/// <param name="powerDamage">The power attack's damage multiplier</param>
		/// <param name="powerStagger">The power attack's stagger multiplier</param>
		/// <returns>What it's good for, or Kind.None</returns>
		internal static Kind Classify(float normalDamage, float normalStagger, float powerDamage, float powerStagger)
		{
			Kind kind = Kind.None;
			if (powerStagger >= normalStagger * StaggerEdge)
			{
				kind |= Kind.Staggers;
			}
			if (powerDamage >= normalDamage * DamageEdge)
			{
				kind |= Kind.HitsHard;
			}
			return(kind);
		}

		/// <summary>
		/// Checks whether a power attack swings wide enough to catch every enemy around the viking
		/// </summary>
		/// <param name="angle">How wide it swings, in degrees</param>
		/// <returns>True when it does</returns>
		internal static bool Wide(float angle)
		{
			return(angle >= WideAngle);
		}

		/// <summary>
		/// Decides whether a power attack is a smart move against an enemy. On one that's staggering, which takes double
		/// damage, only one that hits hard is - a stagger would be wasted on it. Otherwise one that staggers is, on an
		/// enemy that can be staggered, when someone else is at the enemy to make the most of the double damage, or when
		/// it swings wide and catches CrowdSize or more enemies - alone against one, it knocks the enemy back out of reach
		/// before the viking can. And one that hits hard is, unless someone at the enemy can stagger it, when it's saved
		/// for the stagger
		/// </summary>
		/// <param name="kind">What the power attack is good for</param>
		/// <param name="wide">Whether it swings wide (see Wide)</param>
		/// <param name="staggering">Whether the enemy is staggering</param>
		/// <param name="staggerable">Whether the enemy can be staggered</param>
		/// <param name="enemies">How many enemies are within its reach, the enemy among them</param>
		/// <param name="helped">Whether a fellow or a player is at the enemy too</param>
		/// <param name="staggerComing">Whether someone else at the enemy holds a weapon whose power attack staggers</param>
		/// <returns>True when it's a smart move</returns>
		internal static bool Uses(Kind kind, bool wide, bool staggering, bool staggerable, int enemies, bool helped, bool staggerComing)
		{
			bool staggers = ((kind & Kind.Staggers) != 0);
			bool hitsHard = ((kind & Kind.HitsHard) != 0);
			if (staggering)
			{
				return(hitsHard);
			}
			if (staggers && staggerable && (helped || (wide && enemies >= CrowdSize)))
			{
				return(true);
			}
			return(hitsHard && !(staggerable && staggerComing));
		}

		/// <summary>
		/// Works out how long a viking waits after a power attack before its next - as long as a player takes to get back
		/// the stamina it costs them (see Player.m_staminaRegen), since a viking spends none, and at least RestMin
		/// </summary>
		/// <param name="stamina">The stamina the attack costs a player</param>
		/// <returns>The wait, in seconds</returns>
		internal static float Rest(float stamina)
		{
			return(Mathf.Max(RestMin, stamina / BoatRules.StaminaRegen));
		}
	}
}
