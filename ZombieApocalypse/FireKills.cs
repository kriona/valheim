using System.Runtime.CompilerServices;

namespace ZombieApocalypse
{
	/// <summary>
	/// Adds up how much of the damage each creature takes is fire, so a creature burned enough on its way to dying
	/// stays dead (see Plugin.FireStaysDead)
	/// </summary>
	/// <remarks>
	/// Each hit is counted as it arrives, with the creature's resistances applied to a copy of it, since the game
	/// applies them as it deals the hit. Burning counts too, since each of its ticks arrives as a hit of fire. Healing
	/// takes off fire and the rest alike, keeping their shares, and a creature back to full health starts over
	/// </remarks>
	internal static class FireKills
	{
		/// <summary>
		/// The damage a creature has taken, and how much of it was fire
		/// </summary>
		internal class Tally
		{
			/// <summary>
			/// How far under its share of fire, in percentage points, a creature's fire can fall and still count as
			/// burned, so the rounding of healing it partway doesn't take an all-fire tally just under 100 percent
			/// </summary>
			private const float ShareTolerance = 0.01f;

			public float Total;
			public float Fire;

			/// <summary>
			/// Adds a hit, after its resistances
			/// </summary>
			/// <param name="total">All of its damage</param>
			/// <param name="fire">Its fire damage</param>
			public void Add(float total, float fire)
			{
				Total += total;
				Fire += fire;
			}

			/// <summary>
			/// Takes healing off the damage, from fire and the rest in proportion to how much of each there is
			/// </summary>
			/// <param name="healed">How much health the creature got back</param>
			/// <returns>True when it healed all of the damage, so the tally is to be forgotten - it's left unchanged then</returns>
			public bool Heal(float healed)
			{
				if (healed >= Total)
				{
					return(true);
				}
				float left = Total - healed;
				Fire *= left / Total;
				Total = left;
				return(false);
			}

			/// <summary>
			/// Checks whether at least a share of the damage was fire
			/// </summary>
			/// <param name="share">The share, in percent - 0 or less never counts</param>
			/// <returns>True when it was</returns>
			public bool Burned(int share)
			{
				if (share <= 0 || Total <= 0f)
				{
					return(false);
				}
				return(Fire / Total * 100f >= share - ShareTolerance);
			}
		}

		/// <summary>
		/// Each creature's damage so far
		/// </summary>
		private static readonly ConditionalWeakTable<Character, Tally> tallies = new ConditionalWeakTable<Character, Tally>();

		/// <summary>
		/// Counts a hit arriving at a creature
		/// </summary>
		/// <param name="character">The creature</param>
		/// <param name="hit">The hit, before its resistances are applied</param>
		public static void Count(Character character, HitData hit)
		{
			if (character == null || hit == null)
			{
				return;
			}
			HitData copy = hit.Clone();
			copy.ApplyResistance(character.GetDamageModifiers(), out HitData.DamageModifier _);
			float total = copy.GetTotalDamage();
			if (total <= 0f)
			{
				return;
			}
			tallies.GetOrCreateValue(character).Add(total, copy.m_damage.m_fire);
		}

		/// <summary>
		/// Takes healing off a creature's damage, from each kind in proportion to how much of it there is - forgetting it
		/// all once the creature is back to full health
		/// </summary>
		/// <param name="character">The creature</param>
		/// <param name="healed">How much health it got back</param>
		/// <param name="full">True when it's back to full health</param>
		public static void Heal(Character character, float healed, bool full)
		{
			if (character == null || !tallies.TryGetValue(character, out Tally tally))
			{
				return;
			}
			if (full || tally.Heal(healed))
			{
				tallies.Remove(character);
			}
		}

		/// <summary>
		/// Checks whether at least FireStaysDead percent of the damage a creature took was fire
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it was, so it stays dead</returns>
		public static bool Burned(Character character)
		{
			int share = Plugin.FireStaysDead.Value;
			if (share <= 0 || !tallies.TryGetValue(character, out Tally tally))
			{
				return(false);
			}
			return(tally.Burned(share));
		}
	}
}
