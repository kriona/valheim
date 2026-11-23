namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how much of a creature's damage is counted as fire, through hits and healing
	/// </summary>
	public class FireKillsTests
	{
		/// <summary>
		/// Makes a tally from a run of hits
		/// </summary>
		/// <param name="hits">Each hit's total damage and fire damage, in turn</param>
		/// <returns>The tally</returns>
		private static FireKills.Tally Hits(params float[] hits)
		{
			FireKills.Tally tally = new FireKills.Tally();
			for (int i = 0; i + 1 < hits.Length; i += 2)
			{
				tally.Add(hits[i], hits[i + 1]);
			}
			return(tally);
		}

		/// <summary>
		/// Checks that hits add up their total and their fire
		/// </summary>
		[Fact]
		public void AddsUpHits()
		{
			FireKills.Tally tally = Hits(30f, 0f, 20f, 15f, 50f, 10f);
			Assert.Equal(100f, tally.Total, 3);
			Assert.Equal(25f, tally.Fire, 3);
		}

		/// <summary>
		/// Checks that a quarter of the damage as fire counts at a share of 25 and not at 26
		/// </summary>
		[Fact]
		public void CountsAQuarterAtAShareOfAQuarter()
		{
			FireKills.Tally tally = Hits(75f, 0f, 25f, 25f);
			Assert.True(tally.Burned(25));
			Assert.False(tally.Burned(26));
		}

		/// <summary>
		/// Checks that a little under the share of fire doesn't count
		/// </summary>
		[Fact]
		public void LeavesOutJustUnderTheShare()
		{
			Assert.False(Hits(100f, 24.9f).Burned(25));
		}

		/// <summary>
		/// Checks that a share of 0 never counts, even all fire
		/// </summary>
		[Fact]
		public void NeverCountsAShareOfNothing()
		{
			Assert.False(Hits(100f, 100f).Burned(0));
		}

		/// <summary>
		/// Checks that a tally with no damage never counts
		/// </summary>
		[Fact]
		public void NeverCountsNoDamage()
		{
			Assert.False(new FireKills.Tally().Burned(25));
		}

		/// <summary>
		/// Checks that healing takes off fire and the rest in proportion, keeping the share
		/// </summary>
		[Fact]
		public void HealsInProportion()
		{
			FireKills.Tally tally = Hits(75f, 0f, 25f, 25f);
			Assert.False(tally.Heal(60f));
			Assert.Equal(40f, tally.Total, 3);
			Assert.Equal(10f, tally.Fire, 3);
			Assert.True(tally.Burned(25));
		}

		/// <summary>
		/// Checks that all fire still counts at a share of 100 after a partial heal whose rounding leaves the fire a hair
		/// under the total - 37 healed by 13 leaves 23.999998 fire of 24
		/// </summary>
		/// <param name="total">The damage, all of it fire</param>
		/// <param name="healed">How much is healed</param>
		[Theory]
		[InlineData(37f, 13f)]
		[InlineData(37f, 25f)]
		[InlineData(100f, 33f)]
		public void CountsAllFireAfterAPartialHeal(float total, float healed)
		{
			FireKills.Tally tally = Hits(total, total);
			Assert.False(tally.Heal(healed));
			Assert.True(tally.Burned(100));
		}

		/// <summary>
		/// Checks that healing all the damage or more says the tally is to be forgotten
		/// </summary>
		/// <param name="healed">How much is healed of 50 damage</param>
		[Theory]
		[InlineData(50f)]
		[InlineData(80f)]
		public void ForgetsOnceAllHealed(float healed)
		{
			Assert.True(Hits(50f, 20f).Heal(healed));
		}

		/// <summary>
		/// Checks that healing a hair short of all the damage keeps the tally
		/// </summary>
		[Fact]
		public void KeepsAlmostAllHealed()
		{
			FireKills.Tally tally = Hits(50f, 20f);
			Assert.False(tally.Heal(49f));
			Assert.Equal(1f, tally.Total, 3);
			Assert.Equal(0.4f, tally.Fire, 3);
		}
	}
}
