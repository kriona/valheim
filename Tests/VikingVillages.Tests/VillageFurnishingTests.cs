using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a house's floor is laid out for furnishing - how many cells a piece takes, which way the walls run,
	/// the cells a piece against each wall takes, where a table and its stools go, and when the house is still easy
	/// to get around
	/// </summary>
	public class VillageFurnishingTests
	{
		/// <summary>
		/// Makes a grid with every cell free and one way in
		/// </summary>
		/// <param name="columns">How many columns it has</param>
		/// <param name="rows">How many rows it has</param>
		/// <param name="entry">The cell the way in starts from</param>
		/// <returns>The grid</returns>
		private static VillageFurnishing.FloorGrid OpenGrid(int columns, int rows, Vector2Int entry)
		{
			VillageFurnishing.FloorGrid grid = new VillageFurnishing.FloorGrid(columns, rows);
			grid.FreeAtStart = columns * rows;
			grid.Entries.Add(entry);
			return(grid);
		}

		/// <summary>
		/// Takes cells on a grid for one piece
		/// </summary>
		/// <param name="grid">The grid</param>
		/// <param name="cells">The piece's cells</param>
		/// <returns>The pieces laid out - just this one</returns>
		private static List<List<Vector2Int>> Lay(VillageFurnishing.FloorGrid grid, List<Vector2Int> cells)
		{
			grid.Take(cells);
			return(new List<List<Vector2Int>> { cells });
		}

		/// <summary>
		/// Makes a level normal pointing a way
		/// </summary>
		/// <param name="degrees">The way, clockwise from north, in degrees</param>
		/// <returns>The normal</returns>
		private static Vector3 Normal(float degrees)
		{
			float radians = degrees * Mathf.Deg2Rad;
			return(new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)));
		}

		/// <summary>
		/// Makes the normals of four walls at right angles
		/// </summary>
		/// <param name="degrees">How far the first wall's normal is turned from north, in degrees</param>
		/// <returns>The normals</returns>
		private static List<Vector3> SquareWalls(float degrees)
		{
			return(new List<Vector3> { Normal(degrees), Normal(degrees + 90f), Normal(degrees + 180f), Normal(degrees + 270f) });
		}

		/// <summary>
		/// Checks how many half meter cells a piece takes, a piece within 5 centimeters over a whole number of cells
		/// taking no more
		/// </summary>
		/// <param name="size">The piece's size, in meters</param>
		/// <param name="expected">How many cells it takes</param>
		[Theory]
		[InlineData(0f, 0)]
		[InlineData(0.05f, 0)]
		[InlineData(0.3f, 1)]
		[InlineData(0.5f, 1)]
		[InlineData(0.54f, 1)]
		[InlineData(0.55f, 1)]
		[InlineData(0.56f, 2)]
		[InlineData(1f, 2)]
		[InlineData(1.1f, 3)]
		[InlineData(2f, 4)]
		public void CellSpanLeavesSlack(float size, int expected)
		{
			Assert.Equal(expected, VillageFurnishing.CellSpan(size));
		}

		/// <summary>
		/// Checks that walls lined up with the world's axes run at 0, and walls turned 30 degrees either way run at that
		/// </summary>
		/// <param name="turned">How far the walls are turned, in degrees</param>
		/// <param name="expected">The way they're found to run, in degrees</param>
		[Theory]
		[InlineData(0f, 0f)]
		[InlineData(30f, 30f)]
		[InlineData(-30f, -30f)]
		[InlineData(60f, -30f)]
		[InlineData(90f, 0f)]
		[InlineData(40f, 40f)]
		[InlineData(50f, -40f)]
		public void WallYawFoldsIntoAQuarterTurn(float turned, float expected)
		{
			Assert.Equal(expected, VillageFurnishing.WallYaw(SquareWalls(turned)), 2);
		}

		/// <summary>
		/// Checks that a single wall hit gives the way it runs, folded into a quarter turn
		/// </summary>
		[Fact]
		public void WallYawFromOneWall()
		{
			Assert.Equal(20f, VillageFurnishing.WallYaw(new List<Vector3> { Normal(200f) }), 2);
		}

		/// <summary>
		/// Checks that walls a little off square are averaged
		/// </summary>
		[Fact]
		public void WallYawAveragesNearlySquareWalls()
		{
			List<Vector3> normals = new List<Vector3> { Normal(28f), Normal(122f), Normal(208f), Normal(302f) };
			Assert.Equal(30f, VillageFurnishing.WallYaw(normals), 2);
		}

		/// <summary>
		/// Checks that walls either side of the 45 degree fold average out at the fold, rather than cancelling to 0
		/// </summary>
		[Fact]
		public void WallYawAveragesAcrossTheFold()
		{
			float yaw = VillageFurnishing.WallYaw(new List<Vector3> { Normal(44f), Normal(46f) });
			Assert.Equal(45f, Mathf.Abs(yaw), 2);
		}

		/// <summary>
		/// Checks that a piece two cells wide against each wall backs onto that wall's edge of the grid, starting from
		/// the wall's left as seen facing into the house
		/// </summary>
		/// <param name="quarter">Which wall, as quarter turns from facing forward</param>
		/// <param name="firstColumn">The first cell's column</param>
		/// <param name="firstRow">The first cell's row</param>
		/// <param name="secondColumn">The second cell's column</param>
		/// <param name="secondRow">The second cell's row</param>
		[Theory]
		[InlineData(0, 1, 0, 2, 0)]
		[InlineData(1, 0, 2, 0, 1)]
		[InlineData(2, 4, 3, 3, 3)]
		[InlineData(3, 5, 1, 5, 2)]
		public void WallCellsBackOntoEachWall(int quarter, int firstColumn, int firstRow, int secondColumn, int secondRow)
		{
			VillageFurnishing.FloorGrid grid = new VillageFurnishing.FloorGrid(6, 4);
			List<Vector2Int> cells = grid.WallCells(quarter, 1, 0, 2, 1);
			Assert.Equal(new[] { new Vector2Int(firstColumn, firstRow), new Vector2Int(secondColumn, secondRow) }, cells);
		}

		/// <summary>
		/// Checks that a piece a cell out from the wall and two deep leaves the wall's cells and takes the two in front
		/// </summary>
		[Fact]
		public void WallCellsStandOutByTheGap()
		{
			VillageFurnishing.FloorGrid grid = new VillageFurnishing.FloorGrid(6, 4);
			Assert.Equal(new[] { new Vector2Int(0, 1), new Vector2Int(0, 2) }, grid.WallCells(0, 0, 1, 1, 2));
			Assert.Equal(new[] { new Vector2Int(4, 0), new Vector2Int(3, 0) }, grid.WallCells(3, 0, 1, 1, 2));
		}

		/// <summary>
		/// Checks that cells are free only when on the grid and not taken, and free again once released
		/// </summary>
		[Fact]
		public void TakesAndReleasesCells()
		{
			VillageFurnishing.FloorGrid grid = new VillageFurnishing.FloorGrid(3, 3);
			List<Vector2Int> cells = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0) };
			Assert.True(grid.AreFree(cells));
			Assert.False(grid.AreFree(new List<Vector2Int> { new Vector2Int(2, 0), new Vector2Int(3, 0) }));
			Assert.False(grid.AreFree(new List<Vector2Int> { new Vector2Int(0, -1) }));
			grid.Take(cells);
			Assert.False(grid.AreFree(new List<Vector2Int> { new Vector2Int(1, 0) }));
			Assert.False(grid.Walkable(new Vector2Int(1, 0)));
			grid.Release(cells);
			Assert.True(grid.AreFree(cells));
		}

		/// <summary>
		/// Checks that free cells have nothing keeping them from being free
		/// </summary>
		[Fact]
		public void FreeCellsHaveNoHindrance()
		{
			VillageFurnishing.FloorGrid grid = new VillageFurnishing.FloorGrid(3, 3);
			Assert.Equal(VillageFurnishing.FloorGrid.Free, grid.Hindrance(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(2, 2) }));
		}

		/// <summary>
		/// Checks that a cell off the grid counts as blocked
		/// </summary>
		[Fact]
		public void CellOffTheGridIsBlocked()
		{
			VillageFurnishing.FloorGrid grid = new VillageFurnishing.FloorGrid(3, 3);
			Assert.Equal(VillageFurnishing.FloorGrid.Blocked, grid.Hindrance(new List<Vector2Int> { new Vector2Int(2, 0), new Vector2Int(3, 0) }));
		}

		/// <summary>
		/// Checks that each kind of cell that isn't free is named when it's the only one in the way
		/// </summary>
		[Theory]
		[InlineData(VillageFurnishing.FloorGrid.Blocked)]
		[InlineData(VillageFurnishing.FloorGrid.KeepClear)]
		[InlineData(VillageFurnishing.FloorGrid.Taken)]
		public void NamesTheOneCellInTheWay(byte state)
		{
			VillageFurnishing.FloorGrid grid = new VillageFurnishing.FloorGrid(3, 3);
			grid.Cells[1, 0] = state;
			Assert.Equal(state, grid.Hindrance(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }));
		}

		/// <summary>
		/// Checks that blocked cells are named over kept clear ones, and kept clear ones over taken ones, whichever comes
		/// first
		/// </summary>
		[Fact]
		public void NamesTheLongestLastingCellInTheWay()
		{
			VillageFurnishing.FloorGrid grid = new VillageFurnishing.FloorGrid(3, 1);
			grid.Cells[0, 0] = VillageFurnishing.FloorGrid.Taken;
			grid.Cells[1, 0] = VillageFurnishing.FloorGrid.KeepClear;
			grid.Cells[2, 0] = VillageFurnishing.FloorGrid.Blocked;
			List<Vector2Int> cells = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0) };
			Assert.Equal(VillageFurnishing.FloorGrid.KeepClear, grid.Hindrance(cells));
			cells.Reverse();
			Assert.Equal(VillageFurnishing.FloorGrid.KeepClear, grid.Hindrance(cells));
			cells.Add(new Vector2Int(2, 0));
			Assert.Equal(VillageFurnishing.FloorGrid.Blocked, grid.Hindrance(cells));
		}

		/// <summary>
		/// Checks that an empty floor is easy to get around
		/// </summary>
		[Fact]
		public void EmptyFloorIsRoomy()
		{
			Assert.True(OpenGrid(6, 6, new Vector2Int(0, 0)).Roomy(new List<List<Vector2Int>>()));
		}

		/// <summary>
		/// Checks that a piece walling the way in off into its corner leaves the house too cramped
		/// </summary>
		[Fact]
		public void WallingOffTheEntryIsCramped()
		{
			VillageFurnishing.FloorGrid grid = OpenGrid(6, 6, new Vector2Int(0, 0));
			List<List<Vector2Int>> pieces = Lay(grid, new List<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1) });
			Assert.False(grid.Roomy(pieces));
		}

		/// <summary>
		/// Checks that a piece in a corner is fine on its own, but not once another piece walls it in, though most of
		/// the floor is still free
		/// </summary>
		[Fact]
		public void WalledInPieceIsCramped()
		{
			List<Vector2Int> corner = new List<Vector2Int> { new Vector2Int(5, 5) };
			List<Vector2Int> around = new List<Vector2Int> { new Vector2Int(4, 5), new Vector2Int(4, 4), new Vector2Int(5, 4) };
			VillageFurnishing.FloorGrid alone = OpenGrid(6, 6, new Vector2Int(0, 0));
			Assert.True(alone.Roomy(Lay(alone, corner)));
			VillageFurnishing.FloorGrid walled = OpenGrid(6, 6, new Vector2Int(0, 0));
			walled.Take(corner);
			walled.Take(around);
			Assert.False(walled.Roomy(new List<List<Vector2Int>> { corner, around }));
			Assert.True(walled.Roomy(new List<List<Vector2Int>> { around }));
		}

		/// <summary>
		/// Checks that a piece is reached by a gap two cells deep out from it, and not by one cell between it and a wall
		/// of blocked cells
		/// </summary>
		[Fact]
		public void ReachableOnlyByAGapTwoCellsDeep()
		{
			List<Vector2Int> piece = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };
			VillageFurnishing.FloorGrid narrow = OpenGrid(3, 3, new Vector2Int(0, 1));
			narrow.Take(piece);
			for (int column = 0; column < 3; column++)
			{
				narrow.Cells[column, 2] = VillageFurnishing.FloorGrid.Blocked;
			}
			Assert.False(narrow.Reachable(piece, narrow.Reach(out int _)));
			VillageFurnishing.FloorGrid wide = OpenGrid(3, 3, new Vector2Int(0, 1));
			wide.Take(piece);
			Assert.True(wide.Reachable(piece, wide.Reach(out int _)));
		}

		/// <summary>
		/// Checks that a gap two cells deep beside a piece counts only when it can be walked to from the way in
		/// </summary>
		[Fact]
		public void GapHasToBeReached()
		{
			List<Vector2Int> piece = new List<Vector2Int> { new Vector2Int(0, 0) };
			VillageFurnishing.FloorGrid grid = OpenGrid(3, 4, new Vector2Int(2, 3));
			grid.Take(piece);
			grid.Cells[1, 0] = VillageFurnishing.FloorGrid.Blocked;
			grid.Cells[1, 1] = VillageFurnishing.FloorGrid.Blocked;
			grid.Cells[1, 2] = VillageFurnishing.FloorGrid.Blocked;
			grid.Cells[0, 3] = VillageFurnishing.FloorGrid.Blocked;
			grid.Cells[1, 3] = VillageFurnishing.FloorGrid.Blocked;
			Assert.False(grid.Reachable(piece, grid.Reach(out int _)));
		}

		/// <summary>
		/// Checks that counting the floor reached from the way in counts only its free cells, not those kept clear
		/// </summary>
		[Fact]
		public void ReachCountsFreeCells()
		{
			VillageFurnishing.FloorGrid grid = OpenGrid(4, 1, new Vector2Int(0, 0));
			grid.Cells[1, 0] = VillageFurnishing.FloorGrid.KeepClear;
			grid.Cells[3, 0] = VillageFurnishing.FloorGrid.Blocked;
			bool[,] reached = grid.Reach(out int reachedFree);
			Assert.Equal(2, reachedFree);
			Assert.True(reached[2, 0]);
			Assert.False(reached[3, 0]);
		}

		/// <summary>
		/// Checks that a bed laid alongside one already standing, leaving a one cell gap between them, leaves the house
		/// too cramped while the standing bed has to stay reachable - with the wall at its head and a blocked corner
		/// at its foot, the gap was its only way in
		/// </summary>
		[Fact]
		public void BedAlongsideAStandingBedIsCramped()
		{
			VillageFurnishing.FloorGrid grid = OpenGrid(6, 6, new Vector2Int(5, 5));
			List<Vector2Int> standing = new List<Vector2Int>();
			for (int row = 0; row < 4; row++)
			{
				standing.Add(new Vector2Int(0, row));
				standing.Add(new Vector2Int(1, row));
				grid.Cells[0, row] = VillageFurnishing.FloorGrid.Blocked;
				grid.Cells[1, row] = VillageFurnishing.FloorGrid.Blocked;
			}
			grid.Cells[0, 4] = VillageFurnishing.FloorGrid.Blocked;
			grid.Cells[1, 4] = VillageFurnishing.FloorGrid.Blocked;
			grid.Cells[0, 5] = VillageFurnishing.FloorGrid.Blocked;
			grid.Cells[1, 5] = VillageFurnishing.FloorGrid.Blocked;
			grid.FreeAtStart = 24;
			Assert.True(grid.Roomy(new List<List<Vector2Int>> { standing }));
			List<Vector2Int> laid = new List<Vector2Int>();
			for (int row = 0; row < 4; row++)
			{
				laid.Add(new Vector2Int(3, row));
				laid.Add(new Vector2Int(4, row));
			}
			grid.Take(laid);
			Assert.False(grid.Roomy(new List<List<Vector2Int>> { laid, standing }));
			Assert.True(grid.Roomy(new List<List<Vector2Int>> { laid }));
		}

		/// <summary>
		/// Checks that pieces can take half the free floor but no more
		/// </summary>
		[Fact]
		public void KeepsHalfTheFloorFree()
		{
			List<Vector2Int> half = new List<Vector2Int>();
			for (int column = 0; column < 4; column++)
			{
				half.Add(new Vector2Int(column, 2));
				half.Add(new Vector2Int(column, 3));
			}
			VillageFurnishing.FloorGrid even = OpenGrid(4, 4, new Vector2Int(0, 0));
			Assert.True(even.Roomy(Lay(even, half)));
			List<Vector2Int> more = new List<Vector2Int>(half) { new Vector2Int(3, 1) };
			VillageFurnishing.FloorGrid over = OpenGrid(4, 4, new Vector2Int(0, 0));
			Assert.False(over.Roomy(Lay(over, more)));
		}

		/// <summary>
		/// Checks that the cells kept clear by a door can be walked across to the free floor beyond, where blocked
		/// cells can't
		/// </summary>
		[Fact]
		public void WalksAcrossCellsKeptClear()
		{
			VillageFurnishing.FloorGrid clear = OpenGrid(4, 1, new Vector2Int(0, 0));
			clear.Cells[1, 0] = VillageFurnishing.FloorGrid.KeepClear;
			clear.FreeAtStart = 3;
			Assert.True(clear.Roomy(new List<List<Vector2Int>>()));
			VillageFurnishing.FloorGrid blocked = OpenGrid(4, 1, new Vector2Int(0, 0));
			blocked.Cells[1, 0] = VillageFurnishing.FloorGrid.Blocked;
			blocked.FreeAtStart = 3;
			Assert.False(blocked.Roomy(new List<List<Vector2Int>>()));
		}

		/// <summary>
		/// Checks that a house with no way in from a door is entered from its middle cell, or the first nearest it
		/// when that's blocked, and one with a way in already keeps it
		/// </summary>
		[Fact]
		public void EntersAtTheMiddle()
		{
			VillageFurnishing.FloorGrid open = new VillageFurnishing.FloorGrid(5, 5);
			open.EnterAtMiddle();
			Assert.Equal(new[] { new Vector2Int(2, 2) }, open.Entries);
			VillageFurnishing.FloorGrid blocked = new VillageFurnishing.FloorGrid(5, 5);
			blocked.Cells[2, 2] = VillageFurnishing.FloorGrid.Blocked;
			blocked.EnterAtMiddle();
			Assert.Equal(new[] { new Vector2Int(1, 2) }, blocked.Entries);
			VillageFurnishing.FloorGrid entered = OpenGrid(5, 5, new Vector2Int(0, 4));
			entered.EnterAtMiddle();
			Assert.Equal(new[] { new Vector2Int(0, 4) }, entered.Entries);
		}

		/// <summary>
		/// Checks that a house blocked everywhere gets no way in
		/// </summary>
		[Fact]
		public void BlockedHouseHasNoWayIn()
		{
			VillageFurnishing.FloorGrid grid = new VillageFurnishing.FloorGrid(2, 2);
			for (int column = 0; column < 2; column++)
			{
				for (int row = 0; row < 2; row++)
				{
					grid.Cells[column, row] = VillageFurnishing.FloorGrid.Blocked;
				}
			}
			grid.EnterAtMiddle();
			Assert.Empty(grid.Entries);
		}

		/// <summary>
		/// Checks that a table keeps two cells from the grid's edges - a 2 by 2 table fits one spot on a 6 by 6 grid and
		/// none on a 5 by 6 one
		/// </summary>
		[Fact]
		public void TableKeepsTwoCellsFromTheWalls()
		{
			Assert.Equal(new[] { new Vector2Int(2, 2) }, new VillageFurnishing.FloorGrid(6, 6).TableSpots(2, 2));
			Assert.Empty(new VillageFurnishing.FloorGrid(5, 6).TableSpots(2, 2));
		}

		/// <summary>
		/// Checks that a table's spots are tried from the middle of the grid out
		/// </summary>
		[Fact]
		public void TableSpotsStartInTheMiddle()
		{
			List<Vector2Int> spots = new VillageFurnishing.FloorGrid(7, 7).TableSpots(1, 1);
			Assert.Equal(9, spots.Count);
			Assert.Equal(new Vector2Int(3, 3), spots[0]);
			Assert.All(spots, (Vector2Int spot) => Assert.InRange(spot.x, 2, 4));
			Assert.All(spots, (Vector2Int spot) => Assert.InRange(spot.y, 2, 4));
			Assert.Contains(spots.GetRange(5, 4), (Vector2Int spot) => spot == new Vector2Int(2, 2));
		}

		/// <summary>
		/// Checks that a table's block of cells runs from its first cell along the columns and rows
		/// </summary>
		[Fact]
		public void TableBlockCoversItsCells()
		{
			List<Vector2Int> cells = VillageFurnishing.FloorGrid.Block(new Vector2Int(1, 2), 2, 3);
			Assert.Equal(6, cells.Count);
			Assert.Contains(new Vector2Int(1, 2), cells);
			Assert.Contains(new Vector2Int(2, 4), cells);
			Assert.DoesNotContain(new Vector2Int(3, 2), cells);
			Assert.DoesNotContain(new Vector2Int(1, 5), cells);
		}

		/// <summary>
		/// Checks that a table wider than it's deep gets its stools below and above it first, at the middle of each side
		/// </summary>
		[Fact]
		public void StoolsGoAtTheLongSidesOfAWideTable()
		{
			Vector2Int[] sides = VillageFurnishing.FloorGrid.StoolSides(new Vector2Int(2, 3), 3, 2);
			Assert.Equal(new List<Vector2Int> { new Vector2Int(3, 2), new Vector2Int(3, 5), new Vector2Int(1, 4), new Vector2Int(5, 4) }, sides);
		}

		/// <summary>
		/// Checks that a table deeper than it's wide gets its stools left and right of it first
		/// </summary>
		[Fact]
		public void StoolsGoAtTheLongSidesOfADeepTable()
		{
			Vector2Int[] sides = VillageFurnishing.FloorGrid.StoolSides(new Vector2Int(2, 3), 2, 3);
			Assert.Equal(new List<Vector2Int> { new Vector2Int(1, 4), new Vector2Int(4, 4), new Vector2Int(3, 2), new Vector2Int(3, 6) }, sides);
		}

		/// <summary>
		/// Checks that a square table's stools go below and above it first
		/// </summary>
		[Fact]
		public void StoolsAroundASquareTable()
		{
			Vector2Int[] sides = VillageFurnishing.FloorGrid.StoolSides(new Vector2Int(2, 2), 2, 2);
			Assert.Equal(new List<Vector2Int> { new Vector2Int(3, 1), new Vector2Int(3, 4), new Vector2Int(1, 3), new Vector2Int(4, 3) }, sides);
		}

		/// <summary>
		/// Checks that every biome a village can be in has starter food, two different foods each
		/// </summary>
		[Fact]
		public void EveryBiomeHasStarterFood()
		{
			Assert.Equal(Kriona.Vikings.VikingGear.Biomes.Length, VillageFurnishing.StarterFoods.Length);
			foreach (string[] foods in VillageFurnishing.StarterFoods)
			{
				Assert.Equal(2, foods.Length);
				Assert.NotEqual(foods[0], foods[1]);
			}
		}

		/// <summary>
		/// Checks that every biome a village can be in but the Meadows has a starter pickaxe, never a worse one than the
		/// biome before it
		/// </summary>
		[Fact]
		public void EveryBiomeButMeadowsHasAStarterPickaxe()
		{
			string[] order = { "PickaxeAntler", "PickaxeBronze", "PickaxeIron", "PickaxeBlackMetal" };
			Assert.Equal(Kriona.Vikings.VikingGear.Biomes.Length, VillageFurnishing.StarterPickaxes.Length);
			Assert.Null(VillageFurnishing.StarterPickaxes[0]);
			int last = 0;
			for (int i = 1; i < VillageFurnishing.StarterPickaxes.Length; i++)
			{
				string pickaxe = VillageFurnishing.StarterPickaxes[i];
				int rank = System.Array.IndexOf(order, pickaxe);
				Assert.True(rank >= last, pickaxe + " is worse than the biome before it");
				last = rank;
			}
		}

		/// <summary>
		/// Checks that the grid is drawn a row a line, its last row first - pieces by their marks, blocked and taken
		/// cells, the way in, and free and kept clear cells as walked to from the way in or not
		/// </summary>
		[Fact]
		public void DrawsTheGridLastRowFirst()
		{
			VillageFurnishing.FloorGrid grid = OpenGrid(4, 3, new Vector2Int(0, 0));
			grid.Cells[1, 0] = VillageFurnishing.FloorGrid.KeepClear;
			grid.Cells[2, 0] = VillageFurnishing.FloorGrid.Blocked;
			grid.Cells[2, 1] = VillageFurnishing.FloorGrid.Blocked;
			grid.Cells[2, 2] = VillageFurnishing.FloorGrid.Taken;
			grid.Cells[3, 0] = VillageFurnishing.FloorGrid.KeepClear;
			grid.Cells[1, 2] = VillageFurnishing.FloorGrid.Taken;
			Dictionary<Vector2Int, char> marks = new Dictionary<Vector2Int, char> { { new Vector2Int(1, 2), '1' }, { new Vector2Int(2, 1), 'B' } };
			List<string> lines = grid.Draw(grid.Reach(out int _), marks);
			Assert.Equal(new List<string> { ".1x,", "..B,", "E+#-" }, lines);
		}
	}
}
