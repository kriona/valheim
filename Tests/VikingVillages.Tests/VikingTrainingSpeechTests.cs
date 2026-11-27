using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what a viking says to a player with a wooden training weapon in hand - which weapons count, what it calls
	/// the skill each trains, and that every line fills in the player's name and the skill
	/// </summary>
	public class VikingTrainingSpeechTests
	{
		[Fact]
		public void OnlyTheWoodenWeaponsCount()
		{
			foreach (string prefab in new[] { "SwordWood", "THSwordWood", "AxeWood", "BattleaxeWood", "MaceWood", "SledgeWood", "KnifeWood", "SpearWood", "AtgeirWood" })
			{
				Assert.True(VikingTrainingSpeech.IsTrainingWeapon(prefab), prefab + " isn't counted");
			}
			foreach (string prefab in new[] { "SwordIron", "AxeStone", "ShieldWood", "BowFineWood", "ArrowWood", "Wood", "Club", "" })
			{
				Assert.False(VikingTrainingSpeech.IsTrainingWeapon(prefab), prefab + " is counted");
			}
			Assert.False(VikingTrainingSpeech.IsTrainingWeapon(null));
		}

		[Fact]
		public void EachWeaponSkillHasAPhrase()
		{
			Assert.Equal("swordplay", VikingTrainingSpeech.Phrase(Skills.SkillType.Swords));
			Assert.Equal("axe work", VikingTrainingSpeech.Phrase(Skills.SkillType.Axes));
			Assert.Equal("club work", VikingTrainingSpeech.Phrase(Skills.SkillType.Clubs));
			Assert.Equal("knife work", VikingTrainingSpeech.Phrase(Skills.SkillType.Knives));
			Assert.Equal("spear work", VikingTrainingSpeech.Phrase(Skills.SkillType.Spears));
			Assert.Equal("polearm work", VikingTrainingSpeech.Phrase(Skills.SkillType.Polearms));
			Assert.Equal(VikingTrainingSpeech.AnySkill, VikingTrainingSpeech.Phrase(Skills.SkillType.None));
		}

		[Fact]
		public void LinesFillInThePlayerAndTheSkill()
		{
			List<string> lines = new List<string>(VikingTrainingSpeech.CallLines);
			lines.AddRange(VikingTrainingSpeech.ChatLines);
			bool namesSkill = false;
			foreach (string line in lines)
			{
				string said = string.Format(line, "Kriona", "swordplay");
				Assert.Contains("Kriona", said);
				Assert.DoesNotContain("{", said);
				namesSkill = namesSkill || said.Contains("swordplay");
			}
			Assert.True(namesSkill);
		}
	}
}
