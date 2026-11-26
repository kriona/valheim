namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the message naming who activated a forsaken power
	/// </summary>
	public class PowerGiversTests
	{
		/// <summary>
		/// Checks that the message names who activated the power, with a power's leading "The" in lowercase and any
		/// other name as it is
		/// </summary>
		/// <param name="giver">Who activated it</param>
		/// <param name="power">The power's name</param>
		/// <param name="expected">The message</param>
		[Theory]
		[InlineData("Astrid", "Eikthyr", "Astrid activates the power of Eikthyr")]
		[InlineData("Astrid", "The Elder", "Astrid activates the power of the Elder")]
		[InlineData("A Viking", "The Queen", "A Viking activates the power of the Queen")]
		[InlineData("Rolf Bloodaxe", "Theodric", "Rolf Bloodaxe activates the power of Theodric")]
		public void NamesWhoActivated(string giver, string power, string expected)
		{
			Assert.Equal(expected, PowerGivers.Line(giver, power));
		}
	}
}
