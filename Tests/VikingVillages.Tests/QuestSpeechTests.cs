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
				Assert.True(QuestSpeech.GoingTalks.ContainsKey(kind), kind + " has no talks by those going before it sets out");
				Assert.True(QuestSpeech.FellowTalks.ContainsKey(kind), kind + " has no talks between those going before it sets out");
				Assert.True(QuestSpeech.SeeOffTalks.ContainsKey(kind), kind + " has no talks seeing those going off before it sets out");
				Assert.True(QuestSpeech.StayingTalks.ContainsKey(kind), kind + " has no talks between those staying before it sets out");
				Assert.True(QuestSpeech.GoingAnswers.ContainsKey(kind), kind + " has no answers by those going before it sets out");
				Assert.True(QuestSpeech.StayingAnswers.ContainsKey(kind), kind + " has no answers by those staying before it sets out");
			}
		}

		[Fact]
		public void TalksBeforeSettingOutFitWhoIsGoing()
		{
			List<string> none = new List<string>();
			List<string> two = new List<string> { "Ragna", "Ulf" };
			for (int roll = 0; roll < 8; roll++)
			{
				string[] goingAlone = QuestSpeech.SoonTalk(QuestKind.MineCopper, none, true, false, roll);
				Assert.NotNull(goingAlone);
				Assert.DoesNotContain(goingAlone, (string line) => line.Contains(QuestSpeech.WithToken));
				Assert.DoesNotContain(QuestSpeech.SoonTalk(QuestKind.MineCopper, two, true, false, roll), (string line) => line.Contains(QuestSpeech.WithToken));
				string[] fellow = QuestSpeech.SoonTalk(QuestKind.MineCopper, two, true, true, roll);
				Assert.DoesNotContain(fellow, (string line) => line.Contains("Ragna"));
				string[] seeOff = QuestSpeech.SoonTalk(QuestKind.MineCopper, two, false, true, roll);
				Assert.DoesNotContain(seeOff, (string line) => line.Contains("Ragna"));
				string[] staying = QuestSpeech.SoonTalk(QuestKind.MineCopper, two, false, false, roll);
				Assert.Contains(staying, (string line) => line.Contains("Ragna and Ulf"));
				Assert.Null(QuestSpeech.SoonTalk(QuestKind.MineCopper, none, false, false, roll));
			}
		}

		[Fact]
		public void TalksBeforeSettingOutIncludeTheQuestsOwn()
		{
			string[] own = QuestSpeech.GoingTalks[QuestKind.KillEikthyr][0];
			int count = QuestSpeech.Candidates(QuestSpeech.GoingTalks, QuestKind.KillEikthyr, true, QuestSpeech.WithToken).Length;
			bool found = false;
			for (int roll = 0; roll < count; roll++)
			{
				string[] talk = QuestSpeech.SoonTalk(QuestKind.KillEikthyr, new List<string> { "Ragna" }, true, false, roll);
				found = found || talk[0] == own[0].Replace(QuestSpeech.WithToken, "Ragna");
			}
			Assert.True(found);
		}

		[Fact]
		public void AnswersBeforeSettingOutFitWhoIsGoing()
		{
			for (int roll = 0; roll < 4; roll++)
			{
				string alone = QuestSpeech.SoonAnswer(QuestKind.FindBees, new List<string>(), true, "Kriona", roll);
				Assert.NotNull(alone);
				Assert.DoesNotContain("{", alone);
				Assert.EndsWith("Kriona.", alone);
				Assert.Null(QuestSpeech.SoonAnswer(QuestKind.FindBees, new List<string>(), false, "Kriona", roll));
				Assert.Contains("Ragna and Ulf", QuestSpeech.SoonAnswer(QuestKind.FindBees, new List<string> { "Ragna", "Ulf" }, false, "Kriona", roll));
			}
			Assert.Equal("I'm off to find wild bees in the morning, Kriona.", QuestSpeech.SoonAnswer(QuestKind.FindBees, new List<string>(), true, "Kriona", 0));
			Assert.Equal("I'm taking our treasure to Hildir in the morning, Kriona.", QuestSpeech.NameTrader(QuestSpeech.SoonAnswer(QuestKind.SellTreasure, new List<string>(), true, "Kriona", 0), "Hildir"));
		}

		[Fact]
		public void LinesBeforeSettingOutOnlyUseTokensThatAreFilledIn()
		{
			List<string> lines = new List<string>();
			foreach (Dictionary<QuestKind, string[]> answers in new[] { QuestSpeech.GoingAnswers, QuestSpeech.StayingAnswers })
			{
				foreach (string[] some in answers.Values)
				{
					lines.AddRange(some);
				}
			}
			foreach (Dictionary<QuestKind, string[][]> talks in new[] { QuestSpeech.GoingTalks, QuestSpeech.FellowTalks, QuestSpeech.SeeOffTalks, QuestSpeech.StayingTalks })
			{
				foreach (string[][] some in talks.Values)
				{
					foreach (string[] talk in some)
					{
						lines.AddRange(talk);
					}
				}
			}
			foreach (string line in lines)
			{
				foreach (Match token in Regex.Matches(line, "\\{[a-z]+\\}"))
				{
					Assert.Contains(token.Value, new[] { "{away}", "{with}", "{player}", "{name}", "{trader}" });
				}
			}
		}

		[Fact]
		public void LinesBetweenOrToThoseGoingDontNameThoseGoing()
		{
			foreach (Dictionary<QuestKind, string[][]> talks in new[] { QuestSpeech.FellowTalks, QuestSpeech.SeeOffTalks })
			{
				foreach (string[][] some in talks.Values)
				{
					foreach (string[] talk in some)
					{
						Assert.DoesNotContain(talk, (string line) => line.Contains(QuestSpeech.AwayToken) || line.Contains(QuestSpeech.WithToken));
					}
				}
			}
		}

		[Fact]
		public void LinesOnlyUseTokensThatAreFilledIn()
		{
			List<string> lines = new List<string>(QuestSpeech.DepartureLines);
			lines.AddRange(QuestSpeech.AloneLines);
			lines.AddRange(QuestSpeech.MournLines);
			lines.AddRange(QuestSpeech.CompanionMournLines);
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

		[Fact]
		public void MourningACompanionNamesThemAndStartsWithACapital()
		{
			for (int roll = 0; roll < QuestSpeech.CompanionMournLines.Length; roll++)
			{
				string line = QuestSpeech.MournCompanion("bjorn", roll);
				Assert.Contains("bjorn", line.ToLowerInvariant());
				Assert.True(char.IsUpper(line[0]));
			}
		}
	}
}
