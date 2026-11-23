namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how a lurching zombie tells its left bones from its right
	/// </summary>
	public class ZombieLurchTests
	{
		/// <summary>
		/// Checks the side read from the bone names creature rigs use
		/// </summary>
		/// <param name="name">The bone's name, in lower case</param>
		/// <param name="expected">-1 for the left, 1 for the right, 0 for neither</param>
		[Theory]
		[InlineData("leftarm", -1)]
		[InlineData("rightupleg", 1)]
		[InlineData("l_hand", -1)]
		[InlineData("r.foot", 1)]
		[InlineData("l hand", -1)]
		[InlineData("r-thigh", 1)]
		[InlineData("arm_l", -1)]
		[InlineData("leg.r", 1)]
		[InlineData("wing l", -1)]
		[InlineData("claw-r", 1)]
		[InlineData("spine", 0)]
		[InlineData("head", 0)]
		[InlineData("lowerarm", 0)]
		[InlineData("hip", 0)]
		[InlineData("l", 0)]
		[InlineData("", 0)]
		public void SideReadsTheBoneName(string name, int expected)
		{
			Assert.Equal(expected, ZombieLurch.Side(name));
		}

		/// <summary>
		/// Checks that a letter only counts when a separator sets it apart, so words that start or end with l or r
		/// aren't taken for a side
		/// </summary>
		/// <param name="name">The bone's name, in lower case</param>
		[Theory]
		[InlineData("ribs")]
		[InlineData("tail")]
		[InlineData("lr")]
		[InlineData("collar")]
		public void SideNeedsASeparator(string name)
		{
			Assert.Equal(0, ZombieLurch.Side(name));
		}
	}
}
