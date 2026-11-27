using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what a traveler says and what the village says about it - how lines are filled in, which fit, and that
	/// every line holds only tokens that are filled in
	/// </summary>
	public class TravelerSpeechTests
	{
		/// <summary>
		/// The tokens a traveler's lines can hold - its own, and "{name}" in talks, which VikingSpeech.Address fills in
		/// </summary>
		private static readonly HashSet<string> KnownTokens = new HashSet<string> { "{from}", "{to}", "{fromfolk}", "{tofolk}", "{home}", "{traveler}", "{village}", "{player}", "{name}" };

		/// <summary>
		/// Makes the values a traveler's lines are filled in with, everything given
		/// </summary>
		/// <returns>The values</returns>
		private static Dictionary<string, string> AllValues()
		{
			return(TravelerSpeech.Values("Kari", "Ashby", "Brekka", "Dalby", "Eir", "Folki", "the Mountains", "Hrafn"));
		}

		/// <summary>
		/// Lists every line and talk line
		/// </summary>
		/// <returns>The lines</returns>
		private static List<string> EveryLine()
		{
			List<string> lines = new List<string>();
			lines.AddRange(TravelerSpeech.DayFirstGreetings);
			lines.AddRange(TravelerSpeech.NightFirstGreetings);
			lines.AddRange(TravelerSpeech.DayGreetings);
			lines.AddRange(TravelerSpeech.NightGreetings);
			lines.AddRange(TravelerSpeech.DayAnswers);
			lines.AddRange(TravelerSpeech.NightAnswers);
			lines.AddRange(TravelerSpeech.VisitingAnswers);
			lines.AddRange(TravelerSpeech.VisitedAnswers);
			lines.AddRange(TravelerSpeech.TradeLines);
			lines.AddRange(TravelerSpeech.FarewellLines);
			foreach (string[][] talks in new[] { TravelerSpeech.TravelerTalks, TravelerSpeech.VillagerTalks, TravelerSpeech.VisitingTalks, TravelerSpeech.VisitedTalks })
			{
				foreach (string[] talk in talks)
				{
					lines.AddRange(talk);
				}
			}
			return(lines);
		}

		/// <summary>
		/// Checks that every line holds only tokens that get filled in
		/// </summary>
		[Fact]
		public void EveryLineHoldsOnlyKnownTokens()
		{
			foreach (string line in EveryLine())
			{
				foreach (Match token in Regex.Matches(line, "\\{[^}]*\\}"))
				{
					Assert.Contains(token.Value, KnownTokens);
				}
			}
		}

		/// <summary>
		/// Checks that no line has a viking rowing, since every boat in the game sails
		/// </summary>
		[Fact]
		public void NoLineRows()
		{
			foreach (string line in EveryLine())
			{
				Assert.DoesNotContain("row", line.ToLowerInvariant().Replace("arrow", "").Replace("brow", "").Replace("throw", "").Replace("grow", "").Replace("tomorrow", ""));
				Assert.DoesNotContain("oar", line.ToLowerInvariant());
			}
		}

		/// <summary>
		/// Checks that every talk has at least two lines, so each viking speaks
		/// </summary>
		[Fact]
		public void EveryTalkHasTwoSpeakers()
		{
			foreach (string[][] talks in new[] { TravelerSpeech.TravelerTalks, TravelerSpeech.VillagerTalks, TravelerSpeech.VisitingTalks, TravelerSpeech.VisitedTalks })
			{
				foreach (string[] talk in talks)
				{
					Assert.True(talk.Length >= 2);
				}
			}
		}

		/// <summary>
		/// Checks that a line fits only when each of its tokens has something to fill it in, leaving out tokens that aren't
		/// a traveler's
		/// </summary>
		[Fact]
		public void FitsNeedsEveryToken()
		{
			Dictionary<string, string> values = TravelerSpeech.Values("Kari", "", "Brekka", "", "", "", "", "");
			Assert.True(TravelerSpeech.Fits("I came from {from}, {player}.", values));
			Assert.False(TravelerSpeech.Fits("I'm bound for {to}.", values));
			Assert.False(TravelerSpeech.Fits("Welcome to {village}.", values));
			Assert.True(TravelerSpeech.Fits("Hail, {name}.", values));
			Assert.True(TravelerSpeech.Fits("Hail!", values));
		}

		/// <summary>
		/// Checks that a line is filled in once, so a name that reads like a token is said as given, and "{name}" is left
		/// for the talk to fill in
		/// </summary>
		[Fact]
		public void FillFillsInOnePass()
		{
			Dictionary<string, string> values = TravelerSpeech.Values("{to}", "", "Brekka", "Dalby", "", "", "", "");
			Assert.Equal("Hail {to}, from Brekka to Dalby, {name}.", TravelerSpeech.Fill("Hail {player}, from {from} to {to}, {name}.", values));
		}

		/// <summary>
		/// Checks that picking leaves out the lines and talks that don't fit, and gives null when none does
		/// </summary>
		[Fact]
		public void PickLeavesOutWhatDoesNotFit()
		{
			Dictionary<string, string> values = TravelerSpeech.Values("", "", "Brekka", "", "", "", "", "");
			Assert.Equal(new List<string> { "From Brekka." }, TravelerSpeech.FittingLines(new[] { "To {to}.", "From {from}." }, values));
			Assert.Empty(TravelerSpeech.FittingLines(new[] { "To {to}." }, values));
			List<string[]> talks = TravelerSpeech.Fitting(new[] { new[] { "From {from}.", "To {to}." }, new[] { "From {from}, {name}.", "Far." } }, values);
			Assert.Single(talks);
			Assert.Equal(new[] { "From Brekka, {name}.", "Far." }, talks[0]);
		}

		/// <summary>
		/// Checks that with everything given, every line and talk fits and is filled in
		/// </summary>
		[Fact]
		public void EverythingFitsWithAllValues()
		{
			Dictionary<string, string> values = AllValues();
			foreach (string line in EveryLine())
			{
				Assert.True(TravelerSpeech.Fits(line, values));
				Assert.DoesNotMatch("\\{(?:from|to|fromfolk|tofolk|home|traveler|village|player)\\}", TravelerSpeech.Fill(line, values));
			}
		}

		/// <summary>
		/// Checks that a traveler always has something to say with no names at all - a greeting, an answer by day and by
		/// night, a talk each way, a trade line and a farewell
		/// </summary>
		[Fact]
		public void SomethingFitsWithNoNames()
		{
			Dictionary<string, string> values = TravelerSpeech.Values("", "", "", "", "", "", "", "");
			Assert.NotEmpty(TravelerSpeech.FittingLines(TravelerSpeech.DayFirstGreetings, values));
			Assert.NotEmpty(TravelerSpeech.FittingLines(TravelerSpeech.NightFirstGreetings, values));
			Assert.NotEmpty(TravelerSpeech.FittingLines(TravelerSpeech.TradeLines, values));
			Assert.NotEmpty(TravelerSpeech.FittingLines(TravelerSpeech.FarewellLines, values));
			Assert.NotEmpty(TravelerSpeech.FittingLines(TravelerSpeech.DayAnswers, values));
			Assert.NotEmpty(TravelerSpeech.FittingLines(TravelerSpeech.NightAnswers, values));
			Assert.NotEmpty(TravelerSpeech.Fitting(TravelerSpeech.TravelerTalks, values));
			Assert.NotEmpty(TravelerSpeech.Fitting(TravelerSpeech.VillagerTalks, values));
		}
	}
}
