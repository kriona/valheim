using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rules of a village's cart - which villages keep one, what becomes of it wherever it ends up, and which
	/// sides of a house it's parked beside
	/// </summary>
	public class CartRulesTests
	{
		private const double Day = 1800.0;

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
			Assert.Equal(CartRules.CartState.None, CartRules.Decide(false, true, true, false, true, false, true, 0, Day));
		}

		[Fact]
		public void NoCartNotedIsMissing()
		{
			Assert.Equal(CartRules.CartState.Missing, CartRules.Decide(true, false, false, false, false, false, false, 0, Day));
		}

		[Fact]
		public void ACartUnknownWhereItWasLastSeenWasDestroyed()
		{
			Assert.Equal(CartRules.CartState.Missing, CartRules.Decide(true, true, false, true, false, false, false, 0, Day));
		}

		[Fact]
		public void ACartOutOfReachIsAwayUntilADayHasPassed()
		{
			Assert.Equal(CartRules.CartState.Away, CartRules.Decide(true, true, false, false, false, false, false, Day - 1, Day));
			Assert.Equal(CartRules.CartState.Away, CartRules.Decide(true, true, true, false, false, false, false, 10, Day));
			Assert.Equal(CartRules.CartState.Missing, CartRules.Decide(true, true, true, false, false, false, false, Day, Day));
		}

		[Fact]
		public void APlayerUsingItKeepsTheVikingsAway()
		{
			Assert.Equal(CartRules.CartState.InUse, CartRules.Decide(true, true, true, false, true, true, false, 0, Day));
		}

		[Fact]
		public void ALoadedCartIsHomeInsideAndAstrayOutside()
		{
			Assert.Equal(CartRules.CartState.Home, CartRules.Decide(true, true, true, false, true, false, true, 0, Day));
			Assert.Equal(CartRules.CartState.Astray, CartRules.Decide(true, true, true, false, true, false, false, 0, Day));
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
