using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests which quests NPC vikings go on and who of a village's vikings goes first
	/// </summary>
	public class NPCQuestsTests
	{
		/// <summary>
		/// A viking fit to go on a quest, for picking - by name, whether it's an NPC viking and whether it lacks what the
		/// quest buys
		/// </summary>
		private class Candidate
		{
			public string Name;
			public bool NPC;
			public bool Lacks;
		}

		/// <summary>
		/// Makes a viking fit to go
		/// </summary>
		/// <param name="name">Its name</param>
		/// <param name="npc">Whether it's an NPC viking</param>
		/// <param name="lacks">Whether it lacks what the quest buys</param>
		/// <returns>The viking</returns>
		private static Candidate Fit(string name, bool npc, bool lacks = false)
		{
			return(new Candidate { Name = name, NPC = npc, Lacks = lacks });
		}

		/// <summary>
		/// Picks who goes, by name
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="fit">Those fit to go, in the order they'd go</param>
		/// <param name="size">How many go</param>
		/// <returns>The names of those who go, in order</returns>
		private static List<string> Picked(QuestKind kind, List<Candidate> fit, int size)
		{
			return(NPCQuests.Pick(kind, fit, (Candidate c) => c.NPC, (Candidate c) => c.Lacks, size).ConvertAll((Candidate c) => c.Name));
		}

		/// <summary>
		/// Checks that NPC vikings go only on quests that don't fight or take a weapon to the work
		/// </summary>
		[Fact]
		public void GoesOnPeacefulQuests()
		{
			foreach (QuestKind kind in new[] { QuestKind.Forage, QuestKind.FindBees, QuestKind.SellTreasure, QuestKind.BuyCirclet, QuestKind.BuyBelt, QuestKind.BuyWiderPockets, QuestKind.BuyDeeperPockets })
			{
				Assert.True(NPCQuests.Allows(kind));
			}
			foreach (QuestKind kind in new[] { QuestKind.Hunt, QuestKind.ChopTrees, QuestKind.MineCopper, QuestKind.RaidCrypts, QuestKind.KillEikthyr, QuestKind.HuntGoblins })
			{
				Assert.False(NPCQuests.Allows(kind));
			}
		}

		/// <summary>
		/// Checks that selling treasure takes one PC viking and NPC vikings for every other place
		/// </summary>
		[Fact]
		public void SellsWithOnePCAndNPCs()
		{
			List<Candidate> fit = new List<Candidate> { Fit("Ulf", false), Fit("Ragna", false), Fit("Sigrid", true), Fit("Hakon", true) };
			Assert.Equal(new List<string> { "Ulf", "Sigrid" }, Picked(QuestKind.SellTreasure, fit, 2));
			Assert.Equal(new List<string> { "Ulf", "Sigrid", "Hakon" }, Picked(QuestKind.SellTreasure, fit, 3));
		}

		/// <summary>
		/// Checks that selling treasure fills its places with PC vikings once there are no NPC vikings left, and never
		/// goes with NPC vikings alone
		/// </summary>
		[Fact]
		public void SellsWithAPCOrNotAtAll()
		{
			List<Candidate> fit = new List<Candidate> { Fit("Ulf", false), Fit("Ragna", false) };
			Assert.Equal(new List<string> { "Ulf", "Ragna" }, Picked(QuestKind.SellTreasure, fit, 2));
			List<Candidate> npcs = new List<Candidate> { Fit("Sigrid", true), Fit("Hakon", true) };
			Assert.Empty(Picked(QuestKind.SellTreasure, npcs, 2));
		}

		/// <summary>
		/// Checks that buying from Haldor never goes with NPC vikings alone, even for an NPC viking who lacks it
		/// </summary>
		[Fact]
		public void BuysWithAPCOrNotAtAll()
		{
			List<Candidate> npcs = new List<Candidate> { Fit("Sigrid", true, true), Fit("Hakon", true) };
			Assert.Empty(Picked(QuestKind.BuyCirclet, npcs, 2));
			Assert.Empty(Picked(QuestKind.BuyDeeperPockets, npcs, 2));
		}

		/// <summary>
		/// Checks that buying from Haldor takes a PC viking who lacks it before an NPC viking who does, first, with an
		/// NPC viking beside it
		/// </summary>
		[Fact]
		public void BuysForAPCFirst()
		{
			List<Candidate> fit = new List<Candidate> { Fit("Sigrid", true, true), Fit("Ulf", false), Fit("Ragna", false, true), Fit("Hakon", true) };
			Assert.Equal(new List<string> { "Ragna", "Sigrid" }, Picked(QuestKind.BuyBelt, fit, 2));
		}

		/// <summary>
		/// Checks that buying for an NPC viking, when every PC viking has it, takes a PC viking beside it
		/// </summary>
		[Fact]
		public void BuysForAnNPCWithAPC()
		{
			List<Candidate> fit = new List<Candidate> { Fit("Sigrid", true, true), Fit("Hakon", true), Fit("Ulf", false) };
			Assert.Equal(new List<string> { "Sigrid", "Ulf" }, Picked(QuestKind.BuyCirclet, fit, 2));
		}

		/// <summary>
		/// Checks that foraging and finding bees take NPC vikings first, and every other quest goes in its order
		/// </summary>
		[Fact]
		public void ForagesWithNPCsFirst()
		{
			List<Candidate> fit = new List<Candidate> { Fit("Ulf", false), Fit("Sigrid", true), Fit("Ragna", false) };
			Assert.Equal(new List<string> { "Sigrid", "Ulf" }, Picked(QuestKind.Forage, fit, 2));
			Assert.Equal(new List<string> { "Sigrid" }, Picked(QuestKind.FindBees, fit, 1));
			Assert.Equal(new List<string> { "Ulf", "Sigrid" }, Picked(QuestKind.Hunt, fit, 2));
		}
	}
}
