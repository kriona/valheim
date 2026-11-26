using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rules of a village's cart - which villages keep one, what becomes of it wherever it ends up, which sides
	/// of a house it's parked beside, and how a quest loads it
	/// </summary>
	public class CartRulesTests
	{
		private const double Day = 1800.0;

		/// <summary>
		/// The weight of each item the loading tests use - heavy ore, wood, and light trophies and feathers
		/// </summary>
		private static readonly Dictionary<string, float> Weights = new Dictionary<string, float>
		{
			{ "Ore", 10f },
			{ "Wood", 2f },
			{ "Trophy", 1f },
			{ "Feather", 0.1f }
		};

		/// <summary>
		/// How many of each item the loading tests use make a stack
		/// </summary>
		private static readonly Dictionary<string, int> Stacks = new Dictionary<string, int>
		{
			{ "Ore", 30 },
			{ "Wood", 50 },
			{ "Trophy", 20 },
			{ "Feather", 100 }
		};

		[Theory]
		[InlineData(ResourceBook.Meadows, false)]
		[InlineData(ResourceBook.BlackForest, true)]
		[InlineData(ResourceBook.Swamp, true)]
		[InlineData(ResourceBook.Mountain, true)]
		[InlineData(ResourceBook.Plains, true)]
		[InlineData(ResourceBook.Mistlands, true)]
		[InlineData(ResourceBook.AshLands, false)]
		[InlineData(ResourceBook.DeepNorth, true)]
		public void MeadowsAndAshlandsKeepNoCart(int biome, bool expected)
		{
			Assert.Equal(expected, CartRules.Allowed(biome));
		}

		[Fact]
		public void AVillageThatKeepsNoneHasNone()
		{
			Assert.Equal(CartRules.CartState.None, CartRules.Decide(false, false, true, true, false, true, false, true, 0, Day));
		}

		[Fact]
		public void NoCartNotedIsMissing()
		{
			Assert.Equal(CartRules.CartState.Missing, CartRules.Decide(true, false, false, false, false, false, false, false, 0, Day));
		}

		[Fact]
		public void ACartOutOnAQuestIsntMissing()
		{
			Assert.Equal(CartRules.CartState.OnQuest, CartRules.Decide(true, true, false, false, false, false, false, false, 0, Day));
		}

		[Fact]
		public void ACartUnknownWhereItWasLastSeenWasDestroyed()
		{
			Assert.Equal(CartRules.CartState.Missing, CartRules.Decide(true, false, true, false, true, false, false, false, 0, Day));
		}

		[Fact]
		public void ACartOutOfReachIsAwayUntilADayHasPassed()
		{
			Assert.Equal(CartRules.CartState.Away, CartRules.Decide(true, false, true, false, false, false, false, false, Day - 1, Day));
			Assert.Equal(CartRules.CartState.Away, CartRules.Decide(true, false, true, true, false, false, false, false, 10, Day));
			Assert.Equal(CartRules.CartState.Missing, CartRules.Decide(true, false, true, true, false, false, false, false, Day, Day));
		}

		[Fact]
		public void APlayerUsingItKeepsTheVikingsAway()
		{
			Assert.Equal(CartRules.CartState.InUse, CartRules.Decide(true, false, true, true, false, true, true, false, 0, Day));
		}

		[Fact]
		public void ALoadedCartIsHomeInsideAndAstrayOutside()
		{
			Assert.Equal(CartRules.CartState.Home, CartRules.Decide(true, false, true, true, false, true, false, true, 0, Day));
			Assert.Equal(CartRules.CartState.Astray, CartRules.Decide(true, false, true, true, false, true, false, false, 0, Day));
		}

		[Fact]
		public void TheBackOfTheHouseComesFirstThenItsSides()
		{
			List<Vector2> sides = CartRules.Sides(new Vector2(0f, 3f), true);
			Assert.Equal(3, sides.Count);
			AssertNear(new Vector2(0f, -1f), sides[0]);
			AssertNear(new Vector2(-1f, 0f), sides[1]);
			AssertNear(new Vector2(1f, 0f), sides[2]);
		}

		[Fact]
		public void TheRightSideCanComeBeforeTheLeft()
		{
			List<Vector2> sides = CartRules.Sides(new Vector2(0f, 1f), false);
			AssertNear(new Vector2(1f, 0f), sides[1]);
			AssertNear(new Vector2(-1f, 0f), sides[2]);
		}

		[Fact]
		public void TheDoorsSideIsNeverTried()
		{
			foreach (Vector2 side in CartRules.Sides(new Vector2(2f, 0f), true))
			{
				Assert.True(Vector2.Dot(side, new Vector2(1f, 0f)) < 0.5f);
			}
		}

		[Fact]
		public void AHouseWithNoDoorHasEverySideTried()
		{
			List<Vector2> sides = CartRules.Sides(Vector2.zero, true);
			Assert.Equal(4, sides.Count);
		}

		[Fact]
		public void TheCartStandsPastTheWallsAndItsGap()
		{
			Assert.Equal(6.6f, CartRules.SideDistance(4f, 0.6f, 2f), 3);
		}

		[Fact]
		public void ASpotNearADoorBlocksIt()
		{
			List<Vector2> doors = new List<Vector2> { new Vector2(0f, 0f), new Vector2(10f, 0f) };
			Assert.True(CartRules.BlocksDoor(new Vector2(9f, 0f), doors, 2f));
			Assert.False(CartRules.BlocksDoor(new Vector2(5f, 0f), doors, 2f));
			Assert.False(CartRules.BlocksDoor(new Vector2(5f, 0f), new List<Vector2>(), 2f));
		}

		[Fact]
		public void AnEmptyCartDoesntSlowTheTrip()
		{
			Assert.Equal(1f, CartRules.Slowdown(0f), 3);
			Assert.Equal(1f + CartRules.FullSlowdown, CartRules.Slowdown(CartRules.FullWeight), 3);
			Assert.True(CartRules.Slowdown(CartRules.FullWeight * 100f) <= 1.5f);
		}

		[Theory]
		[InlineData(1, 1)]
		[InlineData(20, 1)]
		[InlineData(50, 2)]
		[InlineData(100, 5)]
		[InlineData(999, 5)]
		public void LightThingsGoInSmallStacks(int maxStack, int expected)
		{
			Assert.Equal(expected, CartRules.SmallStack(maxStack));
		}

		[Fact]
		public void TheCartsRoomIsSharedOutAmongTheVikings()
		{
			List<CarrySpace> spaces = new List<CarrySpace> { new CarrySpace { WeightRoom = 100f, FreeSlots = 4 }, new CarrySpace { WeightRoom = 50f, FreeSlots = 2 } };
			List<CarrySpace> pooled = CartRules.Pooled(spaces, 7);
			Assert.Equal(100f + CartRules.FullWeight / 4f, pooled[0].WeightRoom, 3);
			Assert.Equal(50f + CartRules.FullWeight / 4f, pooled[1].WeightRoom, 3);
			Assert.Equal(8, pooled[0].FreeSlots);
			Assert.Equal(5, pooled[1].FreeSlots);
			Assert.Equal(100f, spaces[0].WeightRoom);
		}

		[Fact]
		public void TheCartIsntNeededWhenTheVikingsCarryEverything()
		{
			List<KeyValuePair<string, int>> found = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("Ore", 5) };
			List<CarrySpace> spaces = new List<CarrySpace> { new CarrySpace { WeightRoom = 300f, FreeSlots = 10 } };
			List<Dictionary<string, int>> shares = CartRules.ShareWithCart(found, "Ore", spaces, 18, WeightOf, StackOf, out Dictionary<string, int> cart);
			Assert.Equal(5, shares[0]["Ore"]);
			Assert.Empty(cart);
		}

		[Fact]
		public void VikingsCarryTheHeaviestThingsFirst()
		{
			List<KeyValuePair<string, int>> found = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("Trophy", 10), new KeyValuePair<string, int>("Ore", 10) };
			List<CarrySpace> spaces = new List<CarrySpace> { new CarrySpace { WeightRoom = 100f, FreeSlots = 10 } };
			List<Dictionary<string, int>> shares = CartRules.ShareWithCart(found, "", spaces, 18, WeightOf, StackOf, out Dictionary<string, int> cart);
			Assert.Equal(10, shares[0]["Ore"]);
			Assert.False(shares[0].ContainsKey("Trophy"));
			Assert.Equal(1, cart["Trophy"]);
		}

		[Fact]
		public void TheCartTakesHeavyThingsToHalfItsWeightThenLightOnesInSmallStacks()
		{
			List<KeyValuePair<string, int>> found = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("Ore", 200), new KeyValuePair<string, int>("Trophy", 10), new KeyValuePair<string, int>("Feather", 40) };
			List<CarrySpace> spaces = new List<CarrySpace> { new CarrySpace { WeightRoom = 0f, FreeSlots = 0 } };
			CartRules.ShareWithCart(found, "Ore", spaces, 18, WeightOf, StackOf, out Dictionary<string, int> cart);
			Assert.Equal((int)(CartRules.FullWeight / 2f / 10f), cart["Ore"]);
			Assert.Equal(1, cart["Trophy"]);
			Assert.Equal(CartRules.SmallStack(100), cart["Feather"]);
			Assert.True(CartRules.WeightOf(cart, WeightOf) <= CartRules.FullWeight);
		}

		[Theory]
		[InlineData(ResourceBook.Meadows, false)]
		[InlineData(ResourceBook.BlackForest, false)]
		[InlineData(ResourceBook.Swamp, true)]
		[InlineData(ResourceBook.Mountain, true)]
		[InlineData(ResourceBook.Plains, false)]
		[InlineData(ResourceBook.Mistlands, true)]
		[InlineData(ResourceBook.DeepNorth, false)]
		public void SwampsMountainsAndMistlandsAreRoughForACart(int biome, bool expected)
		{
			Assert.Equal(expected, CartRules.Rough(biome));
		}

		[Fact]
		public void FoodComesFirstAndTrophiesLast()
		{
			int food = CartRules.LootRank(true, false, false);
			int wood = CartRules.LootRank(false, true, false);
			int other = CartRules.LootRank(false, false, false);
			int trophy = CartRules.LootRank(false, false, true);
			Assert.True(food < wood);
			Assert.True(wood < other);
			Assert.True(other < trophy);
		}

		[Fact]
		public void TheCartRunsOutOfSlots()
		{
			List<KeyValuePair<string, int>> found = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("Wood", 500) };
			List<CarrySpace> spaces = new List<CarrySpace> { new CarrySpace { WeightRoom = 0f, FreeSlots = 0 } };
			CartRules.ShareWithCart(found, "Wood", spaces, 2, WeightOf, StackOf, out Dictionary<string, int> cart);
			Assert.Equal(100, cart["Wood"]);
		}

		/// <summary>
		/// Gets a test item's weight
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>Its weight</returns>
		private static float WeightOf(string item)
		{
			return(Weights[item]);
		}

		/// <summary>
		/// Gets how many of a test item make a stack
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The stack size</returns>
		private static int StackOf(string item)
		{
			return(Stacks[item]);
		}

		/// <summary>
		/// Checks two directions are the same, give or take rounding
		/// </summary>
		/// <param name="expected">The direction expected</param>
		/// <param name="actual">The direction found</param>
		private static void AssertNear(Vector2 expected, Vector2 actual)
		{
			Assert.True((expected - actual).magnitude < 0.001f, "Expected " + expected + " but got " + actual);
		}
	}
}
