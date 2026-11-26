using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the lines vikings say away from any village and as they leave a disbanded one
	/// </summary>
	public class VikingRoadSpeechTests
	{
		/// <summary>
		/// The tables of lines naming a village, each of which names it in every line, and the tables for when it has no
		/// name, which never do
		/// </summary>
		private static readonly string[] NamedTables = { "TravelLines", "TravelAnswers", "DisbandedLines", "LetsGoLines", "FoundWayLines", "LandHoLines" };
		private static readonly string[] UnnamedTables = { "UnnamedTravelLines", "UnnamedTravelAnswers", "UnnamedDisbandedLines", "UnnamedLetsGoLines", "UnnamedFoundWayLines", "NowhereLines", "UnnamedLandHoLines" };

		/// <summary>
		/// Finds a token's braces in a line
		/// </summary>
		private static readonly Regex Token = new Regex("\\{[^{}]*\\}");

		/// <summary>
		/// Lists every table of lines in VikingRoadSpeech, by its field name
		/// </summary>
		/// <returns>The tables</returns>
		private static Dictionary<string, string[]> Tables()
		{
			Dictionary<string, string[]> tables = new Dictionary<string, string[]>();
			foreach (FieldInfo field in typeof(VikingRoadSpeech).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
			{
				if (field.FieldType == typeof(string[]))
				{
					tables[field.Name] = (string[])field.GetValue(null);
				}
			}
			return(tables);
		}

		[Fact]
		public void EveryLineFormatsWithThePlayersName()
		{
			foreach (KeyValuePair<string, string[]> table in Tables())
			{
				Assert.NotEmpty(table.Value);
				foreach (string line in table.Value)
				{
					string filled = string.Format(VikingSpeech.WithVillage(line, "Eikvik"), "Ragna");
					Assert.False(Token.IsMatch(filled), table.Key + " leaves a token in: " + line);
				}
			}
		}

		[Fact]
		public void NamedTablesNameTheVillageInEveryLine()
		{
			Dictionary<string, string[]> tables = Tables();
			foreach (string name in NamedTables)
			{
				Assert.All(tables[name], (string line) => Assert.Contains("{village}", line));
			}
			foreach (string name in UnnamedTables)
			{
				Assert.All(tables[name], (string line) => Assert.DoesNotContain("{village}", line));
			}
		}

		[Fact]
		public void TheSpecsLinesAreAmongThem()
		{
			Dictionary<string, string[]> tables = Tables();
			Assert.Contains("Where am I?", tables["ConfusedLines"]);
			Assert.Contains("How did I get here?", tables["ConfusedLines"]);
			Assert.Contains("What's going on?", tables["ConfusedLines"]);
			Assert.Contains("Am I in Valhalla?", tables["ConfusedLines"]);
			Assert.Contains("I need to get to {village}.", tables["TravelLines"]);
			Assert.Contains("I need to get to a village.", tables["UnnamedTravelLines"]);
			Assert.Contains("What now?", tables["WhatNowLines"]);
			Assert.Contains("Where should we go?", tables["WhatNowLines"]);
			Assert.Contains("Let's go to {village}.", tables["LetsGoLines"]);
			Assert.Contains("Let's find another village.", tables["UnnamedLetsGoLines"]);
		}

		[Theory]
		[InlineData(1, "now", "1 Viking is coming back now")]
		[InlineData(3, "now", "3 Vikings are coming back now")]
		[InlineData(1, "tomorrow", "1 Viking is on the way back - expect them tomorrow")]
		[InlineData(4, "in a couple of hours", "4 Vikings are on the way back - expect them in a couple of hours")]
		public void RecalledMessageSaysHowManyAndWhen(int count, string when, string expected)
		{
			Assert.Equal(expected, VillageTravel.RecalledMessage(count, when));
		}
	}
}
