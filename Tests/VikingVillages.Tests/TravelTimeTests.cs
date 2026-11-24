using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how long vikings take on the road and how that's said in game time
	/// </summary>
	public class TravelTimeTests
	{
		/// <summary>
		/// A game day of the game's default length, in seconds, and a game hour of it
		/// </summary>
		private const double Day = 1800.0;
		private const double Hour = Day / 24.0;

		/// <summary>
		/// The morning of the fifth day, in seconds of world time
		/// </summary>
		private const double Morning = Day * 5.0 + Hour * 7.0;

		[Fact]
		public void SecondsIsTheRoadOverTheSpeed()
		{
			Assert.Equal(200f, TravelTime.Seconds(1000f), 3);
			Assert.Equal(0f, TravelTime.Seconds(-50f), 3);
		}

		[Theory]
		[InlineData(0, "raft")]
		[InlineData(1, "karve")]
		[InlineData(2, "karve")]
		[InlineData(3, "longship")]
		[InlineData(5, "longship")]
		[InlineData(6, "drakkar")]
		[InlineData(7, "drakkar")]
		public void EachBiomeSailsItsBestBoat(int biome, string boat)
		{
			Assert.Equal(boat, TravelTime.BoatName(biome));
		}

		[Theory]
		[InlineData(0, "Raft", false)]
		[InlineData(2, "Karve", false)]
		[InlineData(5, "VikingShip", false)]
		[InlineData(6, "VikingShip_Ashlands", true)]
		public void OnlyTheDrakkarHoldsUpInTheAshlandsSea(int biome, string prefab, bool fireproof)
		{
			Assert.Equal(prefab, TravelTime.BoatPrefab(biome));
			Assert.Equal(fireproof, TravelTime.BoatSurvivesAshlands(biome));
		}

		[Fact]
		public void BetterBoatsAreFaster()
		{
			Assert.True(TravelTime.BoatSpeed(0) < TravelTime.BoatSpeed(1));
			Assert.True(TravelTime.BoatSpeed(1) < TravelTime.BoatSpeed(3));
			Assert.True(TravelTime.BoatSpeed(3) < TravelTime.BoatSpeed(6));
		}

		[Theory]
		[InlineData(0.0, "now")]
		[InlineData(-1.0, "now")]
		[InlineData(0.5, "in roughly half an hour")]
		[InlineData(1.0, "in about an hour")]
		[InlineData(2.5, "in a couple of hours")]
		[InlineData(5.0, "in a few hours")]
		[InlineData(10.0, "later today")]
		public void PhraseWithinTheDayGoesByTheHour(double hours, string expected)
		{
			Assert.Equal(expected, TravelTime.Phrase(Morning, Morning + hours * Hour, Day));
		}

		[Fact]
		public void PhraseOnTheNextDayIsTomorrow()
		{
			Assert.Equal("tomorrow", TravelTime.Phrase(Morning, Morning + 20.0 * Hour, Day));
		}

		[Fact]
		public void PhraseAFewHoursAcrossMidnightStaysInHours()
		{
			double lateEvening = Day * 5.0 + Hour * 22.0;
			Assert.Equal("in a few hours", TravelTime.Phrase(lateEvening, lateEvening + 4.0 * Hour, Day));
		}

		[Fact]
		public void PhraseJustOverADayFromLateEveningIsADay()
		{
			double lateEvening = Day * 5.0 + Hour * 23.0;
			Assert.Equal("in a day", TravelTime.Phrase(lateEvening, lateEvening + 26.0 * Hour, Day));
		}

		[Theory]
		[InlineData(2, "in a couple of days")]
		[InlineData(3, "in a couple of days")]
		[InlineData(5, "in a few days")]
		[InlineData(8, "in about a week")]
		[InlineData(14, "in more than a week")]
		public void PhraseLaterOnGoesByTheDay(int days, string expected)
		{
			Assert.Equal(expected, TravelTime.Phrase(Morning, Morning + days * Day, Day));
		}
	}
}
