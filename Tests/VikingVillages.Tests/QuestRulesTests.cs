using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests when quests set out, how many vikings go, how long they're gone, how a quest is noted and what the vikings
	/// say as they go
	/// </summary>
	public class QuestRulesTests
	{
		[Theory]
		[InlineData(0.1f, false)]
		[InlineData(0.23f, false)]
		[InlineData(0.24f, true)]
		[InlineData(0.5f, true)]
		[InlineData(0.59f, true)]
		[InlineData(0.6f, false)]
		[InlineData(0.8f, false)]
		public void QuestsSetOutBetweenWakingAndLateAfternoon(float dayFraction, bool expected)
		{
			Assert.Equal(expected, QuestRules.CanSetOut(dayFraction));
		}

		[Fact]
		public void DurationIsThereAndBackAndTheWork()
		{
			Assert.Equal(1000f, QuestRules.Duration(300f, 400f, 0f), 3);
			Assert.Equal(1020f, QuestRules.Duration(300f, 400f, 1f), 3);
			Assert.Equal(980f, QuestRules.Duration(300f, 400f, -1f), 3);
			Assert.Equal(1020f, QuestRules.Duration(300f, 400f, 5f), 3);
			Assert.Equal(400f, QuestRules.Duration(-10f, 400f, 0f), 3);
		}

		/// <summary>
		/// Makes a rule like mining copper's - two vikings, up to four with more pickaxes
		/// </summary>
		/// <returns>The rule</returns>
		private static QuestRule Mining()
		{
			return(new QuestRule { Kind = QuestKind.MineCopper, Vikings = 2, PlainsVikings = 2, MaxVikings = 4 });
		}

		[Theory]
		[InlineData(0, 10, 10, 2)]
		[InlineData(3, 10, 10, 3)]
		[InlineData(9, 10, 10, 4)]
		[InlineData(9, 3, 10, 3)]
		[InlineData(9, 10, 5, 3)]
		[InlineData(0, 10, 4, 2)]
		[InlineData(0, 10, 3, 0)]
		[InlineData(0, 1, 10, 0)]
		public void GroupSizeLeavesTwoAtHome(int tools, int available, int village, int expected)
		{
			Assert.Equal(expected, QuestRules.GroupSize(Mining(), ResourceBook.BlackForest, tools, available, village));
		}

		[Fact]
		public void HuntingFromThePlainsTakesAGroup()
		{
			QuestRule hunt = new QuestRule { Kind = QuestKind.Hunt, Vikings = 1, PlainsVikings = 3, MaxVikings = 3 };
			Assert.Equal(1, QuestRules.GroupSize(hunt, ResourceBook.Mountain, 0, 10, 10));
			Assert.Equal(3, QuestRules.GroupSize(hunt, ResourceBook.Plains, 0, 10, 10));
			Assert.Equal(0, QuestRules.GroupSize(hunt, ResourceBook.Plains, 0, 10, 4));
		}

		[Fact]
		public void OnlyRemarkableQuestsAreTalkedAbout()
		{
			Assert.False(QuestRules.WorthTalkingAbout(QuestKind.Hunt));
			Assert.False(QuestRules.WorthTalkingAbout(QuestKind.Forage));
			Assert.False(QuestRules.WorthTalkingAbout(QuestKind.ChopTrees));
			Assert.False(QuestRules.WorthTalkingAbout(QuestKind.None));
			Assert.True(QuestRules.WorthTalkingAbout(QuestKind.RaidCrypts));
			Assert.True(QuestRules.WorthTalkingAbout(QuestKind.KillEikthyr));
		}

		[Fact]
		public void EveryQuestTakesSomeTime()
		{
			foreach (QuestKind kind in System.Enum.GetValues(typeof(QuestKind)))
			{
				Assert.True(QuestRules.ActiveSeconds(kind) > 0f);
			}
		}

		/// <summary>
		/// Makes a quest with every value set
		/// </summary>
		/// <returns>The quest</returns>
		private static QuestRecord FullRecord()
		{
			QuestRecord record = new QuestRecord();
			record.Id = "q1";
			record.Kind = QuestKind.MineTin;
			record.Item = "TinOre";
			record.Phase = QuestPhase.Away;
			record.Members.AddRange(new[] { "a", "b" });
			record.Names.AddRange(new[] { "Ragna", "" });
			record.Tools.AddRange(new[] { "PickaxeAntler", "" });
			record.SetOutAt = 1000L;
			record.Due = 2000L;
			record.Exit = new Vector3(1f, 2f, 3f);
			record.Trapped = true;
			return(record);
		}

		[Fact]
		public void QuestSurvivesBeingWrittenAndRead()
		{
			QuestRecord record = FullRecord();
			QuestRecord read = QuestRecord.Decode(record.Encode());
			Assert.Equal(record.Id, read.Id);
			Assert.Equal(record.Kind, read.Kind);
			Assert.Equal(record.Item, read.Item);
			Assert.Equal(record.Phase, read.Phase);
			Assert.Equal(record.Members, read.Members);
			Assert.Equal(record.Names, read.Names);
			Assert.Equal(record.Tools, read.Tools);
			Assert.Equal(record.SetOutAt, read.SetOutAt);
			Assert.Equal(record.Due, read.Due);
			Assert.Equal(record.Exit, read.Exit);
			Assert.True(read.Trapped);
		}

		[Fact]
		public void NoQuestReadsAsNone()
		{
			Assert.Null(QuestRecord.Decode(null));
			Assert.Null(QuestRecord.Decode(new byte[0]));
			Assert.Null(QuestRecord.Decode(new byte[] { 9, 9 }));
		}

		[Fact]
		public void EachVikingHasItsOwnTool()
		{
			QuestRecord record = FullRecord();
			Assert.Equal("PickaxeAntler", record.ToolOf("a"));
			Assert.Equal("", record.ToolOf("b"));
			Assert.Equal("", record.ToolOf("c"));
		}

		[Fact]
		public void OthersLeavesOutTheSpeakerAndTheNameless()
		{
			QuestRecord record = FullRecord();
			record.Names.Add("Bjorn");
			record.Members.Add("c");
			Assert.Equal(new List<string> { "Bjorn" }, record.Others("Ragna"));
			Assert.Equal(new List<string> { "Ragna", "Bjorn" }, record.Others("Ulf"));
		}

		[Fact]
		public void DepartureSaysWhenTheyreBack()
		{
			Assert.Equal("Back tomorrow.", QuestSpeech.Fill("Back {when}.", "tomorrow"));
			foreach (string line in QuestSpeech.DepartureLines)
			{
				Assert.Contains(QuestSpeech.WhenToken, line);
			}
		}
	}
}
