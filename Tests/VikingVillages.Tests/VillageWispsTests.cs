using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests where a Mistlands village's wisp torches are wanted - around rings, along paths, and wherever a path runs
	/// on with no light
	/// </summary>
	public class VillageWispsTests
	{
		/// <summary>
		/// How much a worked out distance can be off from float rounding
		/// </summary>
		private const float Tolerance = 0.001f;

		/// <summary>
		/// Checks that only the Mistlands' villages get wisp lights
		/// </summary>
		[Fact]
		public void OnlyTheMistlandsGetsWisps()
		{
			Assert.True(VillageWisps.GetsWisps(Heightmap.Biome.Mistlands));
			Assert.False(VillageWisps.GetsWisps(Heightmap.Biome.Meadows));
			Assert.False(VillageWisps.GetsWisps(Heightmap.Biome.AshLands));
		}

		/// <summary>
		/// Checks that a ring gets about one point for every spacing around it, and at least one
		/// </summary>
		[Theory]
		[InlineData(41f, 14f, 18)]
		[InlineData(21f, 14f, 9)]
		[InlineData(1f, 14f, 1)]
		[InlineData(0f, 14f, 1)]
		public void RingCountFollowsTheSpacing(float radius, float spacing, int expected)
		{
			Assert.Equal(expected, VillageWisps.RingCount(radius, spacing));
		}

		/// <summary>
		/// Checks that a ring's points are its radius from its middle, evenly apart, the first where it starts
		/// </summary>
		[Fact]
		public void RingPointsSpreadEvenly()
		{
			Vector3 center = new Vector3(10f, 5f, -3f);
			List<Vector3> points = VillageWisps.RingPoints(center, 8f, 4, 0f);
			Assert.Equal(4, points.Count);
			Assert.Equal(center.x + 8f, points[0].x, Tolerance);
			Assert.Equal(center.z, points[0].z, Tolerance);
			foreach (Vector3 point in points)
			{
				Assert.Equal(8f, Vector3.Distance(point, center), Tolerance);
				Assert.Equal(center.y, point.y, Tolerance);
			}
			Assert.Equal(Vector3.Distance(points[0], points[1]), Vector3.Distance(points[1], points[2]), Tolerance);
		}

		/// <summary>
		/// Checks that points along a path come a step apart, carried over its bends, starting at its first point
		/// </summary>
		[Fact]
		public void AlongStepsOverBends()
		{
			List<Vector3> path = new List<Vector3> { Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 5f) };
			List<Vector3> points = VillageWisps.Along(path, 2f);
			Assert.Equal(5, points.Count);
			Assert.Equal(Vector3.zero, points[0]);
			Assert.Equal(2f, points[1].x, Tolerance);
			Assert.Equal(3f, points[2].x, Tolerance);
			Assert.Equal(1f, points[2].z, Tolerance);
			Assert.Equal(3f, points[3].z, Tolerance);
			Assert.Equal(5f, points[4].z, Tolerance);
		}

		/// <summary>
		/// Checks that an empty path or no path has no points along it
		/// </summary>
		[Fact]
		public void AlongNothingIsNothing()
		{
			Assert.Empty(VillageWisps.Along(null, 2f));
			Assert.Empty(VillageWisps.Along(new List<Vector3>(), 2f));
		}

		/// <summary>
		/// Checks that a point needs a light only past the gap from every light, measured across the ground
		/// </summary>
		[Fact]
		public void NeedsLightPastTheGap()
		{
			List<Vector3> lights = new List<Vector3> { Vector3.zero, new Vector3(30f, 0f, 0f) };
			Assert.False(VillageWisps.NeedsLight(new Vector3(9f, 20f, 0f), lights, 10f));
			Assert.False(VillageWisps.NeedsLight(new Vector3(25f, 0f, 0f), lights, 10f));
			Assert.True(VillageWisps.NeedsLight(new Vector3(15f, 0f, 0f), lights, 10f));
			Assert.True(VillageWisps.NeedsLight(Vector3.zero, new List<Vector3>(), 10f));
		}
	}
}
