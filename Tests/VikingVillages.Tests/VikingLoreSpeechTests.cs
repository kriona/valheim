using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what vikings say about the runestones of their biome and to a player about how they're doing and being old
	/// friends - plain text, filling in only the player's name
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
				Assert.NotNull(VikingLoreSpeech.For(biome, "Astrid", (int count) => count - 1));
			}
			Assert.Null(VikingLoreSpeech.For(Heightmap.Biome.Ocean, "Astrid", (int count) => 0));
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
		/// Checks that every runestone, friend and asking line is plain text, fills in only the player's name, and
		/// formats without throwing
		/// </summary>
		[Fact]
		public void LinesAreCleanAndFormat()
		{
			List<string> lines = new List<string>();
			foreach (string[] biomeLines in VikingLoreSpeech.Lines.Values)
			{
				lines.AddRange(biomeLines);
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
