namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how many trees a quest cutting trees fells for the wood it cuts, which its other loot is rolled for
	/// </summary>
	public class QuestGameTests
	{
		/// <summary>
		/// Checks that the trees felled are the wood cut over what a whole tree gives, rounded, and at least one once any
		/// is cut
		/// </summary>
		/// <param name="units">How much wood was cut</param>
		/// <param name="perTree">How much a whole tree gives</param>
		/// <param name="expected">How many trees</param>
		[Theory]
		[InlineData(100, 20f, 5)]
		[InlineData(110, 20f, 6)]
		[InlineData(90, 20f, 4)]
		[InlineData(5, 20f, 1)]
		[InlineData(1, 20f, 1)]
		public void FellsATreeForEachTreesWorthOfWood(int units, float perTree, int expected)
		{
			Assert.Equal(expected, QuestGame.TreesFor(units, perTree));
		}

		/// <summary>
		/// Checks that no trees are felled when no wood was cut, or when a tree gives none of what was cut
		/// </summary>
		[Fact]
		public void FellsNoneWithoutWoodOrATreeThatGivesIt()
		{
			Assert.Equal(0, QuestGame.TreesFor(0, 20f));
			Assert.Equal(0, QuestGame.TreesFor(50, 0f));
		}
	}
}
