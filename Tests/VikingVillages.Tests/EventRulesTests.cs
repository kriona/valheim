using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rules villages' events go by - which event a roll picks, when one is off its cooldown, the time it
	/// starts, the biomes it happens in and how its times are written
	/// </summary>
	public class EventRulesTests
	{
		/// <summary>
		/// Checks that each event takes up its chance of the roll in turn, and a roll past them all is no event
		/// </summary>
		/// <param name="roll">The roll</param>
		/// <param name="expected">The event's index, or -1 for none</param>
		[Theory]
		[InlineData(0f, 0)]
		[InlineData(0.09f, 0)]
		[InlineData(0.1f, 1)]
		[InlineData(0.29f, 1)]
		[InlineData(0.3f, -1)]
		[InlineData(1f, -1)]
		public void PickGoesThroughTheChancesInTurn(float roll, int expected)
		{
			Assert.Equal(expected, EventRules.Pick(new List<float> { 0.1f, 0.2f }, roll));
		}

		/// <summary>
		/// Checks that a 10% event is picked on a roll below 0.1 and none above it
		/// </summary>
		[Fact]
		public void PickMatchesTheTravelerChance()
		{
			Assert.Equal(0, EventRules.Pick(new List<float> { TravelerEvent.Chance }, 0.05f));
			Assert.Equal(-1, EventRules.Pick(new List<float> { TravelerEvent.Chance }, 0.15f));
		}

		/// <summary>
		/// Checks that no event is picked when none can happen, and an event with no chance is never picked
		/// </summary>
		[Fact]
		public void PickFindsNoneWithoutChances()
		{
			Assert.Equal(-1, EventRules.Pick(new List<float>(), 0f));
			Assert.Equal(1, EventRules.Pick(new List<float> { 0f, 0.5f }, 0f));
			Assert.Equal(0, EventRules.Pick(new List<float> { 0.5f, -1f, 0.5f }, 0.4f));
			Assert.Equal(2, EventRules.Pick(new List<float> { 0.5f, -1f, 0.5f }, 0.6f));
		}

		/// <summary>
		/// Checks that an event that never started is off its cooldown, and one that did is off it once its cooldown's
		/// days have passed since the day it started
		/// </summary>
		/// <param name="lastDay">The day it last started, or -1</param>
		/// <param name="today">Today</param>
		/// <param name="expected">Whether it's off its cooldown</param>
		[Theory]
		[InlineData(-1L, 0L, true)]
		[InlineData(-1L, 40L, true)]
		[InlineData(10L, 10L, false)]
		[InlineData(10L, 15L, false)]
		[InlineData(10L, 16L, true)]
		[InlineData(10L, 30L, true)]
		public void CooldownCountsFromTheDayItStarted(long lastDay, long today, bool expected)
		{
			Assert.Equal(expected, EventRules.OffCooldown(lastDay, 6, today));
		}

		/// <summary>
		/// Checks that an event starts within its window, from now once its window has opened, and not at all once it
		/// has closed
		/// </summary>
		/// <param name="now">The time of day</param>
		/// <param name="roll">The roll</param>
		/// <param name="expected">The time it starts, or -1</param>
		[Theory]
		[InlineData(0.1f, 0f, 0.5f)]
		[InlineData(0.1f, 1f, 0.6f)]
		[InlineData(0.1f, 0.5f, 0.55f)]
		[InlineData(0.55f, 0f, 0.55f)]
		[InlineData(0.55f, 0.5f, 0.575f)]
		[InlineData(0.6f, 0.5f, -1f)]
		[InlineData(0.9f, 0.5f, -1f)]
		public void StartAtIsWithinTheWindow(float now, float roll, float expected)
		{
			Assert.Equal(expected, EventRules.StartAt(now, 0.5f, 0.6f, roll), 4);
		}

		/// <summary>
		/// Checks that the traveler comes from noon to two game hours before dusk
		/// </summary>
		[Fact]
		public void TravelerArrivesInTheAfternoon()
		{
			Assert.Equal(0.5f, TravelerEvent.ArriveFrom);
			Assert.Equal(VillageLife.DuskStart - 2f / 24f, TravelerEvent.ArriveUntil, 4);
			Assert.True(TravelerEvent.ArriveUntil > TravelerEvent.ArriveFrom);
		}

		/// <summary>
		/// Checks that an event only happens from its easiest biome to its hardest
		/// </summary>
		/// <param name="biome">The village's biome</param>
		/// <param name="expected">Whether it happens there</param>
		[Theory]
		[InlineData(0, true)]
		[InlineData(4, true)]
		[InlineData(5, false)]
		[InlineData(7, false)]
		[InlineData(-1, false)]
		public void InBiomesKeepsToTheRange(int biome, bool expected)
		{
			Assert.Equal(expected, EventRules.InBiomes(biome, 0, 4));
		}

		/// <summary>
		/// Checks that times of day are written as hours and minutes
		/// </summary>
		/// <param name="fraction">The time of day</param>
		/// <param name="expected">How it's written</param>
		[Theory]
		[InlineData(0f, "00:00")]
		[InlineData(0.5f, "12:00")]
		[InlineData(0.625f, "15:00")]
		[InlineData(0.6271f, "15:03")]
		[InlineData(0.9999f, "23:59")]
		[InlineData(1f, "00:00")]
		public void ClockWritesHoursAndMinutes(float fraction, string expected)
		{
			Assert.Equal(expected, EventRules.Clock(fraction));
		}
	}
}
