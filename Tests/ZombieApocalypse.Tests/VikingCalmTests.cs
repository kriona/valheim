namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests when an angered living viking forgives
	/// </summary>
	public class VikingCalmTests
	{
		/// <summary>
		/// How long a day is in the tests, in seconds - the game's own
		/// </summary>
		private const long Day = 1200L;

		/// <summary>
		/// Checks that a viking angered by day forgives the next morning, and one angered at night as the next night
		/// falls - the same day's night when it was angered after midnight
		/// </summary>
		/// <param name="angered">When it was angered, in world seconds</param>
		/// <param name="expected">When it forgives, in world seconds</param>
		[Theory]
		// Day 2 runs from 2400 - morning at 2580, nightfall at 3420
		[InlineData(2580L, 3780L)]
		[InlineData(3000L, 3780L)]
		[InlineData(3419L, 3780L)]
		[InlineData(3420L, 4620L)]
		[InlineData(3599L, 4620L)]
		[InlineData(3600L, 4620L)]
		[InlineData(3779L, 4620L)]
		[InlineData(3780L, 4980L)]
		[InlineData(0L, 1020L)]
		public void ForgivesAfterSleepingOnIt(long angered, long expected)
		{
			Assert.Equal(expected, VikingCalm.CalmAt(angered, Day), 3);
		}

		/// <summary>
		/// Checks that a viking always forgives after it was angered, and within a day and a night
		/// </summary>
		[Fact]
		public void ForgivesLaterButWithinADayAndANight()
		{
			for (long angered = 0L; angered < Day * 3L; angered += 7L)
			{
				double calm = VikingCalm.CalmAt(angered, Day);
				Assert.True(calm > angered);
				Assert.True(calm - angered <= Day);
			}
		}
	}
}
