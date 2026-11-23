using Kriona.Shared;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how a "Prefab = inherit/true/false ## Name" line's value is written, and the name its entry shows in
	/// Configuration Manager
	/// </summary>
	public class StateEntriesTests
	{
		/// <summary>
		/// Checks that no value is written as inherit, and true and false as themselves
		/// </summary>
		[Fact]
		public void FormatsTheValue()
		{
			Assert.Equal("inherit", StateEntries.Format(null));
			Assert.Equal("true", StateEntries.Format(true));
			Assert.Equal("false", StateEntries.Format(false));
		}

		/// <summary>
		/// Checks that an entry shows its prefab name alone when the name in game is blank or the same in any case, and
		/// the name in game followed by the prefab name otherwise, trimmed
		/// </summary>
		/// <param name="key">The prefab name</param>
		/// <param name="displayName">The name in game</param>
		/// <param name="expected">The name the entry shows</param>
		[Theory]
		[InlineData("Boar", "Boar", "Boar")]
		[InlineData("Boar", "boar", "Boar")]
		[InlineData("Boar", "  BOAR ", "Boar")]
		[InlineData("Boar", "", "Boar")]
		[InlineData("Boar", "   ", "Boar")]
		[InlineData("Greydwarf_Elite", "Greydwarf brute", "Greydwarf brute (Greydwarf_Elite)")]
		[InlineData("Troll", " Troll king ", "Troll king (Troll)")]
		public void NamesTheEntry(string key, string displayName, string expected)
		{
			Assert.Equal(expected, StateEntries.GetEntryName(key, displayName));
		}
	}
}
