namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a village's stock is gathered from its chests - which stacks of an item a chest's count takes in, how
	/// the slots are laid out in rows and the order the stacks are sorted in
	/// </summary>
	public class VillageStockTests
	{
		/// <summary>
		/// Checks that the same item of another quality, look or world level isn't counted with it
		/// </summary>
		[Fact]
		public void KeepsQualityLookAndWorldLevelApart()
		{
			string key = VillageStock.StackKey("ShieldWood", 1, 0, 0);
			Assert.Equal(key, VillageStock.StackKey("ShieldWood", 1, 0, 0));
			Assert.NotEqual(key, VillageStock.StackKey("ShieldWood", 2, 0, 0));
			Assert.NotEqual(key, VillageStock.StackKey("ShieldWood", 1, 1, 0));
			Assert.NotEqual(key, VillageStock.StackKey("ShieldWood", 1, 0, 1));
			Assert.NotEqual(key, VillageStock.StackKey("ShieldBronzeBuckler", 1, 0, 0));
			// A quality of 11 and a look of 0 isn't taken for a quality of 1 and a look of 10
			Assert.NotEqual(VillageStock.StackKey("ShieldWood", 11, 0, 0), VillageStock.StackKey("ShieldWood", 1, 10, 0));
		}

		/// <summary>
		/// Checks that the stock shows enough rows of 8 for all the chests' slots, and at least one
		/// </summary>
		/// <param name="count">How many slots the chests have</param>
		/// <param name="expected">How many rows it shows</param>
		[Theory]
		[InlineData(0, 1)]
		[InlineData(1, 1)]
		[InlineData(8, 1)]
		[InlineData(9, 2)]
		[InlineData(10, 2)]
		[InlineData(20, 3)]
		[InlineData(32, 4)]
		[InlineData(33, 5)]
		[InlineData(64, 8)]
		public void ShowsEnoughRows(int count, int expected)
		{
			Assert.Equal(expected, VillageStock.Rows(count));
		}

		/// <summary>
		/// Checks that the stacks fill each row of 8 from the left before the next
		/// </summary>
		/// <param name="index">The stack's place in the stock</param>
		/// <param name="x">Its column</param>
		/// <param name="y">Its row</param>
		[Theory]
		[InlineData(0, 0, 0)]
		[InlineData(7, 7, 0)]
		[InlineData(8, 0, 1)]
		[InlineData(11, 3, 1)]
		[InlineData(33, 1, 4)]
		public void FillsRowsFromTheLeft(int index, int x, int y)
		{
			Vector2i pos = VillageStock.GridPos(index);
			Assert.Equal(x, pos.x);
			Assert.Equal(y, pos.y);
		}

		/// <summary>
		/// Checks that stacks go by their kind before their name
		/// </summary>
		[Fact]
		public void SortsByKindFirst()
		{
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Material, "Wood", 1, 1, ItemDrop.ItemData.ItemType.Consumable, "Apple", 1, 50) < 0);
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Consumable, "Raspberries", 1, 50, ItemDrop.ItemData.ItemType.Material, "Bronze", 1, 1) > 0);
		}

		/// <summary>
		/// Checks that stacks of a kind go by name, whatever its case
		/// </summary>
		[Fact]
		public void SortsByNameIgnoringCase()
		{
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Material, "bronze", 1, 1, ItemDrop.ItemData.ItemType.Material, "Copper", 1, 50) < 0);
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Material, "Wood", 1, 50, ItemDrop.ItemData.ItemType.Material, "stone", 1, 1) > 0);
			Assert.Equal(0, VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Material, "WOOD", 1, 20, ItemDrop.ItemData.ItemType.Material, "wood", 1, 20));
		}

		/// <summary>
		/// Checks that stacks of the same name go higher quality first
		/// </summary>
		[Fact]
		public void SortsHigherQualityFirst()
		{
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Shield, "Wood shield", 3, 1, ItemDrop.ItemData.ItemType.Shield, "Wood shield", 1, 1) < 0);
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Shield, "Wood shield", 1, 1, ItemDrop.ItemData.ItemType.Shield, "wood Shield", 2, 1) > 0);
		}

		/// <summary>
		/// Checks that stacks of the same item and quality go fullest first
		/// </summary>
		[Fact]
		public void SortsFullestStackFirst()
		{
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Material, "Wood", 1, 50, ItemDrop.ItemData.ItemType.Material, "Wood", 1, 12) < 0);
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Material, "Wood", 1, 3, ItemDrop.ItemData.ItemType.Material, "wood", 1, 40) > 0);
		}
	}
}
