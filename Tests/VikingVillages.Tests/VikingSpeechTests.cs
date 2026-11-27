using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a talk's lines are filled in, and that the lines vikings say - about anything, the weather and a
	/// player's troubles - fill in without breaking, whatever they're picked for
	/// </summary>
	public class VikingSpeechTests
	{
		/// <summary>
		/// The lines read by string.Format, by their table in VikingSpeech, with how many values they're given
		/// </summary>
		private static readonly Dictionary<string, int> FormatTables = new Dictionary<string, int>
		{
			{ "ProudLines", 2 },
			{ "RaidAlarmLines", 0 },
			{ "RaidCallLines", 0 },
			{ "AlarmLines", 1 },
			{ "SpotLines", 1 },
			{ "FoundKnownLines", 1 },
			{ "DayGreetings", 1 },
			{ "NightGreetings", 1 },
			{ "NamedLines", 1 },
			{ "RenamedLines", 1 },
			{ "CreatureRefusals", 1 },
			{ "LegendRefusals", 1 },
			{ "PlaceRefusals", 1 },
			{ "SomeoneRefusals", 1 },
			{ "MarkupRefusals", 1 },
			{ "VillagePlaceRefusals", 1 },
			{ "VillageLegendRefusals", 1 },
			{ "VillageGenericRefusals", 1 },
			{ "VillageNamedLines", 1 },
			{ "VillageRenamedLines", 1 },
			{ "DayFirstGreetings", 0 },
			{ "NightFirstGreetings", 0 },
			{ "Introductions", 1 },
			{ "DayChat", 1 },
			{ "NightChat", 1 },
			{ "LeavingLines", 1 },
			{ "TurnBackLines", 1 },
			{ "PowerLines", 2 },
			{ "PowerUseLines", 1 }
		};

		/// <summary>
		/// The lines said as they are, by their table in VikingSpeech, with the tokens they're filled in at - the night
		/// watch's waking line through Address, the lines naming the dead by Replace, and the lines teaching a power by
		/// Replace and Address
		/// </summary>
		private static readonly Dictionary<string, string[]> PlainTables = new Dictionary<string, string[]>
		{
			{ "WakeLines", new[] { "{name}" } },
			{ "PowerTeachLines", new[] { "{0}", "{name}" } },
			{ "WokenLines", new string[0] },
			{ "HeardLines", new string[0] },
			{ "FoundStrangerLines", new string[0] },
			{ "NothingLines", new string[0] },
			{ "TakeWatchLines", new string[0] },
			{ "HelpLines", new string[0] },
			{ "NamelessRefusals", new string[0] },
			{ "VillageNamelessRefusals", new string[0] },
			{ "SleepyChat", new string[0] },
			{ "BusyChat", new string[0] },
			{ "SwimmingChat", new string[0] },
			{ "MournLines", new[] { "{fallen}" } },
			{ "NamelessMournLines", new string[0] },
			{ "RisingFarewells", new[] { "{fallen}" } },
			{ "NamelessRisingFarewells", new string[0] }
		};

		/// <summary>
		/// The string arrays in VikingSpeech that aren't lines
		/// </summary>
		private static readonly string[] NotLines = { "BuildMaterials" };

		/// <summary>
		/// The tokens a talk's lines are filled in at (see VikingSpeech.Address)
		/// </summary>
		private static readonly string[] TalkTokens = { "{name}", "{other}", "{item}", "{fallen}", "{village}" };

		/// <summary>
		/// Finds a token's braces in a line - "{" and "}" and what's between them
		/// </summary>
		private static readonly Regex Token = new Regex("\\{[^{}]*\\}");

		/// <summary>
		/// Reads one of VikingSpeech's private tables
		/// </summary>
		/// <typeparam name="T">The table's type</typeparam>
		/// <param name="name">The table's field name</param>
		/// <returns>The table</returns>
		private static T Table<T>(string name)
		{
			FieldInfo field = typeof(VikingSpeech).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
			Assert.True(field != null, "VikingSpeech has no table " + name);
			return((T)field.GetValue(null));
		}

		/// <summary>
		/// Lists VikingSpeech's private tables of a type, by their field names
		/// </summary>
		/// <typeparam name="T">The tables' type</typeparam>
		/// <returns>The tables</returns>
		private static Dictionary<string, T> Tables<T>()
		{
			Dictionary<string, T> tables = new Dictionary<string, T>();
			foreach (FieldInfo field in typeof(VikingSpeech).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
			{
				if (field.FieldType == typeof(T))
				{
					tables[field.Name] = (T)field.GetValue(null);
				}
			}
			return(tables);
		}

		/// <summary>
		/// Lists every talk vikings have with each other - VikingSpeech's and the weather's
		/// </summary>
		/// <returns>The talks</returns>
		private static List<string[]> AllTalks()
		{
			List<string[]> talks = new List<string[]>();
			foreach (string[][] table in Tables<string[][]>().Values)
			{
				talks.AddRange(table);
			}
			foreach (Dictionary<VikingWeatherSpeech.Kind, string[][]> weather in new[] { VikingWeatherSpeech.Talks, VikingWeatherSpeech.BeardTalks, VikingWeatherSpeech.ZombieTalks })
			{
				foreach (string[][] table in weather.Values)
				{
					talks.AddRange(table);
				}
			}
			return(talks);
		}

		/// <summary>
		/// Checks that every token in some lines is one they're filled in at
		/// </summary>
		/// <param name="where">Where the lines are, for the message</param>
		/// <param name="lines">The lines</param>
		/// <param name="allowed">The tokens they're filled in at</param>
		private static void AssertTokens(string where, IEnumerable<string> lines, IEnumerable<string> allowed)
		{
			HashSet<string> known = new HashSet<string>(allowed);
			foreach (string line in lines)
			{
				foreach (Match token in Token.Matches(line))
				{
					Assert.True(known.Contains(token.Value), where + " has " + token.Value + " in \"" + line + "\"");
				}
			}
		}

		/// <summary>
		/// Checks that a talk's line with no one to speak to drops the name and the comma beside it
		/// </summary>
		[Fact]
		public void NoListenerDropsTheNameCleanly()
		{
			Assert.Equal("Wake up. Your watch.", VikingSpeech.Address("Wake up, {name}. Your watch.", "", "", "", "", ""));
			Assert.Equal("Up you get - the watch is yours.", VikingSpeech.Address("Up you get, {name} - the watch is yours.", "", "", "", "", ""));
			Assert.Equal("Hang the hides under the eaves, or they'll rot.", VikingSpeech.Address("Hang the hides under the eaves, {name}, or they'll rot.", "", "", "", "", ""));
		}

		/// <summary>
		/// Checks that a line starting with the name drops it and starts with a capital letter, and a line with no one
		/// to speak to starts with one whatever's filled in first
		/// </summary>
		[Fact]
		public void NoListenerCapitalizesTheStart()
		{
			Assert.Equal("Keep the fire high.", VikingSpeech.Address("{name}, keep the fire high.", "", "", "", "", ""));
			Assert.Equal("Astrid loved Ashvale.", VikingSpeech.Address("{fallen} loved {village}.", "", "", "", "astrid", "Ashvale"));
			Assert.Equal("astrid loved Ashvale.", VikingSpeech.Address("{fallen} loved {village}.", "Bo", "", "", "astrid", "Ashvale"));
		}

		/// <summary>
		/// Checks that every token is filled in, and an empty line stays empty
		/// </summary>
		[Fact]
		public void FillsEveryToken()
		{
			Assert.Equal("Bo Asta wood Gorm Ashvale", VikingSpeech.Address("{name} {other} {item} {fallen} {village}", "Bo", "Asta", "wood", "Gorm", "Ashvale"));
			Assert.Equal("Aye, Bo. Keep the bonfire high.", VikingSpeech.Address("Aye, {name}. Keep the bonfire high.", "Bo", "", "", "", ""));
			Assert.Equal("", VikingSpeech.Address("", "", "", "", "", ""));
			Assert.Equal("", VikingSpeech.Address("", "Bo", "Asta", "wood", "Gorm", "Ashvale"));
		}

		/// <summary>
		/// Checks that every line teaching a power names the power and the viking taught, the way PowerTaught fills it
		/// in, leaving no token, and drops the name cleanly for a viking with none
		/// </summary>
		[Fact]
		public void TeachingLinesNameThePower()
		{
			foreach (string line in Table<string[]>("PowerTeachLines"))
			{
				string named = VikingSpeech.Address(line.Replace("{0}", "Eikthyr"), "Astrid", "", "", "", "");
				Assert.Contains("Eikthyr", named);
				Assert.Contains("Astrid", named);
				Assert.False(named.Contains("{") || named.Contains("}"), named);
				string nameless = VikingSpeech.Address(line.Replace("{0}", "Eikthyr"), "", "", "", "", "");
				Assert.Contains("Eikthyr", nameless);
				Assert.False(nameless.Contains("{") || nameless.Contains("}") || nameless.Contains(" ,") || nameless.StartsWith(","), nameless);
				Assert.True(char.IsUpper(nameless[0]), nameless);
			}
		}

		/// <summary>
		/// Checks that a name that reads like a token is said as it was given, not filled in again
		/// </summary>
		[Fact]
		public void NamesLikeTokensAreSaidAsGiven()
		{
			Assert.Equal("The workbench is free, {item}. Don't use up all the wood this time.", VikingSpeech.Address("The workbench is free, {name}. Don't use up all the {item} this time.", "{item}", "", "wood", "", ""));
			Assert.Equal("I keep thinking about {name}.", VikingSpeech.Address("I keep thinking about {fallen}.", "Bo", "", "", "{name}", ""));
			Assert.Equal("Astrid loved {fallen}.", VikingSpeech.Address("{fallen} loved {village}.", "", "", "", "Astrid", "{fallen}"));
			Assert.Equal("Have you seen {village} today?", VikingSpeech.Address("Have you seen {other} today?", "", "{village}", "", "", "Ashvale"));
		}

		/// <summary>
		/// Checks that a village's name is filled in so string.Format shows any braces in it as they are
		/// </summary>
		[Fact]
		public void VillageNameKeepsItsBracesThroughFormat()
		{
			Assert.Equal("To arms! A raid on Ash{0}vale!", string.Format(VikingSpeech.WithVillage("To arms! A raid on {village}!", "Ash{0}vale")));
			Assert.Equal("Trolls in {x}! Everyone up!", string.Format(VikingSpeech.WithVillage("{0} in {village}! Everyone up!", "{x}"), "Trolls"));
			Assert.Equal("Wake up! {0} at the village!", VikingSpeech.WithVillage("Wake up! {0} at the village!", "Ashvale"));
		}

		/// <summary>
		/// Checks that every string array in VikingSpeech is known to these tests, so a new table of lines is checked
		/// like the rest
		/// </summary>
		[Fact]
		public void EveryTableIsKnown()
		{
			foreach (string name in Tables<string[]>().Keys)
			{
				Assert.True(FormatTables.ContainsKey(name) || PlainTables.ContainsKey(name) || NotLines.Contains(name), "VikingSpeech." + name + " isn't listed in these tests");
			}
			foreach (string name in FormatTables.Keys.Concat(PlainTables.Keys))
			{
				Assert.NotEmpty(Table<string[]>(name));
			}
		}

		/// <summary>
		/// Checks that every line read by string.Format formats with the values it's given and the village's name,
		/// leaving no token unfilled
		/// </summary>
		[Fact]
		public void FormatLinesFormat()
		{
			object[] values = { "Bjorn", 7 };
			foreach (KeyValuePair<string, int> table in FormatTables)
			{
				foreach (string line in Table<string[]>(table.Key))
				{
					string said = string.Format(VikingSpeech.WithVillage(line, "Ashvale"), values.Take(table.Value).ToArray());
					Assert.False(said.Contains("{") || said.Contains("}"), table.Key + " leaves a token in \"" + said + "\"");
				}
			}
		}

		/// <summary>
		/// Checks that the lines said as they are use only the tokens they're filled in at - never string.Format's, or
		/// the village's, which is only filled in for string.Format
		/// </summary>
		[Fact]
		public void PlainLinesUseOnlyTheirTokens()
		{
			foreach (KeyValuePair<string, string[]> table in PlainTables)
			{
				AssertTokens(table.Key, Table<string[]>(table.Key), table.Value);
			}
		}

		/// <summary>
		/// Checks that the lines naming the dead always name them, and those for the nameless never try to
		/// </summary>
		[Fact]
		public void MourningLinesNameTheFallen()
		{
			Assert.All(Table<string[]>("MournLines"), (string line) => Assert.Contains("{fallen}", line));
			Assert.All(Table<string[]>("RisingFarewells"), (string line) => Assert.Contains("{fallen}", line));
		}

		/// <summary>
		/// Checks that every table has a line that doesn't name the village, which a village with no name falls back on
		/// </summary>
		[Fact]
		public void EveryTableWorksWithoutAVillageName()
		{
			foreach (string name in FormatTables.Keys.Concat(PlainTables.Keys))
			{
				Assert.True(System.Array.Exists(Table<string[]>(name), (string line) => !line.Contains("{village}")), name + " has only lines naming the village");
			}
		}

		/// <summary>
		/// Checks that the talks use only the tokens Address fills in, never string.Format's, and each has a line
		/// </summary>
		[Fact]
		public void TalksUseOnlyTalkTokens()
		{
			foreach (KeyValuePair<string, string[][]> table in Tables<string[][]>())
			{
				Assert.NotEmpty(table.Value);
				foreach (string[] talk in table.Value)
				{
					Assert.NotEmpty(talk);
					AssertTokens(table.Key, talk, TalkTokens);
				}
			}
		}

		/// <summary>
		/// Checks that only the talks about another viking name one, and they all do - and the same for the talks about
		/// one of the dead and about the village
		/// </summary>
		[Fact]
		public void TalksNameWhatTheyreAbout()
		{
			foreach (KeyValuePair<string, string[][]> table in Tables<string[][]>())
			{
				foreach (string[] talk in table.Value)
				{
					bool other = talk.Any((string line) => line.Contains("{other}"));
					bool fallen = talk.Any((string line) => line.Contains("{fallen}"));
					Assert.True(other == (table.Key == "OtherTalks"), table.Key + ": \"" + talk[0] + "\"");
					Assert.True(fallen == (table.Key == "FallenTalks"), table.Key + ": \"" + talk[0] + "\"");
				}
			}
			Assert.All(Table<string[][]>("VillageTalks"), (string[] talk) => Assert.Contains(talk, (string line) => line.Contains("{village}")));
		}

		/// <summary>
		/// Checks that every talk's lines, and the night watch's waking line, read cleanly with no one to speak to -
		/// the name gone with its comma, no doubled or stray spaces, starting with a capital letter
		/// </summary>
		[Fact]
		public void TalksReadCleanlyWithoutAListener()
		{
			List<string> lines = AllTalks().SelectMany((string[] talk) => talk).ToList();
			lines.AddRange(Table<string[]>("WakeLines"));
			foreach (string line in lines)
			{
				string said = VikingSpeech.Address(line, "", "Asta", "wood", "Gorm", "Ashvale");
				Assert.False(said.Contains("{") || said.Contains("  ") || said.StartsWith(" "), "\"" + line + "\" reads \"" + said + "\"");
				Assert.False(said.Contains(" .") || said.Contains(" ,") || said.Contains(" ?") || said.Contains(" !") || said.Contains(",."), "\"" + line + "\" reads \"" + said + "\"");
				Assert.False(char.IsLower(said[0]), "\"" + line + "\" reads \"" + said + "\"");
			}
		}

		/// <summary>
		/// Checks that every line about the dead walking, left out unless it's true of the world, is one the vikings say
		/// - a line in a table, or the first line of a talk, which is what a talk is left out by
		/// </summary>
		[Fact]
		public void EveryZombieLineIsSaid()
		{
			HashSet<string> said = new HashSet<string>();
			foreach (string[] table in Tables<string[]>().Values)
			{
				said.UnionWith(table);
			}
			foreach (string[][] table in Tables<string[][]>().Values)
			{
				said.UnionWith(table.Select((string[] talk) => talk[0]));
			}
			foreach (string line in Table<Dictionary<string, System.Func<bool>>>("ZombieLines").Keys)
			{
				Assert.True(said.Contains(line), "\"" + line + "\" is in ZombieLines but isn't said");
			}
		}

		/// <summary>
		/// Checks that without zombies, in a village with no name and no materials, there's still a talk the vikings can
		/// have, so picking one again until it fits always ends
		/// </summary>
		[Fact]
		public void NoZombiesStillLeavesATalk()
		{
			Dictionary<string, System.Func<bool>> zombieLines = Table<Dictionary<string, System.Func<bool>>>("ZombieLines");
			Assert.Contains(Table<string[][]>("Talks"), (string[] talk) => !zombieLines.ContainsKey(talk[0]) && !talk.Any((string line) => line.Contains("{item}") || line.Contains("{village}")));
		}

		/// <summary>
		/// Checks that every kind of weather has talks and lines for a player, and that a player's lines format with
		/// their name, leaving no token unfilled
		/// </summary>
		[Fact]
		public void EveryWeatherHasLinesThatFormat()
		{
			foreach (VikingWeatherSpeech.Kind kind in System.Enum.GetValues(typeof(VikingWeatherSpeech.Kind)))
			{
				Assert.True(VikingWeatherSpeech.Talks.ContainsKey(kind) && VikingWeatherSpeech.Talks[kind].Length > 0, "No talks about " + kind);
				Assert.True(VikingWeatherSpeech.PlayerLines.ContainsKey(kind) && VikingWeatherSpeech.PlayerLines[kind].Length > 0, "No lines for a player about " + kind);
				foreach (string line in VikingWeatherSpeech.PlayerLines[kind])
				{
					string said = string.Format(line, "Bjorn");
					Assert.False(said.Contains("{") || said.Contains("}"), kind + " leaves a token in \"" + said + "\"");
				}
			}
		}

		/// <summary>
		/// Checks that the weather's talks name the viking spoken to with {name}, which Address fills in, rather than
		/// string.Format's {0}, and use no other token
		/// </summary>
		[Fact]
		public void WeatherTalksUseTheNameToken()
		{
			foreach (Dictionary<VikingWeatherSpeech.Kind, string[][]> weather in new[] { VikingWeatherSpeech.Talks, VikingWeatherSpeech.BeardTalks, VikingWeatherSpeech.ZombieTalks })
			{
				foreach (KeyValuePair<VikingWeatherSpeech.Kind, string[][]> entry in weather)
				{
					foreach (string[] talk in entry.Value)
					{
						Assert.NotEmpty(talk);
						AssertTokens(entry.Key.ToString(), talk, new[] { "{name}" });
					}
				}
			}
		}

		/// <summary>
		/// Checks that every trouble a viking calls out about a player has lines, a way to tell it and a name of its
		/// own, and that its lines format with the player's name or what a viking calls a stranger
		/// </summary>
		[Fact]
		public void EveryConcernHasLinesThatFormat()
		{
			HashSet<string> names = new HashSet<string>();
			foreach (VikingConcernSpeech.Concern concern in VikingConcernSpeech.Concerns)
			{
				Assert.True(names.Add(concern.Name), "Two concerns are called " + concern.Name);
				Assert.NotNull(concern.Has);
				Assert.NotEmpty(concern.Lines);
				foreach (string line in concern.EveryLine)
				{
					string known = VikingConcernSpeech.Said(line, "Bjorn", true, "swordplay");
					Assert.False(known.Contains("{") || known.Contains("}"), concern.Name + " leaves a token in \"" + known + "\"");
					Assert.Contains("Bjorn", known);
					string stranger = VikingConcernSpeech.Said(line, "Bjorn", false, "swordplay");
					Assert.False(stranger.Contains("{") || stranger.Contains("}") || stranger.Contains("Bjorn"), concern.Name + " reads \"" + stranger + "\"");
					Assert.Contains(VikingSpeech.Stranger, stranger, System.StringComparison.OrdinalIgnoreCase);
				}
			}
		}

		/// <summary>
		/// Checks that a stranger is spoken to as "traveler" and spoken about as "a traveler", with a capital letter at
		/// the start of a sentence, leaving the line's other tokens for string.Format
		/// </summary>
		[Fact]
		public void StrangerIsFilledInBySentence()
		{
			Assert.Equal("Traveler, you're on fire! Get to the water!", VikingSpeech.WithStranger("{0}, you're on fire! Get to the water!", 0));
			Assert.Equal("Fire! Traveler, you're burning!", VikingSpeech.WithStranger("Fire! {0}, you're burning!", 0));
			Assert.Equal("Traveler! You're covered in blood.", VikingSpeech.WithStranger("{0}! You're covered in blood.", 0));
			Assert.Equal("You look green, traveler. Poisoned?", VikingSpeech.WithStranger("You look green, {0}. Poisoned?", 0));
			Assert.Equal("A traveler shares {0}'s blessing! Skål!", VikingSpeech.WithStranger("{1} shares {0}'s blessing! Skål!", 1));
			Assert.Equal("Thank you, traveler - {0} smiles on us all.", VikingSpeech.WithStranger("Thank you, {1} - {0} smiles on us all.", 1));
			Assert.Equal("{0}'s gift! I can feel it, traveler!", VikingSpeech.WithStranger("{0}'s gift! I can feel it, {1}!", 1));
			Assert.Equal("By {0}! I could wrestle a troll.", VikingSpeech.WithStranger("By {0}! I could wrestle a troll.", 1));
			Assert.Equal("Traveler! Is that you, traveler?", VikingSpeech.WithStranger("{0}! Is that you, {0}?", 0));
		}

		/// <summary>
		/// Checks that every line thanking a stranger for a power, a living or dvergr viking's, and every concern called
		/// out to one, starts each sentence with a capital letter
		/// </summary>
		[Fact]
		public void StrangerLinesStartSentencesWithCapitals()
		{
			Regex lowerStart = new Regex("(^|[.!?]\\s+)\\p{Ll}");
			FieldInfo dvergr = typeof(DvergrSpeech).GetField("PowerLines", BindingFlags.NonPublic | BindingFlags.Static);
			List<string> said = new List<string>();
			foreach (string line in Table<string[]>("PowerLines").Concat((string[])dvergr.GetValue(null)))
			{
				said.Add(string.Format(VikingSpeech.WithStranger(line, 1), "Moder", "Bjorn"));
			}
			foreach (VikingConcernSpeech.Concern concern in VikingConcernSpeech.Concerns)
			{
				said.AddRange(concern.EveryLine.Select((string line) => VikingConcernSpeech.Said(line, "Bjorn", false, "swordplay")));
			}
			foreach (string line in said)
			{
				Assert.False(lowerStart.IsMatch(line), "\"" + line + "\" starts a sentence with a small letter");
				Assert.DoesNotContain("Bjorn", line);
			}
		}
	}
}
