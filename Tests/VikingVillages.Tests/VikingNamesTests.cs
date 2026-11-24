using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how names with text markup are told apart and shown, how a name given is kept and judged, and how a player
	/// is noted in the world
	/// </summary>
	public class VikingNamesTests
	{
		/// <summary>
		/// Checks that a name with a rich text tag counts as markup, and one with only angle brackets or none doesn't
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="expected">Whether it's markup</param>
		[Theory]
		[InlineData("<color=red>", true)]
		[InlineData("<color=red>Bjorn", true)]
		[InlineData("Bjorn</color>", true)]
		[InlineData("<b>Bjorn</b>", true)]
		[InlineData("Bj<i>orn", true)]
		[InlineData("<>", true)]
		[InlineData("Bjorn", false)]
		[InlineData("Bjorn Ironside", false)]
		[InlineData("<3 Bjorn", false)]
		[InlineData("Bjorn >:(", false)]
		[InlineData("", false)]
		[InlineData(null, false)]
		public void TellsMarkupFromNames(string name, bool expected)
		{
			Assert.Equal(expected, VikingNames.IsMarkup(name));
		}

		/// <summary>
		/// Checks that a name is wrapped to show its markup as typed, with any noparse tags of its own taken out so they
		/// can't end the wrapping early
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="expected">The wrapped name</param>
		[Theory]
		[InlineData("<color=red>Bjorn", "<noparse><color=red>Bjorn</noparse>")]
		[InlineData("Bjorn", "<noparse>Bjorn</noparse>")]
		[InlineData("</noparse><color=red>", "<noparse><color=red></noparse>")]
		[InlineData("<NoParse>x</NOPARSE>", "<noparse>x</noparse>")]
		[InlineData(null, "<noparse></noparse>")]
		public void MakesMarkupLiteral(string name, string expected)
		{
			Assert.Equal(expected, VikingNames.Literal(name));
		}

		/// <summary>
		/// Checks that the global key noting a player is their name's UTF-8 bytes in hex after the prefix, so a name in
		/// any language makes a single word
		/// </summary>
		/// <param name="lower">The player's name, in lowercase</param>
		/// <param name="expected">The key</param>
		[Theory]
		[InlineData("ab", "kriona_za_player_6162")]
		[InlineData("bo 1", "kriona_za_player_626f2031")]
		[InlineData("å", "kriona_za_player_c3a5")]
		[InlineData("åke", "kriona_za_player_c3a56b65")]
		[InlineData("", "kriona_za_player_")]
		public void PlayerKeyIsTheNameInHex(string lower, string expected)
		{
			Assert.Equal(expected, VikingNames.PlayerKey(lower));
		}

		/// <summary>
		/// Checks that a name of up to MaxLength characters is kept as it is, and a longer one cut to it, without the
		/// space the cut leaves at its end
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="expected">The name kept</param>
		[Theory]
		[InlineData("Alfric Stormbreaker", "Alfric Stormbreaker")]
		[InlineData("abcdefghijklmnopqrstuvwx", "abcdefghijklmnopqrstuvwx")]
		[InlineData("abcdefghijklmnopqrstuvwxy", "abcdefghijklmnopqrstuvwx")]
		[InlineData("Alfric Stormbreaker the Red", "Alfric Stormbreaker the")]
		[InlineData("", "")]
		public void ClipsToMaxLength(string name, string expected)
		{
			Assert.Equal(expected, VikingNames.Clip(name));
		}

		/// <summary>
		/// Checks that a cut that would split a character written as two, like an emoji, leaves the whole character out,
		/// and one that fits is kept
		/// </summary>
		[Fact]
		public void ClipKeepsCharactersWhole()
		{
			string emoji = "😀";
			string fits = new string('a', 22) + emoji;
			Assert.Equal(24, fits.Length);
			Assert.Equal(fits, VikingNames.Clip(fits));
			string split = new string('a', 23) + emoji;
			Assert.Equal(new string('a', 23), VikingNames.Clip(split));
			string after = new string('a', 22) + emoji + "b";
			Assert.Equal(fits, VikingNames.Clip(after));
		}

		/// <summary>
		/// Checks that the name kept has no "$" and is cut to MaxLength, and that a name of MaxLength characters stays
		/// whole while one more is cut
		/// </summary>
		[Fact]
		public void KeptIsPlainAndClipped()
		{
			Assert.Equal("Rolf", VikingNames.Kept("  $Rolf$ "));
			Assert.Equal(VikingNames.MaxLength, VikingNames.Kept(new string('r', 24)).Length);
			Assert.Equal(VikingNames.MaxLength, VikingNames.Kept(new string('r', 25)).Length);
		}

		/// <summary>
		/// Checks what a viking makes of a name from the name alone - markup, "Viking" or nothing, a legend's, a place's,
		/// the zombie word's and the rest, whatever their case
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="expected">What it makes of it, by the verdict's name, since the verdict isn't public</param>
		[Theory]
		[InlineData("<b>Bo</b>", "Markup")]
		[InlineData("", "Cleared")]
		[InlineData("   ", "Cleared")]
		[InlineData("Viking", "Cleared")]
		[InlineData("$VIKING", "Cleared")]
		[InlineData("ODIN", "Legend")]
		[InlineData(" bog witch ", "Legend")]
		[InlineData("Freyr", "Legend")]
		[InlineData("nidhögg", "Legend")]
		[InlineData("Sif", "Legend")]
		[InlineData("Dyrnwyn", "Legend")]
		[InlineData("Black Forest", "Place")]
		[InlineData("valheim", "Place")]
		[InlineData("Zombie", "Creature")]
		[InlineData("zombie troll", "Creature")]
		[InlineData("Zombies", "Taken")]
		[InlineData("Rolf", "Taken")]
		[InlineData("Odin's Friend", "Taken")]
		[InlineData("Vikingur", "Taken")]
		public void JudgesTheNameItself(string name, string expected)
		{
			Assert.Equal(expected, VikingNames.JudgeText(name, " Zombie ").ToString());
		}

		/// <summary>
		/// Checks that with no zombie word, a name starting with "zombie" is like any other
		/// </summary>
		[Fact]
		public void NoZombieWordTakesZombie()
		{
			Assert.Equal(VikingNames.Verdict.Taken, VikingNames.JudgeText("Zombie", ""));
			Assert.Equal(VikingNames.Verdict.Taken, VikingNames.JudgeText("Zombie Troll", "  "));
		}

		/// <summary>
		/// Checks that a name is judged as it would be kept, cut to MaxLength, so a long one is turned down for what's
		/// left of it
		/// </summary>
		[Fact]
		public void JudgesTheNameAsKept()
		{
			// "Odin", 20 spaces and an "x" - cut to "Odin" and its spaces, which leaves "Odin"
			string name = "Odin" + new string(' ', 20) + "x";
			Assert.Equal(25, name.Length);
			Assert.Equal(VikingNames.Verdict.Legend, VikingNames.JudgeText(name, "Zombie"));
			Assert.Equal(VikingNames.Verdict.Taken, VikingNames.JudgeText("Odin" + new string(' ', 19) + "x", "Zombie"));
		}

		/// <summary>
		/// Checks that a new viking's chance of a byname of its own is 20% in the Meadows, 10% more for each biome after,
		/// and 90% in the Deep North, never past 100%
		/// </summary>
		[Theory]
		[InlineData(0, 0.2f)]
		[InlineData(1, 0.3f)]
		[InlineData(2, 0.4f)]
		[InlineData(3, 0.5f)]
		[InlineData(4, 0.6f)]
		[InlineData(5, 0.7f)]
		[InlineData(6, 0.8f)]
		[InlineData(7, 0.9f)]
		[InlineData(-1, 0.2f)]
		[InlineData(20, 1f)]
		public void BynamesGrowLikelierByBiome(int biome, float expected)
		{
			Assert.Equal(expected, VikingNames.BynameChanceFor(biome), 4);
		}

		/// <summary>
		/// Checks that the Deep North is the biome at 90%, by VikingGear's order
		/// </summary>
		[Fact]
		public void DeepNorthIsNinetyPercent()
		{
			Assert.Equal(0.9f, VikingNames.BynameChanceFor(VikingGear.IndexOf(Heightmap.Biome.DeepNorth)), 4);
			Assert.Equal(0.2f, VikingNames.BynameChanceFor(VikingGear.IndexOf(Heightmap.Biome.Meadows)), 4);
		}

		/// <summary>
		/// Checks that a creature's name from the game's text is read without its rich text and spaces, in lowercase, so
		/// Hildir's quest bosses, whose names are colored, are turned down like the rest
		/// </summary>
		/// <param name="localized">The game's text</param>
		/// <param name="expected">The name it's read as</param>
		[Theory]
		[InlineData("<color=orange>Brenna</color>", "brenna")]
		[InlineData("<color=orange>Zil & Thungr</color>", "zil & thungr")]
		[InlineData(" Troll ", "troll")]
		public void LoadedNamesLoseTheirMarkup(string localized, string expected)
		{
			Assert.Equal(expected, VikingNames.LoadedName(localized));
		}

		/// <summary>
		/// Checks that the bear's names are taken though they're a creature's, while other creatures, the zombie word and
		/// a zombie bear aren't
		/// </summary>
		/// <param name="lower">The name, in lowercase</param>
		/// <param name="expected">True when it's turned down as a creature's</param>
		[Theory]
		[InlineData("troll", true)]
		[InlineData("bear", false)]
		[InlineData("bjorn", false)]
		[InlineData("bjørn", false)]
		[InlineData("zombie", true)]
		[InlineData("zombie bear", true)]
		[InlineData("rolf", false)]
		public void BearNamesAreTaken(string lower, bool expected)
		{
			HashSet<string> creatures = new HashSet<string> { "troll", "bear", "bjorn", "bjørn" };
			Assert.Equal(expected, VikingNames.IsCreatureName(lower, creatures, " Zombie "));
		}
	}
}
