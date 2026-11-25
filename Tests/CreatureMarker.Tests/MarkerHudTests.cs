using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace CreatureMarker.Tests
{
	/// <summary>
	/// Tests where the red ! goes beside a provoked creature's arrow, how a name of more than one line is split for its
	/// label, when a speech bubble already names its speaker, and the order markers are drawn over each other in
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

		/// <summary>
		/// Checks that markers are drawn farthest first, so the nearest is drawn over the rest
		/// </summary>
		[Fact]
		public void MarkersAreDrawnFarthestFirst()
		{
			List<int> order = new List<int>();
			MarkerHud.DrawOrder(new List<float> { 25f, 400f, 1f, 100f }, order);
			Assert.Equal(new List<int> { 1, 3, 0, 2 }, order);
		}

		/// <summary>
		/// Checks that markers as near as each other keep the order they're listed in, so they don't swap places
		/// </summary>
		[Fact]
		public void MarkersAsNearAsEachOtherKeepTheirOrder()
		{
			List<int> order = new List<int>();
			MarkerHud.DrawOrder(new List<float> { 9f, 4f, 9f, 4f }, order);
			Assert.Equal(new List<int> { 0, 2, 1, 3 }, order);
		}

		/// <summary>
		/// Checks that a talking creature's marker counts as nearer than any other, even one right at the camera
		/// </summary>
		[Fact]
		public void ATalkingCreaturesMarkerIsDrawnOverTheRest()
		{
			List<int> order = new List<int>();
			MarkerHud.DrawOrder(new List<float> { MarkerHud.LayerKey(0f, false), MarkerHud.LayerKey(2500f, true), MarkerHud.LayerKey(16f, false) }, order);
			Assert.Equal(new List<int> { 2, 0, 1 }, order);
		}

		/// <summary>
		/// Checks that an empty list of markers draws nothing, clearing what the order held before
		/// </summary>
		[Fact]
		public void NoMarkersDrawNothing()
		{
			List<int> order = new List<int> { 3, 1 };
			MarkerHud.DrawOrder(new List<float>(), order);
			Assert.Empty(order);
		}
	}
}
