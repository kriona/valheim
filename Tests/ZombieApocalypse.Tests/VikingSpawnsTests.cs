namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how names are simplified before vikings' spawns are matched to them
	/// </summary>
	public class VikingSpawnsTests
	{
		/// <summary>
		/// Checks that spaces and underscores are taken out and the rest is lowercased
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="expected">The simplified name</param>
		[Theory]
		[InlineData("Dark Meadows", "darkmeadows")]
		[InlineData("Ruin_BlackForest", "ruinblackforest")]
		[InlineData("  Mixed _Case_ ", "mixedcase")]
		[InlineData("plains", "plains")]
		[InlineData("", "")]
		public void SimplifyLowercasesAndJoins(string name, string expected)
		{
			Assert.Equal(expected, VikingSpawns.Simplify(name));
		}

		/// <summary>
		/// Checks that a missing name simplifies to an empty one
		/// </summary>
		[Fact]
		public void SimplifyTreatsNullAsEmpty()
		{
			Assert.Equal("", VikingSpawns.Simplify(null));
		}
	}
}
