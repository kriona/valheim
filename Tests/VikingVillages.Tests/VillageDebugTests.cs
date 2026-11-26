using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests which parts of a village's debug summary show as headers
	/// </summary>
	public class VillageDebugTests
	{
		/// <summary>
		/// Checks that each line's label up to its colon is a header, indented or not
		/// </summary>
		[Fact]
		public void GraysEachLinesHeader()
		{
			string text = "Pieces: 120 noted, 3 missing\nGoal: Bronze at quality 2 - smelting\n  Where they are: at work";
			string expected = "<color=#8C8C8C>Pieces:</color> 120 noted, 3 missing\n<color=#8C8C8C>Goal:</color> Bronze at quality 2 - smelting\n  <color=#8C8C8C>Where they are:</color> at work";
			Assert.Equal(expected, VillageDebug.GrayHeaders(text));
		}

		/// <summary>
		/// Checks that a line with no label at its start is left as it is - one whose first colon comes after a comma, one
		/// with none, and one that starts with a count
		/// </summary>
		[Fact]
		public void LeavesLinesWithNoHeaderAlone()
		{
			string text = "Village at (100, 30, -200), 40m around all of it - 3 zones\nEnemies within 60m\n  3 vikings on the road, due in 6m 32s";
			Assert.Equal(text, VillageDebug.GrayHeaders(text));
		}

		/// <summary>
		/// Checks that a colon in the middle of a word, like a time, doesn't make a header
		/// </summary>
		[Fact]
		public void NeedsASpaceOrTheLineEndAfterTheColon()
		{
			Assert.Equal("Due at 12:30", VillageDebug.GrayHeaders("Due at 12:30"));
			Assert.Equal("<color=#8C8C8C>Setup:</color>", VillageDebug.GrayHeaders("Setup:"));
		}

		/// <summary>
		/// Checks that kinds are counted most first, ties by name
		/// </summary>
		[Fact]
		public void CountsKindsMostFirst()
		{
			List<string> kinds = new List<string> { "Leech", "ZombieViking", "Draugr", "ZombieViking", "Abomination", "ZombieViking" };
			Assert.Equal("ZombieViking 3, Abomination 1, Draugr 1, Leech 1", VillageDebug.KindCounts(kinds));
		}

		/// <summary>
		/// Checks that no kinds count as nothing
		/// </summary>
		[Fact]
		public void CountsNoKindsAsNothing()
		{
			Assert.Equal("", VillageDebug.KindCounts(new List<string>()));
		}
	}
}
