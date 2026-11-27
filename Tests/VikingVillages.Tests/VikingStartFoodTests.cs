using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the food a viking starts out with - the foods of each biome, how many it carries and which
	/// </summary>
	public class VikingStartFoodTests
	{
		/// <summary>
		/// Checks that every biome has three different foods, the first two its village's starter foods, so a viking can
		/// have eaten all three at once
		/// </summary>
		[Fact]
		public void EachBiomeHasThreeFoodsStartingWithTheStarterFoods()
		{
			Assert.Equal(VillageFurnishing.StarterFoods.Length, VikingStartFood.Foods.Length);
			for (int biome = 0; biome < VikingStartFood.Foods.Length; biome++)
			{
				string[] foods = VikingStartFood.Foods[biome];
				Assert.Equal(VikingStartFood.EatenCount, foods.Length);
				Assert.Equal(foods.Length, new HashSet<string>(foods).Count);
				Assert.Equal(VillageFurnishing.StarterFoods[biome][0], foods[0]);
				Assert.Equal(VillageFurnishing.StarterFoods[biome][1], foods[1]);
			}
		}

		/// <summary>
		/// Checks that a biome out of range gets the nearest biome's foods
		/// </summary>
		[Fact]
		public void FoodsForKeepsToTheTable()
		{
			Assert.Same(VikingStartFood.Foods[0], VikingStartFood.FoodsFor(-3));
			Assert.Same(VikingStartFood.Foods[VikingStartFood.Foods.Length - 1], VikingStartFood.FoodsFor(99));
		}

		/// <summary>
		/// Checks that a count is spread evenly across its range, from the fewest to the most
		/// </summary>
		/// <param name="roll">The roll</param>
		/// <param name="min">The fewest</param>
		/// <param name="max">The most</param>
		/// <param name="expected">The count</param>
		[Theory]
		[InlineData(0f, 5, 6, 5)]
		[InlineData(0.49f, 5, 6, 5)]
		[InlineData(0.5f, 5, 6, 6)]
		[InlineData(1f, 5, 6, 6)]
		[InlineData(0f, 0, 2, 0)]
		[InlineData(0.34f, 0, 2, 1)]
		[InlineData(0.67f, 0, 2, 2)]
		[InlineData(1f, 0, 2, 2)]
		public void CountForSpreadsEvenly(float roll, int min, int max, int expected)
		{
			Assert.Equal(expected, VikingStartFood.CountFor(roll, min, max));
		}

		/// <summary>
		/// Checks that each roll carries one of the biome's foods, counted together by food in the table's order
		/// </summary>
		[Fact]
		public void CarriedCountsEachFoodRolled()
		{
			List<KeyValuePair<string, int>> carried = VikingStartFood.Carried(0, new List<float> { 0.9f, 0f, 0.1f, 0.95f, 0.2f });
			Assert.Equal(2, carried.Count);
			Assert.Equal(new KeyValuePair<string, int>("CookedMeat", 3), carried[0]);
			Assert.Equal(new KeyValuePair<string, int>("NeckTailGrilled", 2), carried[1]);
		}

		/// <summary>
		/// Checks that no rolls carry nothing
		/// </summary>
		[Fact]
		public void CarriedIsEmptyWithNoRolls()
		{
			Assert.Empty(VikingStartFood.Carried(3, new List<float>()));
		}
	}
}
