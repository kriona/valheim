using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what the vikings say about their village's quests - how names are joined and filled in, and that every quest
	/// worth talking about has something said about it
	/// </summary>
	public class QuestSpeechTests
	{
		[Fact]
		public void NamesAreJoinedTheWayTheyreSaid()
		{
			Assert.Equal("", QuestSpeech.JoinNames(new List<string>()));
			Assert.Equal("Ragna", QuestSpeech.JoinNames(new List<string> { "Ragna" }));
			Assert.Equal("Ragna and Ulf", QuestSpeech.JoinNames(new List<string> { "Ragna", "Ulf" }));
			Assert.Equal("Ragna, Ulf and Sigrid", QuestSpeech.JoinNames(new List<string> { "Ragna", "", "Ulf", "Sigrid" }));
		}

		[Fact]
		public void FillingTakesTokensInOnePass()
		{
			Assert.Equal("{player} and Ulf, {name}", QuestSpeech.Fill("{away}, {name}", "", "{player} and Ulf", "", "Kriona"));
			Assert.Equal("Ragna went. Back soon, Kriona.", QuestSpeech.Fill("{away} went. Back {when}, {player}.", "soon", "Ragna", "", "Kriona"));
		}

		[Fact]
		public void LinesNamingOthersAreLeftOutWithNoneToName()
		{
			string[][] named = QuestSpeech.Candidates(QuestSpeech.BackTalks, QuestKind.RaidCrypts, true, QuestSpeech.WithToken);
			string[][] alone = QuestSpeech.Candidates(QuestSpeech.BackTalks, QuestKind.RaidCrypts, false, QuestSpeech.WithToken);
			Assert.True(alone.Length < named.Length);
			Assert.DoesNotContain(alone, (string[] talk) => System.Array.Exists(talk, (string line) => line.Contains(QuestSpeech.WithToken)));
			Assert.DoesNotContain(QuestSpeech.Lines(QuestSpeech.BackAnswers, QuestKind.RaidCrypts, false, QuestSpeech.WithToken), (string line) => line.Contains(QuestSpeech.WithToken));
		}

		[Fact]
		public void TalksNameThoseAwayAndTheOthersWhoWent()
		{
			for (int roll = 0; roll < 8; roll++)
			{
				string[] away = QuestSpeech.AwayTalk(QuestKind.HuntTrolls, new List<string> { "Ragna", "Ulf" }, "tomorrow", roll);
				Assert.Contains(away, (string line) => line.Contains("Ragna and Ulf"));
				Assert.Null(QuestSpeech.AwayTalk(QuestKind.HuntTrolls, new List<string>(), "tomorrow", roll));
				string[] back = QuestSpeech.BackTalk(QuestKind.KillEikthyr, new List<string>(), roll);
				Assert.NotNull(back);
				Assert.DoesNotContain(back, (string line) => line.Contains(QuestSpeech.WithToken));
			}
		}

		[Fact]
		public void EveryQuestWorthTalkingAboutHasItsOwnLines()
		{
			foreach (QuestKind kind in System.Enum.GetValues(typeof(QuestKind)))
			{
				if (!QuestRules.WorthTalkingAbout(kind))
				{
					continue;
				}
				Assert.True(QuestSpeech.AwayTalks.ContainsKey(kind), kind + " has no talks while it's out");
				Assert.True(QuestSpeech.AwayAnswers.ContainsKey(kind), kind + " has no answers while it's out");
				Assert.True(QuestSpeech.BackTalks.ContainsKey(kind), kind + " has no talks once it's back");
				Assert.True(QuestSpeech.BackAnswers.ContainsKey(kind), kind + " has no answers once it's back");
			}
		}

		[Fact]
		public void LinesOnlyUseTokensThatAreFilledIn()
		{
			List<string> lines = new List<string>(QuestSpeech.DepartureLines);
			lines.AddRange(QuestSpeech.AloneLines);
			lines.AddRange(QuestSpeech.MournLines);
			foreach (string[] answers in QuestSpeech.AwayAnswers.Values)
			{
				lines.AddRange(answers);
			}
			foreach (string[] answers in QuestSpeech.BackAnswers.Values)
			{
				lines.AddRange(answers);
			}
			foreach (string[][] talks in QuestSpeech.AwayTalks.Values)
			{
				foreach (string[] talk in talks)
				{
					lines.AddRange(talk);
				}
			}
			foreach (string[][] talks in QuestSpeech.BackTalks.Values)
			{
				foreach (string[] talk in talks)
				{
					lines.AddRange(talk);
				}
			}
			foreach (string line in lines)
			{
				foreach (Match token in Regex.Matches(line, "\\{[a-z]+\\}"))
				{
					Assert.Contains(token.Value, new[] { "{when}", "{away}", "{with}", "{player}", "{fallen}", "{name}", "{trader}" });
				}
			}
		}

		[Fact]
		public void MourningNamesTheDead()
		{
			for (int roll = 0; roll < QuestSpeech.MournLines.Length; roll++)
			{
				Assert.Contains("Bjorn", QuestSpeech.Mourn("Bjorn", roll));
			}
		}
	}
}
