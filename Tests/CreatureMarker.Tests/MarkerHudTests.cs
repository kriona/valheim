using UnityEngine;
using Xunit;

namespace CreatureMarker.Tests
{
	/// <summary>
	/// Tests where the red ! goes beside a provoked creature's arrow, and how a name of more than one line is split for
	/// its label
	/// </summary>
	public class MarkerHudTests
	{
		[Fact]
		public void AlarmGoesJustRightOfTheArrowLevelWithItsMiddle()
		{
			Vector2 spot = MarkerHud.AlarmSpot(new Rect(100f, 200f, 24f, 32f), new Vector2(6f, 15f), 2f, 1920f);
			Assert.Equal(new Vector2(132f, 216f), spot);
		}

		[Fact]
		public void AlarmGoesLeftOfAnArrowWithNoRoomOnItsRight()
		{
			Vector2 spot = MarkerHud.AlarmSpot(new Rect(1880f, 500f, 32f, 24f), new Vector2(6f, 15f), 2f, 1920f);
			Assert.Equal(new Vector2(1872f, 512f), spot);
		}

		[Fact]
		public void AlarmThatJustFitsStaysOnTheRight()
		{
			Vector2 spot = MarkerHud.AlarmSpot(new Rect(1882f, 0f, 24f, 32f), new Vector2(6f, 15f), 2f, 1920f);
			Assert.Equal(1914f, spot.x);
		}

		[Fact]
		public void NameOfOneLineIsAllFirstLine()
		{
			Assert.Equal("Greydwarf", MarkerHud.FirstLine("Greydwarf", out string rest));
			Assert.Equal("", rest);
		}

		[Fact]
		public void NameOfMoreLinesSplitsAtTheFirstBreak()
		{
			Assert.Equal("Astrid", MarkerHud.FirstLine("Astrid\n<color=#C0C0C0>Chopping wood</color>\nmore", out string rest));
			Assert.Equal("\n<color=#C0C0C0>Chopping wood</color>\nmore", rest);
		}
	}
}
