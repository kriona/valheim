using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how many of a village's vikings keep watch at night, how the nights and their shifts are told apart, how
	/// the vikings keeping watch are written and read, and who takes the place of one that dies
	/// </summary>
	/// <remarks>
	/// Runs apart from the other tests that make ZDOIDs, since ZDOID keeps a table of user IDs that isn't safe to add to
	/// from tests running at once
	/// </remarks>
	[Collection("ZDOID")]
	public class VillageWatchTests
	{
		/// <summary>
		/// How long a day is in the tests, in seconds - the game's own
		/// </summary>
		private const long Day = 1200L;

		/// <summary>
		/// Vikings in the tests
		/// </summary>
		private static readonly ZDOID Asa = new ZDOID(5L, 1u);
		private static readonly ZDOID Bjorn = new ZDOID(5L, 2u);
		private static readonly ZDOID Dag = new ZDOID(6L, 1u);
		private static readonly ZDOID Eir = new ZDOID(6L, 2u);

		/// <summary>
		/// Checks that one viking keeps watch for every ten, or part of ten, and none for an empty village
		/// </summary>
		/// <param name="vikings">How many vikings the village has</param>
		/// <param name="expected">How many keep watch</param>
		[Theory]
		[InlineData(-3, 0)]
		[InlineData(0, 0)]
		[InlineData(1, 1)]
		[InlineData(9, 1)]
		[InlineData(10, 1)]
		[InlineData(11, 2)]
		[InlineData(20, 2)]
		[InlineData(25, 3)]
		public void WatchCountRoundsUp(int vikings, int expected)
		{
			Assert.Equal(expected, VillageWatch.WatchCount(vikings));
		}

		/// <summary>
		/// Checks that the night's number changes at noon - so a night from dusk to dawn keeps one number - counting
		/// the first night, before the first noon, as -1
		/// </summary>
		/// <param name="seconds">The time, in world seconds</param>
		/// <param name="expected">The night's number</param>
		[Theory]
		[InlineData(0.0, -1L)]
		[InlineData(599.9, -1L)]
		[InlineData(600.0, 0L)]
		[InlineData(1020.0, 0L)]
		[InlineData(1788.0, 0L)]
		[InlineData(1800.0, 1L)]
		[InlineData(3000.0, 2L)]
		public void NightChangesAtNoon(double seconds, long expected)
		{
			Assert.Equal(expected, VillageWatch.Night(seconds, Day));
		}

		/// <summary>
		/// Checks that every time falls in night 0 when the day has no length
		/// </summary>
		/// <param name="dayLength">How long a day is, in seconds</param>
		[Theory]
		[InlineData(0L)]
		[InlineData(-1200L)]
		public void NightIsZeroWithNoDayLength(long dayLength)
		{
			Assert.Equal(0L, VillageWatch.Night(5000.0, dayLength));
		}

		/// <summary>
		/// Checks that the first shift runs until midnight and the second from midnight on
		/// </summary>
		/// <param name="dayFraction">The time of day, from 0 at midnight to 1 at the next</param>
		/// <param name="expected">The shift</param>
		[Theory]
		[InlineData(0f, 1)]
		[InlineData(0.2f, 1)]
		[InlineData(0.49f, 1)]
		[InlineData(0.5f, 0)]
		[InlineData(0.85f, 0)]
		[InlineData(0.99f, 0)]
		public void ShiftChangesAtMidnight(float dayFraction, int expected)
		{
			Assert.Equal(expected, VillageWatch.Shift(dayFraction));
		}

		/// <summary>
		/// Checks that the vikings of the hardest biome are the ones the watch is picked from, all of them when they tie,
		/// and none from an empty list
		/// </summary>
		[Fact]
		public void TheStrongestKeepWatch()
		{
			Assert.Equal(new List<int> { 2 }, VillageWatch.Strongest(new List<int> { 0, 1, 3, 1 }));
			Assert.Equal(new List<int> { 1, 3 }, VillageWatch.Strongest(new List<int> { 2, 4, 0, 4 }));
			Assert.Equal(new List<int> { 0, 1 }, VillageWatch.Strongest(new List<int> { 0, 0 }));
			Assert.Empty(VillageWatch.Strongest(new List<int>()));
		}

		/// <summary>
		/// Checks that no vikings read back as none
		/// </summary>
		[Fact]
		public void NoIdsReadBackAsNone()
		{
			Assert.Empty(VillageWatch.DecodeIds(VillageWatch.EncodeIds(new List<ZDOID>())));
		}

		/// <summary>
		/// Checks that one viking reads back as itself
		/// </summary>
		[Fact]
		public void OneIdReadsBack()
		{
			Assert.Equal(new List<ZDOID> { Asa }, VillageWatch.DecodeIds(VillageWatch.EncodeIds(new List<ZDOID> { Asa })));
		}

		/// <summary>
		/// Checks that several vikings read back in the order they were written, with a gap for none kept in its place
		/// </summary>
		[Fact]
		public void SeveralIdsReadBackInOrder()
		{
			List<ZDOID> ids = new List<ZDOID> { Dag, ZDOID.None, Asa, Eir, ZDOID.None };
			Assert.Equal(ids, VillageWatch.DecodeIds(VillageWatch.EncodeIds(ids)));
		}

		/// <summary>
		/// Checks that a list of vikings cut short reads as none rather than throwing
		/// </summary>
		[Fact]
		public void UnreadableIdsReadAsNone()
		{
			byte[] bytes = VillageWatch.EncodeIds(new List<ZDOID> { Dag, Asa });
			for (int length = 0; length < bytes.Length; length++)
			{
				Assert.Empty(VillageWatch.DecodeIds(bytes[..length]));
			}
		}

		/// <summary>
		/// Checks that a viking who saw a viking keeping watch die takes its place, with no one to wake, and is noted as
		/// having kept watch
		/// </summary>
		[Fact]
		public void ClaimTakesTheDeadOnesPlace()
		{
			List<ZDOID> watchers = new List<ZDOID> { Asa, Bjorn };
			List<ZDOID> watched = new List<ZDOID> { Asa, Bjorn };
			List<ZDOID> reliefs = new List<ZDOID> { Eir };
			Assert.True(VillageWatch.TryClaim(watchers, watched, reliefs, Asa, Dag, 2));
			Assert.Equal(new List<ZDOID> { Dag, Bjorn }, watchers);
			Assert.Equal(new List<ZDOID> { Asa, Bjorn, Dag }, watched);
			Assert.Equal(new List<ZDOID> { ZDOID.None, ZDOID.None }, reliefs);
		}

		/// <summary>
		/// Checks that a viking who saw one keeping watch die takes a place of its own when the dead one's place is gone
		/// but the watch is short
		/// </summary>
		[Fact]
		public void ClaimFillsAShortWatch()
		{
			List<ZDOID> watchers = new List<ZDOID> { Bjorn };
			List<ZDOID> watched = new List<ZDOID> { Asa, Bjorn };
			List<ZDOID> reliefs = new List<ZDOID>();
			Assert.True(VillageWatch.TryClaim(watchers, watched, reliefs, Asa, Dag, 2));
			Assert.Equal(new List<ZDOID> { Bjorn, Dag }, watchers);
			Assert.Equal(new List<ZDOID> { Asa, Bjorn, Dag }, watched);
			Assert.Equal(new List<ZDOID> { ZDOID.None, ZDOID.None }, reliefs);
		}

		/// <summary>
		/// Checks that no viking takes a place when the dead one's place is gone and the watch is full
		/// </summary>
		[Fact]
		public void ClaimFailsWithAFullWatch()
		{
			List<ZDOID> watchers = new List<ZDOID> { Bjorn };
			List<ZDOID> watched = new List<ZDOID> { Asa, Bjorn };
			List<ZDOID> reliefs = new List<ZDOID> { ZDOID.None };
			Assert.False(VillageWatch.TryClaim(watchers, watched, reliefs, Asa, Dag, 1));
			Assert.Equal(new List<ZDOID> { Bjorn }, watchers);
			Assert.Equal(new List<ZDOID> { Asa, Bjorn }, watched);
		}

		/// <summary>
		/// Checks that a viking keeping watch, one that has kept it tonight or one to be woken to keep it can't take the
		/// dead one's place
		/// </summary>
		/// <param name="claimant">Which of the vikings claims the place - 0 for one keeping watch, 1 for one that has
		/// kept it, 2 for one to be woken</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(2)]
		public void ClaimFailsForOneAlreadyOnWatch(int claimant)
		{
			List<ZDOID> watchers = new List<ZDOID> { Asa, Bjorn };
			List<ZDOID> watched = new List<ZDOID> { Asa, Bjorn, Dag };
			List<ZDOID> reliefs = new List<ZDOID> { ZDOID.None, Eir };
			ZDOID[] claimants = { Bjorn, Dag, Eir };
			Assert.False(VillageWatch.TryClaim(watchers, watched, reliefs, Asa, claimants[claimant], 3));
			Assert.Equal(new List<ZDOID> { Asa, Bjorn }, watchers);
			Assert.Equal(new List<ZDOID> { Asa, Bjorn, Dag }, watched);
			Assert.Equal(new List<ZDOID> { ZDOID.None, Eir }, reliefs);
		}
	}
}
