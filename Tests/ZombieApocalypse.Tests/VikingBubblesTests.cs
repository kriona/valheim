using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how a viking's speech bubble is kept on screen
	/// </summary>
	public class VikingBubblesTests
	{
		/// <summary>
		/// The screen's size in the tests, and a bubble's, in pixels
		/// </summary>
		private static readonly Vector2 ScreenSize = new Vector2(1920f, 1080f);
		private static readonly Vector2 Size = new Vector2(200f, 100f);

		/// <summary>
		/// Checks that a bubble is kept within the screen's sides, its bottom no lower than the screen's and its top no
		/// higher, and left where it is when it fits
		/// </summary>
		/// <param name="x">Where its bottom's middle goes across</param>
		/// <param name="y">Where it goes up</param>
		/// <param name="expectedX">Where it's kept across</param>
		/// <param name="expectedY">Where it's kept up</param>
		[Theory]
		[InlineData(960f, 500f, 960f, 500f)]
		[InlineData(50f, 500f, 100f, 500f)]
		[InlineData(100f, 500f, 100f, 500f)]
		[InlineData(1900f, 500f, 1820f, 500f)]
		[InlineData(960f, -20f, 960f, 0f)]
		[InlineData(960f, 1050f, 960f, 980f)]
		[InlineData(960f, 980f, 960f, 980f)]
		[InlineData(-300f, 2000f, 100f, 980f)]
		public void KeepsTheBubbleOnScreen(float x, float y, float expectedX, float expectedY)
		{
			Vector2 placed = VikingBubbles.ClampBubble(new Vector2(x, y), Size, ScreenSize);
			Assert.Equal(expectedX, placed.x, 3);
			Assert.Equal(expectedY, placed.y, 3);
		}

		/// <summary>
		/// Checks that a bubble wider than the screen goes against its left side, and one taller against its bottom,
		/// wherever it's wanted
		/// </summary>
		/// <param name="x">Where its bottom's middle goes across</param>
		/// <param name="y">Where it goes up</param>
		[Theory]
		[InlineData(960f, 500f)]
		[InlineData(0f, -50f)]
		[InlineData(1920f, 1080f)]
		public void KeepsAnOversizedBubbleAtTheCorner(float x, float y)
		{
			Vector2 placed = VikingBubbles.ClampBubble(new Vector2(x, y), new Vector2(2000f, 1200f), ScreenSize);
			Assert.Equal(1000f, placed.x, 3);
			Assert.Equal(0f, placed.y, 3);
		}
	}
}
