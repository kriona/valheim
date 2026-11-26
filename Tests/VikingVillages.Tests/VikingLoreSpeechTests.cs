using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what vikings say about the runestones of their biome and of someone with their name, and to a player about
	/// how they're doing and being old friends - plain text, filling in only the player's name
	/// </summary>
	public class VikingLoreSpeechTests
	{
		/// <summary>
		/// Checks that every biome a village or a dvergr viking stands in has runestone lines, and the sea has none
		/// </summary>
		[Fact]
		public void EveryLandBiomeHasLines()
		{
			Heightmap.Biome[] biomes = { Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain, Heightmap.Biome.Plains, Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth };
			foreach (Heightmap.Biome biome in biomes)
			{
				Assert.True(VikingLoreSpeech.Lines.TryGetValue(biome, out string[] lines) && lines.Length >= 5, biome + " has fewer than 5 lines");
				Assert.NotNull(VikingLoreSpeech.For(biome, "Astrid", "", (int count) => count - 1));
			}
			Assert.Null(VikingLoreSpeech.For(Heightmap.Biome.Ocean, "Astrid", "", (int count) => 0));
		}

		/// <summary>
		/// Checks that a viking talking about the runestones of its biome leaves out the lines that name it, Bjørn
		/// included as the lines' Bjorn, and only those
		/// </summary>
		[Fact]
		public void BiomeLinesLeaveOutTheSpeaker()
		{
			string[] lines = VikingLoreSpeech.Lines[Heightmap.Biome.Mountain];
			HashSet<string> said = new HashSet<string>();
			for (int i = 0; i < lines.Length; i++)
			{
				int index = i;
				said.Add(VikingLoreSpeech.For(Heightmap.Biome.Mountain, "Astrid", "Bjørn the Red", (int count) => System.Math.Min(index, count - 1)));
			}
			Assert.DoesNotContain(said, (string line) => line.Contains("Bjorn"));
			Assert.Equal(lines.Length - 1, said.Count);
		}

		/// <summary>
		/// Checks that every name on the runestones vikings talk about having is a name a new viking can be given - but
		/// Bjorn, the runestones' other spelling of Bjørn - with at least 3 lines
		/// </summary>
		[Fact]
		public void NamesakesAreGivenNames()
		{
			List<string> given = new List<string>(VikingNames.MaleNames);
			given.AddRange(VikingNames.FemaleNames);
			foreach (KeyValuePair<string, string[]> namesake in VikingLoreSpeech.NamesakeLines)
			{
				Assert.True(namesake.Value.Length >= 3, namesake.Key + " has fewer than 3 lines");
				Assert.True(namesake.Key == "Bjorn" || given.Contains(namesake.Key), namesake.Key + " isn't a name vikings are given");
			}
		}

		/// <summary>
		/// Checks that a viking talks about the runestones of someone with its name by its first name, whatever its case
		/// and byname, and not about those of a name it only starts with
		/// </summary>
		/// <param name="viking">The viking's name</param>
		/// <param name="expected">True when it has lines</param>
		[Theory]
		[InlineData("Ulf", true)]
		[InlineData("ulf the Red", true)]
		[InlineData("  Bjørn Bloodaxe ", true)]
		[InlineData("Bjorn", true)]
		[InlineData("Ulfric", false)]
		[InlineData("Rolf", false)]
		[InlineData("", false)]
		[InlineData(null, false)]
		public void NamesakesGoByFirstName(string viking, bool expected)
		{
			Assert.Equal(expected, VikingLoreSpeech.Namesake(viking, "Astrid", (int count) => 0) != null);
		}

		/// <summary>
		/// Checks that every name with Frozen lines is a name a new viking can be given, with at least 3 lines, each
		/// naming the player
		/// </summary>
		[Fact]
		public void FrozenNamesAreGivenNames()
		{
			List<string> given = new List<string>(VikingNames.MaleNames);
			given.AddRange(VikingNames.FemaleNames);
			foreach (KeyValuePair<string, string[]> named in VikingFrozenSpeech.Lines)
			{
				Assert.True(named.Value.Length >= 3, named.Key + " has fewer than 3 lines");
				Assert.True(given.Contains(named.Key), named.Key + " isn't a name vikings are given");
				foreach (string line in named.Value)
				{
					Assert.Contains("{0}", line);
				}
			}
		}

		/// <summary>
		/// Checks that a viking says its Frozen lines by its first name, whatever its case and byname, and not for a name
		/// it only starts with
		/// </summary>
		/// <param name="viking">The viking's name</param>
		/// <param name="expected">True when it has lines</param>
		[Theory]
		[InlineData("Elsa", true)]
		[InlineData("anna the Wise", true)]
		[InlineData(" Kristoff Bloodaxe", true)]
		[InlineData("Olaf", true)]
		[InlineData("Hans the Red", true)]
		[InlineData("Iduna", false)]
		[InlineData("Annabel", false)]
		[InlineData("", false)]
		[InlineData(null, false)]
		public void FrozenLinesGoByFirstName(string viking, bool expected)
		{
			Assert.Equal(expected, VikingFrozenSpeech.For(viking, "Astrid", (int count) => 0) != null);
		}

		/// <summary>
		/// Checks that a line names someone only as a word of its own, whatever its case, with an "ø" in the name read
		/// as an "o"
		/// </summary>
		/// <param name="line">The line</param>
		/// <param name="name">The name</param>
		/// <param name="expected">True when the line names them</param>
		[Theory]
		[InlineData("A beard like Bjorn's.", "Bjorn", true)]
		[InlineData("A beard like Bjorn's.", "Bjørn", true)]
		[InlineData("Then ulf left.", "Ulf", true)]
		[InlineData("Ulfric left.", "Ulf", false)]
		[InlineData("The Ulfs left.", "Ulf", false)]
		public void NamesWholeWords(string line, string name, bool expected)
		{
			Assert.Equal(expected, VikingLoreSpeech.Names(line, name));
		}

		/// <summary>
		/// Checks that the boar stone of the Meadows is among the runestones talked about
		/// </summary>
		[Fact]
		public void MeadowsTalkAboutTheBoarStone()
		{
			Assert.Contains(VikingLoreSpeech.Lines[Heightmap.Biome.Meadows], (string line) => line.Contains("boar stone"));
		}

		/// <summary>
		/// Checks that every runestone, namesake, friend and asking line is plain text, fills in only the player's name,
		/// and formats without throwing
		/// </summary>
		[Fact]
		public void LinesAreCleanAndFormat()
		{
			List<string> lines = new List<string>();
			foreach (string[] biomeLines in VikingLoreSpeech.Lines.Values)
			{
				lines.AddRange(biomeLines);
			}
			foreach (string[] namesakeLines in VikingLoreSpeech.NamesakeLines.Values)
			{
				lines.AddRange(namesakeLines);
			}
			foreach (string[] frozenLines in VikingFrozenSpeech.Lines.Values)
			{
				lines.AddRange(frozenLines);
			}
			lines.AddRange(VikingFriendSpeech.FriendGreetings);
			lines.AddRange(VikingFriendSpeech.FriendChat);
			lines.AddRange(VikingFriendSpeech.AskChat);
			Regex token = new Regex("\\{([^}]*)\\}");
			foreach (string line in lines)
			{
				foreach (char c in line)
				{
					Assert.True(c < 128, "A line has a character that isn't plain text: " + line);
				}
				foreach (Match match in token.Matches(line))
				{
					Assert.True(match.Groups[1].Value == "0", "A line fills in " + match.Value + ": " + line);
				}
				string formatted = string.Format(line, "Astrid");
				Assert.DoesNotContain("{", formatted);
			}
		}

		/// <summary>
		/// Checks that every friend and asking line names the player, since they're said to them
		/// </summary>
		[Fact]
		public void FriendLinesNameThePlayer()
		{
			List<string> lines = new List<string>(VikingFriendSpeech.FriendGreetings);
			lines.AddRange(VikingFriendSpeech.FriendChat);
			lines.AddRange(VikingFriendSpeech.AskChat);
			foreach (string line in lines)
			{
				Assert.Contains("{0}", line);
			}
		}
	}
}
