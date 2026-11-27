namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how the navmesh tiles over an area are told apart for the log - which tile a coordinate falls in, and
	/// whether a tile has caught up with what's been built since a time
	/// </summary>
	public class NavmeshPointsTests
	{
		/// <summary>
		/// How wide the game's navmesh tiles are, in meters - Pathfinding's m_tileSize
		/// </summary>
		private const float TileSize = 32f;

		/// <summary>
		/// Checks that a coordinate falls in the tile centered on the nearest multiple of the tile's width, the way
		/// Pathfinding.GetTile has it, with a coordinate half a tile out starting the next tile
		/// </summary>
		/// <param name="coordinate">The coordinate, in meters</param>
		/// <param name="index">The tile it falls in</param>
		[Theory]
		[InlineData(0f, 0)]
		[InlineData(15.9f, 0)]
		[InlineData(16f, 1)]
		[InlineData(-16f, 0)]
		[InlineData(-16.1f, -1)]
		[InlineData(-1124f, -35)]
		[InlineData(-699f, -22)]
		public void CoordinateFallsInTheNearestTile(float coordinate, int index)
		{
			Assert.Equal(index, NavmeshPoints.TileIndex(coordinate, TileSize));
		}

		/// <summary>
		/// Checks that a tile the game doesn't have, or has never built, counts as never built, whatever else is said
		/// about it
		/// </summary>
		[Fact]
		public void TileNeverBuiltIsUnbuilt()
		{
			Assert.Equal(NavmeshPoints.TileState.Unbuilt, NavmeshPoints.TileStateOf(false, 50f, false, 10f));
			Assert.Equal(NavmeshPoints.TileState.Unbuilt, NavmeshPoints.TileStateOf(true, -1000f, true, 10f));
		}

		/// <summary>
		/// Checks that the tile being built now counts as building, however long ago its build started
		/// </summary>
		[Fact]
		public void TileBeingBuiltIsBuilding()
		{
			Assert.Equal(NavmeshPoints.TileState.Building, NavmeshPoints.TileStateOf(true, 5f, true, 10f));
			Assert.Equal(NavmeshPoints.TileState.Building, NavmeshPoints.TileStateOf(true, 15f, true, 10f));
		}

		/// <summary>
		/// Checks that a tile last built before the time is stale, and one built at it or after has caught up
		/// </summary>
		[Fact]
		public void TileBuiltBeforeTheTimeIsStale()
		{
			Assert.Equal(NavmeshPoints.TileState.Stale, NavmeshPoints.TileStateOf(true, 9.9f, false, 10f));
			Assert.Equal(NavmeshPoints.TileState.Current, NavmeshPoints.TileStateOf(true, 10f, false, 10f));
			Assert.Equal(NavmeshPoints.TileState.Current, NavmeshPoints.TileStateOf(true, 30f, false, 10f));
		}
	}
}
