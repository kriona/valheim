using UnityEngine;
using UnityEngine.AI;

namespace VikingVillages
{
	/// <summary>
	/// Finds points on the navmesh for a kind of agent
	/// </summary>
	/// <remarks>
	/// The game's Pathfinding.FindValidPoint asks the navmesh about the agent by its AgentType number rather than the
	/// agent type id its navmesh is built for, so it never finds a point. This asks with the id, the way the game's
	/// own path finding does
	/// </remarks>
	internal static class NavmeshPoints
	{
		/// <summary>
		/// Finds the point on an agent's navmesh nearest a spot, keeping the navmesh there built
		/// </summary>
		/// <param name="point">The point found, or the spot when there's none</param>
		/// <param name="center">The spot</param>
		/// <param name="range">How far from the spot to look, in meters</param>
		/// <param name="agentType">The kind of agent</param>
		/// <returns>False when there's no navmesh that close</returns>
		public static bool FindValidPoint(out Vector3 point, Vector3 center, float range, Pathfinding.AgentType agentType)
		{
			point = center;
			Pathfinding pathfinding = Pathfinding.instance;
			if (pathfinding == null)
			{
				return(false);
			}
			pathfinding.PokePoint(center, agentType);
			Pathfinding.AgentSettings settings = pathfinding.GetSettings(agentType);
			if (settings == null)
			{
				return(false);
			}
			NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = settings.m_build.agentTypeID, areaMask = settings.m_areaMask };
			if (!NavMesh.SamplePosition(center, out NavMeshHit hit, range, filter))
			{
				return(false);
			}
			point = hit.position;
			return(true);
		}

		/// <summary>
		/// How a navmesh tile stands against what's been built since a time (see TileStateOf)
		/// </summary>
		internal enum TileState
		{
			Unbuilt,
			Building,
			Stale,
			Current
		}

		/// <summary>
		/// Works out how a navmesh tile stands against what's been built since a time - never built, being built now,
		/// last built before then, so it doesn't have what went up since, or built since
		/// </summary>
		/// <param name="known">Whether the game has the tile at all</param>
		/// <param name="buildTime">When its last build started, by Time.time - when the game gathers what's on it</param>
		/// <param name="building">Whether it's the tile being built now</param>
		/// <param name="since">The time, by Time.time</param>
		/// <returns>How it stands</returns>
		internal static TileState TileStateOf(bool known, float buildTime, bool building, float since)
		{
			if (!known || buildTime < 0f)
			{
				return(TileState.Unbuilt);
			}
			if (building)
			{
				return(TileState.Building);
			}
			return((buildTime < since) ? TileState.Stale : TileState.Current);
		}

		/// <summary>
		/// Works out which navmesh tile along one axis a coordinate falls in, the way Pathfinding.GetTile does
		/// </summary>
		/// <param name="coordinate">The coordinate, in meters</param>
		/// <param name="tileSize">How wide a tile is, in meters</param>
		/// <returns>The tile's index along the axis</returns>
		internal static int TileIndex(float coordinate, float tileSize)
		{
			return(Mathf.FloorToInt((coordinate + tileSize / 2f) / tileSize));
		}

		/// <summary>
		/// Describes how far the navmesh tiles over an area have caught up with what's been built since a time, for the
		/// log - how many there are, and how many of them were never built, are being built now, were last built before
		/// then or have been built since, with how long ago the oldest was built
		/// </summary>
		/// <param name="middle">The area's middle</param>
		/// <param name="radius">How far it reaches from its middle, in meters</param>
		/// <param name="since">How many seconds ago the time was, or below 0 when it isn't known</param>
		/// <param name="agentType">The kind of agent whose navmesh it is</param>
		/// <returns>The description</returns>
		public static string DescribeTiles(Vector3 middle, float radius, float since, Pathfinding.AgentType agentType)
		{
			Pathfinding pathfinding = Pathfinding.instance;
			if (pathfinding == null)
			{
				return("no navmesh");
			}
			float size = pathfinding.m_tileSize;
			float from = ((since >= 0f) ? Time.time - since : float.MinValue);
			int[] counts = new int[4];
			float oldest = -1f;
			for (int x = TileIndex(middle.x - radius, size); x <= TileIndex(middle.x + radius, size); x++)
			{
				for (int z = TileIndex(middle.z - radius, size); z <= TileIndex(middle.z + radius, size); z++)
				{
					Vector3Int id = new Vector3Int(x, z, (int)agentType);
					bool known = pathfinding.m_tiles.TryGetValue(id, out Pathfinding.NavMeshTile tile);
					float buildTime = (known ? tile.m_buildTime : -1f);
					counts[(int)TileStateOf(known, buildTime, known && tile == pathfinding.m_buildTile, from)]++;
					if (known && buildTime >= 0f)
					{
						oldest = Mathf.Max(oldest, Time.time - buildTime);
					}
				}
			}
			int total = counts[0] + counts[1] + counts[2] + counts[3];
			string text = "navmesh: " + total + " tiles of " + size.ToString("F0") + "m over it - " + counts[(int)TileState.Unbuilt] + " never built, " + counts[(int)TileState.Building] + " being built";
			if (since >= 0f)
			{
				text += ", " + counts[(int)TileState.Stale] + " last built before the farm was generated " + since.ToString("F1") + "s ago and " + counts[(int)TileState.Current] + " built since";
			}
			return(text + ((oldest >= 0f) ? ", the oldest built " + oldest.ToString("F1") + "s ago" : ""));
		}
	}
}
