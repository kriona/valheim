using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rules a traveling viking goes by - the biomes it comes from, when it leaves, when it left while out of
	/// reach, how much food it takes, and the names of the vikings it knows
	/// </summary>
	public class TravelerTests
	{
		/// <summary>
		/// How long a day is in the tests, in seconds - the game's own
		/// </summary>
		private const long Day = 1800L;

		/// <summary>
		/// Checks that a traveler comes from three or more biomes harder than the village's, up to the hardest
		/// </summary>
		[Fact]
		public void OriginBiomesStartThreeHarder()
		{
			Assert.Equal(new List<int> { 3, 4, 5, 6, 7 }, Traveler.OriginBiomes(0, 7));
			Assert.Equal(new List<int> { 7 }, Traveler.OriginBiomes(4, 7));
			Assert.Empty(Traveler.OriginBiomes(5, 7));
		}

		/// <summary>
		/// Checks that each biome a traveler can come from is as likely, and none when there's none
		/// </summary>
		/// <param name="village">The village's biome</param>
		/// <param name="roll">The roll</param>
		/// <param name="expected">The biome it comes from, or -1</param>
		[Theory]
		[InlineData(4, 0f, 7)]
		[InlineData(4, 0.99f, 7)]
		[InlineData(3, 0f, 6)]
		[InlineData(3, 0.49f, 6)]
		[InlineData(3, 0.5f, 7)]
		[InlineData(3, 1f, 7)]
		[InlineData(0, 0.19f, 3)]
		[InlineData(0, 0.2f, 4)]
		[InlineData(0, 0.99f, 7)]
		[InlineData(5, 0.5f, -1)]
		public void PickOriginSpreadsEvenly(int village, float roll, int expected)
		{
			Assert.Equal(expected, Traveler.PickOrigin(village, 7, roll));
		}

		/// <summary>
		/// Checks that a traveler leaves the next morning after it arrives - the dawn after, and the hours it stays
		/// </summary>
		/// <param name="arrived">When it arrived, as a share of the day it arrived on</param>
		/// <param name="hours">How many game hours it stays after dawn</param>
		/// <param name="expected">When it leaves, in days from the start of the day it arrived on</param>
		[Theory]
		[InlineData(0.55, 1f, 1.25 + 1.0 / 24.0)]
		[InlineData(0.55, 2f, 1.25 + 2.0 / 24.0)]
		[InlineData(0.9, 1.5f, 1.25 + 1.5 / 24.0)]
		[InlineData(0.25, 1f, 1.25 + 1.0 / 24.0)]
		[InlineData(0.1, 1f, 0.25 + 1.0 / 24.0)]
		public void LeaveAtIsTheNextMorning(double arrived, float hours, double expected)
		{
			double start = 40.0 * Day;
			Assert.Equal(start + expected * Day, Traveler.LeaveAt(start + arrived * Day, Day, hours), 3);
		}

		/// <summary>
		/// Checks that a traveler left while away only when it was out of reach and its time to leave has passed
		/// </summary>
		/// <param name="now">The time now</param>
		/// <param name="seen">When it was last noted loaded</param>
		/// <param name="expected">Whether it left while away</param>
		[Theory]
		[InlineData(1000L, 100L, true)]
		[InlineData(1000L, 980L, false)]
		[InlineData(900L, 100L, false)]
		[InlineData(1000L, 0L, false)]
		public void LeftWhileAwayNeedsBothAbsenceAndItsTime(long now, long seen, bool expected)
		{
			Assert.Equal(expected, Traveler.LeftWhileAway(now, 950L, seen, 30L));
		}

		/// <summary>
		/// Checks that a traveler takes no more than the chest holds
		/// </summary>
		/// <param name="wanted">How many it wants</param>
		/// <param name="inChest">How many the chest holds</param>
		/// <param name="expected">How many it takes</param>
		[Theory]
		[InlineData(4, 10, 4)]
		[InlineData(4, 2, 2)]
		[InlineData(4, 0, 0)]
		[InlineData(0, 5, 0)]
		[InlineData(-1, 5, 0)]
		public void TakeCountKeepsToTheChest(int wanted, int inChest, int expected)
		{
			Assert.Equal(expected, Traveler.TakeCount(wanted, inChest));
		}

		/// <summary>
		/// Checks that names written one a line are read back, leaving out blank lines and spaces around them
		/// </summary>
		[Fact]
		public void SplitNamesReadsOneALine()
		{
			Assert.Equal(new List<string> { "Astrid", "Bjorn the Red" }, Traveler.SplitNames("Astrid\n\n Bjorn the Red \n"));
			Assert.Empty(Traveler.SplitNames(""));
			Assert.Empty(Traveler.SplitNames(null));
		}

		/// <summary>
		/// Checks that a traveler carries far more food than a newcomer, and trades a few
		/// </summary>
		[Fact]
		public void TravelersCarryMoreFood()
		{
			Assert.Equal(25, Traveler.FoodMin);
			Assert.Equal(30, Traveler.FoodMax);
			Assert.True(Traveler.FoodMin > VikingStartFood.CarriedMax);
			Assert.Equal(3, Traveler.TradeMin);
			Assert.Equal(4, Traveler.TradeMax);
		}
	}
}
