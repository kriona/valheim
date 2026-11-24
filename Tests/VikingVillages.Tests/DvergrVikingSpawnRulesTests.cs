using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests which creatures count as dvergr, how many dvergr vikings an outpost hires and how many stars they have
	/// </summary>
	public class DvergrVikingSpawnRulesTests
	{
		/// <summary>
		/// Checks that the dvergr and their mages count, and that a dvergr viking and other creatures don't
		/// </summary>
		[Fact]
		public void IsDvergrName_MatchesOnlyTheDvergrsPrefabs()
		{
			Assert.True(DvergrVikingSpawnRules.IsDvergrName("Dverger"));
			Assert.True(DvergrVikingSpawnRules.IsDvergrName("DvergerMageFire"));
			Assert.True(DvergrVikingSpawnRules.IsDvergrName("DvergerMageSupport"));
			Assert.False(DvergrVikingSpawnRules.IsDvergrName("DvergrViking"));
			Assert.False(DvergrVikingSpawnRules.IsDvergrName("Seeker"));
			Assert.False(DvergrVikingSpawnRules.IsDvergrName(null));
		}

		/// <summary>
		/// Checks that an outpost hires at most a quarter of its dvergr, rounded to the nearest, up to 4
		/// </summary>
		[Theory]
		[InlineData(0, 0)]
		[InlineData(1, 0)]
		[InlineData(2, 1)]
		[InlineData(5, 1)]
		[InlineData(6, 2)]
		[InlineData(9, 2)]
		[InlineData(10, 3)]
		[InlineData(14, 4)]
		[InlineData(40, 4)]
		[InlineData(-3, 0)]
		public void Cap_IsAQuarterRoundedUpToFour(int dvergr, int expected)
		{
			Assert.Equal(expected, DvergrVikingSpawnRules.Cap(dvergr));
		}

		/// <summary>
		/// Checks that the roll covers every count from none to the cap, each over an equal share of the rolls
		/// </summary>
		[Theory]
		[InlineData(0, 0.99f, 0)]
		[InlineData(1, 0f, 0)]
		[InlineData(1, 0.49f, 0)]
		[InlineData(1, 0.5f, 1)]
		[InlineData(1, 1f, 1)]
		[InlineData(4, 0f, 0)]
		[InlineData(4, 0.19f, 0)]
		[InlineData(4, 0.2f, 1)]
		[InlineData(4, 0.79f, 3)]
		[InlineData(4, 0.8f, 4)]
		[InlineData(4, 1f, 4)]
		public void Roll_SpreadsEvenlyFromNoneToTheCap(int cap, float roll, int expected)
		{
			Assert.Equal(expected, DvergrVikingSpawnRules.Roll(cap, roll));
		}

		/// <summary>
		/// Checks that a viking has one star only when the roll is within the level up chance
		/// </summary>
		[Theory]
		[InlineData(10f, 5f, 2)]
		[InlineData(10f, 10f, 2)]
		[InlineData(10f, 10.5f, 1)]
		[InlineData(0f, 50f, 1)]
		public void Level_HasOneStarWithinTheChance(float chance, float roll, int expected)
		{
			Assert.Equal(expected, DvergrVikingSpawnRules.Level(chance, roll));
		}
	}
}
