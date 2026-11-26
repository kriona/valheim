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
		public void VikingsWantAFullSetEachTimeTheirFoodWearsOff()
		{
			Assert.Equal(3, QuestSim.MealsWanted(1000f, 1800f));
			Assert.Equal(6, QuestSim.MealsWanted(2000f, 1800f));
			Assert.Equal(0, QuestSim.MealsWanted(0f, 1800f));
		}

		/// <summary>
		/// Makes a food a viking carries on a quest
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="health">The health it adds</param>
		/// <param name="burn">How long it lasts, in seconds</param>
		/// <param name="count">How many of it</param>
		/// <returns>The food</returns>
		private static QuestFood Food(string item, float health, float burn, int count)
		{
			return(new QuestFood { Item = item, Health = health, Burn = burn, Count = count });
		}

		[Fact]
		public void VikingsGoOnWithTheirFoodAndEatWhenAPlaceIsFree()
		{
			// Bread has 100 seconds left as they set out, so the stew and the berries fill the other two places; once
			// the bread wears off there's nothing new to eat, so the second stew waits for the first to wear off
			List<QuestMeal> eating = new List<QuestMeal> { new QuestMeal { Item = "Bread", Health = 30f, Burn = 1000f, Left = 100f } };
			List<QuestFood> carried = new List<QuestFood> { Food("Stew", 20f, 500f, 2), Food("Berries", 10f, 500f, 1) };
			QuestMeals meals = QuestSim.Eat(eating, carried, 1200f, 0f);
			Assert.Equal(2, meals.Eaten["Stew"]);
			Assert.Equal(1, meals.Eaten["Berries"]);
			Assert.False(meals.Eaten.ContainsKey("Bread"));
			Assert.Empty(meals.Lasting);
			// Food was up for 100 + 500 + 500 + 500 of 3 places' 1200 seconds
			Assert.Equal(1600f / 3600f, meals.FedShare, 3);
			Assert.False(meals.Fed);
			Assert.Equal(2, carried[0].Count);
		}

		[Fact]
		public void VikingsNeverEatTwoOfAFoodAtOnce()
		{
			QuestMeals meals = QuestSim.Eat(new List<QuestMeal>(), new List<QuestFood> { Food("Stew", 20f, 1000f, 3) }, 2500f, 0f);
			Assert.Equal(3, meals.Eaten["Stew"]);
			Assert.Single(meals.Lasting);
			Assert.Equal(500f, meals.Lasting[0].Left, 3);
			Assert.Equal(1f / 3f, meals.FedShare, 3);
		}

		[Fact]
		public void VikingsWithAFullSetOfFoodCountAsFed()
		{
			List<QuestFood> carried = new List<QuestFood> { Food("A", 20f, 1000f, 2), Food("B", 20f, 1000f, 2), Food("C", 20f, 1000f, 2) };
			QuestMeals meals = QuestSim.Eat(new List<QuestMeal>(), carried, 1500f, 0f);
			Assert.Equal(1f, meals.FedShare, 3);
			Assert.True(meals.Fed);
			Assert.Equal(3, meals.Lasting.Count);
		}

		[Fact]
		public void FoodSetsVikingsMaxHealthWhileTheyreOut()
		{
			List<QuestFood> carried = new List<QuestFood> { Food("A", 60f, 1000f, 1), Food("B", 60f, 1000f, 1), Food("C", 60f, 1000f, 1) };
			// Just eaten, three foods of 60 add 180 to the base 25
			Assert.Equal(205f, QuestSim.Eat(new List<QuestMeal>(), carried, 0f, 50f).EndMaxHealth, 3);
			// Fading as they wear off, they add 180 / 1.3 on average, and once worn off leave the floor
			QuestMeals meals = QuestSim.Eat(new List<QuestMeal>(), carried, 1000f, 50f);
			Assert.InRange(meals.MaxHealth, 160f, 167f);
			Assert.Equal(50f, meals.EndMaxHealth, 3);
			Assert.Empty(meals.Lasting);
		}

		[Fact]
		public void BetterFoodRaisesVikingsMaxHealthOnAQuest()
		{
			List<QuestFood> plain = new List<QuestFood> { Food("A", 20f, 1800f, 1), Food("B", 20f, 1800f, 1), Food("C", 20f, 1800f, 1) };
			List<QuestFood> rich = new List<QuestFood> { Food("A", 80f, 1800f, 1), Food("B", 80f, 1800f, 1), Food("C", 80f, 1800f, 1) };
			QuestMeals plainMeals = QuestSim.Eat(new List<QuestMeal>(), plain, 900f, 115f);
			QuestMeals richMeals = QuestSim.Eat(new List<QuestMeal>(), rich, 900f, 115f);
			// Food below the floor leaves the viking at it
			Assert.Equal(115f, plainMeals.MaxHealth, 3);
			Assert.True(richMeals.MaxHealth > 200f);
			Assert.True(richMeals.EndMaxHealth > 200f);
		}

		[Fact]
		public void FoodsLastingAtTheEndKeepTheirTimeLeft()
		{
			List<QuestMeal> eating = new List<QuestMeal> { new QuestMeal { Item = "Bread", Health = 30f, Burn = 1000f, Left = 900f } };
			QuestMeals meals = QuestSim.Eat(eating, new List<QuestFood>(), 300f, 0f);
			Assert.Single(meals.Lasting);
			Assert.Equal("Bread", meals.Lasting[0].Item);
			Assert.Equal(600f, meals.Lasting[0].Left, 3);
		}

		[Fact]
		public void VikingsPackDifferentFoodsInTheOrderTheyWantThem()
		{
			List<QuestFood> ranked = new List<QuestFood> { Food("A", 0f, 0f, 10), Food("B", 0f, 0f, 10), Food("C", 0f, 0f, 10) };
			Dictionary<string, int> picks = QuestSim.Pack(ranked, 9, 3, new Dictionary<string, int>());
			Assert.Equal(3, picks["A"]);
			Assert.Equal(3, picks["B"]);
			Assert.Equal(3, picks["C"]);
			Dictionary<string, int> few = QuestSim.Pack(ranked, 2, 3, new Dictionary<string, int>());
			Assert.Equal(2, few["A"]);
			Assert.False(few.ContainsKey("B"));
		}

		[Fact]
		public void VikingsPackMoreOfTheFoodTheyWantMostWhenThereArentEnoughOthers()
		{
			List<QuestFood> ranked = new List<QuestFood> { Food("A", 0f, 0f, 10), Food("B", 0f, 0f, 1) };
			Dictionary<string, int> picks = QuestSim.Pack(ranked, 6, 2, new Dictionary<string, int>());
			Assert.Equal(5, picks["A"]);
			Assert.Equal(1, picks["B"]);
		}

		[Fact]
		public void VikingsPackCountingTheFoodTheyCarry()
		{
			List<QuestFood> ranked = new List<QuestFood> { Food("A", 0f, 0f, 10), Food("B", 0f, 0f, 10) };
			Dictionary<string, int> picks = QuestSim.Pack(ranked, 3, 3, new Dictionary<string, int> { { "A", 2 } });
			Assert.Equal(1, picks["A"]);
			Assert.Equal(2, picks["B"]);
		}

		[Fact]
		public void VikingsPackTheirStrongestFoodForAFight()
		{
			List<QuestFood> foods = new List<QuestFood> { Food("Neck", 25f, 0f, 1), Food("Deer", 35f, 0f, 1), Food("Stew", 45f, 0f, 1), Food("Berries", 8f, 0f, 1) };
			QuestSim.Rank(foods, true, 115f);
			Assert.Equal(new[] { "Stew", "Deer", "Neck", "Berries" }, foods.ConvertAll((QuestFood food) => food.Item));
		}

		[Fact]
		public void VikingsTravelOnTheWeakestFoodThatDoesThemGood()
		{
			// A Black Forest viking's floor of 115 holds 30 a food over the base, so deer meat and stew raise it and the
			// rest don't
			List<QuestFood> foods = new List<QuestFood> { Food("Neck", 25f, 0f, 1), Food("Deer", 35f, 0f, 1), Food("Stew", 45f, 0f, 1), Food("Berries", 8f, 0f, 1) };
			QuestSim.Rank(foods, false, 115f);
			Assert.Equal(new[] { "Deer", "Stew", "Neck", "Berries" }, foods.ConvertAll((QuestFood food) => food.Item));
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
			MemberPlan plan = new MemberPlan { Tool = "PickaxeAntler", ToolLeft = 30f, Hits = 80, Kills = 1, FightHarm = 0.42f };
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
			Assert.Equal(plan.FightHarm, readPlan.FightHarm);
			Assert.Empty(read.PlanOf("nobody").Loot);
			Assert.Equal(-1f, read.PlanOf("nobody").FightHarm);
		}

		[Theory]
		[InlineData("crypt2", "crypt", true)]
		[InlineData("sunkencrypt4", "crypt", false)]
		[InlineData("sunkencrypt4", "sunkencrypt", true)]
		[InlineData("trollcave02", "trollcave", true)]
		[InlineData("hildir_crypt", "crypt", false)]
		[InlineData("mountaincave02", "mountaincave", true)]
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
			Assert.Equal("mountaincave", QuestTargets.LocationOf(QuestKind.RaidFrostCaves));
			Assert.Equal(ResourceBook.Mountain, QuestTargets.BiomeOf(QuestKind.RaidFrostCaves, "JuteRed", ResourceBook.Plains));
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
