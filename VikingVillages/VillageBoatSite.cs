using System;
using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Where a generated village's boat is moored - on water deep enough for it all around, as near the village as there's
	/// room, in water that comes within ShoreReach of the village's edge and runs on to the sea - worked out from the
	/// world's plan apart from the game, so it can be tested
	/// </summary>
	/// <remarks>
	/// The ground around the village is read on a grid of GridStep, out to SpotReach past its edge. The water near its
	/// edge is followed out across the grid, and the spots in it with room for the boat are tried nearest the village
	/// first, each followed on through the world's water, on a coarser grid of FloodStep, until it reaches the sea - a
	/// spot whose water never does, like a lake's, is passed over along with the rest of that water. The boat faces
	/// along the most open water from its spot, away from the village when that's as open
	/// </remarks>
	internal static class VillageBoatSite
	{
		/// <summary>
		/// How far apart the ground is read around the village, how far past its edge water counts as by the village, and
		/// how far past its edge the boat can be moored, in meters
		/// </summary>
		internal const float GridStep = 4f;
		internal const float ShoreReach = 24f;
		internal const float SpotReach = 80f;

		/// <summary>
		/// How far under the sea's surface the ground has to be for there to be water, and for the water to be followed on
		/// to the sea, in meters - how deep it has to be for the boat to float over it is the boat's own
		/// </summary>
		internal const float WetDepth = 0.5f;
		internal const float FloodDepth = 1f;

		/// <summary>
		/// How the water is followed on to the sea - on a grid of FloodStep meters, no farther than FloodReach meters from
		/// the spot, giving up after FloodCells squares, and for no more than FloodsMax stretches of water
		/// </summary>
		internal const float FloodStep = 8f;
		internal const float FloodReach = 3000f;
		internal const int FloodCells = 20000;
		internal const int FloodsMax = 4;

		/// <summary>
		/// How many ways the boat could face are tried, and how far out along each the water is looked at, in meters
		/// </summary>
		internal const int Headings = 16;
		internal const float HeadingRun = 100f;

		/// <summary>
		/// Finds where a village's boat is moored (see the remarks)
		/// </summary>
		/// <param name="middle">The village's middle</param>
		/// <param name="radius">How far from its middle it reaches, in meters</param>
		/// <param name="room">How far around the boat's spot has to be deep water, in meters</param>
		/// <param name="draft">How far under the sea's surface the ground has to be for the boat to float over it, in meters</param>
		/// <param name="water">The sea's level</param>
		/// <param name="outside">How far past the village's edge a point is, 0 inside it</param>
		/// <param name="height">The ground's height at a point, by its x and z</param>
		/// <param name="sea">Whether a point, by its x and z, is out at sea</param>
		/// <param name="spot">Set to where the boat goes, on the water</param>
		/// <param name="facing">Set to which way it faces, flat</param>
		/// <returns>False when there's no such spot</returns>
		internal static bool Find(Vector3 middle, float radius, float room, float draft, float water, Func<Vector3, float> outside, Func<float, float, float> height, Func<float, float, bool> sea, out Vector3 spot, out Vector3 facing)
		{
			spot = Vector3.zero;
			facing = Vector3.forward;
			float half = radius + SpotReach + room + GridStep;
			int size = Mathf.CeilToInt(half * 2f / GridStep) + 1;
			Vector3 origin = new Vector3(middle.x - half, water, middle.z - half);
			float[,] heights = new float[size, size];
			float[,] beyond = new float[size, size];
			bool[,] reached = new bool[size, size];
			Queue<Vector2Int> open = new Queue<Vector2Int>();
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					Vector3 point = Point(origin, i, j);
					heights[i, j] = height(point.x, point.z);
					beyond[i, j] = outside(point);
					if (heights[i, j] < water - WetDepth && beyond[i, j] <= ShoreReach)
					{
						reached[i, j] = true;
						open.Enqueue(new Vector2Int(i, j));
					}
				}
			}
			if (open.Count == 0)
			{
				return(false);
			}
			// The water near the edge is followed out across the grid, so the boat is only moored on water joined to it
			while (open.Count > 0)
			{
				Vector2Int cell = open.Dequeue();
				for (int n = 0; n < 4; n++)
				{
					int i = cell.x + ((n == 0) ? 1 : ((n == 1) ? -1 : 0));
					int j = cell.y + ((n == 2) ? 1 : ((n == 3) ? -1 : 0));
					if (i >= 0 && j >= 0 && i < size && j < size && !reached[i, j] && heights[i, j] < water - WetDepth)
					{
						reached[i, j] = true;
						open.Enqueue(new Vector2Int(i, j));
					}
				}
			}
			List<Vector2Int> candidates = new List<Vector2Int>();
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					if (reached[i, j] && heights[i, j] < water - draft && beyond[i, j] <= SpotReach)
					{
						candidates.Add(new Vector2Int(i, j));
					}
				}
			}
			candidates.Sort((Vector2Int a, Vector2Int b) =>
			{
				int byEdge = beyond[a.x, a.y].CompareTo(beyond[b.x, b.y]);
				return((byEdge != 0) ? byEdge : FlatDistance(Point(origin, a.x, a.y), middle).CompareTo(FlatDistance(Point(origin, b.x, b.y), middle)));
			});
			HashSet<long> landlocked = new HashSet<long>();
			int floods = 0;
			foreach (Vector2Int cell in candidates)
			{
				Vector3 point = Point(origin, cell.x, cell.y);
				if (landlocked.Contains(FloodKey(point)) || !HasRoom(heights, cell, room, water - draft))
				{
					continue;
				}
				if (floods >= FloodsMax)
				{
					return(false);
				}
				floods++;
				if (ReachesSea(point, water, height, sea, landlocked))
				{
					spot = point;
					facing = Facing(point, middle, water - draft, height);
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether the water all around a spot on the grid is deep enough for the boat
		/// </summary>
		/// <param name="heights">The ground's heights on the grid</param>
		/// <param name="cell">The spot</param>
		/// <param name="room">How far around it has to be deep, in meters</param>
		/// <param name="floor">How high the ground can be for the boat to float over it</param>
		/// <returns>True when it is, false when it isn't or that reaches past the grid</returns>
		internal static bool HasRoom(float[,] heights, Vector2Int cell, float room, float floor)
		{
			int reach = Mathf.CeilToInt(room / GridStep);
			int size = heights.GetLength(0);
			for (int di = -reach; di <= reach; di++)
			{
				for (int dj = -reach; dj <= reach; dj++)
				{
					if ((di * di + dj * dj) * GridStep * GridStep > room * room)
					{
						continue;
					}
					int i = cell.x + di;
					int j = cell.y + dj;
					if (i < 0 || j < 0 || i >= size || j >= size || heights[i, j] >= floor)
					{
						return(false);
					}
				}
			}
			return(true);
		}

		/// <summary>
		/// Follows the water from a spot through the world, square by square, until it reaches the sea - noting every
		/// square it went through as landlocked when it doesn't
		/// </summary>
		/// <param name="from">The spot, on the water</param>
		/// <param name="water">The sea's level</param>
		/// <param name="height">The ground's height at a point, by its x and z</param>
		/// <param name="sea">Whether a point, by its x and z, is out at sea</param>
		/// <param name="landlocked">The squares known not to reach the sea, added to</param>
		/// <returns>True when the water reaches the sea</returns>
		internal static bool ReachesSea(Vector3 from, float water, Func<float, float, float> height, Func<float, float, bool> sea, HashSet<long> landlocked)
		{
			int startX = Mathf.RoundToInt(from.x / FloodStep);
			int startZ = Mathf.RoundToInt(from.z / FloodStep);
			HashSet<long> seen = new HashSet<long> { Key(startX, startZ) };
			Queue<Vector2Int> open = new Queue<Vector2Int>();
			open.Enqueue(new Vector2Int(startX, startZ));
			float reachSquared = FloodReach * FloodReach / (FloodStep * FloodStep);
			while (open.Count > 0 && seen.Count <= FloodCells)
			{
				Vector2Int cell = open.Dequeue();
				if (sea(cell.x * FloodStep, cell.y * FloodStep))
				{
					return(true);
				}
				for (int n = 0; n < 4; n++)
				{
					int x = cell.x + ((n == 0) ? 1 : ((n == 1) ? -1 : 0));
					int z = cell.y + ((n == 2) ? 1 : ((n == 3) ? -1 : 0));
					long key = Key(x, z);
					float dx = x - startX;
					float dz = z - startZ;
					if (seen.Contains(key) || landlocked.Contains(key) || dx * dx + dz * dz > reachSquared || height(x * FloodStep, z * FloodStep) >= water - FloodDepth)
					{
						continue;
					}
					seen.Add(key);
					open.Enqueue(new Vector2Int(x, z));
				}
			}
			landlocked.UnionWith(seen);
			return(false);
		}

		/// <summary>
		/// Works out which way the boat faces - along the most open water from its spot, the way most away from the
		/// village among those as open
		/// </summary>
		/// <param name="spot">The boat's spot</param>
		/// <param name="middle">The village's middle</param>
		/// <param name="floor">How high the ground can be for the boat to float over it</param>
		/// <param name="height">The ground's height at a point, by its x and z</param>
		/// <returns>The way, flat</returns>
		internal static Vector3 Facing(Vector3 spot, Vector3 middle, float floor, Func<float, float, float> height)
		{
			Vector3 away = spot - middle;
			away.y = 0f;
			away = ((away.sqrMagnitude > 0.01f) ? away.normalized : Vector3.forward);
			Vector3 best = away;
			float bestRun = -1f;
			float bestAway = float.MinValue;
			for (int h = 0; h < Headings; h++)
			{
				float angle = h * Mathf.PI * 2f / Headings;
				Vector3 way = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
				float run = 0f;
				for (float d = GridStep; d <= HeadingRun; d += GridStep)
				{
					Vector3 point = spot + way * d;
					if (height(point.x, point.z) >= floor)
					{
						break;
					}
					run = d;
				}
				float dot = Vector3.Dot(way, away);
				if (run > bestRun || (run == bestRun && dot > bestAway))
				{
					best = way;
					bestRun = run;
					bestAway = dot;
				}
			}
			return(best);
		}

		/// <summary>
		/// Gets the point of a spot on the grid, at the sea's level
		/// </summary>
		/// <param name="origin">The grid's corner, at the sea's level</param>
		/// <param name="i">The spot's place along x</param>
		/// <param name="j">The spot's place along z</param>
		/// <returns>The point</returns>
		private static Vector3 Point(Vector3 origin, int i, int j)
		{
			return(new Vector3(origin.x + i * GridStep, origin.y, origin.z + j * GridStep));
		}

		/// <summary>
		/// Works out how far apart two points are across the ground, leaving out their heights
		/// </summary>
		/// <param name="a">One point</param>
		/// <param name="b">The other</param>
		/// <returns>The distance</returns>
		private static float FlatDistance(Vector3 a, Vector3 b)
		{
			float dx = a.x - b.x;
			float dz = a.z - b.z;
			return(Mathf.Sqrt(dx * dx + dz * dz));
		}

		/// <summary>
		/// Gets the key of the square of the coarser grid a point is in, as ReachesSea notes them
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The key</returns>
		private static long FloodKey(Vector3 point)
		{
			return(Key(Mathf.RoundToInt(point.x / FloodStep), Mathf.RoundToInt(point.z / FloodStep)));
		}

		/// <summary>
		/// Gets the key of a square of the coarser grid
		/// </summary>
		/// <param name="x">Its place along x</param>
		/// <param name="z">Its place along z</param>
		/// <returns>The key</returns>
		private static long Key(int x, int z)
		{
			return(((long)x << 32) ^ (uint)z);
		}
	}
}
