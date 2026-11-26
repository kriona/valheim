using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what the vikings on a boat talk about as they sail, the most pressing first, and what they call out at once
	/// </summary>
	public class BoatTalkTests
	{
		[Fact]
		public void DangerComesBeforeTheWeather()
		{
			Assert.Equal(BoatTalk.Topic.Flyers, BoatTalk.Pick(true, true, true, true, true, true, true, true));
			Assert.Equal(BoatTalk.Topic.Serpent, BoatTalk.Pick(false, true, true, true, true, true, true, true));
			Assert.Equal(BoatTalk.Topic.Leviathan, BoatTalk.Pick(false, false, true, true, true, true, true, true));
		}

		[Fact]
		public void TheWeatherGoesStormRainColdFogThenNight()
		{
			Assert.Equal(BoatTalk.Topic.Storm, BoatTalk.Pick(false, false, false, true, true, true, true, true));
			Assert.Equal(BoatTalk.Topic.Rain, BoatTalk.Pick(false, false, false, false, true, true, true, true));
			Assert.Equal(BoatTalk.Topic.Cold, BoatTalk.Pick(false, false, false, false, false, true, true, true));
			Assert.Equal(BoatTalk.Topic.Fog, BoatTalk.Pick(false, false, false, false, false, false, true, true));
			Assert.Equal(BoatTalk.Topic.Night, BoatTalk.Pick(false, false, false, false, false, false, false, true));
			Assert.Equal(BoatTalk.Topic.Calm, BoatTalk.Pick(false, false, false, false, false, false, false, false));
		}

		[Fact]
		public void OnlySomethingNewToWorryAboutIsCalledOutAtOnce()
		{
			Assert.True(BoatTalk.CallsOut(BoatTalk.Topic.Serpent, BoatTalk.Topic.Calm));
			Assert.True(BoatTalk.CallsOut(BoatTalk.Topic.Flyers, BoatTalk.Topic.Serpent));
			Assert.True(BoatTalk.CallsOut(BoatTalk.Topic.Leviathan, BoatTalk.Topic.Rain));
			Assert.False(BoatTalk.CallsOut(BoatTalk.Topic.Serpent, BoatTalk.Topic.Serpent));
			Assert.False(BoatTalk.CallsOut(BoatTalk.Topic.Storm, BoatTalk.Topic.Calm));
			Assert.False(BoatTalk.CallsOut(BoatTalk.Topic.Calm, BoatTalk.Topic.Flyers));
		}
	}
}
