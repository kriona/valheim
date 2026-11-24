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

		[Theory]
		[InlineData(5, 0f, 0)]
		[InlineData(5, 899f, 0)]
		[InlineData(5, 1800f, 2)]
		[InlineData(2, 100000f, 2)]
		[InlineData(0, 100000f, 0)]
		public void EatsOneForEveryWhileOnTheRoad(int edible, float seconds, int expected)
		{
			Assert.Equal(expected, TripForage.Eaten(edible, seconds));
		}

		[Fact]
		public void NothingGrowsWhereNothingGrows()
		{
			Dictionary<int, float> seconds = new Dictionary<int, float> { { 3, 5000f } };
			Assert.Empty(TripForage.Pick(seconds, () => 0.5f));
		}

		[Fact]
		public void PicksWhatGrowsWhereItWalked()
		{
			Dictionary<int, float> seconds = new Dictionary<int, float> { { 4, 600f } };
			List<string> found = TripForage.Pick(seconds, () => 0.99f);
			Assert.Equal(3, found.Count);
			Assert.All(found, (string item) => Assert.Equal("Cloudberry", item));
		}

		[Fact]
		public void ARollOfOneStillPicksSomething()
		{
			Dictionary<int, float> seconds = new Dictionary<int, float> { { 0, 400f } };
			Assert.Equal(2, TripForage.Pick(seconds, () => 1f).Count);
		}

		[Fact]
		public void BiomesArePickedByHowLongItWasThere()
		{
			// The roll falls in the Meadows' share first, then the Black Forest's
			Dictionary<int, float> seconds = new Dictionary<int, float> { { 0, 200f }, { 1, 400f } };
			Queue<float> rolls = new Queue<float>(new[] { 0.1f, 0f, 0.9f, 0f, 0.9f, 0f });
			List<string> found = TripForage.Pick(seconds, () => rolls.Dequeue());
			Assert.Equal(new List<string> { "Raspberry", "Blueberries", "Blueberries" }, found);
		}

		[Fact]
		public void WhatsEatenComesOffWhatCanBeEaten()
		{
			List<string> found = new List<string> { "Raspberry", "Thistle", "Raspberry", "Mushroom" };
			List<KeyValuePair<string, int>> left = TripForage.Left(found, (string item) => item != "Thistle", 1800f);
			Assert.Equal(new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("Thistle", 1), new KeyValuePair<string, int>("Mushroom", 1) }, left);
		}

		[Fact]
		public void OnlyMiningAndRaidingWaitForTheWayHome()
		{
			Assert.True(TripForage.OnlyHomeward(QuestKind.MineCopper));
			Assert.True(TripForage.OnlyHomeward(QuestKind.RaidCrypts));
			Assert.True(TripForage.OnlyHomeward(QuestKind.RaidBurialChambers));
			Assert.False(TripForage.OnlyHomeward(QuestKind.ChopTrees));
			Assert.False(TripForage.OnlyHomeward(QuestKind.KillEikthyr));
		}
	}
}
