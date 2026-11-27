namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rules for a PC viking's power attacks - what each weapon's is good for, when it's a smart move and
	/// how long the viking waits before its next
	/// </summary>
	public class PowerAttackRulesTests
	{
		/// <summary>
		/// Checks the game's weapons by their multipliers - an atgeir's and a battleaxe's power attacks stagger, a
		/// sword's, a knife's and an axe's hit hard, a mace's does both, and one that's barely more than the normal blow
		/// counts for nothing
		/// </summary>
		[Fact]
		public void ClassifiesTheGamesWeapons()
		{
			Assert.Equal(PowerAttackRules.Kind.Staggers, PowerAttackRules.Classify(1f, 1f, 1f, 6f));
			Assert.Equal(PowerAttackRules.Kind.Staggers, PowerAttackRules.Classify(1f, 1.5f, 0.5f, 4f));
			Assert.Equal(PowerAttackRules.Kind.HitsHard, PowerAttackRules.Classify(1f, 1f, 3f, 1f));
			Assert.Equal(PowerAttackRules.Kind.HitsHard, PowerAttackRules.Classify(1f, 1f, 1.5f, 1f));
			Assert.Equal(PowerAttackRules.Kind.Staggers | PowerAttackRules.Kind.HitsHard, PowerAttackRules.Classify(1f, 1f, 2.5f, 2f));
			Assert.Equal(PowerAttackRules.Kind.None, PowerAttackRules.Classify(1f, 1f, 1.2f, 1.5f));
		}

		/// <summary>
		/// Checks that a blow is used, and a spear's throw, a shot, or a weapon with no secondary attack isn't
		/// </summary>
		[Fact]
		public void UsesOnlyBlowsThatKeepTheWeapon()
		{
			Assert.True(PowerAttackRules.Usable(Attack.AttackType.Horizontal, false, true));
			Assert.True(PowerAttackRules.Usable(Attack.AttackType.Vertical, false, true));
			Assert.False(PowerAttackRules.Usable(Attack.AttackType.Projectile, true, true));
			Assert.False(PowerAttackRules.Usable(Attack.AttackType.Horizontal, true, true));
			Assert.False(PowerAttackRules.Usable(Attack.AttackType.Horizontal, false, false));
		}

		/// <summary>
		/// Checks that only a swing of half a circle or more counts as wide
		/// </summary>
		[Fact]
		public void OnlyASpinIsWide()
		{
			Assert.True(PowerAttackRules.Wide(360f));
			Assert.False(PowerAttackRules.Wide(30f));
		}

		/// <summary>
		/// Checks that a stagger is used when someone's there to make the most of it, or when a spin catches a crowd,
		/// but not alone against one enemy, on one that can't be staggered, or on one that's staggering already
		/// </summary>
		[Fact]
		public void StaggersWithHelpOrACrowd()
		{
			PowerAttackRules.Kind staggers = PowerAttackRules.Kind.Staggers;
			Assert.True(PowerAttackRules.Uses(staggers, true, false, true, 1, true, false));
			Assert.True(PowerAttackRules.Uses(staggers, false, false, true, 1, true, false));
			Assert.True(PowerAttackRules.Uses(staggers, true, false, true, PowerAttackRules.CrowdSize, false, false));
			Assert.False(PowerAttackRules.Uses(staggers, false, false, true, PowerAttackRules.CrowdSize, false, false));
			Assert.False(PowerAttackRules.Uses(staggers, true, false, true, 1, false, false));
			Assert.False(PowerAttackRules.Uses(staggers, true, false, false, 3, true, false));
			Assert.False(PowerAttackRules.Uses(staggers, true, true, true, 3, true, false));
		}

		/// <summary>
		/// Checks that a hard hit is used on a staggering enemy, and otherwise whenever it's ready, but saved while
		/// someone at the enemy can stagger it
		/// </summary>
		[Fact]
		public void HitsHardOnAStagger()
		{
			PowerAttackRules.Kind hitsHard = PowerAttackRules.Kind.HitsHard;
			Assert.True(PowerAttackRules.Uses(hitsHard, false, true, true, 1, true, true));
			Assert.True(PowerAttackRules.Uses(hitsHard, false, false, true, 1, false, false));
			Assert.False(PowerAttackRules.Uses(hitsHard, false, false, true, 1, true, true));
			Assert.True(PowerAttackRules.Uses(hitsHard, false, false, false, 1, true, true));
		}

		/// <summary>
		/// Checks that a mace's power attack, which does both, staggers with help and hits hard alone
		/// </summary>
		[Fact]
		public void BothStaggersAndHitsHard()
		{
			PowerAttackRules.Kind both = PowerAttackRules.Kind.Staggers | PowerAttackRules.Kind.HitsHard;
			Assert.True(PowerAttackRules.Uses(both, false, false, true, 1, true, true));
			Assert.True(PowerAttackRules.Uses(both, false, false, true, 1, false, false));
			Assert.True(PowerAttackRules.Uses(both, false, true, true, 1, false, false));
		}

		/// <summary>
		/// Checks that the wait after a power attack is the stamina it costs a player at a player's base regen, and
		/// never less than RestMin
		/// </summary>
		[Fact]
		public void RestsForTheStamina()
		{
			Assert.Equal(4.8f, PowerAttackRules.Rest(24f), 3);
			Assert.Equal(4f, PowerAttackRules.Rest(20f), 3);
			Assert.Equal(PowerAttackRules.RestMin, PowerAttackRules.Rest(8f), 3);
		}
	}
}
