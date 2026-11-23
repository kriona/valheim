using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a village's paths are traced on the ground - one that leads from the village back into it stretches
	/// it along the path, bends and all, while a dead end, a road off to somewhere else or a path inside it doesn't
	/// </summary>
	public class VillageGroundPathsTests
	{
		/// <summary>
		/// The village in the tests - a core 10 meters around the middle, and a house 5 meters around 40 meters east
		/// </summary>
		private static readonly List<Village.Zone> Built = new List<Village.Zone>
		{
			new Village.Zone { Center = new Vector3(0f, 0f, 0f), Radius = 10f },
			new Village.Zone { Center = new Vector3(40f, 0f, 0f), Radius = 5f }
		};

		/// <summary>
		/// Traces the village's paths with 4 meter circles at most 3 meters apart, the village's own
		/// </summary>
		/// <param name="isPath">Where the path is</param>
		/// <returns>The circles along the paths</returns>
		private static List<Village.Zone> Trace(System.Func<Vector3, bool> isPath)
		{
			return(VillageGroundPaths.Trace(Built, isPath, 4f, 3f));
		}

		/// <summary>
		/// Checks whether a spot is within some circles
		/// </summary>
		/// <param name="circles">The circles</param>
		/// <param name="x">The spot's place east</param>
		/// <param name="z">Its place north</param>
		/// <returns>True when it is</returns>
		private static bool Covers(List<Village.Zone> circles, float x, float z)
		{
			return(circles.Exists((Village.Zone circle) => Village.FlatDistance(circle.Center, new Vector3(x, 0f, z)) <= circle.Radius));
		}

		/// <summary>
		/// Checks that a path from the core to the house stretches the village all along it, 4 meters either side
		/// </summary>
		[Fact]
		public void StretchesAlongAPathToAHouse()
		{
			List<Village.Zone> bands = Trace((Vector3 spot) => Mathf.Abs(spot.z) <= 1f && spot.x >= 0f && spot.x <= 40f);
			Assert.NotEmpty(bands);
			Assert.All(bands, (Village.Zone band) => Assert.Equal(4f, band.Radius, 3));
			for (float x = 11f; x <= 34f; x += 1f)
			{
				Assert.True(Covers(bands, x, 0f), "No band at " + x);
				Assert.True(Covers(bands, x, 2.5f), "Band too narrow at " + x);
			}
			Assert.False(Covers(bands, 22f, 8f));
		}

		/// <summary>
		/// Checks that a path from the core west to a house stretches the village all along it the same as one east
		/// </summary>
		[Fact]
		public void StretchesAlongAPathToAHouseWest()
		{
			List<Village.Zone> west = new List<Village.Zone>
			{
				new Village.Zone { Center = new Vector3(0f, 0f, 0f), Radius = 10f },
				new Village.Zone { Center = new Vector3(-40f, 0f, 0f), Radius = 5f }
			};
			List<Village.Zone> bands = VillageGroundPaths.Trace(west, (Vector3 spot) => Mathf.Abs(spot.z) <= 1f && spot.x <= 0f && spot.x >= -40f, 4f, 3f);
			Assert.NotEmpty(bands);
			for (float x = -11f; x >= -34f; x -= 1f)
			{
				Assert.True(Covers(bands, x, 0f), "No band at " + x);
				Assert.True(Covers(bands, x, 2.5f), "Band too narrow at " + x);
			}
			Assert.False(Covers(bands, -22f, 8f));
		}

		/// <summary>
		/// Checks that a spot's grid cell has its middle half a meter in from the cell's lower corner, below zero as
		/// well as above it
		/// </summary>
		/// <param name="x">The spot's place east</param>
		/// <param name="z">Its place north</param>
		/// <param name="middleX">The cell's middle east</param>
		/// <param name="middleZ">Its middle north</param>
		[Theory]
		[InlineData(-0.3f, -7.9f, -0.5f, -7.5f)]
		[InlineData(5.2f, -1000.5f, 5.5f, -1000.5f)]
		[InlineData(-100000f, 3f, -99999.5f, 3.5f)]
		[InlineData(0f, 0f, 0.5f, 0.5f)]
		[InlineData(12.99f, 0.01f, 12.5f, 0.5f)]
		public void CellMiddlesRoundTrip(float x, float z, float middleX, float middleZ)
		{
			Vector3 middle = VillageGroundPaths.Middle(VillageGroundPaths.Key(new Vector3(x, 7f, z)));
			Assert.Equal(middleX, middle.x, 3);
			Assert.Equal(0f, middle.y);
			Assert.Equal(middleZ, middle.z, 3);
			Assert.Equal(VillageGroundPaths.Key(new Vector3(x, 0f, z)), VillageGroundPaths.Key(middle));
		}

		/// <summary>
		/// Checks that the four cells meeting at zero, and their neighbors either side of it, all have keys of their own
		/// </summary>
		[Fact]
		public void CellsAcrossZeroHaveKeysOfTheirOwn()
		{
			HashSet<long> keys = new HashSet<long>();
			for (float x = -1.5f; x <= 1.5f; x += 1f)
			{
				for (float z = -1.5f; z <= 1.5f; z += 1f)
				{
					Assert.True(keys.Add(VillageGroundPaths.Key(new Vector3(x, 0f, z))), "Key shared at " + x + ", " + z);
				}
			}
			Assert.Equal(16, keys.Count);
		}

		/// <summary>
		/// Checks that a path that bends away and comes back into the core somewhere else is followed around its bends
		/// </summary>
		[Fact]
		public void FollowsAPathAroundItsBends()
		{
			List<Village.Zone> bands = Trace((Vector3 spot) =>
				(spot.x >= 9f && spot.x <= 21f && Mathf.Abs(spot.z) <= 1f) ||
				(spot.x >= 19f && spot.x <= 21f && spot.z >= -1f && spot.z <= 21f) ||
				(spot.z >= 19f && spot.z <= 21f && spot.x >= -1f && spot.x <= 21f) ||
				(spot.x >= -1f && spot.x <= 1f && spot.z >= 9f && spot.z <= 21f));
			Assert.True(Covers(bands, 20f, 10f));
			Assert.True(Covers(bands, 10f, 20f));
			Assert.True(Covers(bands, 0f, 15f));
		}

		/// <summary>
		/// Checks that a dead end leading out of the village and stopping doesn't stretch it
		/// </summary>
		[Fact]
		public void LeavesOutADeadEnd()
		{
			Assert.Empty(Trace((Vector3 spot) => Mathf.Abs(spot.z) <= 1f && spot.x >= 0f && spot.x <= 25f));
		}

		/// <summary>
		/// Checks that a road off to somewhere else, leaving the village and never coming back, doesn't stretch it
		/// </summary>
		[Fact]
		public void LeavesOutARoadAway()
		{
			Assert.Empty(Trace((Vector3 spot) => Mathf.Abs(spot.x) <= 1f && spot.z >= 0f && spot.z <= 500f));
		}

		/// <summary>
		/// Checks that a path inside the village adds nothing to it
		/// </summary>
		[Fact]
		public void LeavesOutAPathInside()
		{
			Assert.Empty(Trace((Vector3 spot) => Mathf.Abs(spot.z) <= 1f && spot.x >= -8f && spot.x <= 8f));
		}

		/// <summary>
		/// Checks that the spots a path runs into the village at are measured from the two farthest apart
		/// </summary>
		[Fact]
		public void MeasuresHowFarApartAPathsEndsAre()
		{
			List<Vector3> touches = new List<Vector3> { new Vector3(1f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 0f) };
			Assert.Equal(10f, VillageGroundPaths.Span(touches), 3);
			Assert.Equal(0f, VillageGroundPaths.Span(new List<Vector3> { new Vector3(3f, 0f, 3f) }), 3);
		}

		/// <summary>
		/// Checks that circles laid over a path's cells leave no cell farther than the step from a circle's middle
		/// </summary>
		[Fact]
		public void CoversEveryCell()
		{
			List<Vector3> cells = new List<Vector3>();
			for (float x = 0f; x < 30f; x += 1f)
			{
				cells.Add(new Vector3(x, 0f, 0f));
			}
			List<Village.Zone> circles = VillageGroundPaths.Cover(cells, 4f, 3f);
			Assert.All(cells, (Vector3 cell) => Assert.Contains(circles, (Village.Zone circle) => Village.FlatDistance(circle.Center, cell) <= 3f));
			Assert.True(circles.Count < cells.Count);
		}
	}
}
