using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a village works back from what it wants to the next thing it does - gathering, crafting, smelting or
	/// going on a quest - through a book of recipes and smelts like the game's
	/// </summary>
	public class ResourcePlannerTests
	{
		/// <summary>
		/// The tool tiers the test book's tools have and its rocks need - an antler pickaxe breaks copper, tin and muddy
		/// scrap, and silver takes an iron one
		/// </summary>
		private const int AntlerTier = 1;
		private const int IronTier = 3;

		/// <summary>
		/// Makes a book like the game's for the items the tests use - the quests' own table, with bronze crafted at the
		/// forge from copper and tin, the bars smelted at the smelter with coal, coal burnt from wood in the kiln, and
		/// pickaxes and a torch crafted from what quests and gathering bring
		/// </summary>
		/// <returns>The book</returns>
		private static ResourceBook MakeBook()
		{
			ResourceBook book = new ResourceBook();
			book.AddGathered();
			Craft(book, "Bronze", "forge", 1, 1, ("Copper", 2), ("Tin", 1));
			Craft(book, "PickaxeAntler", "piece_workbench", 1, 1, ("Wood", 10), ("HardAntler", 1));
			Craft(book, "PickaxeIron", "forge", 2, 1, ("Wood", 3), ("Iron", 20));
			Craft(book, "Torch", null, 0, 1, ("Wood", 1), ("Resin", 1));
			Smelt(book, "Copper", "CopperOre");
			Smelt(book, "Tin", "TinOre");
			Smelt(book, "Iron", "IronScrap");
			Smelt(book, "Silver", "SilverOre");
			book.Add("Coal", new ResourceSource { Kind = SourceKind.Smelt, Station = ResourceBook.Kiln, StationLevel = 1, From = "Wood" });
			book.AddQuests();
			book.AddTool("PickaxeIron", ToolKind.Pickaxe, IronTier);
			book.AddTool("PickaxeAntler", ToolKind.Pickaxe, AntlerTier);
			foreach (List<ResourceSource> sources in book.Sources.Values)
			{
				foreach (ResourceSource source in sources)
				{
					if (source.Tool == ToolKind.Pickaxe)
					{
						source.ToolTier = ((source.ToolTarget == "silvervein") ? IronTier : AntlerTier);
					}
				}
			}
			return(book);
		}

		/// <summary>
		/// Adds a recipe to a book
		/// </summary>
		/// <param name="book">The book</param>
		/// <param name="item">What it crafts</param>
		/// <param name="station">Its station, or null for none</param>
		/// <param name="level">The station's level</param>
		/// <param name="amount">How many a craft makes</param>
		/// <param name="ingredients">What it takes</param>
		private static void Craft(ResourceBook book, string item, string station, int level, int amount, params (string, int)[] ingredients)
		{
			ResourceSource source = new ResourceSource { Kind = SourceKind.Craft, Station = station, StationLevel = level, Amount = amount };
			foreach ((string name, int count) in ingredients)
			{
				source.Ingredients.Add(new KeyValuePair<string, int>(name, count));
			}
			book.Add(item, source);
		}

		/// <summary>
		/// Adds a smelt at the smelter, burning a coal for each bar, to a book
		/// </summary>
		/// <param name="book">The book</param>
		/// <param name="item">The bar</param>
		/// <param name="from">What it's smelted from</param>
		private static void Smelt(ResourceBook book, string item, string from)
		{
			book.Add(item, new ResourceSource { Kind = SourceKind.Smelt, Station = ResourceBook.Smelter, StationLevel = 1, From = from, Fuel = "Coal", FuelPer = 1 });
		}

		/// <summary>
		/// Makes a Black Forest village with a workbench, a forge, a smelter and a kiln, and nothing in its chests
		/// </summary>
		/// <returns>What it has</returns>
		private static VillageHoldings MakeHoldings()
		{
			VillageHoldings holdings = new VillageHoldings { Biome = ResourceBook.BlackForest };
			holdings.AddStation("piece_workbench", 1);
			holdings.AddStation("forge", 2);
			holdings.AddStation(ResourceBook.Smelter, 1);
			holdings.AddStation(ResourceBook.Kiln, 1);
			return(holdings);
		}

		[Fact]
		public void HavingEverythingIsReady()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("Bronze", 5);
			Assert.Equal(StepKind.Ready, ResourcePlanner.Next(MakeBook(), holdings, "Bronze", 5).Kind);
		}

		[Fact]
		public void BronzeIsCraftedOnceTheBarsAreThere()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("Copper", 4);
			holdings.Put("Tin", 2);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Bronze", 2);
			Assert.Equal(StepKind.Craft, step.Kind);
			Assert.Equal("Bronze", step.Item);
		}

		[Fact]
		public void OreInTheChestsIsSmeltedBeforeMoreIsMined()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("CopperOre", 1);
			holdings.Put("TinOre", 5);
			holdings.Put("Coal", 10);
			holdings.Put("PickaxeAntler", 1);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Bronze", 2);
			Assert.Equal(StepKind.Smelt, step.Kind);
			Assert.Equal("Copper", step.Item);
			Assert.Equal(1, step.Amount);
		}

		[Fact]
		public void NoOreSendsVikingsMining()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("PickaxeAntler", 1);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Bronze", 1);
			Assert.Equal(StepKind.Quest, step.Kind);
			Assert.Equal(QuestKind.MineCopper, step.Quest);
			Assert.Equal("CopperOre", step.Item);
			Assert.Equal(2, step.Amount);
		}

		[Fact]
		public void TinIsMinedOnceThereIsCopper()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("PickaxeAntler", 1);
			holdings.Put("Copper", 2);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Bronze", 1);
			Assert.Equal(QuestKind.MineTin, step.Quest);
		}

		[Fact]
		public void AMeadowsVillageNeverGoesMining()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Biome = ResourceBook.Meadows;
			holdings.Put("PickaxeAntler", 1);
			Assert.Equal(StepKind.Impossible, ResourcePlanner.Next(MakeBook(), holdings, "Bronze", 1).Kind);
		}

		[Fact]
		public void MiningTakesASmelterAndAKiln()
		{
			VillageHoldings holdings = new VillageHoldings { Biome = ResourceBook.BlackForest };
			holdings.AddStation("forge", 1);
			holdings.AddStation(ResourceBook.Smelter, 1);
			holdings.Put("PickaxeAntler", 1);
			Assert.Equal(StepKind.Impossible, ResourcePlanner.Next(MakeBook(), holdings, "Bronze", 1).Kind);
		}

		[Fact]
		public void NoPickaxeMeansCraftingOne()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("HardAntler", 1);
			holdings.Put("Wood", 20);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Bronze", 1);
			Assert.Equal(StepKind.Craft, step.Kind);
			Assert.Equal("PickaxeAntler", step.Item);
		}

		[Fact]
		public void NoAntlerForAPickaxeMeansKillingEikthyr()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("Wood", 20);
			holdings.Put("TrophyDeer", 2);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Bronze", 1);
			Assert.Equal(StepKind.Quest, step.Kind);
			Assert.Equal(QuestKind.KillEikthyr, step.Quest);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void SummoningEikthyrTakesDeerTrophiesHuntedFirst(int trophies)
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("Wood", 20);
			holdings.Put("TrophyDeer", trophies);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Bronze", 1);
			Assert.Equal(StepKind.Quest, step.Kind);
			Assert.Equal(QuestKind.Hunt, step.Quest);
			Assert.Equal("TrophyDeer", step.Item);
		}

		[Fact]
		public void OreWithoutCoalBurnsCoalFirst()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("CopperOre", 3);
			holdings.Put("Wood", 5);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Copper", 3);
			Assert.Equal(StepKind.Smelt, step.Kind);
			Assert.Equal("Coal", step.Item);
			Assert.Equal(3, step.Amount);
		}

		[Fact]
		public void CoalWithoutWoodGathersWood()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("CopperOre", 3);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Copper", 3);
			Assert.Equal(StepKind.Gather, step.Kind);
			Assert.Equal("Wood", step.Item);
		}

		[Fact]
		public void SilverComesDownToRaidingCryptsForAnIronPickaxe()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Biome = ResourceBook.Mountain;
			holdings.Put("PickaxeAntler", 1);
			holdings.Put("Torch", 1);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Silver", 5);
			Assert.Equal(StepKind.Quest, step.Kind);
			Assert.Equal(QuestKind.RaidCrypts, step.Quest);
			Assert.Equal("IronScrap", step.Item);
		}

		[Fact]
		public void SilverIsMinedOnceThereIsAnIronPickaxe()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Biome = ResourceBook.Mountain;
			holdings.Put("PickaxeIron", 1);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Silver", 5);
			Assert.Equal(QuestKind.MineSilver, step.Quest);
		}

		[Fact]
		public void RaidingCryptsWithoutLightCraftsATorch()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Biome = ResourceBook.Swamp;
			holdings.Put("PickaxeAntler", 1);
			holdings.Put("Wood", 2);
			holdings.Put("Resin", 2);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Iron", 5);
			Assert.Equal(StepKind.Craft, step.Kind);
			Assert.Equal("Torch", step.Item);
		}

		[Fact]
		public void ACircletLightsTheWay()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Biome = ResourceBook.Swamp;
			holdings.Put("PickaxeAntler", 1);
			holdings.Put(ResourceBook.Circlet, 1);
			Assert.Equal(QuestKind.RaidCrypts, ResourcePlanner.Next(MakeBook(), holdings, "Iron", 5).Quest);
		}

		[Fact]
		public void AnIngredientThatCantBeGotStopsTheRest()
		{
			ResourceBook book = MakeBook();
			Craft(book, "Thing", null, 0, 1, ("Copper", 2), ("Unobtainium", 1));
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("PickaxeAntler", 1);
			PlanStep step = ResourcePlanner.Next(book, holdings, "Thing", 1);
			Assert.Equal(StepKind.Impossible, step.Kind);
			Assert.Equal("Unobtainium", step.Item);
		}

		[Fact]
		public void TwoNeedsDontCountTheSameItems()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("Copper", 3);
			holdings.Put("PickaxeAntler", 1);
			List<KeyValuePair<string, int>> needs = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("Copper", 2), new KeyValuePair<string, int>("Copper", 2) };
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, needs);
			Assert.Equal(StepKind.Quest, step.Kind);
			Assert.Equal("CopperOre", step.Item);
			Assert.Equal(1, step.Amount);
		}

		[Fact]
		public void AStationTheVillageLacksCantBeGot()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Put("station:forge:1", 1);
			List<KeyValuePair<string, int>> needs = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("station:forge:3", 1) };
			Assert.Equal(StepKind.Impossible, ResourcePlanner.Next(MakeBook(), holdings, needs).Kind);
			needs[0] = new KeyValuePair<string, int>("station:forge:1", 1);
			Assert.Equal(StepKind.Ready, ResourcePlanner.Next(MakeBook(), holdings, needs).Kind);
		}

		[Fact]
		public void HoneyMeansFindingBeesUntilThereAreEnoughHives()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Hives = ResourceBook.HivesWanted - 1;
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "Honey", 3);
			Assert.Equal(QuestKind.FindBees, step.Quest);
			holdings.Hives = ResourceBook.HivesWanted;
			step = ResourcePlanner.Next(MakeBook(), holdings, "Honey", 3);
			Assert.Equal(StepKind.Gather, step.Kind);
		}

		[Fact]
		public void ACircletTakesItsPriceInCoinsAndTreasure()
		{
			ResourceBook book = MakeBook();
			book.RuleOf(QuestKind.BuyCirclet).Coins = 620;
			VillageHoldings holdings = MakeHoldings();
			holdings.Biome = ResourceBook.Mountain;
			holdings.Coins = 600;
			Assert.Equal(StepKind.Impossible, ResourcePlanner.Next(book, holdings, ResourceBook.Circlet, 1).Kind);
			holdings.Coins = 620;
			Assert.Equal(QuestKind.BuyCirclet, ResourcePlanner.Next(book, holdings, ResourceBook.Circlet, 1).Quest);
		}

		[Fact]
		public void ARecipeLoopIsGivenUp()
		{
			ResourceBook book = new ResourceBook();
			Craft(book, "A", null, 0, 1, ("B", 1));
			Craft(book, "B", null, 0, 1, ("A", 1));
			Assert.Equal(StepKind.Impossible, ResourcePlanner.Next(book, new VillageHoldings(), "A", 1).Kind);
		}

		[Fact]
		public void AWayThatFailsGivesBackWhatItSpokeFor()
		{
			ResourceBook book = new ResourceBook();
			book.AddGathered();
			Craft(book, "Thing", null, 0, 1, ("Stone", 2), ("Unobtainium", 1));
			Craft(book, "Thing", null, 0, 1, ("Stone", 2));
			Craft(book, "Pair", null, 0, 1, ("Stone", 2), ("Thing", 1));
			VillageHoldings holdings = new VillageHoldings();
			holdings.Put("Stone", 4);
			PlanStep step = ResourcePlanner.Next(book, holdings, "Pair", 1);
			Assert.Equal(StepKind.Craft, step.Kind);
			Assert.Equal("Thing", step.Item);
		}

		[Fact]
		public void GatheringComesAfterWhatVikingsSetOutToDo()
		{
			PlanStep gather = PlanStep.Of(StepKind.Gather, "Wood", 3, null);
			PlanStep craft = PlanStep.Of(StepKind.Craft, "Torch", 1, null);
			PlanStep ready = PlanStep.Of(StepKind.Ready, "Stone", 0, null);
			Assert.Same(craft, ResourcePlanner.Prefer(gather, craft));
			Assert.Same(craft, ResourcePlanner.Prefer(craft, gather));
			Assert.Same(gather, ResourcePlanner.Prefer(gather, ready));
			Assert.Null(ResourcePlanner.Prefer(null, ready));
		}

		[Fact]
		public void WoodForAPickaxeWaitsOnTheIronForIt()
		{
			VillageHoldings holdings = MakeHoldings();
			holdings.Biome = ResourceBook.Swamp;
			holdings.Put("PickaxeAntler", 1);
			holdings.Put("Torch", 1);
			PlanStep step = ResourcePlanner.Next(MakeBook(), holdings, "PickaxeIron", 1);
			Assert.Equal(QuestKind.RaidCrypts, step.Quest);
		}

		[Theory]
		[InlineData(5, 2, 3)]
		[InlineData(4, 2, 2)]
		[InlineData(0, 1, 0)]
		[InlineData(3, 0, 3)]
		public void CraftsMakeEnough(int missing, int perCraft, int expected)
		{
			Assert.Equal(expected, ResourcePlanner.Crafts(missing, perCraft));
		}

		[Fact]
		public void ToolsAreKeptInOrderOfTier()
		{
			ResourceBook book = MakeBook();
			book.AddTool("PickaxeBronze", ToolKind.Pickaxe, 2);
			List<string> order = book.Tools.ConvertAll((ToolEntry tool) => tool.Prefab);
			Assert.Equal(new List<string> { "PickaxeAntler", "PickaxeBronze", "PickaxeIron" }, order);
		}

		[Fact]
		public void EveryQuestHasARule()
		{
			ResourceBook book = MakeBook();
			foreach (QuestKind kind in System.Enum.GetValues(typeof(QuestKind)))
			{
				if (kind != QuestKind.None)
				{
					Assert.NotNull(book.RuleOf(kind));
				}
			}
		}

		[Theory]
		[InlineData((int)QuestKind.Hunt, ResourceBook.Meadows, 1)]
		[InlineData((int)QuestKind.Hunt, ResourceBook.Mountain, 1)]
		[InlineData((int)QuestKind.Hunt, ResourceBook.Plains, 3)]
		[InlineData((int)QuestKind.Forage, ResourceBook.Plains, 2)]
		[InlineData((int)QuestKind.RaidCrypts, ResourceBook.Swamp, 3)]
		[InlineData((int)QuestKind.FindBees, ResourceBook.AshLands, 1)]
		public void QuestsTakeMoreVikingsFromThePlains(int kind, int biome, int expected)
		{
			Assert.Equal(expected, MakeBook().RuleOf((QuestKind)kind).VikingsFor(biome));
		}

		[Fact]
		public void OresAndScrapNeedAPickaxeAndTreesAnAxe()
		{
			ResourceBook book = MakeBook();
			foreach (string ore in new[] { "CopperOre", "TinOre", "SilverOre", "IronScrap" })
			{
				Assert.Contains(book.SourcesOf(ore), (ResourceSource source) => source.Tool == ToolKind.Pickaxe && source.ToolTarget != null);
			}
			foreach (string wood in new[] { "FineWood", "RoundLog", "ElderBark", "YggdrasilWood" })
			{
				Assert.Contains(book.SourcesOf(wood), (ResourceSource source) => source.Tool == ToolKind.Axe && source.ToolTarget != null);
			}
		}

		[Fact]
		public void WoodIsGatheredBeforeTreesAreGoneForFar()
		{
			List<ResourceSource> sources = MakeBook().SourcesOf("Wood");
			Assert.Equal(SourceKind.Gather, sources[0].Kind);
			Assert.Contains(sources, (ResourceSource source) => source.Quest == QuestKind.ChopTrees);
		}

		[Fact]
		public void YggdrasilWoodIsCutFromTheMistlandsOn()
		{
			ResourceBook book = MakeYggdrasilBook();
			VillageHoldings holdings = MakeHoldings();
			holdings.Biome = ResourceBook.Mistlands;
			holdings.Put("AxeBronze", 1);
			Assert.NotEqual(StepKind.Quest, ResourcePlanner.Next(book, holdings, "YggdrasilWood", 20).Kind);
			holdings.Put("AxeJotunBane", 1);
			PlanStep step = ResourcePlanner.Next(book, holdings, "YggdrasilWood", 20);
			Assert.Equal(StepKind.Quest, step.Kind);
			Assert.Equal(QuestKind.ChopTrees, step.Quest);
			Assert.Equal("YggdrasilWood", step.Item);
			holdings.Biome = ResourceBook.Plains;
			Assert.Equal(StepKind.Impossible, ResourcePlanner.Next(book, holdings, "YggdrasilWood", 20).Kind);
		}

		/// <summary>
		/// Makes a book whose axes are Jotun Bane, which cuts Yggdrasil shoots, and the bronze axe, which doesn't
		/// </summary>
		/// <returns>The book</returns>
		private static ResourceBook MakeYggdrasilBook()
		{
			ResourceBook book = MakeBook();
			book.AddTool("AxeBronze", ToolKind.Axe, 2);
			book.AddTool("AxeJotunBane", ToolKind.Axe, 4);
			foreach (ResourceSource source in book.SourcesOf("YggdrasilWood"))
			{
				source.ToolTier = 4;
			}
			return(book);
		}
	}
}
