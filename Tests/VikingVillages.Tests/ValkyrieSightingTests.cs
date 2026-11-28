using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests when vikings see a valkyrie flying over them, and what they shout
	/// </summary>
	public class ValkyrieSightingTests
	{
		[Fact]
		public void SheIsOverheadHighAboveAndNearlyAbove()
		{
			Vector3 viking = new Vector3(10f, 30f, 10f);
			Assert.True(ValkyrieSighting.Overhead(new Vector3(10f, 50f, 10f), viking));
			Assert.True(ValkyrieSighting.Overhead(new Vector3(20f, 40f, 15f), viking));
			Assert.False(ValkyrieSighting.Overhead(new Vector3(10f, 31f, 10f), viking));
			Assert.False(ValkyrieSighting.Overhead(new Vector3(10f, 80f, 10f), viking));
			Assert.False(ValkyrieSighting.Overhead(new Vector3(30f, 50f, 10f), viking));
			Assert.False(ValkyrieSighting.Overhead(new Vector3(10f, 20f, 10f), viking));
		}

		[Fact]
		public void TheyShoutAboutHerOrHerRider()
		{
			Assert.Contains(ValkyrieSightSpeech.Shout(false, false, "", 0.5f), ValkyrieSightSpeech.Lines);
			Assert.Contains(ValkyrieSightSpeech.Shout(false, true, "", 0.5f), ValkyrieSightSpeech.StrangerLines);
			Assert.Contains("Astrid", ValkyrieSightSpeech.Shout(false, true, "Astrid", 0.5f));
			Assert.Contains(ValkyrieSightSpeech.Shout(true, false, "", 0.99f), ValkyrieSightSpeech.DvergrLines);
			Assert.Contains(ValkyrieSightSpeech.Shout(true, true, "", 0f), ValkyrieSightSpeech.DvergrStrangerLines);
			Assert.Contains("Astrid", ValkyrieSightSpeech.Shout(true, true, "Astrid", 1f));
		}

		[Fact]
		public void TheWayShesComingFromIsNamedByTheCompass()
		{
			Vector3 middle = new Vector3(100f, 30f, 100f);
			Assert.Equal("the north", ValkyrieSightSpeech.Direction(middle, middle + new Vector3(0f, 20f, 50f)));
			Assert.Equal("the east", ValkyrieSightSpeech.Direction(middle, middle + new Vector3(50f, 0f, 0f)));
			Assert.Equal("the south", ValkyrieSightSpeech.Direction(middle, middle + new Vector3(3f, 0f, -50f)));
			Assert.Equal("the southwest", ValkyrieSightSpeech.Direction(middle, middle + new Vector3(-40f, 0f, -40f)));
			Assert.Equal("the northwest", ValkyrieSightSpeech.Direction(middle, middle + new Vector3(-40f, 0f, 38f)));
		}

		[Fact]
		public void TheHeraldSaysWhichWayShesComingFrom()
		{
			for (float roll = 0f; roll <= 1f; roll += 0.05f)
			{
				Assert.Contains("the east", ValkyrieSightSpeech.Herald("the east", roll));
			}
		}

		[Fact]
		public void EveryLineWithARiderNamesThem()
		{
			for (float roll = 0f; roll <= 1f; roll += 0.05f)
			{
				Assert.Contains("Astrid", ValkyrieSightSpeech.Shout(false, true, "Astrid", roll));
				Assert.Contains("Astrid", ValkyrieSightSpeech.Shout(true, true, "Astrid", roll));
			}
		}
	}
}
