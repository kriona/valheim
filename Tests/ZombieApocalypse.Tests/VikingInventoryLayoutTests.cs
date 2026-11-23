using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how the inventory window is laid out for a viking's inventory - where the panels go, where the viking's tabs
	/// mirror the player's, where the health tab stands and how the heart's pixels are lightened
	/// </summary>
	public class VikingInventoryLayoutTests
	{
		/// <summary>
		/// The player's card and inventory in the tests, within the window - the card 300 wide and 200 tall on the right,
		/// the inventory on the left reaching 50 less high
		/// </summary>
		private static readonly Rect Info = Rect.MinMaxRect(100f, 300f, 400f, 500f);
		private static readonly Rect Player = Rect.MinMaxRect(-500f, -200f, -100f, 450f);

		/// <summary>
		/// Checks that the player's card goes on top of their inventory, where it stood, and the viking's card on top of
		/// the viking's inventory, where the player's card stood, each inventory below its card by the gap between the
		/// player's card and the crafting panel
		/// </summary>
		[Fact]
		public void PlacesTheCardsOnTopOfTheInventories()
		{
			Rect crafting = Rect.MinMaxRect(100f, -200f, 400f, 280f);
			VikingInventoryLayout.PlanPlaces(Info, Player, crafting, out Vector2 infoAt, out Vector2 playerAt, out Vector2 cardAt, out Vector2 containerAt);
			// The cards at the card's top of 500, the inventories 200 for the card and 20 for the gap below it
			Assert.Equal(new Vector2(-500f, 500f), infoAt);
			Assert.Equal(new Vector2(-500f, 280f), playerAt);
			Assert.Equal(new Vector2(100f, 500f), cardAt);
			Assert.Equal(new Vector2(100f, 280f), containerAt);
		}

		/// <summary>
		/// Checks that the cards go at the top of the player's inventory when it reaches higher than their card
		/// </summary>
		[Fact]
		public void PlacesTheCardsAtTheHigherTop()
		{
			Rect player = Rect.MinMaxRect(-500f, -200f, -100f, 600f);
			Rect crafting = Rect.MinMaxRect(100f, -200f, 400f, 280f);
			VikingInventoryLayout.PlanPlaces(Info, player, crafting, out Vector2 infoAt, out Vector2 playerAt, out Vector2 cardAt, out Vector2 containerAt);
			Assert.Equal(600f, infoAt.y);
			Assert.Equal(600f, cardAt.y);
			Assert.Equal(380f, playerAt.y);
			Assert.Equal(380f, containerAt.y);
		}

		/// <summary>
		/// Checks that the inventories go 8 below the cards when the gap to the crafting panel is none, less or more than
		/// the card is tall, and by the gap when it's up to the card's height
		/// </summary>
		/// <param name="craftingTop">The top of the crafting panel, under the card's bottom of 300</param>
		/// <param name="expected">The top the inventories go at</param>
		[Theory]
		[InlineData(300f, 292f)]
		[InlineData(320f, 292f)]
		[InlineData(99f, 292f)]
		[InlineData(100f, 100f)]
		[InlineData(299f, 299f)]
		public void FallsBackOnTheGap(float craftingTop, float expected)
		{
			Rect crafting = Rect.MinMaxRect(100f, -200f, 400f, craftingTop);
			VikingInventoryLayout.PlanPlaces(Info, Player, crafting, out Vector2 _, out Vector2 playerAt, out Vector2 _, out Vector2 containerAt);
			Assert.Equal(expected, playerAt.y, 3);
			Assert.Equal(expected, containerAt.y, 3);
		}

		/// <summary>
		/// Checks that a point beside the player's inventory mirrors to beside the viking's - as far left of its left edge
		/// as it stood right of the player's right edge, and as far down from its top
		/// </summary>
		/// <param name="x">The point's place across</param>
		/// <param name="y">Its place up</param>
		/// <param name="expectedX">Where it mirrors to across</param>
		/// <param name="expectedY">Where it mirrors to up</param>
		[Theory]
		// 20 right of the player's right edge of -100 and 50 below their top of 450 goes 20 left of 100 and 50 below 400
		[InlineData(-80f, 400f, 80f, 350f)]
		// 20 within the right edge and 10 above the top goes 20 within the left edge and 10 above
		[InlineData(-120f, 460f, 120f, 410f)]
		// Right on the edge and the top stays on them
		[InlineData(-100f, 450f, 100f, 400f)]
		public void MirrorsTabsAcross(float x, float y, float expectedX, float expectedY)
		{
			Rect to = Rect.MinMaxRect(100f, -100f, 500f, 400f);
			Vector2 mirrored = VikingInventoryLayout.MirrorPoint(new Vector2(x, y), Player, to);
			Assert.Equal(expectedX, mirrored.x, 3);
			Assert.Equal(expectedY, mirrored.y, 3);
		}

		/// <summary>
		/// Checks that the health tab goes halfway between the armor and weight tabs when the room between their edges
		/// fits it, whichever is higher, and as far under the lower as they stand apart when it doesn't
		/// </summary>
		/// <param name="armorY">The middle of the armor tab</param>
		/// <param name="armorHeight">Its height</param>
		/// <param name="weightY">The middle of the weight tab</param>
		/// <param name="weightHeight">Its height</param>
		/// <param name="height">The health tab's height</param>
		/// <param name="expected">The health tab's middle</param>
		[Theory]
		// 80 apart less 30 for the halves of the tabs leaves 50, room for 30
		[InlineData(0f, 30f, -80f, 30f, 30f, -40f)]
		// The armor below the weight
		[InlineData(-80f, 30f, 0f, 30f, 30f, -40f)]
		// Room of 30 just fits it
		[InlineData(0f, 30f, -60f, 30f, 30f, -30f)]
		// Room of 10 doesn't, so it goes 40 under the lower
		[InlineData(0f, 30f, -40f, 30f, 30f, -80f)]
		[InlineData(-40f, 30f, 0f, 30f, 30f, -80f)]
		// 100 apart less 30 for halves of 20 and 40 leaves 70, room for 50
		[InlineData(0f, 20f, -100f, 40f, 50f, -50f)]
		public void PlacesTheHealthTab(float armorY, float armorHeight, float weightY, float weightHeight, float height, float expected)
		{
			Assert.Equal(expected, VikingInventoryLayout.HealthTabY(armorY, armorHeight, weightY, weightHeight, height), 3);
		}

		/// <summary>
		/// Checks that the lightest pixel of the heart turns white and the rest lighten as much, all fully opaque
		/// </summary>
		[Fact]
		public void LightensTheLightestToWhite()
		{
			Color[] pixels = { new Color(0.5f, 0.25f, 0f, 0.5f), new Color(0.2f, 0.2f, 0.2f, 1f) };
			VikingInventoryLayout.BrightenPixels(pixels);
			AssertColor(new Color(1f, 0.5f, 0f, 1f), pixels[0]);
			AssertColor(new Color(0.4f, 0.4f, 0.4f, 1f), pixels[1]);
		}

		/// <summary>
		/// Checks that a pixel no more opaque than the cut is left out of the heart - cleared, and not counted as its
		/// lightest - and one just past it is kept
		/// </summary>
		[Fact]
		public void LeavesOutPixelsPastTheCut()
		{
			Color[] pixels = { new Color(1f, 1f, 1f, 0.1f), new Color(0.5f, 0.5f, 0.5f, 0.11f) };
			VikingInventoryLayout.BrightenPixels(pixels);
			AssertColor(new Color(1f, 1f, 1f, 0f), pixels[0]);
			AssertColor(new Color(1f, 1f, 1f, 1f), pixels[1]);
		}

		/// <summary>
		/// Checks that with nothing past the cut, every pixel keeps its color and is cleared
		/// </summary>
		[Fact]
		public void LeavesAnAllClearHeartAsItIs()
		{
			Color[] pixels = { new Color(0.3f, 0.6f, 0.9f, 0f), new Color(0.2f, 0.1f, 0f, 0.05f) };
			VikingInventoryLayout.BrightenPixels(pixels);
			AssertColor(new Color(0.3f, 0.6f, 0.9f, 0f), pixels[0]);
			AssertColor(new Color(0.2f, 0.1f, 0f, 0f), pixels[1]);
		}

		/// <summary>
		/// Checks that a heart too dark to go by keeps its color, made fully opaque
		/// </summary>
		[Fact]
		public void LeavesABlackHeartBlack()
		{
			Color[] pixels = { new Color(0.005f, 0f, 0f, 1f) };
			VikingInventoryLayout.BrightenPixels(pixels);
			AssertColor(new Color(0.005f, 0f, 0f, 1f), pixels[0]);
		}

		/// <summary>
		/// Checks that a color is the one expected, to within rounding
		/// </summary>
		/// <param name="expected">The color expected</param>
		/// <param name="actual">The color</param>
		private static void AssertColor(Color expected, Color actual)
		{
			Assert.Equal(expected.r, actual.r, 4);
			Assert.Equal(expected.g, actual.g, 4);
			Assert.Equal(expected.b, actual.b, 4);
			Assert.Equal(expected.a, actual.a, 4);
		}
	}
}
