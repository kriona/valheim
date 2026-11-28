using System;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests a valkyrie's name and the few words she says
	/// </summary>
	public class ValkyrieLinesTests
	{
		[Fact]
		public void SheIsGivenOneOfTheValkyriesNames()
		{
			for (float roll = 0f; roll <= 1f; roll += 0.05f)
			{
				Assert.Contains(ValkyrieLines.PickName(roll), TravelerPets.ValkyrieNames);
			}
		}

		[Fact]
		public void HerIntroductionTellsHerName()
		{
			for (float roll = 0f; roll <= 1f; roll += 0.1f)
			{
				Assert.Contains("Skuld", ValkyrieLines.Introduction("Skuld", false, roll));
				Assert.Contains("Skuld", ValkyrieLines.Introduction("Skuld", true, roll));
			}
		}

		[Fact]
		public void SheAnswersWildOrTamed()
		{
			Assert.Contains(ValkyrieLines.Answer(false, false, 0.5f), ValkyrieLines.WildAnswers);
			Assert.Contains(ValkyrieLines.Answer(true, false, 0.5f), ValkyrieLines.TamedAnswers);
			Assert.Contains(ValkyrieLines.Answer(false, false, 0.99f), ValkyrieLines.WildAnswers);
		}

		[Fact]
		public void HungryAndBefriendedSheTalksOfHerHunger()
		{
			Assert.Contains(ValkyrieLines.Answer(true, true, 0.5f), ValkyrieLines.HungryAnswers);
			Assert.Contains(ValkyrieLines.Answer(false, true, 0.5f), ValkyrieLines.WildAnswers);
		}

		[Fact]
		public void SheSaysLittle()
		{
			foreach (string[] lines in new[] { ValkyrieLines.WildIntroductions, ValkyrieLines.TamedIntroductions, ValkyrieLines.WildAnswers, ValkyrieLines.TamedAnswers, ValkyrieLines.CatchLines, ValkyrieLines.NotHereLines, ValkyrieLines.HungryAnswers })
			{
				foreach (string line in lines)
				{
					Assert.True(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length <= 5, line);
				}
			}
		}
	}
}
