using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a village's debug summary describes the vikings on their way to it and its quest - the times it counts
	/// down, what brings each group, where it is on its trip, and how the groups and the quest's vikings are listed
	/// </summary>
	public class VillageArrivalsTests
	{
		/// <summary>
		/// How many ZNet time ticks a second has
		/// </summary>
		private const long Second = System.TimeSpan.TicksPerSecond;

		/// <summary>
		/// The time now in the tests, in ZNet time ticks
		/// </summary>
		private const long Now = 1000L * Second;

		/// <summary>
		/// Makes a group on its way to a village
		/// </summary>
		/// <param name="count">How many vikings</param>
		/// <param name="questId">The quest it's out on, or ""</param>
		/// <param name="dueIn">How long until it's due, in seconds</param>
		/// <returns>The group</returns>
		private static VillageArrivals.Arrival Group(int count, string questId, long dueIn)
		{
			return(new VillageArrivals.Arrival { Count = count, QuestId = questId, What = ((questId.Length > 0) ? "out on Hunt" : "on the road"), Due = Now + dueIn * Second });
		}

		/// <summary>
		/// Makes a group out on a quest whose trip is a road there, the work, and a road back, each road 100 meters on foot
		/// </summary>
		/// <param name="work">How long the work takes, in seconds</param>
		/// <returns>The group, which set out at tick 0</returns>
		private static TravelEntry QuestGroup(float work)
		{
			TravelRoad there = new TravelRoad();
			there.Add(Vector3.zero, TravelLeg.Foot);
			there.Add(new Vector3(100f, 0f, 0f), TravelLeg.Foot);
			TravelRoad back = new TravelRoad();
			back.Add(new Vector3(100f, 0f, 0f), TravelLeg.Foot);
			back.Add(Vector3.zero, TravelLeg.Foot);
			TravelJourney journey = new TravelJourney { BoatSpeed = 6f };
			journey.AddRoad(there, false, false);
			journey.AddStay(work);
			journey.AddRoad(back, false, true);
			TravelEntry entry = new TravelEntry { QuestId = "q1", QuestKind = (int)QuestKind.MineCopper, Unloaded = 0L, Journey = journey };
			entry.Remaining = journey.TotalSeconds();
			entry.Due = (long)(entry.Remaining * 10.0);
			return(entry);
		}

		[Theory]
		[InlineData(0.0, "0s")]
		[InlineData(45.0, "45s")]
		[InlineData(392.0, "6m 32s")]
		[InlineData(59.6, "1m 0s")]
		[InlineData(3912.0, "1h 5m 12s")]
		[InlineData(-5.0, "0s")]
		public void SpanLeavesOutTheLargerPartsThatAreNone(double seconds, string expected)
		{
			Assert.Equal(expected, VillageArrivals.Span(seconds));
		}

		[Theory]
		[InlineData(392L, "in 6m 32s")]
		[InlineData(-12L, "12s ago")]
		[InlineData(0L, "now")]
		public void CountsDownToWhenSomethingIsDue(long seconds, string expected)
		{
			Assert.Equal(expected, VillageArrivals.Countdown(Now + seconds * Second, Now));
		}

		[Fact]
		public void LessThanHalfASecondAwayIsNow()
		{
			Assert.Equal("now", VillageArrivals.Countdown(Now + Second * 4L / 10L, Now));
			Assert.Equal("now", VillageArrivals.Countdown(Now - Second * 4L / 10L, Now));
		}

		[Fact]
		public void GroupReadsBackAsItWasWritten()
		{
			VillageArrivals.Arrival arrival = new VillageArrivals.Arrival { Count = 3, QuestId = "abc", What = "out on RaidCrypts", Where = "out of the world, at work at about (1, 2, 3)", Due = 638000000000000000L };
			arrival.Names.Add("Astrid");
			arrival.Names.Add("Bjorn");
			ZPackage pkg = new ZPackage();
			arrival.Write(pkg);
			pkg.SetPos(0);
			VillageArrivals.Arrival read = VillageArrivals.Arrival.Read(pkg);
			Assert.Equal(arrival.Count, read.Count);
			Assert.Equal(arrival.Names, read.Names);
			Assert.Equal(arrival.QuestId, read.QuestId);
			Assert.Equal(arrival.What, read.What);
			Assert.Equal(arrival.Where, read.Where);
			Assert.Equal(arrival.Due, read.Due);
		}

		[Fact]
		public void LineSaysWhoIsComingWhereTheyAreAndWhenTheyreDue()
		{
			VillageArrivals.Arrival arrival = new VillageArrivals.Arrival { Count = 2, What = "on the road from Eikvik", Where = "in the world, 450m to go", Due = Now + 90L * Second };
			arrival.Names.Add("Astrid");
			arrival.Names.Add("Bjorn");
			Assert.Equal("2 vikings on the road from Eikvik (Astrid, Bjorn), in the world, 450m to go, due in 1m 30s", VillageArrivals.Line(arrival, Now));
		}

		[Fact]
		public void GroupLookingForAVillageIsNotDueYet()
		{
			VillageArrivals.Arrival arrival = new VillageArrivals.Arrival { Count = 1, What = "on the road", Due = long.MaxValue };
			Assert.Equal("1 viking on the road, not due until it finds a village", VillageArrivals.Line(arrival, Now));
		}

		[Fact]
		public void ListLeavesOutTheVillagesOwnQuestAndPutsTheSoonestDueFirst()
		{
			List<VillageArrivals.Arrival> arrivals = new List<VillageArrivals.Arrival> { Group(2, "q1", 10L), Group(1, "", 200L), Group(3, "q2", 50L) };
			StringBuilder text = new StringBuilder();
			VillageArrivals.Describe(arrivals, "q1", Now, text);
			string[] lines = text.ToString().TrimEnd('\n').Split('\n');
			Assert.Equal(3, lines.Length);
			Assert.Equal("On the road here: 4 vikings in 2 groups", lines[0]);
			Assert.Equal("  3 vikings out on Hunt, due in 50s", lines[1]);
			Assert.Equal("  1 viking on the road, due in 3m 20s", lines[2]);
		}

		[Fact]
		public void NothingIsListedWithNoGroupButTheVillagesOwnQuest()
		{
			StringBuilder text = new StringBuilder();
			VillageArrivals.Describe(new List<VillageArrivals.Arrival> { Group(2, "q1", 10L) }, "q1", Now, text);
			Assert.Equal("", text.ToString());
		}

		[Fact]
		public void VillageWithNoQuestListsEveryGroup()
		{
			StringBuilder text = new StringBuilder();
			VillageArrivals.Describe(new List<VillageArrivals.Arrival> { Group(2, "q1", 10L) }, "", Now, text);
			Assert.Equal("On the road here: 2 vikings\n  2 vikings out on Hunt, due in 10s\n", text.ToString());
		}

		[Fact]
		public void ClientWaitingForTheServerSaysSo()
		{
			StringBuilder text = new StringBuilder();
			VillageArrivals.Describe(null, "", Now, text);
			Assert.Equal("On the road here: asking the server\n", text.ToString());
		}

		[Fact]
		public void SaysWhatBringsAGroup()
		{
			Assert.Equal("out on MineCopper for CopperOre", VillageArrivals.What(new TravelEntry { QuestId = "q", QuestKind = (int)QuestKind.MineCopper, QuestItem = "CopperOre" }));
			Assert.Equal("out on Hunt", VillageArrivals.What(new TravelEntry { QuestId = "q", QuestKind = (int)QuestKind.Hunt }));
			Assert.Equal("coming back to the village started again at their bonfire", VillageArrivals.What(new TravelEntry { Returning = true }));
			Assert.Equal("on the road from Eikvik", VillageArrivals.What(new TravelEntry { FromName = "Eikvik" }));
			Assert.Equal("on the road", VillageArrivals.What(new TravelEntry()));
		}

		[Theory]
		[InlineData(0, "out of the world, on the way there on foot at about")]
		[InlineData(1, "out of the world, at work at about")]
		[InlineData(2, "out of the world, on the way home on foot at about")]
		[InlineData(3, "out of the world, there and waiting to be brought in")]
		public void QuestGroupIsOnItsWayThereAtWorkOrOnItsWayHome(int stage, string expected)
		{
			float road = 100f / TravelTime.Speed;
			float work = 30f;
			float[] elapsed = { road / 2f, road + work / 2f, road + work + road / 2f, road * 2f + work + 10f };
			TravelEntry entry = QuestGroup(work);
			Assert.StartsWith(expected, VillageArrivals.Where(entry, (long)(elapsed[stage] * 10f), 10.0));
		}

		[Fact]
		public void GroupOnTheRoadSaysHowItCrossesTheLegItsOn()
		{
			TravelRoad road = new TravelRoad();
			road.Add(Vector3.zero, TravelLeg.Foot);
			road.Add(new Vector3(600f, 0f, 0f), TravelLeg.Sea);
			TravelEntry entry = new TravelEntry { Unloaded = 0L, Journey = TravelJourney.Along(road, 6f, false) };
			entry.Remaining = entry.Journey.TotalSeconds();
			entry.Due = (long)(entry.Remaining * 10.0);
			Assert.StartsWith("out of the world, by sea at about", VillageArrivals.Where(entry, 100L, 10.0));
		}

		[Fact]
		public void GroupWithNoTripIsOnlyOutOfTheWorld()
		{
			Assert.Equal("out of the world", VillageArrivals.Where(new TravelEntry { Due = 5L }, 0L, 10.0));
			Assert.Equal("out of the world, looking for a village to go to", VillageArrivals.Where(new TravelEntry { Due = long.MaxValue }, 0L, 10.0));
		}

		[Fact]
		public void QuestHeadlineCountsDownToWhenItsDueBack()
		{
			QuestRecord record = new QuestRecord { Kind = QuestKind.MineCopper, Item = "CopperOre", Phase = QuestPhase.Away, Due = Now + 392L * Second, Trapped = true };
			Assert.Equal("Quest: MineCopper for CopperOre, away, due back in 6m 32s (game time: in a few hours), trapped", VillageQuests.Headline(record, Now, "in a few hours"));
			Assert.Equal("Quest: MineCopper for CopperOre, away, due back in 6m 32s, trapped", VillageQuests.Headline(record, Now, ""));
		}

		[Fact]
		public void QuestWaitingForTheMorningIsNotDueYet()
		{
			QuestRecord record = new QuestRecord { Kind = QuestKind.Hunt, Phase = QuestPhase.Waiting };
			Assert.Equal("Quest: Hunt, waiting, waiting for the morning", VillageQuests.Headline(record, Now, "tomorrow"));
		}

		[Fact]
		public void MemberLineSaysWhatTheVikingTakesAndBringsHome()
		{
			QuestRecord record = new QuestRecord { Kind = QuestKind.MineCopper };
			record.Members.Add("guid-1");
			record.Names.Add("Astrid");
			record.Tools.Add("PickaxeBronze");
			MemberPlan plan = new MemberPlan { Tool = "PickaxeBronze", Hits = 245, ToolLeft = 120f, Kills = 2 };
			plan.Loot.Add(new KeyValuePair<string, int>("CopperOre", 24));
			record.Plans.Add(plan);
			Assert.Equal("Astrid, taking PickaxeBronze from a chest, working with PickaxeBronze (245 hits, 120 durability left), 2 kills, bringing home 24 CopperOre", VillageQuests.MemberLine(record, 0));
		}

		[Fact]
		public void MemberLineGoesByTheVikingsIdWithNoNameAndNothingPlannedYet()
		{
			QuestRecord record = new QuestRecord { Kind = QuestKind.Hunt };
			record.Members.Add("guid-1");
			record.Names.Add("");
			record.Tools.Add("");
			Assert.Equal("guid-1, bringing home nothing", VillageQuests.MemberLine(record, 0));
		}
	}
}
