using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
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

		[Fact]
		public void ForageGrowsInTheVillagesBiomeAndThoseBefore()
		{
			Assert.Equal(new List<string> { "Raspberry", "Mushroom", "Dandelion" }, QuestLoot.ForageFor(ResourceBook.Meadows));
			Assert.Contains("Blueberries", QuestLoot.ForageFor(ResourceBook.Swamp));
			Assert.Contains("Cloudberry", QuestLoot.ForageFor(ResourceBook.AshLands));
			Assert.DoesNotContain("Cloudberry", QuestLoot.ForageFor(ResourceBook.Mountain));
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
