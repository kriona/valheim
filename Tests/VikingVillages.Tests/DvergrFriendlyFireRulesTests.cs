using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests which hits between the dvergr's side are kept from landing
	/// </summary>
	public class DvergrFriendlyFireRulesTests
	{
		/// <summary>
		/// Checks that a hit between the dvergr's side is spared only with a viking in it and no enmity between them
		/// </summary>
		[Theory]
		[InlineData(true, true, true, false, true)]
		[InlineData(true, true, true, true, false)]
		[InlineData(true, true, false, false, false)]
		[InlineData(true, false, true, false, false)]
		[InlineData(false, true, true, false, false)]
		public void Spared_OnlyBetweenTheDvergrsSideWithAVikingAndNoEnmity(bool victimSide, bool attackerSide, bool viking, bool enemies, bool expected)
		{
			Assert.Equal(expected, DvergrFriendlyFireRules.Spared(victimSide, attackerSide, viking, enemies));
		}
	}
}
