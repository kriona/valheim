namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests the spirit damage a silver-lined shield deals to the undead whose blows it holds
	/// </summary>
	public class SilverLiningTests
	{
		/// <summary>
		/// Checks that a shield deals a fifth of its block armor, rounded
		/// </summary>
		[Fact]
		public void DealsAFifthOfBlockArmor()
		{
			Assert.Equal(10f, SilverLining.ShieldSpirit(50f, false), 3);
			Assert.Equal(7f, SilverLining.ShieldSpirit(36f, false), 3);
		}

		/// <summary>
		/// Checks that a weak shield still deals the least a lining deals
		/// </summary>
		[Fact]
		public void DealsAtLeastFive()
		{
			Assert.Equal(5f, SilverLining.ShieldSpirit(10f, false), 3);
			Assert.Equal(5f, SilverLining.ShieldSpirit(0f, false), 3);
		}

		/// <summary>
		/// Checks that a staggering parry deals double, the least included
		/// </summary>
		[Fact]
		public void CriticalDealsDouble()
		{
			Assert.Equal(20f, SilverLining.ShieldSpirit(50f, true), 3);
			Assert.Equal(10f, SilverLining.ShieldSpirit(10f, true), 3);
		}
	}
}
