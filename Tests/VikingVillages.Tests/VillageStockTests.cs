using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a village's stock is gathered from its chests - which items share a stack, how the stacks are laid out
	/// in rows and the order they're sorted in
	/// </summary>
	public class VillageStockTests
	{
		/// <summary>
		/// Makes an item found in a chest, for gathering into the stock
		/// </summary>
		/// <param name="key">Its key, or null when it doesn't stack</param>
		/// <param name="stack">How many it is</param>
		/// <returns>The entry</returns>
		private static KeyValuePair<string, int> Entry(string key, int stack)
		{
			return(new KeyValuePair<string, int>(key, stack));
		}

		/// <summary>
		/// Checks that the same item from different chests is gathered into one stack, in the place it was first found,
		/// noting each it came from
		/// </summary>
		[Fact]
		public void MergesTheSameItem()
		{
			string wood = VillageStock.StackKey("Wood", 1, 0, 0);
			string stone = VillageStock.StackKey("Stone", 1, 0, 0);
			List<VillageStock.Group> groups = VillageStock.Merge(new List<KeyValuePair<string, int>> { Entry(wood, 30), Entry(stone, 10), Entry(wood, 40) });
			Assert.Equal(2, groups.Count);
			Assert.Equal(0, groups[0].First);
			Assert.Equal(70, groups[0].Stack);
			Assert.Equal(new List<int> { 0, 2 }, groups[0].Members);
			Assert.Equal(1, groups[1].First);
			Assert.Equal(10, groups[1].Stack);
			Assert.Equal(new List<int> { 1 }, groups[1].Members);
		}

		/// <summary>
		/// Checks that a merged stack holds them all, however many more than a stack of them holds in a chest
		/// </summary>
		[Fact]
		public void MergesPastAFullStack()
		{
			string wood = VillageStock.StackKey("Wood", 1, 0, 0);
			List<VillageStock.Group> groups = VillageStock.Merge(new List<KeyValuePair<string, int>> { Entry(wood, 50), Entry(wood, 50), Entry(wood, 50) });
			Assert.Single(groups);
			Assert.Equal(150, groups[0].Stack);
		}

		/// <summary>
		/// Checks that items that don't stack each get a stack of their own, even of the same item
		/// </summary>
		[Fact]
		public void KeepsUnstackableItemsApart()
		{
			List<VillageStock.Group> groups = VillageStock.Merge(new List<KeyValuePair<string, int>> { Entry(null, 1), Entry(null, 1), Entry("Wood|1|0|0", 5) });
			Assert.Equal(3, groups.Count);
			Assert.All(groups, (VillageStock.Group group) => Assert.Single(group.Members));
			Assert.Equal(1, groups[1].First);
			Assert.Equal(1, groups[1].Stack);
			Assert.Equal(5, groups[2].Stack);
		}

		/// <summary>
		/// Checks that nothing found makes no stacks
		/// </summary>
		[Fact]
		public void MergesNothingIntoNothing()
		{
			Assert.Empty(VillageStock.Merge(new List<KeyValuePair<string, int>>()));
		}

		/// <summary>
		/// Checks that the same item of another quality, look or world level gets a stack of its own
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
			List<VillageStock.Group> groups = VillageStock.Merge(new List<KeyValuePair<string, int>> { Entry(key, 1), Entry(VillageStock.StackKey("ShieldWood", 2, 0, 0), 1), Entry(VillageStock.StackKey("ShieldWood", 1, 1, 0), 1), Entry(VillageStock.StackKey("ShieldWood", 1, 0, 1), 1) });
			Assert.Equal(4, groups.Count);
		}

		/// <summary>
		/// Checks that the stock shows enough rows of 8 for all its stacks, and never fewer than 4
		/// </summary>
		/// <param name="count">How many stacks it has</param>
		/// <param name="expected">How many rows it shows</param>
		[Theory]
		[InlineData(0, 4)]
		[InlineData(8, 4)]
		[InlineData(9, 4)]
		[InlineData(32, 4)]
		[InlineData(33, 5)]
		[InlineData(40, 5)]
		[InlineData(41, 6)]
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
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Material, "Wood", 1, ItemDrop.ItemData.ItemType.Consumable, "Apple", 1) < 0);
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Consumable, "Raspberries", 1, ItemDrop.ItemData.ItemType.Material, "Bronze", 1) > 0);
		}

		/// <summary>
		/// Checks that stacks of a kind go by name, whatever its case
		/// </summary>
		[Fact]
		public void SortsByNameIgnoringCase()
		{
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Material, "bronze", 1, ItemDrop.ItemData.ItemType.Material, "Copper", 1) < 0);
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Material, "Wood", 1, ItemDrop.ItemData.ItemType.Material, "stone", 1) > 0);
			Assert.Equal(0, VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Material, "WOOD", 1, ItemDrop.ItemData.ItemType.Material, "wood", 1));
		}

		/// <summary>
		/// Checks that stacks of the same name go higher quality first
		/// </summary>
		[Fact]
		public void SortsHigherQualityFirst()
		{
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Shield, "Wood shield", 3, ItemDrop.ItemData.ItemType.Shield, "Wood shield", 1) < 0);
			Assert.True(VillageStock.CompareStock(ItemDrop.ItemData.ItemType.Shield, "Wood shield", 1, ItemDrop.ItemData.ItemType.Shield, "wood Shield", 2) > 0);
		}
	}
}
