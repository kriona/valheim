using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a village's waiting deaths are written, read, kept in order and put right, and when its vikings are due
	/// back
	/// </summary>
	public class VillageRespawnTests
	{
		/// <summary>
		/// How long a day is in the tests, in seconds - a day is 1000000000 ticks, three days 3000000000, both exactly
		/// </summary>
		private const long Day = 100L;

		/// <summary>
		/// Checks that deaths read back with when each happened and whether a player killed it, in the order they were
		/// written
		/// </summary>
		[Fact]
		public void DeathsReadBackWhole()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death>
			{
				Death(100L, false),
				Death(250L, true),
				Death(250L, false),
				Death(6000000000000L, true)
			};
			List<VillageRespawn.Death> read = VillageRespawn.DecodeDeaths(VillageRespawn.EncodeDeaths(deaths));
			Assert.Equal(new[] { 100L, 250L, 250L, 6000000000000L }, Times(read));
			Assert.Equal(new[] { false, true, false, true }, read.ConvertAll((VillageRespawn.Death death) => death.ByPlayer));
		}

		/// <summary>
		/// Checks that only the earliest 64 deaths are written, when there are more
		/// </summary>
		[Fact]
		public void MoreThanTheMostKeepsTheEarliest()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death>();
			for (long time = 1L; time <= 70L; time++)
			{
				deaths.Add(Death(time, time % 3L == 0L));
			}
			List<VillageRespawn.Death> read = VillageRespawn.DecodeDeaths(VillageRespawn.EncodeDeaths(deaths));
			Assert.Equal(64, read.Count);
			Assert.Equal(1L, read[0].Time);
			Assert.Equal(64L, read[63].Time);
			Assert.True(read[62].ByPlayer);
		}

		/// <summary>
		/// Checks that no deaths read back as none
		/// </summary>
		[Fact]
		public void NoDeathsReadBackAsNone()
		{
			Assert.Empty(VillageRespawn.DecodeDeaths(VillageRespawn.EncodeDeaths(new List<VillageRespawn.Death>())));
		}

		/// <summary>
		/// Checks that deaths cut short or bytes that don't hold deaths read as none rather than throwing
		/// </summary>
		[Fact]
		public void UnreadableDeathsReadAsNone()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death>
			{
				new VillageRespawn.Death { Time = 100L, ByPlayer = false },
				new VillageRespawn.Death { Time = 200L, ByPlayer = true }
			};
			byte[] bytes = VillageRespawn.EncodeDeaths(deaths);
			for (int length = 0; length < bytes.Length; length++)
			{
				Assert.Empty(VillageRespawn.DecodeDeaths(bytes[..length]));
			}
			Assert.Empty(VillageRespawn.DecodeDeaths(new byte[] { 1, 2 }));
		}

		/// <summary>
		/// Checks that a viking is due back a day after its death or after the last one came back, whichever is later, or
		/// three days when a player killed it
		/// </summary>
		/// <param name="lastRespawn">When the last viking came back, in ticks</param>
		/// <param name="died">When the viking died, in ticks</param>
		/// <param name="byPlayer">Whether a player killed it</param>
		/// <param name="expected">When it's due back, in ticks</param>
		[Theory]
		[InlineData(0L, 5000000000L, false, 6000000000L)]
		[InlineData(4000000000L, 5000000000L, false, 6000000000L)]
		[InlineData(5000000000L, 5000000000L, false, 6000000000L)]
		[InlineData(8000000000L, 5000000000L, false, 9000000000L)]
		[InlineData(0L, 5000000000L, true, 8000000000L)]
		[InlineData(8000000000L, 5000000000L, true, 11000000000L)]
		public void DueADayAfterTheLaterOfDeathAndLastRespawn(long lastRespawn, long died, bool byPlayer, long expected)
		{
			Assert.Equal(expected, VillageRespawn.DueAt(lastRespawn, Death(died, byPlayer), Day));
		}

		/// <summary>
		/// Checks that no death is due before a day has passed since it, or since the last viking came back
		/// </summary>
		[Fact]
		public void NoneDueBeforeADay()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death> { Death(5000000000L, false), Death(5500000000L, false) };
			Assert.Equal(0, VillageRespawn.DueCount(0L, deaths, Day, 5900000000L, 8));
			Assert.Equal(0, VillageRespawn.DueCount(5500000000L, deaths, Day, 6400000000L, 8));
		}

		/// <summary>
		/// Checks that deaths waiting together come due a day apart, each a day after the one before it was due
		/// </summary>
		[Fact]
		public void DeathsComeDueADayApart()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death> { Death(5000000000L, false), Death(5000000000L, false), Death(5000000000L, false) };
			Assert.Equal(1, VillageRespawn.DueCount(0L, deaths, Day, 6000000000L, 8));
			Assert.Equal(1, VillageRespawn.DueCount(0L, deaths, Day, 6999999999L, 8));
			Assert.Equal(2, VillageRespawn.DueCount(0L, deaths, Day, 7000000000L, 8));
			Assert.Equal(3, VillageRespawn.DueCount(0L, deaths, Day, 20000000000L, 8));
		}

		/// <summary>
		/// Checks that a death after the one before it came due is due a day after it happened, and one a player caused
		/// three days after
		/// </summary>
		[Fact]
		public void LaterDeathsComeDueADayAfterThey()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death> { Death(5000000000L, false), Death(9000000000L, false), Death(9000000000L, true) };
			Assert.Equal(1, VillageRespawn.DueCount(0L, deaths, Day, 9999999999L, 8));
			Assert.Equal(2, VillageRespawn.DueCount(0L, deaths, Day, 10000000000L, 8));
			Assert.Equal(2, VillageRespawn.DueCount(0L, deaths, Day, 12999999999L, 8));
			Assert.Equal(3, VillageRespawn.DueCount(0L, deaths, Day, 13000000000L, 8));
		}

		/// <summary>
		/// Checks that no more are counted due than asked for, or than are waiting
		/// </summary>
		[Fact]
		public void DueCountsNoMoreThanTheMost()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death> { Death(1000000000L, false), Death(1000000000L, false), Death(1000000000L, false) };
			Assert.Equal(2, VillageRespawn.DueCount(0L, deaths, Day, 90000000000L, 2));
			Assert.Equal(3, VillageRespawn.DueCount(0L, deaths, Day, 90000000000L, 8));
			Assert.Equal(0, VillageRespawn.DueCount(0L, new List<VillageRespawn.Death>(), Day, 90000000000L, 8));
		}

		/// <summary>
		/// Checks how many vikings have to be due before they come back to a village enemies hold
		/// </summary>
		/// <param name="enemies">The enemies there</param>
		/// <param name="home">The vikings at home</param>
		/// <param name="waiting">The vikings waiting to come back</param>
		/// <param name="expected">How many have to be due</param>
		[Theory]
		[InlineData(0, 0, 8, 1)]
		[InlineData(0, 5, 3, 1)]
		[InlineData(1, 7, 1, 1)]
		[InlineData(3, 2, 6, 2)]
		[InlineData(4, 0, 8, 5)]
		[InlineData(12, 0, 8, 8)]
		[InlineData(12, 3, 5, 5)]
		public void GroupOutnumbersTheEnemies(int enemies, int home, int waiting, int expected)
		{
			Assert.Equal(expected, VillageRespawn.GroupNeeded(enemies, home, waiting));
		}

		/// <summary>
		/// Checks that a village with no wait noted starts one now
		/// </summary>
		[Fact]
		public void NoWaitNotedStartsNow()
		{
			Assert.Equal(500f, VillageRespawn.WaitStart(false, 100f, 495f, 500f));
		}

		/// <summary>
		/// Checks that a wait looked at again within the gap goes on from when it started
		/// </summary>
		[Fact]
		public void WaitLookedAtWithinTheGapGoesOn()
		{
			Assert.Equal(100f, VillageRespawn.WaitStart(true, 100f, 495f, 500f));
			Assert.Equal(100f, VillageRespawn.WaitStart(true, 100f, 500f - VillageRespawn.EnemyWaitGap, 500f));
		}

		/// <summary>
		/// Checks that a wait not looked at for longer than the gap, as while no viking was due, starts over
		/// </summary>
		[Fact]
		public void WaitBrokenOffStartsOver()
		{
			Assert.Equal(5000f, VillageRespawn.WaitStart(true, 100f, 700f, 5000f));
			Assert.Equal(500f, VillageRespawn.WaitStart(true, 100f, 499f - VillageRespawn.EnemyWaitGap, 500f));
		}

		/// <summary>
		/// Checks that an enemy holds vikings back until the patience runs out, and for as long as a raid is on
		/// </summary>
		[Fact]
		public void EnemyHoldsBackUntilThePatienceRunsOut()
		{
			Assert.True(VillageRespawn.HoldsBack(100f, 100f, false));
			Assert.True(VillageRespawn.HoldsBack(100f, 99f + VillageRespawn.EnemyPatience, false));
			Assert.False(VillageRespawn.HoldsBack(100f, 100f + VillageRespawn.EnemyPatience, false));
			Assert.True(VillageRespawn.HoldsBack(100f, 5000f, true));
		}

		/// <summary>
		/// Checks that a death added to none is the only one
		/// </summary>
		[Fact]
		public void InsertIntoNone()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death>();
			VillageRespawn.InsertByTime(deaths, Death(40L, true));
			Assert.Equal(new[] { 40L }, Times(deaths));
		}

		/// <summary>
		/// Checks that deaths reported out of order are kept earliest first
		/// </summary>
		[Fact]
		public void InsertKeepsDeathsInOrder()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death>();
			foreach (long time in new[] { 30L, 10L, 50L, 20L, 5L })
			{
				VillageRespawn.InsertByTime(deaths, Death(time, false));
			}
			Assert.Equal(new[] { 5L, 10L, 20L, 30L, 50L }, Times(deaths));
		}

		/// <summary>
		/// Checks that a death at the same time as some already waiting goes after them
		/// </summary>
		[Fact]
		public void InsertGoesAfterEqualTimes()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death> { Death(10L, false), Death(10L, false), Death(20L, false) };
			VillageRespawn.InsertByTime(deaths, Death(10L, true));
			Assert.Equal(new[] { 10L, 10L, 10L, 20L }, Times(deaths));
			Assert.Equal(new[] { false, false, true, false }, deaths.ConvertAll((VillageRespawn.Death death) => death.ByPlayer));
		}

		/// <summary>
		/// Checks that a village short of its head count notes a death now, not by a player, for each viking it's short
		/// </summary>
		[Fact]
		public void ReconcileAddsADeathForEachMissing()
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death> { Death(10L, true) };
			VillageRespawn.Reconcile(deaths, 2, 50L);
			Assert.Equal(new[] { 10L, 50L, 50L }, Times(deaths));
			Assert.Equal(new[] { true, false, false }, deaths.ConvertAll((VillageRespawn.Death death) => death.ByPlayer));
		}

		/// <summary>
		/// Checks that a village over its head count forgets its latest deaths, one for each viking over, and all of them
		/// when it's over by more than it has
		/// </summary>
		/// <param name="gap">How far under the head count the vikings and deaths fall, less than 0 for over</param>
		/// <param name="expected">When each death left happened</param>
		[Theory]
		[InlineData(0, new[] { 10L, 20L, 30L })]
		[InlineData(-1, new[] { 10L, 20L })]
		[InlineData(-2, new[] { 10L })]
		[InlineData(-3, new long[0])]
		[InlineData(-5, new long[0])]
		public void ReconcileForgetsTheLatest(int gap, long[] expected)
		{
			List<VillageRespawn.Death> deaths = new List<VillageRespawn.Death> { Death(10L, false), Death(20L, true), Death(30L, false) };
			VillageRespawn.Reconcile(deaths, gap, 99L);
			Assert.Equal(expected, Times(deaths));
		}

		/// <summary>
		/// Makes a death
		/// </summary>
		/// <param name="time">When it happened, in ticks</param>
		/// <param name="byPlayer">Whether a player killed it</param>
		/// <returns>The death</returns>
		private static VillageRespawn.Death Death(long time, bool byPlayer)
		{
			return(new VillageRespawn.Death { Time = time, ByPlayer = byPlayer });
		}

		/// <summary>
		/// Lists when each of some deaths happened
		/// </summary>
		/// <param name="deaths">The deaths</param>
		/// <returns>Their times, in the same order</returns>
		private static long[] Times(List<VillageRespawn.Death> deaths)
		{
			return(deaths.ConvertAll((VillageRespawn.Death death) => death.Time).ToArray());
		}
	}
}
