using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests the grid a village's paths are found on - which cell a point is in, how the ground is smoothed and
	/// flattened, how a path finds its way between what stands in it, and where it crosses water and on what bridges
	/// </summary>
	public class VillagePathsGridTests
	{
		/// <summary>
		/// The water's height in the tests, the height of the dry ground, and of the ground under water, in meters
		/// </summary>
		private const float Water = 0f;
		private const float Dry = 1f;
		private const float Wet = -1f;

		/// <summary>
		/// Makes a grid of ground from 0 to a size east and north, a cell a meter
		/// </summary>
		/// <param name="size">How far it reaches each way, in meters</param>
		/// <param name="height">The ground's height at each place east and north</param>
		/// <returns>The grid</returns>
		private static VillagePaths.Grid Ground(int size, System.Func<float, float, float> height)
		{
			return(new VillagePaths.Grid(Vector3.zero, new Vector3(size, 0f, size), Water, (Vector3 point) => height(point.x, point.z)));
		}

		/// <summary>
		/// Makes flat dry ground
		/// </summary>
		/// <param name="size">How far it reaches each way, in meters</param>
		/// <returns>The grid</returns>
		private static VillagePaths.Grid Flat(int size)
		{
			return(Ground(size, (float x, float z) => Dry));
		}

		/// <summary>
		/// Makes the ground's height with a river running north to south across it
		/// </summary>
		/// <param name="fromX">The river's west edge, the first cell in the water</param>
		/// <param name="toX">Its east edge, the last cell in the water</param>
		/// <returns>The ground's height at each place east and north</returns>
		private static System.Func<float, float, float> River(float fromX, float toX)
		{
			return((float x, float z) => ((x >= fromX && x <= toX) ? Wet : Dry));
		}

		/// <summary>
		/// Makes a path east along a row of cells, on the ground
		/// </summary>
		/// <param name="height">The ground's height at each place east and north</param>
		/// <param name="fromX">Where it starts east</param>
		/// <param name="toX">Where it ends east</param>
		/// <param name="z">The row's place north</param>
		/// <returns>The path's points</returns>
		private static List<Vector3> Row(System.Func<float, float, float> height, int fromX, int toX, float z)
		{
			List<Vector3> path = new List<Vector3>();
			for (int x = fromX; x <= toX; x++)
			{
				path.Add(new Vector3(x, height(x, z), z));
			}
			return(path);
		}

		/// <summary>
		/// Finds the cheapest way across a grid between two cells
		/// </summary>
		/// <param name="grid">The grid</param>
		/// <param name="fromX">The cell it starts at's place east</param>
		/// <param name="fromZ">Its place north</param>
		/// <param name="toX">The cell it ends at's place east</param>
		/// <param name="toZ">Its place north</param>
		/// <param name="forced">True to step through what's in the way</param>
		/// <returns>The way's points, or null when there's none</returns>
		private static List<Vector3> Search(VillagePaths.Grid grid, int fromX, int fromZ, int toX, int toZ, bool forced)
		{
			return(grid.Search(grid.Cell(new Vector3(fromX, 0f, fromZ)), grid.Cell(new Vector3(toX, 0f, toZ)), forced));
		}

		/// <summary>
		/// Checks that a point is in a cell
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="x">The cell's place east</param>
		/// <param name="z">Its place north</param>
		private static void AssertAt(Vector3 point, float x, float z)
		{
			Assert.Equal(x, point.x, 3);
			Assert.Equal(z, point.z, 3);
		}

		/// <summary>
		/// Makes something a path steers around standing in a single cell
		/// </summary>
		/// <param name="x">Its place east</param>
		/// <param name="z">Its place north</param>
		/// <returns>The object</returns>
		private static VillagePaths.Blocker Post(float x, float z)
		{
			return(new VillagePaths.Blocker { Center = new Vector3(x, 0f, z), Radius = 0.4f, Kind = VillagePaths.Kind.Avoid });
		}

		/// <summary>
		/// Checks that a point is in the cell nearest it, wherever it is up or down
		/// </summary>
		[Fact]
		public void FindsTheCellAPointIsIn()
		{
			VillagePaths.Grid grid = Flat(4);
			Assert.Equal(0, grid.Cell(new Vector3(0f, 0f, 0f)));
			Assert.Equal(8, grid.Cell(new Vector3(3f, 0f, 1f)));
			Assert.Equal(8, grid.Cell(new Vector3(3.4f, 50f, 1.4f)));
			Assert.Equal(24, grid.Cell(new Vector3(4f, 0f, 4f)));
			Assert.Equal(0, grid.Cell(new Vector3(-0.4f, 0f, 0f)));
		}

		/// <summary>
		/// Checks that a point off the grid is in no cell
		/// </summary>
		[Fact]
		public void FindsNoCellOffTheGrid()
		{
			VillagePaths.Grid grid = Flat(4);
			Assert.Equal(-1, grid.Cell(new Vector3(-1f, 0f, 2f)));
			Assert.Equal(-1, grid.Cell(new Vector3(-0.6f, 0f, 0f)));
			Assert.Equal(-1, grid.Cell(new Vector3(4.6f, 0f, 0f)));
			Assert.Equal(-1, grid.Cell(new Vector3(2f, 0f, 5f)));
			Assert.Equal(-1, grid.Cell(new Vector3(0f, 0f, -3f)));
		}

		/// <summary>
		/// Checks that the cells sit on whole meters, starting at or before the area's lowest corner and reaching to or
		/// past its highest
		/// </summary>
		[Fact]
		public void PutsTheCellsOnWholeMeters()
		{
			VillagePaths.Grid grid = new VillagePaths.Grid(new Vector3(0.7f, 0f, -0.3f), new Vector3(3.2f, 0f, 2.2f), Water, (Vector3 point) => Dry);
			Assert.Equal(0, grid.Cell(new Vector3(0f, 0f, -1f)));
			Assert.Equal(24, grid.Cell(new Vector3(4f, 0f, 3f)));
			Assert.Equal(-1, grid.Cell(new Vector3(5f, 0f, 3f)));
			Assert.Equal(-1, grid.Cell(new Vector3(0f, 0f, -2f)));
		}

		/// <summary>
		/// Checks how strongly a spot is flattened from 10 meters out to 20 - fully inside, not at all outside, half
		/// way at the middle and easing in and out between
		/// </summary>
		/// <param name="distance">How far the spot is, in meters</param>
		/// <param name="expected">How strongly it's flattened</param>
		[Theory]
		[InlineData(0f, 1f)]
		[InlineData(5f, 1f)]
		[InlineData(10f, 1f)]
		[InlineData(12.5f, 0.84375f)]
		[InlineData(15f, 0.5f)]
		[InlineData(17.5f, 0.15625f)]
		[InlineData(20f, 0f)]
		[InlineData(30f, 0f)]
		public void FadesTheFlattening(float distance, float expected)
		{
			Assert.Equal(expected, VillagePaths.Grid.Falloff(distance, 10f, 20f), 4);
		}

		/// <summary>
		/// Checks that flattening never lowers dry ground into the water - it stops half a meter above it, leaves ground
		/// already that low where it is, and still raises ground and lowers high ground as it would
		/// </summary>
		[Theory]
		[InlineData(32f, -1f, 30f, -1f)]
		[InlineData(31f, -1f, 30f, -0.5f)]
		[InlineData(30.6f, -3f, 30f, -0.1f)]
		[InlineData(30.3f, -1f, 30f, 0f)]
		[InlineData(29f, -1f, 30f, 0f)]
		[InlineData(29f, 2f, 30f, 2f)]
		[InlineData(30.3f, 0.5f, 30f, 0.5f)]
		[InlineData(40f, -3f, 30f, -3f)]
		public void KeepsFlattenedGroundAboveWater(float height, float change, float water, float expected)
		{
			Assert.Equal(expected, VillagePaths.Grid.AboveWater(height, change, water), 4);
		}

		/// <summary>
		/// Checks that a swamp village's ground is raised to half a meter above the water, by the share it's raised by,
		/// by no more than 3 meters, and that ground already high enough or not raised at all is left alone
		/// </summary>
		[Theory]
		[InlineData(29f, 1f, 30f, 1.5f)]
		[InlineData(30f, 1f, 30f, 0.5f)]
		[InlineData(29f, 0.5f, 30f, 0.75f)]
		[InlineData(25f, 1f, 30f, 3f)]
		[InlineData(30.5f, 1f, 30f, 0f)]
		[InlineData(31f, 1f, 30f, 0f)]
		[InlineData(29f, 0f, 30f, 0f)]
		[InlineData(29f, 2f, 30f, 1.5f)]
		public void RaisesSwampGroundOutOfWater(float height, float weight, float water, float expected)
		{
			Assert.Equal(expected, VillagePaths.Grid.RaisedOutOfWater(height, weight, water), 4);
		}

		/// <summary>
		/// Checks that fully raised ground near enough the water ends up dry, at half a meter above it
		/// </summary>
		[Fact]
		public void FullyRaisedGroundIsDry()
		{
			for (float height = 27.5f; height < 30.5f; height += 0.25f)
			{
				Assert.Equal(30.5f, height + VillagePaths.Grid.RaisedOutOfWater(height, 1f, 30f), 3);
			}
		}

		/// <summary>
		/// Checks that flattened ground that was dry never ends up under the water, whatever the change
		/// </summary>
		[Fact]
		public void FlattenedDryGroundStaysDry()
		{
			for (float height = 30.5f; height <= 34f; height += 0.25f)
			{
				for (float change = -3f; change <= 3f; change += 0.25f)
				{
					Assert.True(height + VillagePaths.Grid.AboveWater(height, change, 30f) >= 30.5f - 0.0001f, "Lowered " + height + " by " + change + " into the water");
				}
			}
		}

		/// <summary>
		/// Checks that the flattening only ever weakens farther out
		/// </summary>
		[Fact]
		public void WeakensTheFlatteningFartherOut()
		{
			float last = 1f;
			for (float distance = 0f; distance <= 25f; distance += 0.1f)
			{
				float weight = VillagePaths.Grid.Falloff(distance, 10f, 20f);
				Assert.True(weight <= last, "Stronger at " + distance);
				Assert.InRange(weight, 0f, 1f);
				last = weight;
			}
		}

		/// <summary>
		/// Checks that smoothing ground of one height leaves it at that height, out to any reach
		/// </summary>
		[Fact]
		public void KeepsLevelGroundLevel()
		{
			VillagePaths.Grid grid = Flat(9);
			float[] values = new float[100];
			for (int i = 0; i < values.Length; i++)
			{
				values[i] = 3.5f;
			}
			foreach (int reach in new[] { 0, 1, 3, 20 })
			{
				Assert.All(grid.Blur(values, reach), (float value) => Assert.Equal(3.5f, value, 4));
			}
		}

		/// <summary>
		/// Checks that smoothing spreads a spike evenly over the cells around it, averaging only the cells on the grid
		/// at its edges
		/// </summary>
		[Fact]
		public void SpreadsASpikeOverTheCellsAround()
		{
			VillagePaths.Grid grid = Flat(4);
			float[] middle = new float[25];
			middle[12] = 9f;
			float[] blurred = grid.Blur(middle, 1);
			Assert.Equal(1f, blurred[12], 4);
			Assert.Equal(1f, blurred[6], 4);
			Assert.Equal(1f, blurred[7], 4);
			Assert.Equal(0f, blurred[2], 4);
			Assert.Equal(0f, blurred[0], 4);
			Assert.Equal(0f, blurred[24], 4);
			float[] corner = new float[25];
			corner[0] = 9f;
			blurred = grid.Blur(corner, 1);
			Assert.Equal(2.25f, blurred[0], 4);
			Assert.Equal(1.5f, blurred[1], 4);
			Assert.Equal(1f, blurred[6], 4);
			Assert.Equal(0f, blurred[12], 4);
		}

		/// <summary>
		/// Checks that a way across flat open ground runs straight, cell by cell on the ground
		/// </summary>
		[Fact]
		public void RunsStraightAcrossOpenGround()
		{
			List<Vector3> path = Search(Flat(10), 1, 5, 9, 5, false);
			Assert.NotNull(path);
			Assert.Equal(9, path.Count);
			for (int i = 0; i < path.Count; i++)
			{
				AssertAt(path[i], 1 + i, 5);
				Assert.Equal(Dry, path[i].y, 3);
			}
		}

		/// <summary>
		/// Checks that a way across open ground cuts straight across on the diagonal
		/// </summary>
		[Fact]
		public void CutsAcrossOnTheDiagonal()
		{
			List<Vector3> path = Search(Flat(10), 0, 0, 3, 3, false);
			Assert.NotNull(path);
			Assert.Equal(4, path.Count);
			for (int i = 0; i < path.Count; i++)
			{
				AssertAt(path[i], i, i);
			}
		}

		/// <summary>
		/// Checks that a way goes around the end of a wall rather than through it
		/// </summary>
		[Fact]
		public void GoesAroundAWall()
		{
			VillagePaths.Grid grid = Flat(10);
			List<VillagePaths.Blocker> wall = new List<VillagePaths.Blocker>();
			for (int z = 0; z <= 8; z++)
			{
				wall.Add(Post(5f, z));
			}
			grid.Mark(wall);
			List<Vector3> path = Search(grid, 2, 2, 8, 2, false);
			Assert.NotNull(path);
			AssertAt(path[0], 2, 2);
			AssertAt(path[path.Count - 1], 8, 2);
			Assert.All(path, (Vector3 point) => Assert.False(Mathf.RoundToInt(point.x) == 5 && point.z < 8.5f, "Through the wall at " + point.x + ", " + point.z));
			Assert.Contains(path, (Vector3 point) => point.z > 8.5f);
		}

		/// <summary>
		/// Checks that a way doesn't step diagonally past the corner of a cell it can't step into
		/// </summary>
		[Fact]
		public void DoesntCutACorner()
		{
			VillagePaths.Grid grid = Ground(10, (float x, float z) => ((x == 5f && z == 4f) ? Wet : Dry));
			List<Vector3> path = Search(grid, 4, 4, 5, 5, false);
			Assert.NotNull(path);
			Assert.Equal(3, path.Count);
			AssertAt(path[0], 4, 4);
			AssertAt(path[1], 4, 5);
			AssertAt(path[2], 5, 5);
		}

		/// <summary>
		/// Checks that there's no way out of a ring of water no bridge crosses, unless the way is forced through it,
		/// when it crosses the ring once
		/// </summary>
		[Fact]
		public void ForcesTheOnlyWayOutOfARing()
		{
			VillagePaths.Grid grid = Ground(10, (float x, float z) => ((Mathf.Max(Mathf.Abs(x - 5f), Mathf.Abs(z - 5f)) == 2f) ? Wet : Dry));
			Assert.Null(Search(grid, 5, 5, 9, 9, false));
			List<Vector3> forced = Search(grid, 5, 5, 9, 9, true);
			Assert.NotNull(forced);
			AssertAt(forced[0], 5, 5);
			AssertAt(forced[forced.Count - 1], 9, 9);
			Assert.Single(forced, (Vector3 point) => point.y < Water);
		}

		/// <summary>
		/// Checks that a river is only crossed once the water a bridge can span is noted, and that water too far from
		/// either bank to bridge stays shut
		/// </summary>
		[Fact]
		public void CrossesOnlyWaterABridgeSpans()
		{
			VillagePaths.Grid narrow = Ground(10, River(4f, 6f));
			Assert.Null(Search(narrow, 1, 5, 9, 5, false));
			narrow.MarkBridgeable();
			Assert.NotNull(Search(narrow, 1, 5, 9, 5, false));
			VillagePaths.Grid wide = Ground(30, River(5f, 24f));
			wide.MarkBridgeable();
			Assert.Null(Search(wide, 1, 15, 29, 15, false));
		}

		/// <summary>
		/// Checks that a stretch of water a path crosses is found from its first point in the water to its last
		/// </summary>
		[Fact]
		public void FindsTheWaterAPathCrosses()
		{
			VillagePaths.Grid grid = Ground(10, River(4f, 6f));
			Assert.Equal(new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(4, 6) }, grid.WaterRuns(Row(River(4f, 6f), 0, 10, 5f)));
			System.Func<float, float, float> twoStreams = (float x, float z) => (((x >= 2f && x <= 3f) || (x >= 6f && x <= 7f)) ? Wet : Dry);
			Assert.Equal(new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(2, 3), new KeyValuePair<int, int>(6, 7) }, Ground(10, twoStreams).WaterRuns(Row(twoStreams, 0, 10, 5f)));
		}

		/// <summary>
		/// Checks that water a path starts or ends in, without dry ground on both sides, isn't a stretch it crosses
		/// </summary>
		[Fact]
		public void LeavesOutWaterAtAPathsEnds()
		{
			VillagePaths.Grid grid = Ground(10, River(4f, 6f));
			Assert.Empty(grid.WaterRuns(Row(River(4f, 6f), 5, 10, 5f)));
			Assert.Empty(grid.WaterRuns(Row(River(4f, 6f), 0, 5, 5f)));
			Assert.Empty(grid.WaterRuns(Row(River(4f, 6f), 4, 6, 5f)));
			Assert.Empty(grid.WaterRuns(Row(River(4f, 6f), 0, 3, 5f)));
		}

		/// <summary>
		/// Checks that a narrow river gets one bridge from bank to bank, and that a path crossing beside it crosses on
		/// it, while one farther along gets its own
		/// </summary>
		[Fact]
		public void BridgesANarrowRiverOnce()
		{
			System.Func<float, float, float> river = River(4f, 6f);
			VillagePaths.Grid grid = Ground(10, river);
			List<VillagePaths.Bridge> bridges = grid.FindBridges(Row(river, 0, 10, 5f));
			Assert.Single(bridges);
			Assert.Equal(new Vector3(3f, Dry, 5f), bridges[0].Start);
			Assert.Equal(new Vector3(7f, Dry, 5f), bridges[0].End);
			Assert.Empty(grid.FindBridges(Row(river, 0, 10, 5f)));
			Assert.Empty(grid.FindBridges(Row(river, 0, 10, 6f)));
			Assert.Single(grid.FindBridges(Row(river, 0, 10, 8f)));
		}

		/// <summary>
		/// Checks that a river is bridged when its banks are up to 14 meters apart, and not when they're farther
		/// </summary>
		/// <param name="lastWet">The river's east edge, from 1 meter east</param>
		/// <param name="expected">How many bridges it gets</param>
		[Theory]
		[InlineData(13f, 1)]
		[InlineData(14f, 0)]
		[InlineData(18f, 0)]
		public void BridgesARiverUpToTheWidestSpan(float lastWet, int expected)
		{
			System.Func<float, float, float> river = River(1f, lastWet);
			Assert.Equal(expected, Ground(20, river).FindBridges(Row(river, 0, 20, 5f)).Count);
		}

		/// <summary>
		/// Checks that a river 4 meters across is bridged from a bank a meter high to one up to 25 degrees higher, and
		/// not to one steeper than that
		/// </summary>
		/// <param name="farBank">The far bank's height, in meters</param>
		/// <param name="expected">How many bridges it gets</param>
		[Theory]
		[InlineData(1f, 1)]
		[InlineData(2.8f, 1)]
		[InlineData(3f, 0)]
		[InlineData(4f, 0)]
		public void BridgesARiverNoSteeperThanTheSteepestSlope(float farBank, int expected)
		{
			System.Func<float, float, float> river = (float x, float z) => ((x < 4f) ? Dry : ((x <= 6f) ? Wet : farBank));
			Assert.Equal(expected, Ground(10, river).FindBridges(Row(river, 0, 10, 5f)).Count);
		}

		/// <summary>
		/// Checks that a river isn't bridged where something stands in the water in the way, but is beside it
		/// </summary>
		[Fact]
		public void BridgesNothingStandingInTheWater()
		{
			System.Func<float, float, float> river = River(4f, 6f);
			VillagePaths.Grid grid = Ground(10, river);
			grid.Mark(new List<VillagePaths.Blocker> { Post(5f, 5f) });
			Assert.Empty(grid.FindBridges(Row(river, 0, 10, 5f)));
			Assert.Single(grid.FindBridges(Row(river, 0, 10, 8f)));
		}

		/// <summary>
		/// Checks that a path stepping into the water and back out where it went in gets no bridge, since one from a
		/// bank to the same spot is too short to build
		/// </summary>
		[Fact]
		public void BridgesNothingFromABankBackToItself()
		{
			VillagePaths.Grid grid = Ground(10, River(4f, 6f));
			List<Vector3> path = new List<Vector3> { new Vector3(3f, Dry, 5f), new Vector3(4f, Wet, 5f), new Vector3(3f, Dry, 5f) };
			Assert.Single(grid.WaterRuns(path));
			Assert.Empty(grid.FindBridges(path));
		}
	}
}
