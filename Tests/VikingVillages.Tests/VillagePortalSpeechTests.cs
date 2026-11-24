using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what the vikings of a village with a portal say about the village on its other side - which talks they have,
	/// which tokens the lines use, and how they read once they're filled in
	/// </summary>
	public class VillagePortalSpeechTests
	{
		/// <summary>
		/// Any token in a line
		/// </summary>
		private static readonly Regex Token = new Regex("\\{[^}]*\\}");

		/// <summary>
		/// Lists both sides' talks
		/// </summary>
		/// <returns>The talks</returns>
		private static List<string[]> AllTalks()
		{
			return(VillagePortalSpeech.EasierTalks.Concat(VillagePortalSpeech.HarderTalks).ToList());
		}

		/// <summary>
		/// Lists both sides' lines said to a player
		/// </summary>
		/// <returns>The lines</returns>
		private static List<string> AllChat()
		{
			return(VillagePortalSpeech.EasierChat.Concat(VillagePortalSpeech.HarderChat).Concat(VillagePortalSpeech.HarderBiomeChat.Values).ToList());
		}

		/// <summary>
		/// Checks that a player hears how to dress for the Ashlands or the Deep North only of the biome the village through
		/// the portal is in, and nothing of it from the gentler side
		/// </summary>
		[Fact]
		public void TellsHowToDressForTheBiome()
		{
			List<string> ashlands = VillagePortalSpeech.ChatFor(Heightmap.Biome.AshLands);
			List<string> deepNorth = VillagePortalSpeech.ChatFor(Heightmap.Biome.DeepNorth);
			Assert.Contains(ashlands, (string line) => line.EndsWith("Dress for the heat."));
			Assert.DoesNotContain(ashlands, (string line) => line.EndsWith("Dress warm."));
			Assert.Contains(deepNorth, (string line) => line.EndsWith("Dress warm."));
			Assert.DoesNotContain(deepNorth, (string line) => line.EndsWith("Dress for the heat."));
			Assert.DoesNotContain(VillagePortalSpeech.ChatFor(Heightmap.Biome.Meadows), (string line) => line.Contains("Dress"));
		}

		/// <summary>
		/// Checks that the talks use only the tokens Address fills in for them, and each names the other village
		/// </summary>
		[Fact]
		public void TalksNameTheOtherVillage()
		{
			HashSet<string> allowed = new HashSet<string> { "{name}", "{linked}", "{biome}" };
			foreach (string[] talk in AllTalks())
			{
				Assert.NotEmpty(talk);
				Assert.Contains(talk, (string line) => line.Contains("{linked}"));
				foreach (string line in talk)
				{
					foreach (Match token in Token.Matches(line))
					{
						Assert.True(allowed.Contains(token.Value), "\"" + line + "\" has " + token.Value);
					}
				}
			}
		}

		/// <summary>
		/// Checks that the lines said to a player use only the player's name, the other village and its biome, and name
		/// both
		/// </summary>
		[Fact]
		public void ChatNamesThePlayerAndTheOtherVillage()
		{
			HashSet<string> allowed = new HashSet<string> { "{0}", "{linked}", "{biome}" };
			foreach (string line in AllChat())
			{
				Assert.Contains("{0}", line);
				Assert.Contains("{linked}", line);
				foreach (Match token in Token.Matches(line))
				{
					Assert.True(allowed.Contains(token.Value), "\"" + line + "\" has " + token.Value);
				}
			}
		}

		/// <summary>
		/// Checks that no line starts with the biome, which starts with a small "the"
		/// </summary>
		[Fact]
		public void StartsNoLineWithTheBiome()
		{
			foreach (string line in AllTalks().SelectMany((string[] talk) => talk).Concat(AllChat()))
			{
				Assert.False(line.StartsWith("{biome}"), "\"" + line + "\"");
				Assert.DoesNotContain(". {biome}", line);
			}
		}

		/// <summary>
		/// Checks that every talk's lines read cleanly filled in with no one to speak to - no token left, no doubled or
		/// stray spaces, starting with a capital letter
		/// </summary>
		[Fact]
		public void TalksReadCleanlyWithoutAListener()
		{
			foreach (string line in AllTalks().SelectMany((string[] talk) => talk))
			{
				string said = VikingSpeech.Address(line, "", "", "", "", "", "Hedeby", "the Ashlands");
				Assert.False(said.Contains("{") || said.Contains("  ") || said.StartsWith(" "), "\"" + line + "\" reads \"" + said + "\"");
				Assert.False(said.Contains(" .") || said.Contains(" ,") || said.Contains(" ?") || said.Contains(" !") || said.Contains(",."), "\"" + line + "\" reads \"" + said + "\"");
				Assert.False(char.IsLower(said[0]), "\"" + line + "\" reads \"" + said + "\"");
			}
		}

		/// <summary>
		/// Checks that a talk fills in the other village and its biome, and leaves a name that reads like a token as it is
		/// </summary>
		[Fact]
		public void FillsInTheOtherVillage()
		{
			Assert.Equal("Bo, Hedeby lies in the Plains.", VikingSpeech.Address("{name}, {linked} lies in {biome}.", "Bo", "", "", "", "", "Hedeby", "the Plains"));
			Assert.Equal("{name} lies in the Plains.", VikingSpeech.Address("{linked} lies in {biome}.", "Bo", "", "", "", "", "{name}", "the Plains"));
		}

		/// <summary>
		/// Checks that a line said to a player fills in the player, the other village and its biome, with braces in the
		/// names shown as they are
		/// </summary>
		[Fact]
		public void FillsInAChatLine()
		{
			Assert.Equal("Hail Gorm, Hedeby is in the Swamp.", VillagePortalSpeech.Fill("Hail {0}, {linked} is in {biome}.", "Gorm", "Hedeby", "the Swamp"));
			Assert.Equal("Hail Gorm, {0} is in the Swamp.", VillagePortalSpeech.Fill("Hail {0}, {linked} is in {biome}.", "Gorm", "{0}", "the Swamp"));
			foreach (string line in AllChat())
			{
				string said = VillagePortalSpeech.Fill(line, "Gorm", "Hedeby", "the Deep North");
				Assert.False(said.Contains("{") || said.Contains("  "), "\"" + line + "\" reads \"" + said + "\"");
			}
		}

		/// <summary>
		/// Checks that every biome a portal leads from or to is named with "the" before it
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="expected">Its name</param>
		[Theory]
		[InlineData(Heightmap.Biome.Meadows, "the Meadows")]
		[InlineData(Heightmap.Biome.BlackForest, "the Black Forest")]
		[InlineData(Heightmap.Biome.Swamp, "the Swamp")]
		[InlineData(Heightmap.Biome.Mountain, "the Mountains")]
		[InlineData(Heightmap.Biome.Plains, "the Plains")]
		[InlineData(Heightmap.Biome.AshLands, "the Ashlands")]
		[InlineData(Heightmap.Biome.DeepNorth, "the Deep North")]
		public void NamesTheBiome(Heightmap.Biome biome, string expected)
		{
			Assert.Equal(expected, VillagePortalSpeech.BiomeName(biome));
		}

		/// <summary>
		/// Checks that a village whose portal leads to the Ashlands or the Deep North talks of the hard land, and one
		/// whose portal leads anywhere else of the gentler one
		/// </summary>
		[Fact]
		public void TalksOfTheLandOnTheOtherSide()
		{
			Assert.Same(VillagePortalSpeech.HarderTalks, VillagePortalSpeech.Talks(Heightmap.Biome.AshLands));
			Assert.Same(VillagePortalSpeech.HarderTalks, VillagePortalSpeech.Talks(Heightmap.Biome.DeepNorth));
			Assert.Same(VillagePortalSpeech.EasierTalks, VillagePortalSpeech.Talks(Heightmap.Biome.Meadows));
			Assert.Same(VillagePortalSpeech.EasierTalks, VillagePortalSpeech.Talks(Heightmap.Biome.Plains));
		}
	}
}
