namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the names a village's vikings turn down for it - just Village, and a legend's
	/// </summary>
	public class VillageNamesTests
	{
		/// <summary>
		/// Checks that Village is too plain for a village in any case and with spaces or "$" around it, while a name
		/// with more to it isn't
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="expected">Whether it's too plain</param>
		[Theory]
		[InlineData("Village", true)]
		[InlineData("village", true)]
		[InlineData("VILLAGE", true)]
		[InlineData("  Village  ", true)]
		[InlineData("$Village", true)]
		[InlineData("Viking Village", false)]
		[InlineData("Villages", false)]
		[InlineData("Hedeby", false)]
		[InlineData("", false)]
		[InlineData(null, false)]
		public void TellsGenericNames(string name, bool expected)
		{
			Assert.Equal(expected, VillageNames.IsGeneric(name));
		}

		/// <summary>
		/// Checks that a god's, a boss's, a trader's or a legendary weapon's name counts as a legend's in any case, while
		/// a village's or a place's doesn't
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="expected">Whether it's a legend's</param>
		[Theory]
		[InlineData("Odin", true)]
		[InlineData("odin", true)]
		[InlineData(" Thor ", true)]
		[InlineData("Fenrir", true)]
		[InlineData("Eikthyr", true)]
		[InlineData("Yagluth", true)]
		[InlineData("Haldor", true)]
		[InlineData("Dyrnwyn", true)]
		[InlineData("Odinsby", false)]
		[InlineData("Hedeby", false)]
		[InlineData("Valhalla", false)]
		[InlineData("", false)]
		public void TellsLegendsNames(string name, bool expected)
		{
			Assert.Equal(expected, VikingNames.IsLegend(name));
		}
	}
}
