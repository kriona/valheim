using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests that no boss's trophy is among what quests bring home
	/// </summary>
	public class QuestTrophyTests
	{
		[Fact]
		public void EveryBossTrophyIsLeftBehind()
		{
			Assert.Contains("TrophyEikthyr", QuestLoot.BossTrophies);
			Assert.Equal(7, QuestLoot.BossTrophies.Length);
			Assert.Null(ResourceBookTrophySource());
		}

		/// <summary>
		/// Finds a quest that brings back a boss's trophy in the quests' list of what they bring back
		/// </summary>
		/// <returns>The trophy, or null when no quest brings one back</returns>
		private static string ResourceBookTrophySource()
		{
			ResourceBook book = new ResourceBook();
			book.AddQuests();
			foreach (string trophy in QuestLoot.BossTrophies)
			{
				if (book.SourcesOf(trophy).Count > 0)
				{
					return(trophy);
				}
			}
			return(null);
		}
	}

	/// <summary>
	/// Tests what each quest brings back - what it kills, opens and picks, what trading with Haldor gets, and the skill
	/// what's found goes by
	/// </summary>
	public class QuestLootTests
	{
		[Fact]
		public void SellingTreasureBringsItsWorthInCoins()
		{
			List<KeyValuePair<string, int>> loot = QuestLoot.Trade(QuestKind.SellTreasure, 5, 120, 620, out bool spends);
			Assert.True(spends);
			Assert.Equal(new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(ResourceBook.Coins, 125) }, loot);
		}

		[Fact]
		public void SellingNothingSpendsNothing()
		{
			Assert.Empty(QuestLoot.Trade(QuestKind.SellTreasure, 50, 0, 620, out bool spends));
			Assert.False(spends);
		}

		[Fact]
		public void ACircletIsBoughtWithCoinsAndTreasureAndTheRestComesBack()
		{
			List<KeyValuePair<string, int>> loot = QuestLoot.Trade(QuestKind.BuyCirclet, 500, 200, 620, out bool spends);
			Assert.True(spends);
			Assert.Contains(new KeyValuePair<string, int>(ResourceBook.Circlet, 1), loot);
			Assert.Contains(new KeyValuePair<string, int>(ResourceBook.Coins, 80), loot);
		}

		[Fact]
		public void ACircletTooDearIsNotBought()
		{
			Assert.Empty(QuestLoot.Trade(QuestKind.BuyCirclet, 500, 100, 620, out bool spends));
			Assert.False(spends);
		}

		[Fact]
		public void ACircletPaidExactlyLeavesNoCoins()
		{
			List<KeyValuePair<string, int>> loot = QuestLoot.Trade(QuestKind.BuyCirclet, 620, 0, 620, out bool _);
			Assert.Single(loot);
		}

		[Theory]
		[InlineData(2f, 2, 1, 2)]
		[InlineData(2f, 2, 3, 6)]
		[InlineData(0.5f, 1, 1, 1)]
		[InlineData(0.5f, 1, 4, 2)]
		[InlineData(0f, 1, 5, 1)]
		public void MoreVikingsKillAndOpenMore(float each, int least, int vikings, int expected)
		{
			Assert.Equal(expected, new QuestLoot.LootSource("X", each, least, true).CountFor(vikings));
		}

		[Fact]
		public void RaidsHaveChestsAndEnemies()
		{
			Assert.Contains(QuestLoot.Sources(QuestKind.RaidBurialChambers), (QuestLoot.LootSource source) => !source.Kill);
			Assert.Contains(QuestLoot.Sources(QuestKind.RaidBurialChambers), (QuestLoot.LootSource source) => source.Kill);
			Assert.Contains(QuestLoot.Sources(QuestKind.RaidCrypts), (QuestLoot.LootSource source) => source.Prefab == "TreasureChest_sunkencrypt");
			Assert.Single(QuestLoot.Sources(QuestKind.KillEikthyr));
			Assert.Empty(QuestLoot.Sources(QuestKind.MineCopper));
		}

		/// <summary>
		/// Checks that burial chamber and sunken crypt raids pick the yellow mushrooms growing there, rather than killing
		/// them
		/// </summary>
		[Fact]
		public void RaidsPickYellowMushrooms()
		{
			Assert.Contains(QuestLoot.Sources(QuestKind.RaidBurialChambers), (QuestLoot.LootSource source) => source.Prefab == "Pickable_Mushroom_yellow" && !source.Kill);
			Assert.Contains(QuestLoot.Sources(QuestKind.RaidCrypts), (QuestLoot.LootSource source) => source.Prefab == "Pickable_Mushroom_yellow" && !source.Kill);
		}

		/// <summary>
		/// Checks that each viking on a burial chamber or sunken crypt raid picks 2 yellow mushrooms, however many go,
		/// for its Farming skill
		/// </summary>
		/// <param name="vikings">How many went</param>
		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(3)]
		public void EachRaiderPicksTwoMushrooms(int vikings)
		{
			for (int i = 0; i < vikings; i++)
			{
				Assert.Equal(2, QuestLoot.PlantsPicked(QuestKind.RaidBurialChambers, vikings, i));
				Assert.Equal(2, QuestLoot.PlantsPicked(QuestKind.RaidCrypts, vikings, i));
			}
		}

		/// <summary>
		/// Checks that a quest with no plants to pick besides foraging, or a viking who isn't on the quest, picks none
		/// </summary>
		[Fact]
		public void PicksNoneWithoutPlantsOrAPlace()
		{
			Assert.Equal(0, QuestLoot.PlantsPicked(QuestKind.HuntTrolls, 3, 0));
			Assert.Equal(0, QuestLoot.PlantsPicked(QuestKind.Forage, 2, 0));
			Assert.Equal(0, QuestLoot.PlantsPicked(QuestKind.RaidBurialChambers, 2, -1));
			Assert.Equal(0, QuestLoot.PlantsPicked(QuestKind.RaidBurialChambers, 0, 0));
		}

		/// <summary>
		/// Counts how many of a prey a hunter takes in its time, the way a hunt does
		/// </summary>
		/// <param name="prey">The prey's prefab name</param>
		/// <param name="most">The most prey's time the hunter spends</param>
		/// <returns>How many it takes</returns>
		private static int TakenInTime(string prey, int most)
		{
			float spent = 0f;
			int taken = 0;
			while (spent < most - 0.01f && QuestLoot.HasTimeFor(spent, QuestLoot.EffortOf(prey), most, taken))
			{
				spent += QuestLoot.EffortOf(prey);
				taken++;
			}
			return(taken);
		}

		[Theory]
		[InlineData("Boar", 8, 12)]
		[InlineData("Deer", 3, 4)]
		[InlineData("Crow", 8, 8)]
		[InlineData("Wolf", 8, 8)]
		[InlineData("Lox", 8, 3)]
		[InlineData("Lox", 3, 1)]
		[InlineData("Moose", 8, 4)]
		[InlineData("Seal", 3, 1)]
		public void SomePreyAreTakenMoreOrLessOften(string prey, int most, int expected)
		{
			Assert.Equal(expected, TakenInTime(prey, most));
		}

		/// <summary>
		/// Checks whether an item is meat, for the tests' made-up drops
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <returns>True when it ends in "Meat"</returns>
		private static bool IsMeat(string item)
		{
			return(item.EndsWith("Meat"));
		}

		[Fact]
		public void FoodIsWhatAKillDropsToEatOnAverage()
		{
			LootTable deer = new LootTable { Drops = new List<LootDrop> { new LootDrop { Item = "DeerMeat", Min = 2, Max = 2 }, new LootDrop { Item = "DeerHide", Min = 1, Max = 3 } } };
			LootTable neck = new LootTable { Drops = new List<LootDrop> { new LootDrop { Item = "NeckMeat", Min = 1, Max = 1, Chance = 0.7f } } };
			LootTable moose = new LootTable { Drops = new List<LootDrop> { new LootDrop { Item = "MooseMeat", Min = 4, Max = 6 } } };
			Assert.Equal(2f, QuestLoot.FoodOf(deer, IsMeat), 3);
			Assert.Equal(0.7f, QuestLoot.FoodOf(neck, IsMeat), 3);
			Assert.Equal(5f, QuestLoot.FoodOf(moose, IsMeat), 3);
			Assert.Equal(0f, QuestLoot.FoodOf(new LootTable(), IsMeat), 3);
		}

		[Theory]
		[InlineData(0.0, 0)]
		[InlineData(0.53, 0)]
		[InlineData(0.55, 1)]
		[InlineData(0.99, 2)]
		[InlineData(1.0, 2)]
		public void FoodHuntsTakeTheMeatierPreyMoreOften(double roll, int expected)
		{
			// Deer 2, boar 1, necks 0.7 - deer take 2 in 3.7 of the rolls
			Assert.Equal(expected, QuestLoot.PickWeighted(new List<float> { 2f, 1f, 0.7f }, 3, roll));
		}

		[Theory]
		[InlineData(0.0, 0)]
		[InlineData(0.5, 1)]
		[InlineData(0.99, 1)]
		public void PreyAreTakenEvenlyWithoutWeights(double roll, int expected)
		{
			Assert.Equal(expected, QuestLoot.PickWeighted(null, 2, roll));
			Assert.Equal(expected, QuestLoot.PickWeighted(new List<float> { 0f, 0f }, 2, roll));
		}

		/// <summary>
		/// Checks what a few biomes' birds give - feathers in the Meadows, the Black Forest, the Plains and the Ashlands
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <param name="item">The item, or "" for food</param>
		/// <returns>True when the biome's birds give it</returns>
		private static bool BirdsGive(int biome, string item)
		{
			bool birds = (biome == ResourceBook.Meadows || biome == ResourceBook.BlackForest || biome == ResourceBook.Plains || biome == ResourceBook.AshLands);
			return(item == "Feathers" && birds);
		}

		[Fact]
		public void FeathersCanBeHuntedWhereverThereAreBirds()
		{
			Assert.Equal(new List<int> { ResourceBook.Meadows, ResourceBook.BlackForest, ResourceBook.Plains }, QuestLoot.TargetBiomes("Feathers", ResourceBook.Mistlands, BirdsGive));
			Assert.Equal(new List<int> { ResourceBook.Meadows }, QuestLoot.TargetBiomes("Feathers", ResourceBook.Meadows, BirdsGive));
		}

		[Fact]
		public void HuntingForFoodHasOneBiome()
		{
			Assert.Equal(new List<int> { ResourceBook.Plains }, QuestLoot.TargetBiomes("", ResourceBook.AshLands, PreyGives));
			Assert.Equal(new List<int> { ResourceBook.Swamp }, QuestLoot.TargetBiomes("Nothing", ResourceBook.Swamp, PreyGives));
		}

		[Fact]
		public void ForagingPicksOnlyWhatItsFor()
		{
			Assert.Equal(new List<string> { "Thistle" }, QuestLoot.PlantsFor(ResourceBook.BlackForest, "Thistle"));
			Assert.Empty(QuestLoot.PlantsFor(ResourceBook.Meadows, "Thistle"));
			Assert.Empty(QuestLoot.PlantsFor(ResourceBook.Swamp, ""));
		}

		[Fact]
		public void ForagingForFoodLeavesWhatsNoFood()
		{
			Assert.Equal(new List<string> { "Raspberry", "Mushroom" }, QuestLoot.PlantsFor(ResourceBook.Meadows, ""));
			Assert.Equal(new List<string> { "Blueberries", "MushroomYellow" }, QuestLoot.PlantsFor(ResourceBook.BlackForest, ""));
		}

		[Theory]
		[InlineData("Raspberry", ResourceBook.Plains, ResourceBook.Meadows)]
		[InlineData("Thistle", ResourceBook.Mountain, ResourceBook.BlackForest)]
		[InlineData("Cloudberry", ResourceBook.AshLands, ResourceBook.Plains)]
		[InlineData("", ResourceBook.Meadows, ResourceBook.Meadows)]
		[InlineData("", ResourceBook.Mountain, ResourceBook.BlackForest)]
		[InlineData("", ResourceBook.Plains, ResourceBook.Plains)]
		[InlineData("", ResourceBook.DeepNorth, ResourceBook.Plains)]
		public void ForagingGoesWhereWhatItsForGrows(string item, int village, int expected)
		{
			Assert.Equal(expected, QuestLoot.BiomeFor(QuestKind.Forage, item, village));
		}

		/// <summary>
		/// Checks what a few biomes' prey give - boar's leather scraps in the Meadows and wolves' pelts in the Mountains,
		/// and meat in the Meadows, the Mountains and the Plains
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <param name="item">The item, or "" for food</param>
		/// <returns>True when the biome's prey give it</returns>
		private static bool PreyGives(int biome, string item)
		{
			if (item.Length == 0)
			{
				return(biome == ResourceBook.Meadows || biome == ResourceBook.Mountain || biome == ResourceBook.Plains);
			}
			return((item == "LeatherScraps" && biome == ResourceBook.Meadows) || (item == "WolfPelt" && biome == ResourceBook.Mountain));
		}

		[Fact]
		public void HuntingForAnItemGoesToTheEasiestBiomeWithIt()
		{
			Assert.Equal(ResourceBook.Meadows, QuestLoot.TargetBiome("LeatherScraps", ResourceBook.Plains, PreyGives));
			Assert.Equal(ResourceBook.Mountain, QuestLoot.TargetBiome("WolfPelt", ResourceBook.Plains, PreyGives));
			Assert.Equal(ResourceBook.Swamp, QuestLoot.TargetBiome("WolfPelt", ResourceBook.Swamp, PreyGives));
		}

		[Fact]
		public void HuntingForFoodGoesToTheHardestBiomeWithIt()
		{
			Assert.Equal(ResourceBook.Plains, QuestLoot.TargetBiome("", ResourceBook.AshLands, PreyGives));
			Assert.Equal(ResourceBook.Mountain, QuestLoot.TargetBiome("", ResourceBook.Mountain, PreyGives));
			Assert.Equal(ResourceBook.Meadows, QuestLoot.TargetBiome("", ResourceBook.Swamp, PreyGives));
		}

		[Fact]
		public void HuntingAndForagingGoBySkill()
		{
			Assert.Equal(Skills.SkillType.Bows, QuestLoot.FindSkill(QuestKind.Hunt));
			Assert.Equal(Skills.SkillType.Farming, QuestLoot.FindSkill(QuestKind.Forage));
			Assert.Equal(Skills.SkillType.None, QuestLoot.FindSkill(QuestKind.RaidCrypts));
		}
	}
}
