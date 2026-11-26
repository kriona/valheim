using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what vikings out of every player's reach pick along their road - a handful, never stacks
	/// </summary>
	public class TripForageTests
	{
		[Theory]
		[InlineData(0f, 0)]
		[InlineData(179f, 0)]
		[InlineData(360f, 2)]
		[InlineData(900f, 5)]
		[InlineData(100000f, 5)]
		[InlineData(-5f, 0)]
		public void FindsMoreTheLongerItWalksUpToAFew(float seconds, int expected)
		{
			Assert.Equal(expected, TripForage.Found(seconds));
		}

		[Fact]
		public void NothingGrowsWhereNothingGrows()
		{
			Dictionary<int, float> seconds = new Dictionary<int, float> { { 3, 5000f } };
			Assert.Empty(TripForage.Pick(seconds, ResourceBook.DeepNorth, () => 0.5f));
		}

		[Fact]
		public void PicksWhatGrowsWhereItWalked()
		{
			Dictionary<int, float> seconds = new Dictionary<int, float> { { 4, 600f } };
			List<string> found = TripForage.Pick(seconds, ResourceBook.DeepNorth, () => 0.99f);
			Assert.Equal(3, found.Count);
			Assert.All(found, (string item) => Assert.Equal("Cloudberry", item));
		}

		[Fact]
		public void ARollOfOneStillPicksSomething()
		{
			Dictionary<int, float> seconds = new Dictionary<int, float> { { 0, 400f } };
			Assert.Equal(2, TripForage.Pick(seconds, ResourceBook.DeepNorth, () => 1f).Count);
		}

		[Fact]
		public void BiomesArePickedByHowLongItWasThere()
		{
			// The roll falls in the Meadows' share first, then the Black Forest's
			Dictionary<int, float> seconds = new Dictionary<int, float> { { 0, 200f }, { 1, 400f } };
			Queue<float> rolls = new Queue<float>(new[] { 0.1f, 0f, 0.9f, 0f, 0.9f, 0f });
			List<string> found = TripForage.Pick(seconds, ResourceBook.DeepNorth, () => rolls.Dequeue());
			Assert.Equal(new List<string> { "Raspberry", "Blueberries", "Blueberries" }, found);
		}

		[Fact]
		public void NothingIsFoundInBiomesHarderThanTheVikingsOwn()
		{
			Dictionary<int, float> seconds = new Dictionary<int, float> { { ResourceBook.Plains, 900f } };
			Assert.Empty(TripForage.Pick(seconds, ResourceBook.Meadows, () => 0.5f));
			Assert.Equal(5, TripForage.Pick(seconds, ResourceBook.Plains, () => 0.5f).Count);
		}

		[Fact]
		public void OnlyTheVikingsOwnBiomesCountTowardWhatsFound()
		{
			// The Plains' 900 seconds count for nothing, leaving the Meadows' 400 for 2 finds
			Dictionary<int, float> seconds = new Dictionary<int, float> { { ResourceBook.Meadows, 400f }, { ResourceBook.Plains, 900f } };
			List<string> found = TripForage.Pick(seconds, ResourceBook.BlackForest, () => 0.99f);
			Assert.Equal(2, found.Count);
			Assert.DoesNotContain("Cloudberry", found);
		}

		[Theory]
		[InlineData(299f, 0)]
		[InlineData(300f, 1)]
		[InlineData(600f, 2)]
		[InlineData(100000f, 2)]
		public void HuntersTakeACouplePreyOnTheRoad(float seconds, int expected)
		{
			Assert.Equal(expected, TripForage.PreyFound(seconds));
		}

		/// <summary>
		/// Gets the prey of a biome - boar in the Meadows, and lox everywhere else
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>The prey, by prefab name</returns>
		private static string[] PreyIn(int biome)
		{
			return((biome == ResourceBook.Meadows) ? new[] { "Boar" } : new[] { "Lox" });
		}

		[Fact]
		public void HuntersOnlyTakePreyOfTheirOwnBiomesOnTheRoad()
		{
			Dictionary<int, float> seconds = new Dictionary<int, float> { { ResourceBook.Meadows, 300f }, { ResourceBook.Plains, 3000f } };
			Assert.Equal(new List<string> { "Boar" }, TripForage.PickPrey(seconds, ResourceBook.Meadows, PreyIn, () => 0.5f));
			Assert.Equal(2, TripForage.PickPrey(seconds, ResourceBook.Plains, PreyIn, () => 0.5f).Count);
		}

		[Fact]
		public void WhatsFoundIsCountedInTheOrderItsFirstFound()
		{
			List<string> found = new List<string> { "Raspberry", "Thistle", "Raspberry", "Mushroom" };
			List<KeyValuePair<string, int>> counted = TripForage.Counted(found);
			Assert.Equal(new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("Raspberry", 2), new KeyValuePair<string, int>("Thistle", 1), new KeyValuePair<string, int>("Mushroom", 1) }, counted);
		}

		[Fact]
		public void WhatsFoundIsSpreadEvenlyOverTheRoad()
		{
			Assert.Equal(300f, TripFood.FoundAt(0, 2, 900f), 3);
			Assert.Equal(600f, TripFood.FoundAt(1, 2, 900f), 3);
			Assert.Equal(0f, TripFood.FoundAt(0, 1, -5f), 3);
		}

		[Fact]
		public void OnlyMiningAndRaidingWaitForTheWayHome()
		{
			Assert.True(TripForage.OnlyHomeward(QuestKind.MineCopper));
			Assert.True(TripForage.OnlyHomeward(QuestKind.RaidCrypts));
			Assert.True(TripForage.OnlyHomeward(QuestKind.RaidBurialChambers));
			Assert.True(TripForage.OnlyHomeward(QuestKind.RaidFrostCaves));
			Assert.False(TripForage.OnlyHomeward(QuestKind.ChopTrees));
			Assert.False(TripForage.OnlyHomeward(QuestKind.KillEikthyr));
		}
	}
}
