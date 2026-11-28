using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a valkyrie's spin effect is whitened and how her bones are named in the Fallen Valkyrie's skeleton
	/// </summary>
	public class ValkyrieCombatTests
	{
		[Fact]
		public void APurpleTurnsWhiteAsBrightAsItsBrightestChannel()
		{
			Color white = ValkyrieCombat.Whiten(new Color(2f, 0f, 0.88f, 0.5f), 0f);
			Assert.Equal(2f, white.r);
			Assert.Equal(2f, white.g);
			Assert.Equal(2f, white.b);
			Assert.Equal(0.5f, white.a);
		}

		[Fact]
		public void ADarkColorIsRaisedToTheFloor()
		{
			Color white = ValkyrieCombat.Whiten(new Color(0.12f, 0.09f, 0.16f, 1f), 0.9f);
			Assert.Equal(0.9f, white.r);
			Assert.Equal(0.9f, white.g);
			Assert.Equal(0.9f, white.b);
			Assert.Equal(1f, white.a);
		}

		[Fact]
		public void AColorBrighterThanTheFloorKeepsItsBrightness()
		{
			Color white = ValkyrieCombat.Whiten(new Color(0.2f, 0.95f, 0.1f, 1f), 0.9f);
			Assert.Equal(0.95f, white.r);
			Assert.Equal(0.95f, white.b);
		}

		[Fact]
		public void HerRiderSwoopsInTheAirClawsOnTheGroundAndSpinsEither()
		{
			Assert.Equal(ValkyrieCombat.SwoopName, ValkyrieCombat.RiderAttackName(false, true, true));
			Assert.Equal(ValkyrieCombat.ClawsName, ValkyrieCombat.RiderAttackName(false, false, true));
			Assert.Equal(ValkyrieCombat.ClawsName, ValkyrieCombat.RiderAttackName(false, true, false));
			Assert.Equal(ValkyrieCombat.SpinName, ValkyrieCombat.RiderAttackName(true, true, true));
			Assert.Equal(ValkyrieCombat.SpinName, ValkyrieCombat.RiderAttackName(true, false, true));
		}

		[Fact]
		public void TheUndeadAreTheUndeadFactionOrWeakToSpirit()
		{
			Assert.True(ValkyrieBanish.Undead(Character.Faction.Undead, HitData.DamageModifier.Normal));
			Assert.True(ValkyrieBanish.Undead(Character.Faction.ForestMonsters, HitData.DamageModifier.Weak));
			Assert.True(ValkyrieBanish.Undead(Character.Faction.ForestMonsters, HitData.DamageModifier.VeryWeak));
			Assert.False(ValkyrieBanish.Undead(Character.Faction.ForestMonsters, HitData.DamageModifier.Normal));
			Assert.False(ValkyrieBanish.Undead(Character.Faction.Demon, HitData.DamageModifier.Immune));
		}

		[Fact]
		public void ABrighterColorKeepsItsAlpha()
		{
			Color brighter = ValkyrieCombat.Brighter(new Color(0.9f, 0.9f, 0.9f, 0.5f), 2f);
			Assert.Equal(1.8f, brighter.r, 5);
			Assert.Equal(1.8f, brighter.b, 5);
			Assert.Equal(0.5f, brighter.a);
		}

		[Fact]
		public void HerCorpsesCapsulesRunAlongEachBone()
		{
			Assert.Equal(0, ValkyrieCorpse.LongestAxis(new Vector3(-2f, 1f, 0.5f)));
			Assert.Equal(1, ValkyrieCorpse.LongestAxis(new Vector3(0.1f, 0.9f, -0.3f)));
			Assert.Equal(2, ValkyrieCorpse.LongestAxis(new Vector3(0.1f, -0.2f, -0.9f)));
		}

		[Fact]
		public void ARiderAttackWaitsOutItsInterval()
		{
			Assert.False(ValkyrieCombat.Ready(12f, 10f, 3f));
			Assert.True(ValkyrieCombat.Ready(13f, 10f, 3f));
			Assert.True(ValkyrieCombat.Ready(5f, 0f, 3f));
		}

		[Fact]
		public void HerBonesFallIntoHerBodyLegsAndWings()
		{
			Assert.Equal(ValkyrieAttackPose.BodyPart, ValkyrieAttackPose.PartOf("pelvis"));
			Assert.Equal(ValkyrieAttackPose.BodyPart, ValkyrieAttackPose.PartOf("spine2"));
			Assert.Equal(ValkyrieAttackPose.BodyPart, ValkyrieAttackPose.PartOf("neck"));
			Assert.Equal(ValkyrieAttackPose.BodyPart, ValkyrieAttackPose.PartOf("head"));
			Assert.Equal(ValkyrieAttackPose.BodyPart, ValkyrieAttackPose.PartOf("tail.001"));
			Assert.Equal(ValkyrieAttackPose.LegsPart, ValkyrieAttackPose.PartOf("l_leg1"));
			Assert.Equal(ValkyrieAttackPose.LegsPart, ValkyrieAttackPose.PartOf("r_foot"));
			Assert.Equal(ValkyrieAttackPose.LegsPart, ValkyrieAttackPose.PartOf("l_toe_b1"));
			Assert.Equal(ValkyrieAttackPose.WingsPart, ValkyrieAttackPose.PartOf("l_arm2"));
			Assert.Equal(ValkyrieAttackPose.WingsPart, ValkyrieAttackPose.PartOf("r_shoulder"));
		}

		[Fact]
		public void HerClawsKeepHerLegsAndHalveHerLeanWhileOtherAttacksTakeItAll()
		{
			Assert.Equal(0.8f, ValkyrieAttackPose.PartShare(ValkyrieAttackPose.BodyPart, 0.8f, false));
			Assert.Equal(0.8f, ValkyrieAttackPose.PartShare(ValkyrieAttackPose.LegsPart, 0.8f, false));
			Assert.Equal(0.8f * ValkyrieAttackPose.ClawsBodyShare, ValkyrieAttackPose.PartShare(ValkyrieAttackPose.BodyPart, 0.8f, true), 5);
			Assert.Equal(0f, ValkyrieAttackPose.PartShare(ValkyrieAttackPose.LegsPart, 0.8f, true));
			Assert.Equal(0.8f, ValkyrieAttackPose.PartShare(ValkyrieAttackPose.WingsPart, 0.8f, true));
		}

		[Fact]
		public void HerPelvisAndTailAreRenamedAndTheRestKeepTheirNames()
		{
			Assert.Equal("hip", ValkyrieAttackPose.RigName("pelvis"));
			Assert.Equal("tail0", ValkyrieAttackPose.RigName("tail"));
			Assert.Equal("tail1", ValkyrieAttackPose.RigName("tail.001"));
			Assert.Equal("tail2", ValkyrieAttackPose.RigName("tail.002"));
			Assert.Equal("l_arm2", ValkyrieAttackPose.RigName("l_arm2"));
			Assert.Equal("spine1", ValkyrieAttackPose.RigName("spine1"));
		}
	}
}
