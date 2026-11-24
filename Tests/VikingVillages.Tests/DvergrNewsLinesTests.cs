using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the pools dvergr vikings tell news from - their own for every kind of event, and the living vikings' for
	/// the Mistlands' news
	/// </summary>
	public class DvergrNewsLinesTests
	{
		/// <summary>
		/// Makes an event of a kind and subject
		/// </summary>
		/// <param name="kind">Its kind</param>
		/// <param name="subject">Its subject</param>
		/// <returns>The event</returns>
		private static VikingNews.Event Event(string kind, string subject)
		{
			VikingNews.Event news = new VikingNews.Event();
			news.Kind = kind;
			news.Subject = subject;
			news.Remaining = 4;
			return(news);
		}

		/// <summary>
		/// Checks that every kind of event goes to a dvergr viking's pool that's there
		/// </summary>
		[Theory]
		[InlineData(VikingNews.Boss, "Eikthyr", "dvergr/boss")]
		[InlineData(VikingNews.Miniboss, "Brenna", "dvergr/miniboss")]
		[InlineData(VikingNews.Trader, "Haldor", "dvergr/trader")]
		[InlineData(VikingNews.Dungeon, "TrollCave", "dvergr/dungeon")]
		[InlineData(VikingNews.Biome, "Plains", "dvergr/biome")]
		[InlineData(VikingNews.Tame, "Wolf", "dvergr/tame")]
		[InlineData(VikingNews.Skill, "Swords", "dvergr/skill")]
		[InlineData(VikingNews.AllSkills, "", "dvergr/allskills")]
		[InlineData(VikingNews.Achievement, "ach_grindtrees", "dvergr/achievement")]
		[InlineData(VikingNews.Leviathan, "", "dvergr/leviathan")]
		[InlineData(VikingNews.Tree, "Troll", "dvergr/tree")]
		[InlineData(VikingNews.Serpent, "Serpent", "dvergr/serpent")]
		[InlineData(VikingNews.Edge, "", "dvergr/edge")]
		[InlineData(VikingNews.Death, "any", "dvergr/death/any")]
		[InlineData(VikingNews.Death, "player", "dvergr/death/player")]
		[InlineData(VikingNews.Death, "dvergr", "dvergr/death/dvergr")]
		[InlineData(VikingNews.Death, "hit/Fall", "dvergr/death/hit")]
		[InlineData(VikingNews.Death, "boss/Eikthyr", "dvergr/death/boss")]
		[InlineData(VikingNews.Death, "miniboss/Brenna", "dvergr/death/boss")]
		[InlineData(VikingNews.Death, "troll", "dvergr/death/creature")]
		[InlineData(VikingNews.Death, "creature", "dvergr/death/creature")]
		public void EveryKindHasADvergrPool(string kind, string subject, string expected)
		{
			string key = VikingNewsLines.DvergrPoolKey(Event(kind, subject));
			Assert.Equal(expected, key);
			Assert.True(VikingNewsLines.Pools().ContainsKey(key), "Missing pool " + key);
		}

		/// <summary>
		/// Checks that the Mistlands' news is told with the living vikings' lines
		/// </summary>
		[Theory]
		[InlineData(VikingNews.Biome, "Mistlands", "biome/Mistlands")]
		[InlineData(VikingNews.Boss, "SeekerQueen", "boss/SeekerQueen")]
		[InlineData(VikingNews.Death, "seeker", "death/seeker")]
		[InlineData(VikingNews.Death, "tick", "death/tick")]
		[InlineData(VikingNews.Death, "gjall", "death/gjall")]
		[InlineData(VikingNews.Death, "boss/SeekerQueen", "death/boss/SeekerQueen")]
		public void SharesTheMistlandsLines(string kind, string subject, string expected)
		{
			Assert.Equal(expected, VikingNewsLines.DvergrPoolKey(Event(kind, subject)));
		}

		/// <summary>
		/// Checks that a dvergr viking picks its own line, with its own impressed sentence after one about a boss beyond
		/// the Mistlands
		/// </summary>
		[Fact]
		public void ChoosesItsOwnLines()
		{
			VikingNews.Event news = Event(VikingNews.Boss, "Fader");
			string[] pool = VikingNewsLines.Pools()["dvergr/boss"];
			string[] after = VikingNewsLines.Pools()["dvergr/" + VikingNewsLines.ImpressedBoss];
			string said = VikingNewsLines.Choose(news, 5, "Astrid", "Fader", (int count) => 0, out int line, true);
			Assert.Equal(pool[0].GetStableHashCode(), line);
			Assert.Equal(VikingNewsLines.Fill(pool[0] + " " + after[0], "Astrid", "Fader"), said);
		}

		/// <summary>
		/// Checks that no living viking's pool is one a dvergr viking tells news from, but for the Mistlands' shared
		/// ones, so the two kinds' words stay apart
		/// </summary>
		[Fact]
		public void KeepsItsWordsApart()
		{
			List<string> own = new List<string>();
			foreach (string key in VikingNewsLines.Pools().Keys)
			{
				if (key.StartsWith(VikingNewsLines.DvergrPrefix, System.StringComparison.Ordinal))
				{
					own.Add(key);
				}
			}
			Assert.True(own.Count > 0);
			foreach (string shared in VikingNewsLines.SharedWithDvergr)
			{
				Assert.True(VikingNewsLines.Pools().ContainsKey(shared), "Missing shared pool " + shared);
				Assert.False(shared.StartsWith(VikingNewsLines.DvergrPrefix, System.StringComparison.Ordinal));
			}
		}
	}
}
