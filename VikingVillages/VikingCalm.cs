using System.Runtime.CompilerServices;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Calms an aggravated living viking once it has slept on it - angered by day, it forgives the next morning, and
	/// angered at night, as the next night falls - with no player hurting it since
	/// </summary>
	/// <remarks>
	/// The world time it was last angered is kept in the viking's ZDO, so the count carries on through reloads and
	/// owner changes. A player's hit sets it to the time of the hit. A viking aggravated without being hurt itself, by a
	/// player hurting another creature nearby, counts from when its owner first finds it aggravated, as does one loaded
	/// aggravated with no time kept
	/// </remarks>
	internal static class VikingCalm
	{
		/// <summary>
		/// The world time in seconds an aggravated viking was last angered, or 0 while it's calm
		/// </summary>
		public static readonly int CalmFromKey = "kriona.VikingVillages.calmFrom".GetStableHashCode();

		/// <summary>
		/// How far through a day of world time the morning comes and the night falls, before the game rescales them for
		/// the sun (see EnvMan.RescaleDayFraction)
		/// </summary>
		private const double MorningAt = 0.15;
		private const double NightfallAt = 0.85;

		/// <summary>
		/// How often the owner checks each viking, in seconds
		/// </summary>
		private const float CheckInterval = 5f;

		/// <summary>
		/// When each AI is next checked, in Time.time seconds
		/// </summary>
		private class NextCheck
		{
			public float Time;
		}

		/// <summary>
		/// The next check of each AI seen
		/// </summary>
		private static readonly ConditionalWeakTable<MonsterAI, NextCheck> checks = new ConditionalWeakTable<MonsterAI, NextCheck>();

		/// <summary>
		/// Restarts a living viking's day when a player hurts it, on the viking's owner where its damage is applied
		/// </summary>
		/// <param name="ai">The AI of the creature hurt</param>
		/// <param name="attacker">Who hurt it, or null</param>
		public static void OnHurt(MonsterAI ai, Character attacker)
		{
			if (attacker == null || !attacker.IsPlayer() || ZNet.instance == null)
			{
				return;
			}
			ZNetView view = ai.m_nview;
			Character character = ai.m_character;
			if (view == null || !view.IsValid() || !view.IsOwner() || character == null || !VikingCore.IsLivingViking(character))
			{
				return;
			}
			view.GetZDO().Set(CalmFromKey, Now());
		}

		/// <summary>
		/// Checks a living viking every CheckInterval seconds on its owner - notes when it was angered when it's
		/// aggravated with no time kept, calms it once it forgives (see CalmAt), and clears the time once it's calm
		/// </summary>
		/// <param name="ai">The AI updating</param>
		public static void Update(MonsterAI ai)
		{
			ZNetView view = ai.m_nview;
			if (view == null || !view.IsValid() || !view.IsOwner() || ZNet.instance == null || EnvMan.instance == null)
			{
				return;
			}
			NextCheck next = checks.GetOrCreateValue(ai);
			if (Time.time < next.Time)
			{
				return;
			}
			next.Time = Time.time + CheckInterval;
			Character character = ai.m_character;
			if (character == null || character.IsDead() || !VikingCore.IsLivingViking(character))
			{
				return;
			}
			ZDO zdo = view.GetZDO();
			long calmFrom = zdo.GetLong(CalmFromKey, 0L);
			if (!ai.IsAggravated())
			{
				if (calmFrom != 0L)
				{
					zdo.Set(CalmFromKey, 0L);
				}
				return;
			}
			long now = Now();
			if (calmFrom == 0L)
			{
				zdo.Set(CalmFromKey, now);
			}
			else if (now >= CalmAt(calmFrom, EnvMan.instance.m_dayLengthSec))
			{
				ai.SetAggravated(false, BaseAI.AggravatedReason.Damage);
				zdo.Set(CalmFromKey, 0L);
			}
		}

		/// <summary>
		/// Works out when a viking angered at a time forgives - the next morning when it was angered by day, and as the
		/// next night falls when it was angered at night, whether before or after midnight
		/// </summary>
		/// <param name="angered">The world time it was angered, in seconds</param>
		/// <param name="dayLength">How long a day is, in seconds</param>
		/// <returns>The world time it forgives, in seconds</returns>
		internal static double CalmAt(long angered, long dayLength)
		{
			double length = System.Math.Max(1L, dayLength);
			double day = System.Math.Floor(angered / length);
			double fraction = (angered - day * length) / length;
			if (fraction < MorningAt)
			{
				// Angered after midnight, the night that falls next is the same day's
				return((day + NightfallAt) * length);
			}
			if (fraction < NightfallAt)
			{
				return((day + 1 + MorningAt) * length);
			}
			return((day + 1 + NightfallAt) * length);
		}

		/// <summary>
		/// The world time in whole seconds, never 0 so it can't be mistaken for no time kept
		/// </summary>
		/// <returns>The time</returns>
		private static long Now()
		{
			return(System.Math.Max(1L, (long)ZNet.instance.GetTimeSeconds()));
		}
	}
}
