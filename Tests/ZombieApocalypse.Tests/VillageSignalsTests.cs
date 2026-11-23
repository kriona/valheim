using System.Collections.Generic;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests who calls for help when they're hurt, and how often the same victim's calls go out
	/// </summary>
	/// <remarks>
	/// Runs apart from the other tests that make ZDOIDs, since ZDOID keeps a table of user IDs that isn't safe to add to
	/// from tests running at once
	/// </remarks>
	[Collection("ZDOID")]
	public class VillageSignalsTests
	{
		/// <summary>
		/// Victims in the tests
		/// </summary>
		private static readonly ZDOID Asa = new ZDOID(5L, 1u);
		private static readonly ZDOID Bjorn = new ZDOID(5L, 2u);

		/// <summary>
		/// Checks that a player or living viking hurt by a monster calls for help
		/// </summary>
		/// <param name="victimPlayer">Whether the victim is a player, or else a living viking</param>
		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void AMonstersVictimCalls(bool victimPlayer)
		{
			Assert.True(VillageSignals.CallsForHelp(victimPlayer, !victimPlayer, false, false, false, Character.Faction.Undead));
			Assert.True(VillageSignals.CallsForHelp(victimPlayer, !victimPlayer, false, false, false, Character.Faction.ForestMonsters));
		}

		/// <summary>
		/// Checks that someone hurt by a player, a tamed creature, a living viking, a dverger or something a player
		/// summoned calls nobody
		/// </summary>
		/// <param name="attacker">Who hurt them - 0 for a player, 1 for a tamed creature, 2 for a living viking, 3 for a
		/// dverger, 4 for something a player summoned</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(4)]
		public void FriendlyAttackersCallNobody(int attacker)
		{
			Character.Faction faction = ((attacker == 3) ? Character.Faction.Dverger : ((attacker == 4) ? Character.Faction.PlayerSpawned : Character.Faction.Undead));
			Assert.False(VillageSignals.CallsForHelp(true, false, attacker == 0, attacker == 1, attacker == 2, faction));
			Assert.False(VillageSignals.CallsForHelp(false, true, attacker == 0, attacker == 1, attacker == 2, faction));
		}

		/// <summary>
		/// Checks that something hurt that's neither a player nor a living viking calls nobody, even when a monster hurt
		/// it
		/// </summary>
		[Fact]
		public void OthersCallNobody()
		{
			Assert.False(VillageSignals.CallsForHelp(false, false, false, false, false, Character.Faction.Undead));
		}

		/// <summary>
		/// Checks that a victim's second call inside the interval is dropped, and one a whole interval after its first
		/// goes out
		/// </summary>
		[Fact]
		public void ACallInsideTheIntervalIsDropped()
		{
			Dictionary<ZDOID, float> last = new Dictionary<ZDOID, float>();
			Assert.True(VillageSignals.Due(last, Asa, 10f, 1f));
			Assert.False(VillageSignals.Due(last, Asa, 10.5f, 1f));
			Assert.False(VillageSignals.Due(last, Asa, 10.99f, 1f));
			Assert.True(VillageSignals.Due(last, Asa, 11f, 1f));
			Assert.False(VillageSignals.Due(last, Asa, 11.5f, 1f));
		}

		/// <summary>
		/// Checks that one victim's call doesn't hold up another's
		/// </summary>
		[Fact]
		public void VictimsDontHoldEachOtherUp()
		{
			Dictionary<ZDOID, float> last = new Dictionary<ZDOID, float>();
			Assert.True(VillageSignals.Due(last, Asa, 10f, 1f));
			Assert.True(VillageSignals.Due(last, Bjorn, 10.2f, 1f));
			Assert.False(VillageSignals.Due(last, Asa, 10.4f, 1f));
			Assert.False(VillageSignals.Due(last, Bjorn, 10.4f, 1f));
		}

		/// <summary>
		/// Checks that the calls noted an interval or more ago are dropped as another is noted, so the table doesn't grow
		/// by a victim for every one there's been, and the ones still holding a victim up are kept
		/// </summary>
		[Fact]
		public void OldCallsAreDropped()
		{
			Dictionary<ZDOID, float> last = new Dictionary<ZDOID, float>();
			for (uint i = 1u; i <= 5u; i++)
			{
				Assert.True(VillageSignals.Due(last, new ZDOID(7L, i), i * 10f, 1f));
			}
			Assert.Single(last);
			Assert.True(VillageSignals.Due(last, Asa, 50.5f, 1f));
			Assert.Equal(2, last.Count);
			Assert.False(VillageSignals.Due(last, new ZDOID(7L, 5u), 50.9f, 1f));
			Assert.Equal(2, last.Count);
			Assert.True(VillageSignals.Due(last, Bjorn, 51.4f, 1f));
			Assert.Equal(2, last.Count);
			Assert.True(last.ContainsKey(Asa));
			Assert.True(last.ContainsKey(Bjorn));
		}
	}
}
