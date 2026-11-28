using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the part of the landing clip a valkyrie's landing plays, where her pelvis is held, when she
	/// glides, when a rider brings her down, and her waddle
	/// </summary>
	public class ValkyrieMotionTests
	{
		private const float Length = 3.83f;

		[Fact]
		public void LandingPlaysFromTheFlareToTheEnd()
		{
			Assert.Equal(ValkyrieMotion.FlareTime, ValkyrieMotion.LandingTime(0f, Length), 4);
			Assert.Equal(ValkyrieMotion.FlareTime + 0.5f, ValkyrieMotion.LandingTime(0.5f, Length), 4);
			Assert.Equal(Length, ValkyrieMotion.LandingTime(10f, Length), 4);
		}

		[Fact]
		public void LandingDoesntPlayBeforeItStarts()
		{
			Assert.Equal(ValkyrieMotion.FlareTime, ValkyrieMotion.LandingTime(-1f, Length), 4);
		}

		[Fact]
		public void ThePelvisIsHeldFromFlyingToStandingUntilTouchdown()
		{
			Assert.True(ValkyrieMotion.HeldPelvis(ValkyrieMotion.LevelTime, out Vector3 level));
			Assert.Equal(ValkyrieMotion.FlyingPelvis, level);
			Assert.True(ValkyrieMotion.HeldPelvis(0f, out Vector3 diving));
			Assert.Equal(ValkyrieMotion.FlyingPelvis, diving);
			float middle = (ValkyrieMotion.LevelTime + ValkyrieMotion.TouchdownTime) / 2f;
			Assert.True(ValkyrieMotion.HeldPelvis(middle, out Vector3 between));
			Vector3 expected = Vector3.Lerp(ValkyrieMotion.FlyingPelvis, ValkyrieMotion.StandingPelvis, 0.5f);
			Assert.Equal(expected.y, between.y, 5);
			Assert.Equal(expected.z, between.z, 5);
			Assert.False(ValkyrieMotion.HeldPelvis(ValkyrieMotion.TouchdownTime, out _));
			Assert.False(ValkyrieMotion.HeldPelvis(Length, out _));
		}

		[Fact]
		public void SheFlapsClimbingAndSlowAndGlidesComingDownAndFast()
		{
			Assert.Equal(0f, ValkyrieMotion.GlideShare(20f, 1f, 8f));
			Assert.Equal(1f, ValkyrieMotion.GlideShare(0f, -2f, 8f));
			Assert.Equal(0f, ValkyrieMotion.GlideShare(4f, 0f, 8f));
			Assert.Equal(0.5f, ValkyrieMotion.GlideShare(12f, 0f, 8f), 4);
			Assert.Equal(1f, ValkyrieMotion.GlideShare(16f, 0f, 8f));
		}

		[Fact]
		public void ClimbingWinsOverComingDown()
		{
			Assert.Equal(1f, ValkyrieMotion.Vertical(true, true));
			Assert.Equal(1f, ValkyrieMotion.Vertical(true, false));
			Assert.Equal(-1f, ValkyrieMotion.Vertical(false, true));
			Assert.Equal(0f, ValkyrieMotion.Vertical(false, false));
		}

		[Fact]
		public void SheLandsOnlyGoingDownLowOrOnTheGround()
		{
			Assert.True(ValkyrieMotion.Lands(-1f, 1f, false));
			Assert.True(ValkyrieMotion.Lands(-1f, 50f, true));
			Assert.False(ValkyrieMotion.Lands(-1f, ValkyrieMotion.LandAltitude, false));
			Assert.False(ValkyrieMotion.Lands(0f, 0.5f, true));
			Assert.False(ValkyrieMotion.Lands(1f, 0.5f, true));
		}

		[Fact]
		public void AFullWaddleCoversItsLengthAtHerSize()
		{
			Assert.Equal(Mathf.PI * 2f, ValkyrieMotion.WaddleStep(ValkyrieMotion.WaddleLength, 1f, 1f), 4);
			Assert.Equal(Mathf.PI, ValkyrieMotion.WaddleStep(ValkyrieMotion.WaddleLength, 2f, 1f), 4);
			Assert.Equal(0f, ValkyrieMotion.WaddleStep(0f, 1f, 1f));
		}

		[Fact]
		public void SheWaddlesMoreAsSheSpeedsUpToAFullWaddle()
		{
			Assert.Equal(0f, ValkyrieMotion.WaddleStrength(0f));
			Assert.Equal(0.5f, ValkyrieMotion.WaddleStrength(ValkyrieMotion.FullWaddleSpeed / 2f), 4);
			Assert.Equal(1f, ValkyrieMotion.WaddleStrength(ValkyrieMotion.FullWaddleSpeed * 3f));
		}

		[Fact]
		public void TheLegsSwingOppositeWaysAndOnlyTheOneComingForwardBends()
		{
			float quarter = Mathf.PI / 2f;
			Assert.Equal(ValkyrieMotion.WaddleSwing, ValkyrieMotion.Swing(quarter, 1f, true), 4);
			Assert.Equal(-ValkyrieMotion.WaddleSwing, ValkyrieMotion.Swing(quarter, 1f, false), 4);
			Assert.Equal(ValkyrieMotion.WaddleKnee, ValkyrieMotion.Knee(0f, 1f, true), 4);
			Assert.Equal(0f, ValkyrieMotion.Knee(0f, 1f, false));
			Assert.Equal(0f, ValkyrieMotion.Knee(Mathf.PI, 1f, true), 4);
			Assert.Equal(ValkyrieMotion.WaddleKnee, ValkyrieMotion.Knee(Mathf.PI, 1f, false), 4);
		}

		[Fact]
		public void SheFightsFromTheAirWhatSheCantFightOnTheGround()
		{
			Assert.True(ValkyrieMotion.FightsInTheAir(true, false, 5f, false, false));
			Assert.True(ValkyrieMotion.FightsInTheAir(false, true, 5f, false, false));
			Assert.True(ValkyrieMotion.FightsInTheAir(false, false, ValkyrieMotion.FarTarget + 1f, false, false));
			Assert.True(ValkyrieMotion.FightsInTheAir(false, false, 5f, true, false));
			Assert.True(ValkyrieMotion.FightsInTheAir(false, false, 5f, false, true));
			Assert.False(ValkyrieMotion.FightsInTheAir(false, false, 5f, false, false));
			Assert.False(ValkyrieMotion.FightsInTheAir(false, false, ValkyrieMotion.FarTarget, false, false));
		}

		[Fact]
		public void SheStaysUpAWhileThenLandsForAGroundFightOrOnceItsOver()
		{
			float settled = ValkyrieMotion.MinAirTime + 1f;
			Assert.False(ValkyrieMotion.AILands(true, false, ValkyrieMotion.MinAirTime / 2f, 0f));
			Assert.False(ValkyrieMotion.AILands(false, false, ValkyrieMotion.MinAirTime / 2f, 100f));
			Assert.True(ValkyrieMotion.AILands(true, false, settled, 0f));
			Assert.False(ValkyrieMotion.AILands(true, true, settled, 0f));
			Assert.False(ValkyrieMotion.AILands(false, false, settled, ValkyrieMotion.LandDelay / 2f));
			Assert.True(ValkyrieMotion.AILands(false, false, settled, ValkyrieMotion.LandDelay + 1f));
		}

		[Fact]
		public void StretchedWingsBeatGentlyAndOnlyWhileSpread()
		{
			float quarterBeat = 0.25f / ValkyrieMotion.StretchBeat;
			Assert.Equal(ValkyrieMotion.StretchLift, ValkyrieMotion.StretchFlap(quarterBeat, 1f, false), 3);
			Assert.Equal(ValkyrieMotion.StretchLift / 2f, ValkyrieMotion.StretchFlap(quarterBeat, 0.5f, false), 3);
			Assert.Equal(0f, ValkyrieMotion.StretchFlap(quarterBeat, 0f, false));
			Assert.True(Mathf.Abs(ValkyrieMotion.StretchFlap(quarterBeat, 1f, true)) <= ValkyrieMotion.StretchLift * ValkyrieMotion.StretchOuter + 0.001f);
		}

		[Fact]
		public void AStretchOpensHoldsAndCloses()
		{
			float open = ValkyrieMotion.StretchOpen;
			float hold = ValkyrieMotion.StretchHold;
			float close = ValkyrieMotion.StretchClose;
			Assert.Equal(0f, ValkyrieMotion.StretchShare(0f));
			Assert.Equal(0.5f, ValkyrieMotion.StretchShare(open / 2f), 4);
			Assert.Equal(1f, ValkyrieMotion.StretchShare(open + hold / 2f));
			Assert.Equal(0.5f, ValkyrieMotion.StretchShare(open + hold + close / 2f), 4);
			Assert.Equal(0f, ValkyrieMotion.StretchShare(open + hold + close));
			Assert.Equal(0f, ValkyrieMotion.StretchShare(-1f));
		}

		[Fact]
		public void ATurnRoundsToTheNearestQuarter()
		{
			Assert.Equal(0f, ValkyrieMotion.NearestQuarter(12f));
			Assert.Equal(0f, ValkyrieMotion.NearestQuarter(-30f));
			Assert.Equal(180f, ValkyrieMotion.NearestQuarter(171f));
			Assert.Equal(-180f, ValkyrieMotion.NearestQuarter(-160f));
			Assert.Equal(90f, ValkyrieMotion.NearestQuarter(100f));
		}

		[Fact]
		public void StandingSheIsMovedSoHerLowestFootMeetsTheGround()
		{
			Assert.Equal(-1.5f, ValkyrieMotion.GroundShift(10f, 11.5f, 1f), 4);
			Assert.Equal(0.25f, ValkyrieMotion.GroundShift(10f, 9.75f, 1f), 4);
			Assert.Equal(-0.75f, ValkyrieMotion.GroundShift(10f, 11.5f, 0.5f), 4);
			Assert.Equal(0f, ValkyrieMotion.GroundShift(10f, 11.5f, 0f), 4);
		}

		[Fact]
		public void StandingStillShesNeitherRockedNorSwung()
		{
			Assert.Equal(0f, ValkyrieMotion.Roll(1f, 0f));
			Assert.Equal(0f, ValkyrieMotion.Swing(1f, 0f, true));
			Assert.Equal(0f, ValkyrieMotion.Knee(0f, 0f, true));
		}
	}
}
