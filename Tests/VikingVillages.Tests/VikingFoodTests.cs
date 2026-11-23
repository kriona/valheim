using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests a viking's food - how the time left on it is written, how long it lasts, when it can eat, how much health a
	/// food adds as it wears off, the floor its max health keeps to, and how its meals are noted and put off while
	/// players sleep
	/// </summary>
	public class VikingFoodTests
	{
		/// <summary>
		/// Makes a meal
		/// </summary>
		/// <param name="prefab">The food's prefab hash</param>
		/// <param name="until">Until when it lasts, in ticks</param>
		/// <returns>The meal</returns>
		private static VikingFood.Meal Meal(int prefab, long until)
		{
			return(new VikingFood.Meal { Prefab = prefab, Until = until });
		}

		/// <summary>
		/// Checks that times are written in minutes and seconds, rounding part of a second up
		/// </summary>
		/// <param name="seconds">The time, in seconds</param>
		/// <param name="expected">How it should be written</param>
		[Theory]
		[InlineData(0f, "0s")]
		[InlineData(0.2f, "1s")]
		[InlineData(59f, "59s")]
		[InlineData(59.5f, "1m 0s")]
		[InlineData(60f, "1m 0s")]
		[InlineData(252f, "4m 12s")]
		[InlineData(3600f, "60m 0s")]
		public void DurationWritesMinutesAndSeconds(float seconds, string expected)
		{
			Assert.Equal(expected, VikingFood.Duration(seconds));
		}

		/// <summary>
		/// Checks that a time already past is written as no time at all
		/// </summary>
		[Fact]
		public void DurationNeverGoesNegative()
		{
			Assert.Equal("0s", VikingFood.Duration(-30f));
		}

		/// <summary>
		/// Checks that a new food fits beside up to two others, but not three
		/// </summary>
		[Fact]
		public void EatsUpToThreeFoods()
		{
			Assert.True(VikingFood.HasRoom(new List<VikingFood.Meal>(), 9, 1000L, 500L));
			Assert.True(VikingFood.HasRoom(new List<VikingFood.Meal> { Meal(1, 5000L), Meal(2, 5000L) }, 9, 1000L, 500L));
			Assert.False(VikingFood.HasRoom(new List<VikingFood.Meal> { Meal(1, 5000L), Meal(2, 5000L), Meal(3, 5000L) }, 9, 1000L, 500L));
		}

		/// <summary>
		/// Checks that a food already eaten is eaten again once less than half of it is left, and not while half or
		/// more is
		/// </summary>
		/// <param name="until">Until when the food eaten lasts, in ticks</param>
		/// <param name="expected">Whether it's eaten again</param>
		[Theory]
		[InlineData(1499L, true)]
		[InlineData(1500L, false)]
		[InlineData(1600L, false)]
		public void EatsTheSameFoodAgainOnceHalfGone(long until, bool expected)
		{
			Assert.Equal(expected, VikingFood.HasRoom(new List<VikingFood.Meal> { Meal(1, 5000L), Meal(9, until) }, 9, 1000L, 500L));
		}

		/// <summary>
		/// Checks that a food already eaten is eaten again once half gone even with three foods eaten, since it takes its
		/// own place
		/// </summary>
		[Fact]
		public void EatsTheSameFoodAgainBesideTwoOthers()
		{
			Assert.True(VikingFood.HasRoom(new List<VikingFood.Meal> { Meal(1, 5000L), Meal(2, 5000L), Meal(9, 1200L) }, 9, 1000L, 500L));
		}

		/// <summary>
		/// Checks how much of a food's health it adds - all of it when just eaten or longer, none once worn off, and
		/// more than half at half time, since it fades slowly at first
		/// </summary>
		/// <param name="left">How long the food has left, in seconds</param>
		/// <param name="expected">The share of its health</param>
		[Theory]
		[InlineData(100f, 1f)]
		[InlineData(150f, 1f)]
		[InlineData(50f, 0.81225f)]
		[InlineData(0f, 0f)]
		[InlineData(-10f, 0f)]
		public void HealthShareFadesAsItWearsOff(float left, float expected)
		{
			Assert.Equal(expected, VikingFood.HealthShare(left, 100f), 3);
		}

		/// <summary>
		/// Checks that a food lasts its burn time at the usual food rate, half as long at twice the rate, twice as long at
		/// half the rate, and its burn time when the rate isn't above 0
		/// </summary>
		/// <param name="foodRate">The world's food rate</param>
		/// <param name="expected">How long the food lasts, in seconds</param>
		[Theory]
		[InlineData(1f, 1200f)]
		[InlineData(2f, 600f)]
		[InlineData(0.5f, 2400f)]
		[InlineData(0f, 1200f)]
		public void FoodLastsByTheFoodRate(float foodRate, float expected)
		{
			Assert.Equal(expected, VikingFood.Lasts(1200f, foodRate), 3);
		}

		/// <summary>
		/// Checks that meals read back as they were written
		/// </summary>
		[Fact]
		public void MealsRoundTrip()
		{
			List<VikingFood.Meal> meals = new List<VikingFood.Meal> { Meal(-123456, 638000000000000000L), Meal(42, 0L) };
			Assert.Equal(meals, VikingFood.Decode(VikingFood.Encode(meals)));
			Assert.Empty(VikingFood.Decode(VikingFood.Encode(new List<VikingFood.Meal>())));
		}

		/// <summary>
		/// Checks that no bytes, bytes too short to hold a count, and bytes cut off partway through a meal all read as no
		/// meals rather than failing
		/// </summary>
		[Fact]
		public void CorruptMealsReadAsNone()
		{
			Assert.Empty(VikingFood.Decode(null));
			Assert.Empty(VikingFood.Decode(new byte[] { 1, 2, 3 }));
			byte[] bytes = VikingFood.Encode(new List<VikingFood.Meal> { Meal(1, 100L), Meal(2, 200L) });
			Assert.Empty(VikingFood.Decode(bytes[..(bytes.Length - 1)]));
		}

		/// <summary>
		/// Checks that a meal lasting until now or earlier has worn off, and one lasting past now hasn't
		/// </summary>
		[Fact]
		public void MealsWearOffAtTheirEnd()
		{
			List<VikingFood.Meal> lasting = VikingFood.Lasting(new List<VikingFood.Meal> { Meal(1, 999L), Meal(2, 1000L), Meal(3, 1001L) }, 1000L);
			Assert.Equal(new List<VikingFood.Meal> { Meal(3, 1001L) }, lasting);
		}

		/// <summary>
		/// Checks that putting off meals by a skip moves on those still lasting as it began - including one that wore off
		/// during it - and leaves out those that had worn off by then
		/// </summary>
		[Fact]
		public void PostponesMealsStillLastingAsTheSkipBegan()
		{
			// The skip began at 7000 and ended at 10000
			List<VikingFood.Meal> meals = new List<VikingFood.Meal> { Meal(1, 6000L), Meal(2, 7000L), Meal(3, 8000L), Meal(4, 12000L) };
			List<VikingFood.Meal> postponed = VikingFood.Postponed(meals, 10000L, 3000L);
			Assert.Equal(new List<VikingFood.Meal> { Meal(3, 11000L), Meal(4, 15000L) }, postponed);
			Assert.Equal(8000L, meals[2].Until);
		}

		/// <summary>
		/// Checks that no skip, or a skip backwards, puts nothing off
		/// </summary>
		/// <param name="skipped">How far the world skipped ahead, in ticks</param>
		[Theory]
		[InlineData(0L)]
		[InlineData(-500L)]
		public void PostponesNothingWithoutASkip(long skipped)
		{
			Assert.Empty(VikingFood.Postponed(new List<VikingFood.Meal> { Meal(1, 12000L) }, 10000L, skipped));
		}

		/// <summary>
		/// Checks that every biome vikings come from has a health floor
		/// </summary>
		[Fact]
		public void EveryBiomeHasAHealthFloor()
		{
			Assert.Equal(VikingGear.Biomes.Length, VikingFood.HealthFloors.Length);
		}

		/// <summary>
		/// Checks each biome's health floor - the base health and the previous biome's three best foods
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="expected">Its floor</param>
		[Theory]
		[InlineData(Heightmap.Biome.Meadows, 50f)]
		[InlineData(Heightmap.Biome.BlackForest, 115f)]
		[InlineData(Heightmap.Biome.Swamp, 155f)]
		[InlineData(Heightmap.Biome.Mountain, 175f)]
		[InlineData(Heightmap.Biome.Plains, 195f)]
		[InlineData(Heightmap.Biome.Mistlands, 235f)]
		[InlineData(Heightmap.Biome.AshLands, 270f)]
		[InlineData(Heightmap.Biome.DeepNorth, 325f)]
		public void HealthFloorFollowsThePreviousBiomesFood(Heightmap.Biome biome, float expected)
		{
			Assert.Equal(expected, VikingFood.HealthFloor(VikingGear.IndexOf(biome)));
		}

		/// <summary>
		/// Checks that a biome index out of the table's range gets the nearest biome's floor
		/// </summary>
		[Fact]
		public void HealthFloorKeepsWithinTheTable()
		{
			Assert.Equal(50f, VikingFood.HealthFloor(-1));
			Assert.Equal(325f, VikingFood.HealthFloor(99));
		}

		/// <summary>
		/// Checks that a viking's max health is the base and its food when that's above its floor, and the floor when
		/// it isn't
		/// </summary>
		/// <param name="foodHealth">What its food adds</param>
		/// <param name="expected">Its max health</param>
		[Theory]
		[InlineData(0f, 325f)]
		[InlineData(250f, 325f)]
		[InlineData(300f, 325f)]
		[InlineData(325f, 350f)]
		public void MaxHealthIsFoodOrTheFloor(float foodHealth, float expected)
		{
			Assert.Equal(expected, VikingFood.MaxHealth(325f, foodHealth), 3);
		}
	}
}
