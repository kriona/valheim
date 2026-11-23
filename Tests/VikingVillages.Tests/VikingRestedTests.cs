namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests a living viking's rest - when it's still resting, how long being rested lasts, and how it's put off while
	/// players sleep
	/// </summary>
	public class VikingRestedTests
	{
		/// <summary>
		/// Checks that a viking is resting while it was last seen resting no more than a second and a half ago, and not
		/// before it has started
		/// </summary>
		/// <param name="since">Since when it has rested, in seconds, or -1</param>
		/// <param name="now">The time now, in seconds - it was last seen resting at 20</param>
		/// <param name="expected">Whether it's resting</param>
		[Theory]
		[InlineData(10f, 20f, true)]
		[InlineData(10f, 21.5f, true)]
		[InlineData(10f, 21.6f, false)]
		[InlineData(10f, 30f, false)]
		[InlineData(0f, 20f, true)]
		[InlineData(-1f, 20f, false)]
		public void RestsWhileSeenRestingLately(float since, float now, bool expected)
		{
			Assert.Equal(expected, VikingRested.Resting(since, 20f, now));
		}

		/// <summary>
		/// Checks that being rested lasts the base time at comfort 1 and longer for each level above it
		/// </summary>
		/// <param name="comfort">The comfort level</param>
		/// <param name="expected">How long it lasts, in seconds</param>
		[Theory]
		[InlineData(1, 300f)]
		[InlineData(2, 360f)]
		[InlineData(5, 540f)]
		public void RestedTimeGrowsWithComfort(int comfort, float expected)
		{
			Assert.Equal(expected, VikingRested.RestedTime(300f, 60f, comfort), 3);
		}

		/// <summary>
		/// Checks that a rest lasting past the start of a skip is put off by the skip - including one that wore off
		/// during it - while one that had worn off by then, or was never had, is left alone
		/// </summary>
		/// <param name="until">Until when it was rested, in ticks</param>
		/// <param name="expected">Until when it's rested after the skip, in ticks</param>
		[Theory]
		// The skip began at 7000 and ended at 10000
		[InlineData(0L, 0L)]
		[InlineData(6999L, 6999L)]
		[InlineData(7000L, 7000L)]
		[InlineData(7001L, 10001L)]
		[InlineData(12000L, 15000L)]
		public void PostponesARestLastingAsTheSkipBegan(long until, long expected)
		{
			Assert.Equal(expected, VikingRested.Postponed(until, 10000L, 3000L));
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
			Assert.Equal(12000L, VikingRested.Postponed(12000L, 10000L, skipped));
		}
	}
}
