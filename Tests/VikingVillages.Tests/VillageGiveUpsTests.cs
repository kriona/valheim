using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests when the vikings all give up on reaching something, how long they leave it alone, and when it's forgotten
	/// </summary>
	public class VillageGiveUpsTests
	{
		/// <summary>
		/// The thing tried for and the vikings trying, in the tests
		/// </summary>
		private static readonly ZDOID Thing = new ZDOID(1L, 1u);
		private static readonly ZDOID Other = new ZDOID(1L, 2u);
		private static readonly ZDOID Astrid = new ZDOID(2L, 1u);
		private static readonly ZDOID Jarl = new ZDOID(2L, 2u);

		/// <summary>
		/// Checks that three tries by two vikings make them give up for AvoidTime, and that it's only that thing
		/// </summary>
		[Fact]
		public void ThreeTriesByTwoVikingsGiveUp()
		{
			VillageGiveUps giveUps = new VillageGiveUps();
			Assert.Equal(0f, giveUps.Fail(Thing, Astrid, 0f));
			Assert.Equal(0f, giveUps.Fail(Thing, Jarl, 10f));
			Assert.False(giveUps.GivenUp(Thing, 15f));
			Assert.Equal(VillageGiveUps.AvoidTime, giveUps.Fail(Thing, Astrid, 20f));
			Assert.True(giveUps.GivenUp(Thing, 21f));
			Assert.True(giveUps.GivenUp(Thing, 20f + VillageGiveUps.AvoidTime - 1f));
			Assert.False(giveUps.GivenUp(Thing, 20f + VillageGiveUps.AvoidTime));
			Assert.False(giveUps.GivenUp(Other, 21f));
		}

		/// <summary>
		/// Checks that one viking failing again and again doesn't make them all give up - it may only be stuck itself
		/// </summary>
		[Fact]
		public void OneVikingAloneDoesntGiveUpForAll()
		{
			VillageGiveUps giveUps = new VillageGiveUps();
			for (int i = 0; i < 10; i++)
			{
				Assert.Equal(0f, giveUps.Fail(Thing, Astrid, i * 10f));
			}
			Assert.False(giveUps.GivenUp(Thing, 100f));
		}

		/// <summary>
		/// Checks that tries spread out past Window start the count over
		/// </summary>
		[Fact]
		public void TriesOutsideTheWindowStartOver()
		{
			VillageGiveUps giveUps = new VillageGiveUps();
			giveUps.Fail(Thing, Astrid, 0f);
			giveUps.Fail(Thing, Jarl, 10f);
			Assert.Equal(0f, giveUps.Fail(Thing, Astrid, VillageGiveUps.Window + 20f));
			Assert.False(giveUps.GivenUp(Thing, VillageGiveUps.Window + 21f));
		}

		/// <summary>
		/// Checks that giving up on something again leaves it alone longer, up to AvoidMax
		/// </summary>
		[Fact]
		public void GivingUpAgainWaitsLonger()
		{
			Assert.Equal(VillageGiveUps.AvoidTime, VillageGiveUps.LeftAlone(1));
			Assert.Equal(VillageGiveUps.AvoidTime * 2f, VillageGiveUps.LeftAlone(2));
			Assert.Equal(VillageGiveUps.AvoidMax, VillageGiveUps.LeftAlone(10));
			VillageGiveUps giveUps = new VillageGiveUps();
			giveUps.Fail(Thing, Astrid, 0f);
			giveUps.Fail(Thing, Jarl, 1f);
			giveUps.Fail(Thing, Astrid, 2f);
			float later = 2f + VillageGiveUps.AvoidTime + 1f;
			giveUps.Fail(Thing, Astrid, later);
			giveUps.Fail(Thing, Jarl, later + 1f);
			Assert.Equal(VillageGiveUps.AvoidTime * 2f, giveUps.Fail(Thing, Astrid, later + 2f));
		}

		/// <summary>
		/// Checks that pruning keeps what's still left alone or was given up on lately, and forgets the rest
		/// </summary>
		[Fact]
		public void PruneForgetsOnlyWhatsLongDone()
		{
			VillageGiveUps giveUps = new VillageGiveUps();
			giveUps.Fail(Thing, Astrid, 0f);
			giveUps.Fail(Thing, Jarl, 1f);
			giveUps.Fail(Thing, Astrid, 2f);
			giveUps.Prune(100f);
			Assert.True(giveUps.GivenUp(Thing, 100f));
			float forgotten = 2f + VillageGiveUps.AvoidTime + VillageGiveUps.AvoidMax + 1f;
			giveUps.Prune(forgotten);
			// Forgotten, so giving up on it again leaves it alone for the first time's AvoidTime
			giveUps.Fail(Thing, Astrid, forgotten);
			giveUps.Fail(Thing, Jarl, forgotten + 1f);
			Assert.Equal(VillageGiveUps.AvoidTime, giveUps.Fail(Thing, Astrid, forgotten + 2f));
		}
	}
}
