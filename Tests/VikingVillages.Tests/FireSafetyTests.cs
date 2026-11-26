using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a living viking's path bends around a fire and where it stands to cook over one
	/// </summary>
	public class FireSafetyTests
	{
		/// <summary>
		/// How far past a fire's flames a viking stands to cook over it, in meters - FireSafety's CookMargin
		/// </summary>
		private const float CookMargin = 1.75f;

		/// <summary>
		/// How far a bonfire's flames reach from its middle, and how near a cauldron the game lets a player use it, in
		/// meters
		/// </summary>
		private const float BonfireFlames = 1.8f;
		private const float CauldronUseDistance = 1.9f;

		/// <summary>
		/// How far apart the spots VillageCooking tries for a cauldron are, in meters - its OffsetStep
		/// </summary>
		private const float OffsetStep = 0.1f;

		/// <summary>
		/// How much a distance can come up short of where it should be from float rounding, in meters
		/// </summary>
		private const float Tolerance = 0.0001f;

		/// <summary>
		/// Works out how near a segment comes to a point, seen from above
		/// </summary>
		/// <param name="a">One end of the segment</param>
		/// <param name="b">The other end</param>
		/// <param name="point">The point</param>
		/// <returns>The distance, in meters</returns>
		private static float SegmentDistance(Vector3 a, Vector3 b, Vector3 point)
		{
			Vector2 start = new Vector2(a.x - point.x, a.z - point.z);
			Vector2 leg = new Vector2(b.x - a.x, b.z - a.z);
			float along = ((leg.sqrMagnitude > 0f) ? Mathf.Clamp01(-Vector2.Dot(start, leg) / leg.sqrMagnitude) : 0f);
			return((start + leg * along).magnitude);
		}

		/// <summary>
		/// Checks that every straight stretch of a bent path, from its start through its corners to its end, keeps a
		/// distance from a point
		/// </summary>
		/// <param name="start">Where the path starts</param>
		/// <param name="corners">Its corners</param>
		/// <param name="end">Where it ends</param>
		/// <param name="middle">The point</param>
		/// <param name="clear">How far from the point it keeps, in meters</param>
		private static void AssertKeepsClear(Vector3 start, List<Vector3> corners, Vector3 end, Vector3 middle, float clear)
		{
			List<Vector3> points = new List<Vector3> { start };
			points.AddRange(corners);
			points.Add(end);
			for (int i = 0; i + 1 < points.Count; i++)
			{
				Assert.True(SegmentDistance(points[i], points[i + 1], middle) >= clear - Tolerance);
			}
		}

		/// <summary>
		/// Checks that a leg passing no nearer than the distance, or exactly at it, isn't bent
		/// </summary>
		/// <param name="side">How far to the side of the fire's middle the leg passes, in meters</param>
		[Theory]
		[InlineData(6f)]
		[InlineData(5f)]
		[InlineData(-5f)]
		public void LegKeepingClearIsLeftAlone(float side)
		{
			Assert.Null(FireSafety.Bend(new Vector3(-10f, 0f, side), new Vector3(10f, 0f, side), Vector3.zero, 5f));
		}

		/// <summary>
		/// Checks that a leg heading away from the fire isn't bent, though the line it lies on runs through the middle
		/// </summary>
		[Fact]
		public void LegHeadingAwayIsLeftAlone()
		{
			Assert.Null(FireSafety.Bend(new Vector3(-10f, 0f, 0f), new Vector3(-20f, 0f, 0f), Vector3.zero, 5f));
		}

		/// <summary>
		/// Checks that a leg starting or ending inside the distance isn't bent, since the viking is running out of the
		/// flames or headed for a spot beside them
		/// </summary>
		[Fact]
		public void LegStartingOrEndingTooNearIsLeftAlone()
		{
			Assert.Null(FireSafety.Bend(new Vector3(-4.9f, 0f, 0f), new Vector3(10f, 0f, 0f), Vector3.zero, 5f));
			Assert.Null(FireSafety.Bend(new Vector3(-10f, 0f, 0f), new Vector3(0f, 0f, 2f), Vector3.zero, 5f));
		}

		/// <summary>
		/// Checks that a leg straight through the fire's middle gets corners at least the distance out, and that the path
		/// through them keeps clear all along
		/// </summary>
		[Fact]
		public void LegThroughTheMiddleBendsAround()
		{
			Vector3 middle = new Vector3(100f, 20f, -50f);
			Vector3 start = middle + new Vector3(-10f, 0f, 0f);
			Vector3 end = middle + new Vector3(10f, 0f, 0f);
			List<Vector3> corners = FireSafety.Bend(start, end, middle, 5f);
			Assert.NotNull(corners);
			// A half turn has a corner between each 30 degree stretch - 5, or 6 when rounding takes the turn just past 180
			Assert.InRange(corners.Count, 5, 6);
			foreach (Vector3 corner in corners)
			{
				Assert.True(Utils.DistanceXZ(corner, middle) >= 5f);
			}
			AssertKeepsClear(start, corners, end, middle, 5f);
		}

		/// <summary>
		/// Checks that a leg passing a little to one side of the fire's middle goes round that side, the shorter way,
		/// keeping clear all along
		/// </summary>
		/// <param name="side">How far to the side of the fire's middle the leg passes, in meters</param>
		[Theory]
		[InlineData(1f)]
		[InlineData(-1f)]
		[InlineData(4.9f)]
		public void LegGoesRoundTheShorterWay(float side)
		{
			Vector3 start = new Vector3(-10f, 0f, side);
			Vector3 end = new Vector3(10f, 0f, side);
			List<Vector3> corners = FireSafety.Bend(start, end, Vector3.zero, 5f);
			Assert.NotNull(corners);
			Assert.NotEmpty(corners);
			foreach (Vector3 corner in corners)
			{
				Assert.Equal(Mathf.Sign(side), Mathf.Sign(corner.z));
			}
			AssertKeepsClear(start, corners, end, Vector3.zero, 5f);
		}

		/// <summary>
		/// Checks that the corners' heights climb evenly from the leg's start to its end
		/// </summary>
		[Fact]
		public void CornersClimbFromStartToEnd()
		{
			// The leg turns about 169 degrees around the middle, so it has 6 stretches and 5 corners
			List<Vector3> corners = FireSafety.Bend(new Vector3(-10f, 0f, 1f), new Vector3(10f, 6f, 1f), Vector3.zero, 5f);
			Assert.NotNull(corners);
			Assert.Equal(5, corners.Count);
			for (int i = 0; i < corners.Count; i++)
			{
				Assert.Equal(i + 1f, corners[i].y, 4);
			}
		}

		/// <summary>
		/// Checks that a viking cooking at a station over a fire stands CookMargin past the flames on the station's side,
		/// at the station's height
		/// </summary>
		[Fact]
		public void CookSpotIsOnTheStationsSide()
		{
			Vector3 middle = new Vector3(10f, 5f, 10f);
			Vector3 spot = FireSafety.SpotAround(new Vector3(10f, 5.8f, 11f), new Vector3(30f, 4f, 10f), middle, 1.5f);
			Assert.Equal(10f, spot.x, 4);
			Assert.Equal(10f + 1.5f + CookMargin, spot.z, 4);
			Assert.Equal(5.8f, spot.y, 4);
		}

		/// <summary>
		/// Checks that the spot is the same distance past the flames whichever side the station leans to, however far
		/// from the middle it stands
		/// </summary>
		/// <param name="x">Where the station stands across from the fire's middle</param>
		/// <param name="z">Where it stands deep from it</param>
		[Theory]
		[InlineData(0.3f, 0.4f)]
		[InlineData(-3f, 2f)]
		[InlineData(0f, -8f)]
		public void CookSpotIsFlamesPlusMarginOut(float x, float z)
		{
			Vector3 spot = FireSafety.SpotAround(new Vector3(x, 0f, z), new Vector3(20f, 0f, 20f), Vector3.zero, 2f);
			Assert.Equal(2f + CookMargin, Utils.DistanceXZ(spot, Vector3.zero), 4);
			Assert.True(Vector2.Dot(new Vector2(spot.x, spot.z), new Vector2(x, z)) > 0f);
		}

		/// <summary>
		/// Checks that a station right over the fire's middle has the viking stand on its own side, ignoring how high it
		/// stands
		/// </summary>
		[Fact]
		public void StationOverTheMiddleFallsBackOnTheVikingsSide()
		{
			Vector3 spot = FireSafety.SpotAround(new Vector3(0f, 1f, 0.05f), new Vector3(-6f, 40f, 0f), Vector3.zero, 1f);
			Assert.Equal(-1f - CookMargin, spot.x, 4);
			Assert.Equal(0f, spot.z, 4);
			Assert.Equal(1f, spot.y, 4);
		}

		/// <summary>
		/// Checks that with both the station and the viking over the fire's middle, the viking stands along the world's
		/// forward axis
		/// </summary>
		[Fact]
		public void EverythingOverTheMiddleFallsBackOnForward()
		{
			Vector3 spot = FireSafety.SpotAround(new Vector3(0f, 1f, 0f), new Vector3(0f, 3f, 0f), Vector3.zero, 1f);
			Assert.Equal(0f, spot.x, 4);
			Assert.Equal(1f + CookMargin, spot.z, 4);
			Assert.Equal(1f, spot.y, 4);
		}

		/// <summary>
		/// Checks that a cauldron within a step of the edge of a bonfire's flames, as VillageCooking places it, is worked
		/// from outside them within the game's use distance
		/// </summary>
		/// <param name="angle">Which way from the fire's middle the cauldron stands, in degrees</param>
		/// <param name="inside">How far inside the flames' edge it stands, in steps</param>
		[Theory]
		[InlineData(0f, 0)]
		[InlineData(90f, 1)]
		[InlineData(225f, 1)]
		public void CauldronAtTheFlamesEdgeIsInReachFromOutside(float angle, int inside)
		{
			Vector3 middle = new Vector3(50f, 30f, -20f);
			Vector3 outward = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
			Vector3 cauldron = middle + outward * (BonfireFlames - inside * OffsetStep);
			Vector3 spot = FireSafety.SpotAround(cauldron, middle + new Vector3(-10f, 0f, 3f), middle, BonfireFlames);
			Assert.True(Utils.DistanceXZ(spot, middle) > BonfireFlames);
			Assert.True(Vector3.Distance(spot, cauldron) < CauldronUseDistance);
		}

		/// <summary>
		/// Checks that a cauldron over a bonfire's middle is out of the game's use distance from outside its flames, so
		/// it would only be used from inside them
		/// </summary>
		[Fact]
		public void CauldronOverTheMiddleIsOutOfReachFromOutside()
		{
			Vector3 spot = FireSafety.SpotAround(Vector3.zero, new Vector3(-10f, 0f, 3f), Vector3.zero, BonfireFlames);
			Assert.True(Vector3.Distance(spot, Vector3.zero) > CauldronUseDistance);
		}
	}
}
