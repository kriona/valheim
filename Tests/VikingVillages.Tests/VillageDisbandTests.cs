using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests where the group of a disbanded village is at as it gathers at the fire and sets out
	/// </summary>
	public class VillageDisbandTests
	{
		/// <summary>
		/// Ticks a second has, for the tests
		/// </summary>
		private const long Second = 10L;

		/// <summary>
		/// Works out where a group is at, with its village disbanded at 0 and the server's answer in
		/// </summary>
		/// <param name="seconds">How long since it was disbanded</param>
		/// <param name="gatheredAt">When they were all at the fire, in seconds, or -1 while they aren't</param>
		/// <returns>Where they're at</returns>
		private static VillageDisband.Phase At(float seconds, float gatheredAt)
		{
			long gathered = ((gatheredAt < 0f) ? 0L : (long)(gatheredAt * Second));
			return(VillageDisband.PhaseAt((long)(seconds * Second), 1L, gathered, 25f, true, Second, out long _, out long _));
		}

		[Theory]
		[InlineData(0f, (int)VillageDisband.Phase.Gathering)]
		[InlineData(239f, (int)VillageDisband.Phase.Gathering)]
		[InlineData(241f, (int)VillageDisband.Phase.Remaining)]
		[InlineData(264f, (int)VillageDisband.Phase.Remaining)]
		[InlineData(266f, (int)VillageDisband.Phase.Announcing)]
		[InlineData(268f, (int)VillageDisband.Phase.SetOut)]
		public void GatheringGivesUpWaitingAfterFourMinutes(float seconds, int expected)
		{
			Assert.Equal(expected, (int)At(seconds, -1f));
		}

		[Theory]
		[InlineData(29f, (int)VillageDisband.Phase.Gathering)]
		[InlineData(31f, (int)VillageDisband.Phase.Remaining)]
		[InlineData(56f, (int)VillageDisband.Phase.Announcing)]
		[InlineData(58f, (int)VillageDisband.Phase.SetOut)]
		public void GatheringEndsOnceEveryoneIsThere(float seconds, int expected)
		{
			Assert.Equal(expected, (int)At(seconds, 30f));
		}

		[Fact]
		public void AnnouncementWaitsForTheServersAnswer()
		{
			long now = 280L * Second;
			Assert.Equal(VillageDisband.Phase.Remaining, VillageDisband.PhaseAt(now, 1L, 0L, 25f, false, Second, out long announceAt, out long _));
			Assert.True(announceAt > now);
		}

		[Fact]
		public void AnnouncementWaitsNoLongerThanTheAnswerWait()
		{
			long now = 320L * Second;
			Assert.Equal(VillageDisband.Phase.SetOut, VillageDisband.PhaseAt(now, 1L, 0L, 25f, false, Second, out long announceAt, out long setOutAt));
			Assert.Equal(1L + (240L + 25L + 30L) * Second, announceAt);
			Assert.Equal(announceAt + 2L * Second, setOutAt);
		}

		[Fact]
		public void BeforeComparesTheClientThenTheID()
		{
			Assert.True(VillageDisband.Before(new ZDOID(1L, 50u), new ZDOID(2L, 1u)));
			Assert.True(VillageDisband.Before(new ZDOID(2L, 1u), new ZDOID(2L, 5u)));
			Assert.False(VillageDisband.Before(new ZDOID(2L, 5u), new ZDOID(2L, 5u)));
		}
	}
}
