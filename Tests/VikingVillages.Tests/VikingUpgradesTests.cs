using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the strength belt and pockets a living viking starts with by its biome, the pockets Haldor can sew for it,
	/// and the belt a Mistlands viking wears for a quest outside the mist
	/// </summary>
	public class VikingUpgradesTests
	{
		[Theory]
		[InlineData(ResourceBook.Meadows, 0f)]
		[InlineData(ResourceBook.BlackForest, 0.2f)]
		[InlineData(ResourceBook.Swamp, 0.4f)]
		[InlineData(ResourceBook.Mountain, 0.6f)]
		[InlineData(ResourceBook.Plains, 0.8f)]
		[InlineData(ResourceBook.Mistlands, 1f)]
		[InlineData(ResourceBook.AshLands, 1f)]
		[InlineData(ResourceBook.DeepNorth, 1f)]
		public void BeltsAreLikelierTheHarderTheBiome(int biome, float chance)
		{
			Assert.Equal(chance, VikingUpgrades.ChanceOf(VikingUpgrades.BeltChances, biome));
			Assert.Equal(chance > 0f, VikingUpgrades.LivingBelt(biome, 0f));
			Assert.False(VikingUpgrades.LivingBelt(biome, chance));
		}

		[Theory]
		[InlineData(ResourceBook.Swamp, 0f, 0f)]
		[InlineData(ResourceBook.Mountain, 0.2f, 0f)]
		[InlineData(ResourceBook.Plains, 0.4f, 0f)]
		[InlineData(ResourceBook.Mistlands, 0.6f, 0.2f)]
		[InlineData(ResourceBook.AshLands, 0.8f, 0.4f)]
		[InlineData(ResourceBook.DeepNorth, 1f, 0.6f)]
		public void PocketsAreLikelierTheHarderTheBiome(int biome, float wider, float deeper)
		{
			Assert.Equal(wider, VikingUpgrades.ChanceOf(VikingUpgrades.WiderPocketsChances, biome));
			Assert.Equal(deeper, VikingUpgrades.ChanceOf(VikingUpgrades.DeeperPocketsChances, biome));
		}

		[Fact]
		public void DeeperPocketsAreNeverLikelierThanWiderOnes()
		{
			for (int biome = ResourceBook.Meadows; biome <= ResourceBook.DeepNorth; biome++)
			{
				Assert.True(VikingUpgrades.ChanceOf(VikingUpgrades.DeeperPocketsChances, biome) <= VikingUpgrades.ChanceOf(VikingUpgrades.WiderPocketsChances, biome));
			}
		}

		[Theory]
		[InlineData(0.1f, VikingUpgrades.DeeperRows)]
		[InlineData(0.3f, VikingUpgrades.WiderRows)]
		[InlineData(0.59f, VikingUpgrades.WiderRows)]
		[InlineData(0.6f, VikingUpgrades.PlainRows)]
		public void AMistlandsVikingsRowsComeFromOneRoll(float roll, int rows)
		{
			Assert.Equal(rows, VikingUpgrades.LivingRows(ResourceBook.Mistlands, roll));
		}

		[Fact]
		public void EachPocketChanceHoldsOnItsOwn()
		{
			// Over evenly spread rolls, as many vikings have each kind of pocket as its table says
			int wider = 0;
			int deeper = 0;
			const int rolls = 1000;
			for (int i = 0; i < rolls; i++)
			{
				int rows = VikingUpgrades.LivingRows(ResourceBook.AshLands, (i + 0.5f) / rolls);
				wider += ((rows >= VikingUpgrades.WiderRows) ? 1 : 0);
				deeper += ((rows >= VikingUpgrades.DeeperRows) ? 1 : 0);
			}
			Assert.Equal(800, wider);
			Assert.Equal(400, deeper);
		}

		[Fact]
		public void AVikingOfTheEasierBiomesHasNoPockets()
		{
			Assert.Equal(VikingUpgrades.PlainRows, VikingUpgrades.LivingRows(ResourceBook.Meadows, 0f));
			Assert.Equal(VikingUpgrades.PlainRows, VikingUpgrades.LivingRows(ResourceBook.Swamp, 0f));
			Assert.Equal(VikingUpgrades.DeeperRows, VikingUpgrades.LivingRows(ResourceBook.DeepNorth, 0.59f));
			Assert.Equal(VikingUpgrades.WiderRows, VikingUpgrades.LivingRows(ResourceBook.DeepNorth, 0.99f));
		}

		[Theory]
		[InlineData((int)QuestKind.BuyWiderPockets, VikingUpgrades.PlainRows, false, true)]
		[InlineData((int)QuestKind.BuyWiderPockets, VikingUpgrades.WiderRows, true, false)]
		[InlineData((int)QuestKind.BuyWiderPockets, VikingUpgrades.DeeperRows, true, false)]
		[InlineData((int)QuestKind.BuyDeeperPockets, VikingUpgrades.PlainRows, false, false)]
		[InlineData((int)QuestKind.BuyDeeperPockets, VikingUpgrades.WiderRows, false, true)]
		[InlineData((int)QuestKind.BuyDeeperPockets, VikingUpgrades.DeeperRows, true, false)]
		[InlineData((int)QuestKind.BuyBelt, VikingUpgrades.PlainRows, false, false)]
		public void DeeperPocketsAreSewnOnlyOntoWiderOnes(int kind, int rows, bool has, bool canTake)
		{
			Assert.Equal(has, VikingUpgrades.HasPockets((QuestKind)kind, rows));
			Assert.Equal(canTake, VikingUpgrades.CanTakePockets((QuestKind)kind, rows));
		}

		[Theory]
		[InlineData(true, true, 0f, 150f, 150f)]
		[InlineData(true, false, 0f, 150f, 0f)]
		[InlineData(false, true, 150f, 150f, 0f)]
		[InlineData(true, true, 0f, 0f, 0f)]
		public void AMistlandsVikingWearsItsBeltOnlyOutsideTheMist(bool wornWisp, bool leavesMist, float wornAdds, float carriedAdds, float bonus)
		{
			Assert.Equal(bonus, VikingUpgrades.QuestCarryBonus(wornWisp, leavesMist, wornAdds, carriedAdds));
		}
	}
}
