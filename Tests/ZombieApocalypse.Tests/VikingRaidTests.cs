namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how many zombie vikings a raid sends compared to the draugr of the raid it copies
	/// </summary>
	public class VikingRaidTests
	{
		/// <summary>
		/// Checks that a raid sends 60 percent as many vikings as draugr, rounded, and always at least one
		/// </summary>
		/// <param name="draugr">How many draugr</param>
		/// <param name="expected">How many vikings</param>
		[Theory]
		[InlineData(0, 1)]
		[InlineData(1, 1)]
		[InlineData(2, 1)]
		[InlineData(3, 2)]
		[InlineData(4, 2)]
		[InlineData(5, 3)]
		[InlineData(10, 6)]
		public void ScalesDraugrToVikings(int draugr, int expected)
		{
			Assert.Equal(expected, VikingRaid.Scaled(draugr));
		}
	}
}
