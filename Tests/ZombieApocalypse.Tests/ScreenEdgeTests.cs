using Kriona.Shared;
using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests where something pointed at shows on screen - on screen or not, where an arrow pointing at it off screen is
	/// pinned, which way it's turned, and where a label goes behind it
	/// </summary>
	public class ScreenEdgeTests
	{
		/// <summary>
		/// The screen's size in the tests, and how far in from its edge an arrow's tip sits, in pixels
		/// </summary>
		private static readonly Vector2 Size = new Vector2(1920f, 1080f);
		private const float Margin = 40f;

		/// <summary>
		/// Checks that a point in front of the camera and within the screen is on screen, and one past its edge or behind
		/// the camera isn't
		/// </summary>
		/// <param name="x">The point's place across, in pixels</param>
		/// <param name="y">Its place up, in pixels</param>
		/// <param name="z">How far in front of the camera it is</param>
		/// <param name="expected">Whether it's on screen</param>
		[Theory]
		[InlineData(960f, 540f, 10f, true)]
		[InlineData(0f, 0f, 10f, true)]
		[InlineData(1920f, 1080f, 10f, true)]
		[InlineData(-1f, 540f, 10f, false)]
		[InlineData(960f, 1081f, 10f, false)]
		[InlineData(960f, 540f, -10f, false)]
		public void TellsOnScreen(float x, float y, float z, bool expected)
		{
			Assert.Equal(expected, ScreenEdge.OnScreen(new Vector3(x, y, z), Size));
		}

		/// <summary>
		/// Checks that an arrow is pinned to the edge the point lies past, pointing at it - mirrored through the middle
		/// for a point behind the camera, and into the bottom half for one above that isn't higher than the player
		/// </summary>
		/// <param name="x">The point's place across, in pixels</param>
		/// <param name="y">Its place up, in pixels</param>
		/// <param name="z">How far in front of the camera it is</param>
		/// <param name="higher">Whether it's higher than the player</param>
		/// <param name="tipX">Where the arrow's tip goes across</param>
		/// <param name="tipY">Where it goes up</param>
		/// <param name="pointX">The way the arrow points, across</param>
		/// <param name="pointY">The way it points, up</param>
		[Theory]
		[InlineData(3000f, 540f, 10f, false, 1880f, 540f, 1f, 0f)]
		[InlineData(-3000f, 540f, 10f, false, 40f, 540f, -1f, 0f)]
		[InlineData(960f, -500f, 10f, false, 960f, 40f, 0f, -1f)]
		[InlineData(960f, 2000f, 10f, false, 960f, 40f, 0f, -1f)]
		[InlineData(960f, 2000f, 10f, true, 960f, 1040f, 0f, 1f)]
		[InlineData(3000f, 540f, -10f, false, 40f, 540f, -1f, 0f)]
		[InlineData(960f, 540f, -10f, false, 960f, 40f, 0f, -1f)]
		public void PinsToTheEdge(float x, float y, float z, bool higher, float tipX, float tipY, float pointX, float pointY)
		{
			Vector2 tip = ScreenEdge.Pin(new Vector3(x, y, z), Size, Margin, higher, out Vector2 pointing);
			Assert.Equal(tipX, tip.x, 3);
			Assert.Equal(tipY, tip.y, 3);
			Assert.Equal(pointX, pointing.x, 3);
			Assert.Equal(pointY, pointing.y, 3);
		}

		/// <summary>
		/// Checks that a pinned arrow stays within the margin of every edge, even pointing into a corner
		/// </summary>
		[Fact]
		public void PinsWithinTheMargin()
		{
			for (float angle = 0f; angle < 360f; angle += 7.5f)
			{
				float radians = angle * Mathf.Deg2Rad;
				Vector3 point = new Vector3(960f + Mathf.Cos(radians) * 5000f, 540f + Mathf.Sin(radians) * 5000f, 10f);
				Vector2 tip = ScreenEdge.Pin(point, Size, Margin, true, out Vector2 _);
				Assert.InRange(tip.x, Margin - 0.01f, Size.x - Margin + 0.01f);
				Assert.InRange(tip.y, Margin - 0.01f, Size.y - Margin + 0.01f);
			}
		}

		/// <summary>
		/// Checks that an arrow pointing at a point behind a window is pinned to the window's edge, out from its middle
		/// through the point, pointing back in at it - and down from the middle for a point right at it
		/// </summary>
		/// <param name="x">The point's place across</param>
		/// <param name="y">Its place up</param>
		/// <param name="tipX">Where the arrow's tip goes across</param>
		/// <param name="tipY">Where it goes up</param>
		/// <param name="pointX">The way the arrow points, across</param>
		/// <param name="pointY">The way it points, up</param>
		[Theory]
		[InlineData(250f, 150f, 300f, 150f, -1f, 0f)]
		[InlineData(150f, 150f, 100f, 150f, 1f, 0f)]
		[InlineData(200f, 180f, 200f, 200f, 0f, -1f)]
		[InlineData(200f, 150f, 200f, 100f, 0f, 1f)]
		public void PinsToAWindowsEdge(float x, float y, float tipX, float tipY, float pointX, float pointY)
		{
			Rect window = Rect.MinMaxRect(100f, 100f, 300f, 200f);
			Vector2 tip = ScreenEdge.PinToRect(new Vector2(x, y), window, out Vector2 pointing);
			Assert.Equal(tipX, tip.x, 3);
			Assert.Equal(tipY, tip.y, 3);
			Assert.Equal(pointX, pointing.x, 3);
			Assert.Equal(pointY, pointing.y, 3);
		}

		/// <summary>
		/// Checks that a graphic pointing down is turned to point the way wanted
		/// </summary>
		/// <param name="x">The way wanted, across</param>
		/// <param name="y">The way wanted, up</param>
		/// <param name="expected">The turn, in degrees</param>
		[Theory]
		[InlineData(0f, -1f, 0f)]
		[InlineData(1f, 0f, 90f)]
		[InlineData(0f, 1f, 180f)]
		[InlineData(-1f, 0f, 270f)]
		public void TurnsToPoint(float x, float y, float expected)
		{
			Assert.Equal(expected, ScreenEdge.Angle(new Vector2(x, y)), 3);
		}

		/// <summary>
		/// Checks that a label goes behind an arrow's tail, clear of it, and stays on screen
		/// </summary>
		/// <param name="tipX">Where the arrow's tip is across</param>
		/// <param name="tipY">Where it is up</param>
		/// <param name="pointX">The way the arrow points, across</param>
		/// <param name="pointY">The way it points, up</param>
		/// <param name="centerX">Where the label's middle goes across</param>
		/// <param name="centerY">Where it goes up</param>
		[Theory]
		[InlineData(1880f, 540f, 1f, 0f, 1800f, 540f)]
		[InlineData(960f, 40f, 0f, -1f, 960f, 90f)]
		[InlineData(1880f, 1075f, 1f, 0f, 1800f, 1060f)]
		[InlineData(20f, 540f, 0f, -1f, 50f, 590f)]
		public void PutsTheLabelBehind(float tipX, float tipY, float pointX, float pointY, float centerX, float centerY)
		{
			Vector2 center = ScreenEdge.Behind(new Vector2(tipX, tipY), new Vector2(pointX, pointY), 30f, new Vector2(50f, 20f), Size);
			Assert.Equal(centerX, center.x, 3);
			Assert.Equal(centerY, center.y, 3);
		}
	}
}
