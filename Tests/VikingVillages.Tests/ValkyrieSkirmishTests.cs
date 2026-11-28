using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests which creatures a valkyrie fights hit and run, how far their blows reach, and how high she hovers above them
	/// </summary>
	public class ValkyrieSkirmishTests
	{
		[Fact]
		public void ThrowingOrShootingStrikesAtRange()
		{
			Assert.True(ValkyrieSkirmish.StrikesAtRange(new[] { Attack.AttackType.Horizontal, Attack.AttackType.Projectile }));
			Assert.True(ValkyrieSkirmish.StrikesAtRange(new[] { Attack.AttackType.TriggerProjectile }));
			Assert.False(ValkyrieSkirmish.StrikesAtRange(new[] { Attack.AttackType.Horizontal, Attack.AttackType.Vertical, Attack.AttackType.Area }));
			Assert.False(ValkyrieSkirmish.StrikesAtRange(new Attack.AttackType[0]));
		}

		[Fact]
		public void GreydwarvesThrownStonesDontCount()
		{
			Assert.False(ValkyrieSkirmish.RangedCounts("Greydwarf"));
			Assert.False(ValkyrieSkirmish.RangedCounts("Greydwarf_Elite"));
			Assert.False(ValkyrieSkirmish.RangedCounts("Greydwarf_Shaman_Frozen"));
			Assert.True(ValkyrieSkirmish.RangedCounts("GoblinArcher"));
			Assert.True(ValkyrieSkirmish.RangedCounts("Draugr_Ranged"));
		}

		[Fact]
		public void ACreaturesReachIsItsLongestAttacksOrADefault()
		{
			Assert.Equal(3.5f, ValkyrieSkirmish.ReachOf(new[] { 2f, 3.5f, 1f }));
			Assert.Equal(ValkyrieSkirmish.DefaultReach, ValkyrieSkirmish.ReachOf(new float[0]));
		}

		[Fact]
		public void SheHoversAboveItsHeadAndReachWithAMargin()
		{
			Assert.Equal(1.8f + 2.5f + ValkyrieSkirmish.Margin, ValkyrieSkirmish.SafeHeight(1.8f, 2.5f), 4);
		}
	}
}
