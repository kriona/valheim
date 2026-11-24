using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests when a viking counts as new to its village and the talks there are about it
	/// </summary>
	public class VillageNewcomersTests
	{
		[Theory]
		[InlineData(0L, 500L, false)]
		[InlineData(100L, 100L, true)]
		[InlineData(100L, 1099L, true)]
		[InlineData(100L, 1100L, false)]
		[InlineData(2000L, 1000L, false)]
		public void NewForAWindowAfterJoining(long joinedAt, long now, bool expected)
		{
			Assert.Equal(expected, VillageNewcomers.IsNew(joinedAt, now, 1000L));
		}

		[Fact]
		public void TalksAboutTheVillageItCameFromWhenThereIsOne()
		{
			List<string[]> talks = VillageNewcomers.Candidates(VillageNewcomers.AboutTalks, VillageNewcomers.AboutFromTalks, true, true);
			Assert.Equal(VillageNewcomers.AboutFromTalks.Length, talks.Count);
			Assert.All(talks, (string[] talk) => Assert.Contains(talk, (string line) => line.Contains(VillageNewcomers.FromToken)));
		}

		[Fact]
		public void TalksLeaveOutTheVillageItCameFromWhenThereIsNone()
		{
			List<string[]> talks = VillageNewcomers.Candidates(VillageNewcomers.WelcomeTalks, VillageNewcomers.WelcomeFromTalks, false, true);
			Assert.Equal(VillageNewcomers.WelcomeTalks.Length, talks.Count);
			Assert.All(talks, (string[] talk) => Assert.DoesNotContain(talk, (string line) => line.Contains(VillageNewcomers.FromToken)));
		}

		[Fact]
		public void TalksNamingTheVillageAreLeftOutWhenItHasNoName()
		{
			List<string[]> talks = VillageNewcomers.Candidates(VillageNewcomers.WelcomeTalks, VillageNewcomers.WelcomeFromTalks, false, false);
			Assert.NotEmpty(talks);
			Assert.All(talks, (string[] talk) => Assert.DoesNotContain(talk, (string line) => line.Contains("{village}")));
		}

		[Fact]
		public void TalksAboutANewcomerNameIt()
		{
			foreach (string[] talk in VillageNewcomers.AboutTalks)
			{
				Assert.Contains(talk, (string line) => line.Contains(VillageNewcomers.NewcomerToken));
			}
			foreach (string[] talk in VillageNewcomers.AboutFromTalks)
			{
				Assert.Contains(talk, (string line) => line.Contains(VillageNewcomers.NewcomerToken));
			}
		}

		[Fact]
		public void AddressFillsInTheNewcomerAndTheirVillage()
		{
			string line = VikingSpeech.Address("{newcomer} came all the way from {from}, {name}.", "Astrid", "", "", "", "", "", "", "Einar", "Hrafnheim");
			Assert.Equal("Einar came all the way from Hrafnheim, Astrid.", line);
		}
	}
}
