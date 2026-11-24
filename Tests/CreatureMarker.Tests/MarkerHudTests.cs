using UnityEngine;
using Xunit;

namespace CreatureMarker.Tests
{
	/// <summary>
	/// Tests where the red ! goes beside a provoked creature's arrow, how a name of more than one line is split for its
	/// label, and when a speech bubble already names its speaker
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

		[Fact]
		public void ABubbleThatStartsWithTheNameNamesTheSpeaker()
		{
			Assert.True(MarkerHud.NamesSpeaker("<color=orange>Vebjorn Stormcloak</color>\nDid you see that?", "Vebjorn Stormcloak"));
			Assert.True(MarkerHud.NamesSpeaker("Haldor", "Haldor"));
		}

		[Fact]
		public void ANameWithItsJobUnderItIsMatchedByItsFirstLine()
		{
			Assert.True(MarkerHud.NamesSpeaker("<color=orange>Astrid</color>\nHello!", "Astrid\n<color=#C0C0C0>Chopping wood</color>"));
		}

		[Fact]
		public void ABubbleThatOnlyMentionsTheNameDoesntNameTheSpeaker()
		{
			Assert.False(MarkerHud.NamesSpeaker("Did you see that?", "Vebjorn Stormcloak"));
			Assert.False(MarkerHud.NamesSpeaker("Vebjorn Stormcloak is a fine archer.", "Vebjorn Stormcloak"));
			Assert.False(MarkerHud.NamesSpeaker("<color=orange>Åsa</color>\nVebjorn Stormcloak!", "Vebjorn Stormcloak"));
			Assert.False(MarkerHud.NamesSpeaker(null, "Astrid"));
			Assert.False(MarkerHud.NamesSpeaker("Astrid", ""));
		}
	}
}
