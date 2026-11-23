namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests which pairs a hunting viking, its prey and the village's livestock take as enemies
	/// </summary>
	public class HuntEnemyPatchTests
	{
		/// <summary>
		/// Checks that a living viking takes livestock as an enemy only when it hunts it, whatever the game says
		/// </summary>
		/// <param name="hunts">Whether the viking hunts it</param>
		/// <param name="enemies">Whether the game has them as enemies</param>
		/// <param name="expected">Whether they're enemies</param>
		[Theory]
		[InlineData(true, false, true)]
		[InlineData(true, true, true)]
		[InlineData(false, true, false)]
		[InlineData(false, false, false)]
		public void VikingTakesLivestockWhenItHuntsIt(bool hunts, bool enemies, bool expected)
		{
			Assert.Equal(expected, HuntEnemyPatch.Enemies(true, false, hunts, false, enemies));
		}

		/// <summary>
		/// Checks that livestock never takes a living viking as an enemy, even one hunting it
		/// </summary>
		/// <param name="firstHunts">Whether the livestock hunts the viking</param>
		/// <param name="otherHunts">Whether the viking hunts the livestock</param>
		/// <param name="enemies">Whether the game has them as enemies</param>
		[Theory]
		[InlineData(false, false, false)]
		[InlineData(false, false, true)]
		[InlineData(false, true, true)]
		[InlineData(true, true, true)]
		public void LivestockNeverTakesAViking(bool firstHunts, bool otherHunts, bool enemies)
		{
			Assert.False(HuntEnemyPatch.Enemies(false, true, firstHunts, otherHunts, enemies));
		}

		/// <summary>
		/// Checks that a hunter and its prey are enemies either way round, and any other pair as the game has them
		/// </summary>
		/// <param name="firstHunts">Whether the first hunts the other</param>
		/// <param name="otherHunts">Whether the other hunts the first</param>
		/// <param name="enemies">Whether the game has them as enemies</param>
		/// <param name="expected">Whether they're enemies</param>
		[Theory]
		[InlineData(true, false, false, true)]
		[InlineData(false, true, false, true)]
		[InlineData(true, true, false, true)]
		[InlineData(false, false, false, false)]
		[InlineData(false, false, true, true)]
		[InlineData(true, false, true, true)]
		public void HunterAndPreyAreEnemies(bool firstHunts, bool otherHunts, bool enemies, bool expected)
		{
			Assert.Equal(expected, HuntEnemyPatch.Enemies(false, false, firstHunts, otherHunts, enemies));
		}
	}
}
