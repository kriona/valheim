using UnityEngine;

namespace Kriona.Vikings.Tests
{
	/// <summary>
	/// Tests how a viking's skills rise and scale its damage, how it weighs up monsters, weapons and armor, and when it
	/// climbs
	/// </summary>
	public class VikingCoreTests
	{
		/// <summary>
		/// The normal of ground sloping up toward +x at 30 degrees
		/// </summary>
		private static readonly Vector3 SlopeUpX = new Vector3(-0.5f, 0.8660254f, 0f);

		/// <summary>
		/// Checks that the gain to the next level is the game's own - Skills.Skill.GetNextLevelRequirement, the next
		/// whole level to the power of 1.5, halved, plus a half - the same anywhere within a level
		/// </summary>
		/// <param name="level">The level</param>
		/// <param name="expected">The gain the next level takes</param>
		[Theory]
		[InlineData(0f, 1f)]
		[InlineData(0.7f, 1f)]
		[InlineData(3f, 4.5f)]
		[InlineData(3.9f, 4.5f)]
		[InlineData(15f, 32.5f)]
		[InlineData(99f, 500.5f)]
		public void TakesThePlayersGainToTheNextLevel(float level, float expected)
		{
			Assert.Equal(expected, VikingCore.LevelRequirement(level), 3);
		}

		/// <summary>
		/// Checks that a gain of what the next level takes raises a whole level, the way the game levels up a player
		/// whose gains reach it, and less stays within the level
		/// </summary>
		[Fact]
		public void RisesAWholeLevelForThePlayersGain()
		{
			float raised = VikingCore.RaisedLevel(3f, 4.5f);
			Assert.Equal(4f, raised, 3);
			Assert.True(VikingCore.LeveledUp(3f, raised));
			float under = VikingCore.RaisedLevel(3f, 4.4f);
			Assert.Equal(3.978f, under, 3);
			Assert.False(VikingCore.LeveledUp(3f, under));
		}

		/// <summary>
		/// Checks that two gains of half what the next level takes reach it, the way a player's add up toward it
		/// </summary>
		[Fact]
		public void AddsUpGainsWithinALevel()
		{
			float half = VikingCore.RaisedLevel(3f, 2.25f);
			Assert.Equal(3.5f, half, 3);
			Assert.False(VikingCore.LeveledUp(3f, half));
			float whole = VikingCore.RaisedLevel(half, 2.25f);
			Assert.Equal(4f, whole, 3);
			Assert.True(VikingCore.LeveledUp(half, whole));
		}

		/// <summary>
		/// Checks that a viking with no skill rises by the whole gain, since its first level takes 1
		/// </summary>
		[Fact]
		public void RisesFromNoSkill()
		{
			Assert.Equal(0.5f, VikingCore.RaisedLevel(0f, 0.5f), 3);
			Assert.False(VikingCore.LeveledUp(0f, 0.5f));
			Assert.True(VikingCore.LeveledUp(0f, VikingCore.RaisedLevel(0f, 1f)));
		}

		/// <summary>
		/// Checks that a rise from partway through a level crosses into the next at that level's rate
		/// </summary>
		[Fact]
		public void CrossesIntoTheNextLevel()
		{
			// Level 11 takes 11^1.5 / 2 + 1/2 = 18.7414, so a fifth of it takes 10.9 to 11.1
			float raised = VikingCore.RaisedLevel(10.9f, 3.74829f);
			Assert.Equal(11.1f, raised, 3);
			Assert.True(VikingCore.LeveledUp(10.9f, raised));
		}

		/// <summary>
		/// Checks that a skill never rises past 100, and reaching it counts as a new level
		/// </summary>
		[Fact]
		public void StopsAt100()
		{
			Assert.Equal(100f, VikingCore.RaisedLevel(99.5f, 1000f), 3);
			Assert.True(VikingCore.LeveledUp(99.5f, 100f));
			Assert.Equal(100f, VikingCore.RaisedLevel(100f, 5f), 3);
			Assert.False(VikingCore.LeveledUp(100f, 100f));
		}

		/// <summary>
		/// Checks that damage is scaled by skill the way a player's is - 40 percent with none, all of it at 100, with a
		/// level below 0 or past 100 counting as 0 or 100
		/// </summary>
		/// <param name="level">The skill level</param>
		/// <param name="expected">The damage dealt of 100</param>
		[Theory]
		[InlineData(-1f, 40f)]
		[InlineData(0f, 40f)]
		[InlineData(50f, 70f)]
		[InlineData(100f, 100f)]
		[InlineData(150f, 100f)]
		public void ScalesDamageBySkill(float level, float expected)
		{
			Assert.Equal(expected, VikingCore.SkillScaled(100f, level), 3);
		}

		/// <summary>
		/// Checks that a skill boosts by nothing with no skill and by the whole bonus at 100, the way a player's Run and
		/// Jump do, with a level below 0 or past 100 counting as 0 or 100
		/// </summary>
		/// <param name="level">The skill level</param>
		/// <param name="bonus">How much more the skill adds at 100</param>
		/// <param name="expected">The factor</param>
		[Theory]
		[InlineData(-20f, 0.4f, 1f)]
		[InlineData(0f, 0.4f, 1f)]
		[InlineData(50f, 0.4f, 1.2f)]
		[InlineData(25f, 0.25f, 1.0625f)]
		[InlineData(100f, 0.4f, 1.4f)]
		[InlineData(150f, 0.4f, 1.4f)]
		public void BoostsBySkill(float level, float bonus, float expected)
		{
			Assert.Equal(expected, VikingCore.SkillBoost(level, bonus), 4);
		}

		/// <summary>
		/// Checks that a skill's factor is its level as a fraction of 100, capped at 1, and None's is always 0, the same
		/// as the game's Skills.GetSkillFactor
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="level">The viking's level in it</param>
		/// <param name="expected">The factor</param>
		[Theory]
		[InlineData(Skills.SkillType.Swords, 0f, 0f)]
		[InlineData(Skills.SkillType.Swords, 50f, 0.5f)]
		[InlineData(Skills.SkillType.Swords, 100f, 1f)]
		[InlineData(Skills.SkillType.Swords, 150f, 1f)]
		[InlineData(Skills.SkillType.None, 50f, 0f)]
		public void FactorsSkillByLevel(Skills.SkillType skill, float level, float expected)
		{
			Assert.Equal(expected, VikingSkillFactors.Factor(skill, level), 3);
		}

		/// <summary>
		/// Checks that the damage factor is rolled 15 percent either side of 40 percent with no skill rising to all of it
		/// at full skill, kept from 0 to 1, the same as the game's Skills.GetRandomSkillFactor
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="level">The viking's level in it</param>
		/// <param name="expectedMin">The least the factor can be</param>
		/// <param name="expectedMax">The most the factor can be</param>
		[Theory]
		[InlineData(Skills.SkillType.Swords, 0f, 0.25f, 0.55f)]
		[InlineData(Skills.SkillType.Swords, 50f, 0.55f, 0.85f)]
		[InlineData(Skills.SkillType.Swords, 100f, 0.85f, 1f)]
		[InlineData(Skills.SkillType.Swords, 200f, 0.85f, 1f)]
		[InlineData(Skills.SkillType.None, 100f, 0.25f, 0.55f)]
		public void RollsDamageAroundTheSkillFactor(Skills.SkillType skill, float level, float expectedMin, float expectedMax)
		{
			VikingSkillFactors.DamageRange(skill, level, out float min, out float max);
			Assert.Equal(expectedMin, min, 3);
			Assert.Equal(expectedMax, max, 3);
		}

		/// <summary>
		/// Checks that a bow counts for 60 percent of its damage against a melee weapon, and the melee weapon wins a tie
		/// </summary>
		/// <param name="ranged">How hard the bow hits</param>
		/// <param name="melee">How hard the melee weapon hits</param>
		/// <param name="expected">Whether the bow is chosen</param>
		[Theory]
		[InlineData(10f, 5.9f, true)]
		[InlineData(10f, 6f, false)]
		[InlineData(10f, 6.1f, false)]
		[InlineData(10f, 10f, false)]
		[InlineData(40f, 0f, true)]
		public void PrefersTheBowOnlyWhenItHurtsMoreForItsPace(float ranged, float melee, bool expected)
		{
			Assert.Equal(expected, VikingCore.PrefersRanged(ranged, melee));
		}

		/// <summary>
		/// Checks that a troll outlasts a viking in light armor - 30 of its hits to bring down against under 2 of the
		/// troll's
		/// </summary>
		[Fact]
		public void FindsATrollTough()
		{
			Assert.True(VikingCore.Outlasts(600f, 70f, 0f, 100f, 6f, 20f));
		}

		/// <summary>
		/// Checks that a weak monster whose hits barely get through the viking's armor isn't tough
		/// </summary>
		[Fact]
		public void FindsAWeakMonsterEasy()
		{
			Assert.False(VikingCore.Outlasts(50f, 10f, 0f, 100f, 20f, 25f));
		}

		/// <summary>
		/// Checks that a monster's own armor takes off the viking's swings - an even fight against one with no armor is
		/// a tough one once it wears some
		/// </summary>
		[Fact]
		public void CountsTheMonstersArmorAgainstTheViking()
		{
			// 10 through 2 armor is 8 a swing, so 100 health takes 12.5 swings against the viking's 10
			Assert.False(VikingCore.Outlasts(100f, 10f, 0f, 100f, 0f, 10f));
			Assert.True(VikingCore.Outlasts(100f, 10f, 2f, 100f, 0f, 10f));
		}

		/// <summary>
		/// Checks that a zombie viking of the Mountains at one star is tough enough for more vikings than a village of
		/// 8 has - 600 health in half-strength Fenris armor against vikings in Fenris whose best swing is a frost arrow's
		/// 37, counted at the bow's pace, ahead of a Fang Spear's 26 against a zombie's pierce resistance
		/// </summary>
		[Fact]
		public void StarredMountainZombieTakesMoreThanAVillage()
		{
			// 37 through 15.5 armor is 21.5 a swing, 28 swings, against 2.9 of its 91.5 through 31 armor
			Assert.True(VikingCore.FightersNeeded(600f, 91.5f, 15.5f, 175f, 31f, 37f) > 8);
		}

		/// <summary>
		/// Checks that a monster taking as many hits to bring down as the viking can take isn't tough, and one taking a
		/// bit more is
		/// </summary>
		[Fact]
		public void FindsAnEvenFightNotTough()
		{
			Assert.False(VikingCore.Outlasts(100f, 10f, 0f, 100f, 0f, 10f));
			Assert.True(VikingCore.Outlasts(101f, 10f, 0f, 100f, 0f, 10f));
		}

		/// <summary>
		/// Checks that a viking's hit counts for at least 1, so a viking that can't hurt a monster isn't counted as
		/// taking endless hits
		/// </summary>
		/// <param name="strike">The viking's hardest hit</param>
		[Theory]
		[InlineData(0f)]
		[InlineData(0.5f)]
		public void CountsTheVikingsHitAsAtLeastOne(float strike)
		{
			Assert.False(VikingCore.Outlasts(5f, 2f, 0f, 10f, 0f, strike));
			Assert.True(VikingCore.Outlasts(6f, 2f, 0f, 10f, 0f, strike));
		}

		/// <summary>
		/// Checks that a viking's hit through the monster's armor counts for at least 1, so heavy armor on the monster
		/// doesn't make it last forever
		/// </summary>
		[Fact]
		public void CountsTheVikingsHitThroughArmorAsAtLeastOne()
		{
			// 10 through 1000 armor is 0.025, counted as 1, so the monster takes 5 hits
			Assert.False(VikingCore.Outlasts(5f, 2f, 1000f, 10f, 0f, 10f));
			Assert.True(VikingCore.Outlasts(6f, 2f, 1000f, 10f, 0f, 10f));
		}

		/// <summary>
		/// Checks that a monster's hit through the viking's armor counts for at least 1, so heavy armor doesn't make the
		/// viking last forever
		/// </summary>
		[Fact]
		public void CountsTheMonstersHitAsAtLeastOne()
		{
			// 10 through 1000 armor is 0.025, counted as 1, so the viking takes 100 hits
			Assert.False(VikingCore.Outlasts(1000f, 10f, 0f, 100f, 1000f, 10f));
			Assert.True(VikingCore.Outlasts(1010f, 10f, 0f, 100f, 1000f, 10f));
		}

		/// <summary>
		/// Checks that only a zombie viking of the world's hits hand to hand are scaled by its biome's MeleeScale - not a
		/// living or dvergr viking's, a tombstone viking's, a shot, or a viking with no data yet
		/// </summary>
		[Fact]
		public void ScalesOnlyAZombieVikingsMeleeHits()
		{
			Assert.True(VikingCore.GetsMeleeScale(true, false, false, false));
			Assert.False(VikingCore.GetsMeleeScale(true, false, true, false));
			Assert.False(VikingCore.GetsMeleeScale(true, true, false, false));
			Assert.False(VikingCore.GetsMeleeScale(true, false, false, true));
			Assert.False(VikingCore.GetsMeleeScale(false, false, false, false));
		}

		/// <summary>
		/// Checks that a viking's fists count only when it has no weapon it can use, since it never fights unarmed with
		/// one in its pack
		/// </summary>
		[Fact]
		public void CountsTheFistsOnlyWithNoWeapon()
		{
			Assert.Equal(63f, VikingCore.BestStrike(63f, null, null, 1f));
			Assert.Equal(45f, VikingCore.BestStrike(63f, 45f, null, 1f));
			Assert.Equal(25f, VikingCore.BestStrike(63f, null, 50f, 0.5f));
		}

		/// <summary>
		/// Checks that a bow counts at its share against a melee weapon, and wins only when that share hurts more
		/// </summary>
		[Fact]
		public void CountsTheBowAtItsShare()
		{
			Assert.Equal(45f, VikingCore.BestStrike(0f, 45f, 58f, 0.6f));
			Assert.Equal(58f, VikingCore.BestStrike(0f, 45f, 58f, 1f));
		}

		/// <summary>
		/// Checks that a swing starts at once at something already within reach, whether or not it's coming in
		/// </summary>
		[Fact]
		public void SwingsAtOnceWithinReach()
		{
			Assert.True(VikingCore.SwingNow(2f, 0f, 2.5f, 0.5f, 0.05f));
			Assert.True(VikingCore.SwingNow(2f, 10f, 2.5f, 0.5f, 0.05f));
		}

		/// <summary>
		/// Checks that a swing at something diving in starts once it's due within reach no later than the swing lands,
		/// less a moment - at 10 meters a second with a half-second swing, from 4.5 meters out past the reach and no
		/// farther
		/// </summary>
		[Fact]
		public void TimesTheSwingToTheDive()
		{
			// 2.5 reach + 10 m/s * (0.5 - 0.05) s = 7 meters
			Assert.True(VikingCore.SwingNow(6.9f, 10f, 2.5f, 0.5f, 0.05f));
			Assert.False(VikingCore.SwingNow(7.1f, 10f, 2.5f, 0.5f, 0.05f));
		}

		/// <summary>
		/// Checks that nothing out of reach that isn't coming in is swung at
		/// </summary>
		[Fact]
		public void HoldsTheSwingForWhatsNotComing()
		{
			Assert.False(VikingCore.SwingNow(4f, 0f, 2.5f, 0.5f, 0.05f));
			Assert.False(VikingCore.SwingNow(4f, -3f, 2.5f, 0.5f, 0.05f));
		}

		/// <summary>
		/// Checks that a monster that isn't tough takes one viking, and one just past an even fight takes two
		/// </summary>
		[Fact]
		public void EvenFightTakesOneViking()
		{
			Assert.Equal(1, VikingCore.FightersNeeded(50f, 10f, 0f, 100f, 20f, 25f));
			Assert.Equal(1, VikingCore.FightersNeeded(100f, 10f, 0f, 100f, 0f, 10f));
			Assert.Equal(2, VikingCore.FightersNeeded(101f, 10f, 0f, 100f, 0f, 10f));
		}

		/// <summary>
		/// Checks that a monster outlasting a viking several times over takes that many vikings and half again, rounded
		/// up - 4 times over takes 6, and 3 times over takes 5
		/// </summary>
		[Fact]
		public void ToughMonsterTakesAGroup()
		{
			// 400 health at 10 a hit is 40 hits, against 10 hits of 10 the viking can take
			Assert.Equal(6, VikingCore.FightersNeeded(400f, 10f, 0f, 100f, 0f, 10f));
			Assert.Equal(5, VikingCore.FightersNeeded(300f, 10f, 0f, 100f, 0f, 10f));
		}

		/// <summary>
		/// Checks that a tough monster worn down takes fewer vikings, since they're counted against the health it has
		/// left
		/// </summary>
		[Fact]
		public void WoundedMonsterTakesFewer()
		{
			Assert.True(VikingCore.FightersNeeded(150f, 10f, 0f, 100f, 0f, 10f) < VikingCore.FightersNeeded(400f, 10f, 0f, 100f, 0f, 10f));
		}

		/// <summary>
		/// Checks that away from freezing weather armor is taken up only for more armor, or a slot with nothing in it
		/// </summary>
		/// <param name="warm">Whether the piece keeps the frost off</param>
		/// <param name="hasWorn">Whether anything is worn in the slot</param>
		/// <param name="armor">The piece's armor</param>
		/// <param name="wornArmor">The worn piece's armor</param>
		/// <param name="expected">Whether it's taken up</param>
		[Theory]
		[InlineData(false, true, 10f, 5f, true)]
		[InlineData(false, true, 5f, 10f, false)]
		[InlineData(false, true, 5f, 5f, false)]
		[InlineData(false, false, 0f, 0f, true)]
		[InlineData(true, true, 2f, 20f, false)]
		public void TakesUpMoreArmorWhereItDoesntFreeze(bool warm, bool hasWorn, float armor, float wornArmor, bool expected)
		{
			Assert.Equal(expected, VikingCore.WantsArmor(warm, false, false, false, hasWorn, armor, wornArmor));
		}

		/// <summary>
		/// Checks that where it freezes, with nothing else worn keeping the frost off, a piece that keeps it off is taken
		/// up whatever its armor, and one that doesn't is never taken up in its place
		/// </summary>
		[Fact]
		public void PutsFrostFirstWhereItFreezes()
		{
			Assert.True(VikingCore.WantsArmor(true, false, true, false, true, 2f, 20f));
			Assert.False(VikingCore.WantsArmor(false, true, true, false, true, 50f, 5f));
		}

		/// <summary>
		/// Checks that where it freezes, armor decides once something else worn keeps the frost off
		/// </summary>
		[Fact]
		public void WeighsArmorWhenOtherGearKeepsTheFrostOff()
		{
			Assert.False(VikingCore.WantsArmor(true, false, true, true, true, 2f, 20f));
			Assert.True(VikingCore.WantsArmor(false, true, true, true, true, 50f, 5f));
		}

		/// <summary>
		/// Checks that where it freezes, armor decides between pieces that both keep the frost off, or neither, and a
		/// piece for an empty slot is taken up
		/// </summary>
		[Fact]
		public void WeighsArmorBetweenPiecesAlikeInTheFrost()
		{
			Assert.True(VikingCore.WantsArmor(true, true, true, false, true, 10f, 5f));
			Assert.False(VikingCore.WantsArmor(true, true, true, false, true, 5f, 10f));
			Assert.False(VikingCore.WantsArmor(false, false, true, false, true, 5f, 10f));
			Assert.True(VikingCore.WantsArmor(false, false, true, false, false, 5f, 0f));
			Assert.True(VikingCore.WantsArmor(true, false, true, false, false, 0f, 0f));
		}

		/// <summary>
		/// Checks that heading up a steep enough slope is a climb, straight up it or at an angle, counting only the way
		/// across the ground
		/// </summary>
		/// <param name="x">The way the viking moves along x</param>
		/// <param name="y">The way it moves along y</param>
		/// <param name="z">The way it moves along z</param>
		[Theory]
		[InlineData(1f, 0f, 0f)]
		[InlineData(1f, 0f, 1f)]
		[InlineData(1f, 5f, 0f)]
		public void ClimbsHeadingUpASlope(float x, float y, float z)
		{
			Assert.True(VikingCore.IsClimbStep(SlopeUpX, new Vector3(x, y, z), true));
		}

		/// <summary>
		/// Checks that heading down a slope, across it, mostly across it, or not moving isn't a climb
		/// </summary>
		/// <param name="x">The way the viking moves along x</param>
		/// <param name="y">The way it moves along y</param>
		/// <param name="z">The way it moves along z</param>
		[Theory]
		[InlineData(-1f, 0f, 0f)]
		[InlineData(0f, 0f, 1f)]
		[InlineData(0.2f, 0f, 1f)]
		[InlineData(0f, 0f, 0f)]
		[InlineData(0f, 1f, 0f)]
		public void DoesntClimbOtherWays(float x, float y, float z)
		{
			Assert.False(VikingCore.IsClimbStep(SlopeUpX, new Vector3(x, y, z), true));
		}

		/// <summary>
		/// Checks that flat or gently sloping ground, or being off the ground, isn't a climb
		/// </summary>
		[Fact]
		public void DoesntClimbWithoutASteepSlopeUnderfoot()
		{
			Vector3 up = new Vector3(1f, 0f, 0f);
			Assert.False(VikingCore.IsClimbStep(new Vector3(0f, 1f, 0f), up, true));
			// 10 degrees
			Assert.False(VikingCore.IsClimbStep(new Vector3(-0.1736482f, 0.9848078f, 0f), up, true));
			Assert.False(VikingCore.IsClimbStep(SlopeUpX, up, false));
		}
	}
}
