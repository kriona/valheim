using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a PC viking's stretch eases in and out, how its head nods, what a client does with its emotes,
	/// and when it counts as standing
	/// </summary>
	public class PoseCurvesTests
	{
		/// <summary>
		/// A moment in ZNet time ticks the emotes and stretches are read at
		/// </summary>
		private const long Now = 1000000000L;

		/// <summary>
		/// Checks that the stretch shows nothing before it starts, as it starts, as it ends and after
		/// </summary>
		/// <param name="time">Seconds since it started</param>
		[Theory]
		[InlineData(-1f)]
		[InlineData(0f)]
		[InlineData(2.4f)]
		[InlineData(3f)]
		public void StretchCurveIsZeroOutsideTheStretch(float time)
		{
			Assert.Equal(0f, PoseCurves.StretchCurve(time), 4);
		}

		/// <summary>
		/// Checks that the stretch is shown in full between easing in for 0.7 seconds and easing out over the last 0.8
		/// </summary>
		/// <param name="time">Seconds since it started</param>
		[Theory]
		[InlineData(0.7f)]
		[InlineData(1.2f)]
		[InlineData(1.6f)]
		public void StretchCurveHoldsItsPlateau(float time)
		{
			Assert.Equal(1f, PoseCurves.StretchCurve(time), 4);
		}

		/// <summary>
		/// Checks that the stretch is shown half way at the middle of easing in and of easing out
		/// </summary>
		/// <param name="time">Seconds since it started</param>
		[Theory]
		[InlineData(0.35f)]
		[InlineData(2f)]
		public void StretchCurveIsHalfWayThroughEachEase(float time)
		{
			Assert.Equal(0.5f, PoseCurves.StretchCurve(time), 3);
		}

		/// <summary>
		/// Checks that the stretch is always between none and all of it, from before it starts to after it ends
		/// </summary>
		[Fact]
		public void StretchCurveStaysWithinZeroAndOne()
		{
			for (float time = -1f; time <= 4f; time += 0.05f)
			{
				float weight = PoseCurves.StretchCurve(time);
				Assert.InRange(weight, 0f, 1f);
			}
		}

		/// <summary>
		/// Checks that the head isn't nodding with no nod, before a nod starts or after it ends
		/// </summary>
		/// <param name="now">The time</param>
		/// <param name="start">When the nod started</param>
		[Theory]
		[InlineData(10.25f, -1f)]
		[InlineData(9.9f, 10f)]
		[InlineData(10.6f, 10f)]
		public void BeatShapeIsZeroOutsideTheNod(float now, float start)
		{
			Assert.Equal(0f, PoseCurves.BeatShape(now, start, 0.5f, false));
			Assert.Equal(0f, PoseCurves.BeatShape(now, start, 0.5f, true));
		}

		/// <summary>
		/// Checks that a single nod rises from rest to its deepest at its middle, half way at a quarter of the way
		/// </summary>
		[Fact]
		public void BeatShapeSingleNodPeaksInTheMiddle()
		{
			Assert.Equal(0f, PoseCurves.BeatShape(10f, 10f, 0.5f, false), 4);
			Assert.Equal(0.5f, PoseCurves.BeatShape(10.125f, 10f, 0.5f, false), 4);
			Assert.Equal(1f, PoseCurves.BeatShape(10.25f, 10f, 0.5f, false), 4);
			Assert.Equal(0f, PoseCurves.BeatShape(10.5f, 10f, 0.5f, false), 4);
		}

		/// <summary>
		/// Checks that a double nod is deepest a quarter and three quarters of the way through and back at rest in the
		/// middle
		/// </summary>
		[Fact]
		public void BeatShapeDoubleNodPeaksTwice()
		{
			Assert.Equal(1f, PoseCurves.BeatShape(10.125f, 10f, 0.5f, true), 4);
			Assert.Equal(0f, PoseCurves.BeatShape(10.25f, 10f, 0.5f, true), 4);
			Assert.Equal(1f, PoseCurves.BeatShape(10.375f, 10f, 0.5f, true), 4);
		}

		/// <summary>
		/// Checks that no emote, or a one-shot that started more than 3 seconds ago, ends the emote playing on a client
		/// that has read the ZDO before, and does nothing on one reading it the first time
		/// </summary>
		/// <param name="emote">The emote</param>
		/// <param name="oneshot">Whether it plays once</param>
		/// <param name="age">How long ago it started, in ticks</param>
		[Theory]
		[InlineData("", false, 0L)]
		[InlineData("", true, 0L)]
		[InlineData("point", true, 30000001L)]
		[InlineData("wave", true, 600000000L)]
		public void EmoteActionEndsAStaleOrEmptyEmote(string emote, bool oneshot, long age)
		{
			Assert.Equal(PoseCurves.EmoteStep.Stop, PoseCurves.EmoteAction(false, emote, oneshot, Now - age, Now));
			Assert.Equal(PoseCurves.EmoteStep.None, PoseCurves.EmoteAction(true, emote, oneshot, Now - age, Now));
		}

		/// <summary>
		/// Checks that a looping emote plays however long ago it started
		/// </summary>
		/// <param name="age">How long ago it started, in ticks</param>
		[Theory]
		[InlineData(0L)]
		[InlineData(30000001L)]
		[InlineData(600000000L)]
		public void EmoteActionLoopsALoopingEmote(long age)
		{
			Assert.Equal(PoseCurves.EmoteStep.Loop, PoseCurves.EmoteAction(false, "sit", false, Now - age, Now));
			Assert.Equal(PoseCurves.EmoteStep.Loop, PoseCurves.EmoteAction(true, "sit", false, Now - age, Now));
		}

		/// <summary>
		/// Checks that a one-shot that started no more than 3 seconds ago plays, up to exactly 3 seconds
		/// </summary>
		/// <param name="age">How long ago it started, in ticks</param>
		[Theory]
		[InlineData(0L)]
		[InlineData(10000000L)]
		[InlineData(30000000L)]
		public void EmoteActionTriggersAFreshOneShot(long age)
		{
			Assert.Equal(PoseCurves.EmoteStep.Trigger, PoseCurves.EmoteAction(false, "laugh", true, Now - age, Now));
			Assert.Equal(PoseCurves.EmoteStep.Trigger, PoseCurves.EmoteAction(true, "laugh", true, Now - age, Now));
		}

		/// <summary>
		/// Checks that a stretch plays when there is one that started no more than 3 seconds ago
		/// </summary>
		[Fact]
		public void StretchFreshWithinTheLateLimit()
		{
			Assert.True(PoseCurves.StretchFresh(Now, Now));
			Assert.True(PoseCurves.StretchFresh(Now - 30000000L, Now));
			Assert.False(PoseCurves.StretchFresh(Now - 30000001L, Now));
		}

		/// <summary>
		/// Checks that no stretch doesn't play, even early on when 0 ticks is recent
		/// </summary>
		[Fact]
		public void StretchFreshNeedsAStretch()
		{
			Assert.False(PoseCurves.StretchFresh(0L, 0L));
			Assert.False(PoseCurves.StretchFresh(0L, Now));
		}

		/// <summary>
		/// Checks that a viking stands when its hips are 0.7 meters or more above its lower foot, going by the lower
		/// one rather than a raised one
		/// </summary>
		[Fact]
		public void IsStandingGoesByTheLowerFoot()
		{
			Vector3 up = new Vector3(0f, 1f, 0f);
			Assert.True(PoseCurves.IsStanding(new Vector3(0f, 1f, 0f), new Vector3(0f, 0.25f, 0f), new Vector3(0.2f, 0.6f, 0f), up, 1f));
			Assert.True(PoseCurves.IsStanding(new Vector3(0f, 1f, 0f), new Vector3(0.2f, 0.6f, 0f), new Vector3(0f, 0.25f, 0f), up, 1f));
			Assert.False(PoseCurves.IsStanding(new Vector3(0f, 0.9f, 0f), new Vector3(0f, 0.25f, 0f), new Vector3(0f, 0.3f, 0f), up, 1f));
		}

		/// <summary>
		/// Checks that a bigger viking needs its hips higher to stand
		/// </summary>
		[Fact]
		public void IsStandingScalesWithTheViking()
		{
			Vector3 up = new Vector3(0f, 1f, 0f);
			Vector3 hips = new Vector3(0f, 1f, 0f);
			Vector3 foot = new Vector3(0f, 0.2f, 0f);
			Assert.True(PoseCurves.IsStanding(hips, foot, foot, up, 1f));
			Assert.False(PoseCurves.IsStanding(hips, foot, foot, up, 1.2f));
		}

		/// <summary>
		/// Checks that the hips' height is measured along the viking's own up, not the world's
		/// </summary>
		[Fact]
		public void IsStandingGoesByTheVikingsUp()
		{
			Vector3 up = new Vector3(1f, 0f, 0f);
			Vector3 foot = new Vector3(0.1f, 3f, 0f);
			Assert.True(PoseCurves.IsStanding(new Vector3(0.9f, 0f, 0f), foot, foot, up, 1f));
			Assert.False(PoseCurves.IsStanding(new Vector3(0.5f, 5f, 0f), foot, foot, up, 1f));
		}
	}
}
