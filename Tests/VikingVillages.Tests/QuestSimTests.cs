using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what happens to vikings out on a quest - what their tools get done, what they find and carry home, how hurt
	/// they come back and what they eat and drink - and how it's noted and where they go
	/// </summary>
	public class QuestSimTests
	{
		[Fact]
		public void ToolsSwingTogetherUntilTheresEnough()
		{
			List<QuestTool> tools = new List<QuestTool>
			{
				new QuestTool { Damage = 10f, Durability = 100f, Drain = 1f },
				new QuestTool { Damage = 10f, Durability = 100f, Drain = 1f }
			};
			GatherResult result = QuestSim.Gather(10, 20f, tools, 2f, 1f);
			Assert.Equal(10, result.Units);
			Assert.Equal(10, result.Hits[0]);
			Assert.Equal(10, result.Hits[1]);
			Assert.Equal(20f, result.Seconds, 3);
			Assert.Equal(90f, result.DurabilityLeft[0], 3);
		}

		[Fact]
		public void MoreToolsTakeLessTime()
		{
			List<QuestTool> one = new List<QuestTool> { new QuestTool { Damage = 10f, Durability = 1000f, Drain = 1f } };
			List<QuestTool> three = new List<QuestTool>
			{
				new QuestTool { Damage = 10f, Durability = 1000f, Drain = 1f },
				new QuestTool { Damage = 10f, Durability = 1000f, Drain = 1f },
				new QuestTool { Damage = 10f, Durability = 1000f, Drain = 1f }
			};
			Assert.True(QuestSim.Gather(30, 10f, three, 1f, 1f).Seconds < QuestSim.Gather(30, 10f, one, 1f, 1f).Seconds);
		}

		[Fact]
		public void AToolThatBreaksStopsShort()
		{
			List<QuestTool> tools = new List<QuestTool> { new QuestTool { Damage = 10f, Durability = 5f, Drain = 1f } };
			GatherResult result = QuestSim.Gather(100, 10f, tools, 1f, 1f);
			Assert.Equal(5, result.Units);
			Assert.Equal(0f, result.DurabilityLeft[0], 3);
		}

		[Fact]
		public void TheWorldsDurabilityRateWearsToolsFaster()
		{
			List<QuestTool> tools = new List<QuestTool> { new QuestTool { Damage = 10f, Durability = 100f, Drain = 1f } };
			Assert.Equal(80f, QuestSim.Gather(10, 10f, tools, 1f, 2f).DurabilityLeft[0], 3);
		}

		[Fact]
		public void NoToolsGetNothing()
		{
			GatherResult result = QuestSim.Gather(10, 10f, new List<QuestTool>(), 1f, 1f);
			Assert.Equal(0, result.Units);
			Assert.Equal(0f, result.Seconds, 3);
		}

		[Fact]
		public void CarryingIsLimitedByWeightAndSlots()
		{
			List<CarrySpace> spaces = new List<CarrySpace>
			{
				new CarrySpace { WeightRoom = 100f, FreeSlots = 10 },
				new CarrySpace { WeightRoom = 1000f, FreeSlots = 1 }
			};
			Assert.Equal(10 + 30, QuestSim.CarryUnits(spaces, 10f, 30));
		}

		[Fact]
		public void ShareSpreadsTheLoadAndLeavesWhatDoesntFit()
		{
			List<CarrySpace> spaces = new List<CarrySpace>
			{
				new CarrySpace { WeightRoom = 60f, FreeSlots = 5 },
				new CarrySpace { WeightRoom = 60f, FreeSlots = 5 }
			};
			List<KeyValuePair<string, int>> found = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("CopperOre", 10), new KeyValuePair<string, int>("Amber", 5) };
			List<Dictionary<string, int>> shares = QuestSim.Share(found, spaces, (string item) => ((item == "CopperOre") ? 10f : 1f), (string item) => 30);
			Assert.Equal(5, shares[0]["CopperOre"]);
			Assert.Equal(5, shares[1]["CopperOre"]);
			Assert.Equal(5, shares[0]["Amber"] + shares[1]["Amber"]);
		}

		[Fact]
		public void ShareLeavesWhatNoOneHasRoomFor()
		{
			List<CarrySpace> spaces = new List<CarrySpace> { new CarrySpace { WeightRoom = 60f, FreeSlots = 5 } };
			List<KeyValuePair<string, int>> found = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("CopperOre", 20) };
			List<Dictionary<string, int>> shares = QuestSim.Share(found, spaces, (string item) => 10f, (string item) => 30);
			Assert.Equal(6, shares[0]["CopperOre"]);
		}

		[Fact]
		public void ShareNeedsASlotForANewStack()
		{
			List<CarrySpace> spaces = new List<CarrySpace> { new CarrySpace { WeightRoom = 1000f, FreeSlots = 1 } };
			List<KeyValuePair<string, int>> found = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("A", 3), new KeyValuePair<string, int>("B", 3) };
			List<Dictionary<string, int>> shares = QuestSim.Share(found, spaces, (string item) => 1f, (string item) => 2);
			Assert.Equal(2, shares[0]["A"]);
			Assert.False(shares[0].ContainsKey("B"));
		}

		[Fact]
		public void ACreaturesDropsRollOnTheirOwn()
		{
			LootTable table = new LootTable { Picks = false };
			table.Drops.Add(new LootDrop { Item = "TrollHide", Min = 5, Max = 5, Chance = 1f });
			table.Drops.Add(new LootDrop { Item = "Coins", Min = 10, Max = 10, Chance = 0f });
			Dictionary<string, int> tally = new Dictionary<string, int>();
			QuestSim.Roll(table, new System.Random(1), tally);
			Assert.Equal(5, tally["TrollHide"]);
			Assert.False(tally.ContainsKey("Coins"));
		}

		[Fact]
		public void AChestPicksItsDrops()
		{
			LootTable table = new LootTable { Picks = true, PicksMin = 3, PicksMax = 3, OneOfEach = true };
			table.Drops.Add(new LootDrop { Item = "Amber", Min = 1, Max = 1, Weight = 1f });
			table.Drops.Add(new LootDrop { Item = "Coins", Min = 2, Max = 2, Weight = 1f });
			table.Drops.Add(new LootDrop { Item = "Ruby", Min = 1, Max = 1, Weight = 1f });
			Dictionary<string, int> tally = new Dictionary<string, int>();
			QuestSim.Roll(table, new System.Random(7), tally);
			Assert.Equal(1, tally["Amber"]);
			Assert.Equal(2, tally["Coins"]);
			Assert.Equal(1, tally["Ruby"]);
		}

		[Fact]
		public void ATableThatNeverDropsDropsNothing()
		{
			LootTable table = new LootTable { Picks = true, Chance = 0f };
			table.Drops.Add(new LootDrop { Item = "Amber" });
			Dictionary<string, int> tally = new Dictionary<string, int>();
			QuestSim.Roll(table, new System.Random(3), tally);
			Assert.Empty(tally);
		}

		[Fact]
		public void SkillFindsMore()
		{
			Dictionary<string, int> tally = new Dictionary<string, int> { { "Raspberry", 10 } };
			QuestSim.ApplyBonus(tally, QuestSim.SkillBonus(100f));
			Assert.Equal(15, tally["Raspberry"]);
			Assert.Equal(0f, QuestSim.SkillBonus(0f), 3);
		}

		[Theory]
		[InlineData(0f, 0.4f)]
		[InlineData(50f, 0.7f)]
		[InlineData(100f, 1f)]
		[InlineData(150f, 1f)]
		public void SkillFactorIsTheGamesAverage(float level, float expected)
		{
			Assert.Equal(expected, QuestSim.SkillFactor(level), 3);
		}

		[Fact]
		public void HarmGrowsWithAHarderBiomeAndFewerVikings()
		{
			float even = QuestSim.Harm(QuestKind.RaidCrypts, 2, 2, 3, 3, 1800f, 100f);
			Assert.Equal(100f * QuestSim.Danger(QuestKind.RaidCrypts), even, 3);
			Assert.Equal(even * 2f, QuestSim.Harm(QuestKind.RaidCrypts, 3, 2, 3, 3, 1800f, 100f), 3);
			Assert.Equal(even / 2f, QuestSim.Harm(QuestKind.RaidCrypts, 1, 2, 3, 3, 1800f, 100f), 3);
			Assert.True(QuestSim.Harm(QuestKind.RaidCrypts, 2, 2, 6, 3, 1800f, 100f) < even);
			Assert.True(QuestSim.Harm(QuestKind.RaidCrypts, 2, 2, 1, 3, 1800f, 100f) > even);
		}

		[Theory]
		[InlineData(0, 0.1f)]
		[InlineData(-1, 0.05f)]
		[InlineData(-3, 0.01f)]
		[InlineData(-4, 0f)]
		[InlineData(-7, 0f)]
		[InlineData(4, 1.6f)]
		public void HarmHasNoCapAndRoundsDownToWholeHundredths(int gap, float expected)
		{
			// Eikthyr's danger of 0.6 for his 5 minutes is a tenth of a viking's health at its own biome
			Assert.Equal(expected, QuestSim.HarmShare(0.6f, gap, 2, 2, 300f), 4);
		}

		[Theory]
		[InlineData(0f, 0f)]
		[InlineData(0.1f, 0f)]
		[InlineData(0.36f, 0f)]
		[InlineData(0.4f, 0.012f)]
		[InlineData(0.6f, 0.027f)]
		[InlineData(1f, 0.075f)]
		[InlineData(3f, 0.075f)]
		public void DeathOnAQuestGoesByTheSquareOfItsHarmFromOnePercent(float harmShare, float expected)
		{
			Assert.Equal(expected, QuestSim.DeathChance(harmShare), 5);
		}

		[Fact]
		public void NoQuestIsExpectedToKillAVikingOfItsOwnBiome()
		{
			foreach (QuestKind kind in System.Enum.GetValues(typeof(QuestKind)))
			{
				Assert.False(QuestSim.ExpectedToDie(kind, 0));
			}
		}

		[Fact]
		public void VikingsFarBehindAQuestStayHome()
		{
			// A crypt raid takes 60% of a Swamp viking's health, so a Black Forest one would lose all of it
			Assert.False(QuestSim.ExpectedToDie(QuestKind.RaidCrypts, 0));
			Assert.True(QuestSim.ExpectedToDie(QuestKind.RaidCrypts, 1));
			// A fuling raid takes 53% of a Plains viking's health, and a Mountains one would lose all of it
			Assert.True(QuestSim.ExpectedToDie(QuestKind.HuntGoblins, 1));
			Assert.False(QuestSim.ExpectedToDie(QuestKind.KillEikthyr, 2));
		}

		[Fact]
		public void VikingsEatAFullSetEachTimeTheirFoodWearsOff()
		{
			Assert.Equal(3, QuestSim.MealsWanted(1000f, 1800f));
			Assert.Equal(6, QuestSim.MealsWanted(2000f, 1800f));
			Assert.Equal(4, QuestSim.MealsEaten(2000f, 1800f, 4));
			Assert.Equal(0, QuestSim.MealsWanted(0f, 1800f));
		}

		[Fact]
		public void FoodAndPotionsBringVikingsHomeHealthier()
		{
			float hungry = QuestSim.EndHealth(100f, 120f, false, 0, 0f, out int _);
			float fed = QuestSim.EndHealth(100f, 120f, true, 0, 0f, out int _);
			Assert.True(fed > hungry);
			float dosed = QuestSim.EndHealth(100f, 120f, false, 2, 50f, out int drunk);
			Assert.True(dosed > hungry);
			Assert.Equal(1, drunk);
		}

		[Fact]
		public void VikingsNeverComeHomeDead()
		{
			Assert.Equal(100f * QuestSim.HealthFloor, QuestSim.EndHealth(100f, 10000f, false, 0, 0f, out int _), 3);
			Assert.Equal(100f, QuestSim.EndHealth(100f, 0f, false, 0, 0f, out int _), 3);
		}

		[Fact]
		public void OutcomeSurvivesBeingWrittenAndRead()
		{
			QuestOutcome outcome = new QuestOutcome();
			outcome.Kind = QuestKind.HuntTrolls;
			outcome.Item = "TrollHide";
			outcome.Companions.AddRange(new[] { "Ulf", "Sigrid" });
			outcome.Loot.Add(new KeyValuePair<string, int>("TrollHide", 7));
			outcome.Tool = "AxeBronze";
			outcome.ToolLeft = 12.5f;
			outcome.Used.Add(new KeyValuePair<string, int>("CookedMeat", 3));
			outcome.HealthShare = 0.4f;
			outcome.Skills.Add(new KeyValuePair<int, float>(7, 12f));
			outcome.Kills = 2;
			outcome.SetOutAt = 99L;
			outcome.Village = "Eikvik";
			QuestOutcome read = QuestOutcome.Decode(outcome.Encode());
			Assert.Equal(outcome.Kind, read.Kind);
			Assert.Equal(outcome.Item, read.Item);
			Assert.Equal(outcome.Companions, read.Companions);
			Assert.Equal(outcome.Loot, read.Loot);
			Assert.Equal(outcome.Tool, read.Tool);
			Assert.Equal(outcome.ToolLeft, read.ToolLeft);
			Assert.Equal(outcome.Used, read.Used);
			Assert.Equal(outcome.HealthShare, read.HealthShare);
			Assert.Equal(outcome.Skills, read.Skills);
			Assert.Equal(outcome.Kills, read.Kills);
			Assert.Equal(outcome.SetOutAt, read.SetOutAt);
			Assert.Equal(outcome.Village, read.Village);
			Assert.Null(QuestOutcome.Decode(new byte[0]));
		}

		[Fact]
		public void PlansSurviveBeingWrittenAndRead()
		{
			QuestRecord record = new QuestRecord { Id = "q", Kind = QuestKind.MineCopper, Item = "CopperOre", Amount = 20, ActiveSeconds = 400f, QuestBiome = 1, Wanted = 2 };
			record.Members.Add("a");
			record.Names.Add("Ragna");
			record.Tools.Add("PickaxeAntler");
			MemberPlan plan = new MemberPlan { Tool = "PickaxeAntler", ToolLeft = 30f, Hits = 80, Kills = 1 };
			plan.Loot.Add(new KeyValuePair<string, int>("CopperOre", 20));
			plan.Spend.Add(new KeyValuePair<string, int>("Amber", 2));
			record.Plans.Add(plan);
			QuestRecord read = QuestRecord.Decode(record.Encode());
			Assert.Equal(20, read.Amount);
			Assert.Equal(400f, read.ActiveSeconds);
			Assert.Equal(1, read.QuestBiome);
			Assert.Equal(2, read.Wanted);
			MemberPlan readPlan = read.PlanOf("a");
			Assert.Equal(plan.Loot, readPlan.Loot);
			Assert.Equal(plan.Tool, readPlan.Tool);
			Assert.Equal(plan.ToolLeft, readPlan.ToolLeft);
			Assert.Equal(plan.Hits, readPlan.Hits);
			Assert.Equal(plan.Kills, readPlan.Kills);
			Assert.Equal(plan.Spend, readPlan.Spend);
			Assert.Empty(read.PlanOf("nobody").Loot);
		}

		[Theory]
		[InlineData("crypt2", "crypt", true)]
		[InlineData("sunkencrypt4", "crypt", false)]
		[InlineData("sunkencrypt4", "sunkencrypt", true)]
		[InlineData("trollcave02", "trollcave", true)]
		[InlineData("hildir_crypt", "crypt", false)]
		public void BurialChambersAreNotSunkenCrypts(string name, string kind, bool expected)
		{
			Assert.Equal(expected, QuestTargets.Matches(name, kind));
		}

		[Fact]
		public void QuestsGoToTheirOwnPlaces()
		{
			Assert.Equal("eikthyrnir", QuestTargets.LocationOf(QuestKind.KillEikthyr));
			Assert.Equal("vendor_blackforest", QuestTargets.LocationOf(QuestKind.BuyCirclet));
			Assert.Null(QuestTargets.LocationOf(QuestKind.MineCopper));
			Assert.Equal(ResourceBook.Mountain, QuestTargets.BiomeOf(QuestKind.MineSilver, "SilverOre", ResourceBook.Plains));
			Assert.Equal(ResourceBook.Plains, QuestTargets.BiomeOf(QuestKind.Hunt, "", ResourceBook.Plains));
			Assert.Equal(ResourceBook.BlackForest, QuestTargets.BiomeOf(QuestKind.ChopTrees, "RoundLog", ResourceBook.Swamp));
			Assert.Equal(ResourceBook.Swamp, QuestTargets.BiomeOf(QuestKind.ChopTrees, "Wood", ResourceBook.Swamp));
		}

		[Fact]
		public void TreasureIsSoldToTheTradersOfTheVillagesBiomeAndThoseBefore()
		{
			Assert.Equal(new List<string> { "hildir_camp" }, QuestTargets.TradersFor(ResourceBook.Meadows));
			Assert.Equal(new List<string> { "hildir_camp", "vendor_blackforest" }, QuestTargets.TradersFor(ResourceBook.BlackForest));
			Assert.Equal(new List<string> { "hildir_camp", "vendor_blackforest", "bogwitch_camp" }, QuestTargets.TradersFor(ResourceBook.Swamp));
			Assert.Equal(3, QuestTargets.TradersFor(ResourceBook.DeepNorth).Count);
			Assert.Null(QuestTargets.LocationOf(QuestKind.SellTreasure));
			Assert.Equal(ResourceBook.Meadows, QuestTargets.BiomeOf(QuestKind.SellTreasure, "", ResourceBook.Meadows));
			Assert.Equal(ResourceBook.Swamp, QuestTargets.BiomeOf(QuestKind.SellTreasure, "", ResourceBook.Plains));
		}

		[Theory]
		[InlineData(ResourceBook.Meadows, ResourceBook.Plains, "Hildir")]
		[InlineData(ResourceBook.BlackForest, ResourceBook.Plains, "Haldor")]
		[InlineData(ResourceBook.Swamp, ResourceBook.Plains, "the Bog Witch")]
		[InlineData(-1, ResourceBook.Meadows, "Hildir")]
		[InlineData(-1, ResourceBook.BlackForest, "Haldor")]
		[InlineData(-1, ResourceBook.Mountain, "the Bog Witch")]
		public void TradersAreNamedByTheirCampsBiome(int biome, int village, string expected)
		{
			Assert.Equal(expected, QuestTargets.TraderName(biome, village));
		}

		[Fact]
		public void TheTraderIsNamedInTheLines()
		{
			Assert.Equal("The Bog Witch drives a hard bargain.", QuestSpeech.NameTrader("{trader} drives a hard bargain.", "the Bog Witch"));
			Assert.Equal("Ragna took our amber to Hildir.", QuestSpeech.NameTrader("Ragna took our amber to {trader}.", "Hildir"));
			Assert.Equal("No trader here.", QuestSpeech.NameTrader("No trader here.", "Hildir"));
			Assert.Null(QuestSpeech.NameTrader((string)null, "Hildir"));
		}
	}
}
