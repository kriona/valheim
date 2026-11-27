namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rules an NPC viking's village life goes by - how long a PC viking leaves a chore to an NPC viking, and
	/// when an NPC viking running from an enemy is caught
	/// </summary>
	public class VillageLifeNPCTests
	{
		/// <summary>
		/// Checks that a PC viking leaves a chore it keeps finding to an NPC viking until DeferLimit has passed since it
		/// first found it, then takes it on
		/// </summary>
		[Fact]
		public void LeavesAChoreForAWhile()
		{
			float first = -1f;
			float last = -1f;
			Assert.True(VillageLife.Defers(ref first, ref last, 100f));
			Assert.Equal(100f, first, 3);
			Assert.True(VillageLife.Defers(ref first, ref last, 105f));
			Assert.True(VillageLife.Defers(ref first, ref last, 100f + VillageLife.DeferLimit - 1f));
			Assert.False(VillageLife.Defers(ref first, ref last, 100f + VillageLife.DeferLimit + 1f));
			Assert.Equal(100f, first, 3);
		}

		/// <summary>
		/// Checks that a chore that went unseen for longer than DeferGap starts its wait over
		/// </summary>
		[Fact]
		public void StartsOverAfterAGap()
		{
			float first = 100f;
			float last = 100f;
			float later = 100f + VillageLife.DeferGap + 1f;
			Assert.True(VillageLife.Defers(ref first, ref last, later));
			Assert.Equal(later, first, 3);
			Assert.Equal(later, last, 3);
		}

		/// <summary>
		/// Checks when an NPC viking trying to get away is caught - always when it can't get away with the enemy close,
		/// and otherwise only when it was hit while getting less than CornerGain farther in CornerTime
		/// </summary>
		/// <param name="hit">Whether it was hit</param>
		/// <param name="stuck">Whether it can't get away</param>
		/// <param name="gained">How much farther it got</param>
		/// <param name="trying">How long it has tried</param>
		/// <param name="expected">True when it's caught</param>
		[Theory]
		[InlineData(false, true, 5f, 0f, true)]
		[InlineData(true, false, 0f, 3f, true)]
		[InlineData(true, false, 0.5f, 4f, true)]
		[InlineData(true, false, 2f, 3f, false)]
		[InlineData(true, false, 0f, 1f, false)]
		[InlineData(false, false, 0f, 5f, false)]
		public void KnowsWhenCaught(bool hit, bool stuck, float gained, float trying, bool expected)
		{
			Assert.Equal(expected, VillageLife.IsCaught(hit, stuck, gained, trying));
		}
	}
}
