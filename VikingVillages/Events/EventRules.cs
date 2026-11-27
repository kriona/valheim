using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The rules a village's events go by - which one a day's roll picks, when one is off its cooldown, the time of day
	/// it starts at, and the biomes it happens in (see VillageEvents)
	/// </summary>
	internal static class EventRules
	{
		/// <summary>
		/// Picks the event a day's roll lands on - each event taking up its chance of the roll in turn, so the roll past
		/// all of them is no event
		/// </summary>
		/// <param name="chances">Each event's chance, from 0 to 1, adding up to no more than 1</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The event's index, or -1 for no event</returns>
		internal static int Pick(IList<float> chances, float roll)
		{
			float upTo = 0f;
			for (int i = 0; i < chances.Count; i++)
			{
				upTo += Mathf.Max(0f, chances[i]);
				if (roll < upTo)
				{
					return(i);
				}
			}
			return(-1);
		}

		/// <summary>
		/// Checks whether an event is off its cooldown on a day - it has never started, or its cooldown has run since the
		/// day it last did
		/// </summary>
		/// <param name="lastDay">The day it last started, or -1 when it never has</param>
		/// <param name="cooldownDays">How many days its cooldown lasts, counting the day it starts</param>
		/// <param name="today">The day</param>
		/// <returns>True when it can happen that day</returns>
		internal static bool OffCooldown(long lastDay, int cooldownDays, long today)
		{
			return(lastDay < 0L || today - lastDay >= cooldownDays);
		}

		/// <summary>
		/// Picks the time of day an event starts at - at random from the start of its window, or from now when that has
		/// passed, to the window's end
		/// </summary>
		/// <param name="now">The time of day now, from 0 at midnight to 1</param>
		/// <param name="windowStart">The time of day its window opens</param>
		/// <param name="windowEnd">The time of day its window closes</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The time of day, or -1 when the window has closed</returns>
		internal static float StartAt(float now, float windowStart, float windowEnd, float roll)
		{
			float from = Mathf.Max(now, windowStart);
			if (from >= windowEnd)
			{
				return(-1f);
			}
			return(Mathf.Lerp(from, windowEnd, roll));
		}

		/// <summary>
		/// Checks whether a village's biome is one an event happens in
		/// </summary>
		/// <param name="biome">The village's biome, by its index in VikingGear's tables</param>
		/// <param name="easiest">The easiest biome it happens in</param>
		/// <param name="hardest">The hardest biome it happens in</param>
		/// <returns>True when it is</returns>
		internal static bool InBiomes(int biome, int easiest, int hardest)
		{
			return(biome >= easiest && biome <= hardest);
		}

		/// <summary>
		/// Writes a time of day the way a clock shows it, for the debug text
		/// </summary>
		/// <param name="fraction">The time of day, from 0 at midnight to 1</param>
		/// <returns>The hours and minutes, like "14:05"</returns>
		internal static string Clock(float fraction)
		{
			int minutes = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(fraction, 1f) * 24f * 60f), 0, 24 * 60 - 1);
			return((minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00"));
		}
	}
}
