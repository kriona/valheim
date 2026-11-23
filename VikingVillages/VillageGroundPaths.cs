using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Finds where each loaded village's paths run, from the path painted on the ground - its own, a viking's or one a
	/// player dug with a hoe - so the village reaches along them as they wind (see Village.SetGroundPaths), every
	/// ScanInterval seconds on every client
	/// </summary>
	/// <remarks>
	/// A path is followed out from the edge of what the village has built (see Village.BaseZones) for as long as it
	/// runs outside it, up to PathReach. One that comes back in somewhere at least MinSpan from where it left - to a
	/// house standing off on its own, say - stretches the village along it, PathMargin either side. One that comes back
	/// in where it left, like a dead end, or never does, like a road off to somewhere else, doesn't
	/// </remarks>
	internal static class VillageGroundPaths
	{
		/// <summary>
		/// How often the villages' paths are traced, in seconds
		/// </summary>
		private const float ScanInterval = 30f;

		/// <summary>
		/// How wide a cell of the grid the path is followed on is, how far past the village a path is followed, how many
		/// cells at most are followed for a village, and how far apart a path's way out and its way back in have to be
		/// for it to count, in meters
		/// </summary>
		private const float Cell = 1f;
		private const float PathReach = 60f;
		private const int MaxCells = 20000;
		private const float MinSpan = 8f;

		/// <summary>
		/// How far past each of a village's zones the ground has to be loaded for its paths to be traced, in meters
		/// </summary>
		private const float LoadedMargin = 8f;

		/// <summary>
		/// When the villages' paths are next traced, in Time.time seconds
		/// </summary>
		private static float nextScan;

		/// <summary>
		/// The heightmap a spot was last looked up on, which the next spot is most likely on too
		/// </summary>
		private static Heightmap lastHeightmap;

		/// <summary>
		/// Traces each loaded village's paths every ScanInterval seconds - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (Time.time < nextScan || ZoneSystem.instance == null)
			{
				return;
			}
			nextScan = Time.time + ScanInterval;
			foreach (Village village in Village.Loaded())
			{
				// Ground that isn't loaded reads as no path, so a village is only traced once it's all loaded
				if (village.BaseZones.Count == 0 || !GroundLoaded(village.BaseZones))
				{
					continue;
				}
				List<Village.Zone> paths = Trace(village.BaseZones, IsPath, Village.PathMargin, Village.PathStep);
				for (int i = 0; i < paths.Count; i++)
				{
					Village.Zone zone = paths[i];
					zone.Center.y = ZoneSystem.instance.GetGroundHeight(zone.Center);
					paths[i] = zone;
				}
				village.SetGroundPaths(paths);
			}
			lastHeightmap = null;
		}

		/// <summary>
		/// Checks whether the ground is loaded across some zones and LoadedMargin past them - at each one's middle and
		/// at its four sides
		/// </summary>
		/// <remarks>
		/// A path running farther out over ground that isn't loaded yet is found once it is, on a later look
		/// </remarks>
		/// <param name="zones">The zones</param>
		/// <returns>True when it is</returns>
		private static bool GroundLoaded(List<Village.Zone> zones)
		{
			foreach (Village.Zone zone in zones)
			{
				float far = zone.Radius + LoadedMargin;
				Vector3[] spots = { zone.Center, zone.Center + Vector3.forward * far, zone.Center + Vector3.back * far, zone.Center + Vector3.left * far, zone.Center + Vector3.right * far };
				foreach (Vector3 spot in spots)
				{
					Heightmap heightmap = Heightmap.FindHeightmap(spot);
					if (heightmap == null || heightmap.m_paintMask == null)
					{
						return(false);
					}
				}
			}
			return(true);
		}

		/// <summary>
		/// Checks whether the ground at a spot is painted as a path - dirt, as the hoe's path paints it, or paving - and
		/// not cultivated, like a field
		/// </summary>
		/// <param name="spot">The spot - only where it is across the ground counts</param>
		/// <returns>True when it is, false too when the ground there isn't loaded</returns>
		private static bool IsPath(Vector3 spot)
		{
			if (lastHeightmap == null || !lastHeightmap.IsPointInside(spot))
			{
				lastHeightmap = Heightmap.FindHeightmap(spot);
			}
			if (lastHeightmap == null || lastHeightmap.m_paintMask == null)
			{
				return(false);
			}
			// The paint is looked up half a vertex over, the way the game looks it up for cleared ground
			Color paint = lastHeightmap.GetPaintMask(new Vector3(spot.x - 0.5f, spot.y, spot.z - 0.5f));
			return((paint.r > 0.5f || paint.b > 0.5f) && paint.g <= 0.5f);
		}

		/// <summary>
		/// Follows the paths out from the edge of some zones on a grid of Cell, for as long as they run outside them, and
		/// lays the paths that come back in at least MinSpan from where they left out as circles along them
		/// </summary>
		/// <param name="zones">The zones, around what the village has built</param>
		/// <param name="isPath">Checks whether a spot is on a path</param>
		/// <param name="reach">How far either side of a path the circles reach, in meters</param>
		/// <param name="step">How far apart the circles stand at most, in meters</param>
		/// <returns>The circles, at ground level 0</returns>
		internal static List<Village.Zone> Trace(List<Village.Zone> zones, System.Func<Vector3, bool> isPath, float reach, float step)
		{
			List<Village.Zone> bands = new List<Village.Zone>();
			HashSet<long> seen = new HashSet<long>();
			int followed = 0;
			foreach (Village.Zone zone in zones)
			{
				int marks = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * (zone.Radius + Cell) / Cell));
				for (int i = 0; i < marks && followed < MaxCells; i++)
				{
					float angle = i * 2f * Mathf.PI / marks;
					Vector3 edge = zone.Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (zone.Radius + Cell * 0.5f);
					long start = Key(edge);
					Vector3 middle = Middle(start);
					if (seen.Contains(start) || Inside(zones, middle) || !isPath(middle))
					{
						continue;
					}
					List<Vector3> cells = Follow(zones, isPath, start, seen, out List<Vector3> touches);
					followed += cells.Count;
					if (Span(touches) >= MinSpan)
					{
						bands.AddRange(Cover(cells, reach, step));
					}
				}
			}
			return(bands);
		}

		/// <summary>
		/// Follows a path outside some zones from a cell on it - each cell it runs on that's out of them, across and
		/// diagonally, up to PathReach from the first - noting each spot it runs back into them at
		/// </summary>
		/// <param name="zones">The zones</param>
		/// <param name="isPath">Checks whether a spot is on a path</param>
		/// <param name="start">The first cell</param>
		/// <param name="seen">The cells already followed, added to</param>
		/// <param name="touches">Set to the middles of the cells inside the zones it runs into</param>
		/// <returns>The middles of the cells it runs on</returns>
		private static List<Vector3> Follow(List<Village.Zone> zones, System.Func<Vector3, bool> isPath, long start, HashSet<long> seen, out List<Vector3> touches)
		{
			List<Vector3> cells = new List<Vector3>();
			touches = new List<Vector3>();
			Vector3 first = Middle(start);
			Queue<long> open = new Queue<long>();
			open.Enqueue(start);
			seen.Add(start);
			while (open.Count > 0 && cells.Count < MaxCells)
			{
				long cell = open.Dequeue();
				Vector3 middle = Middle(cell);
				cells.Add(middle);
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dz = -1; dz <= 1; dz++)
					{
						if (dx == 0 && dz == 0)
						{
							continue;
						}
						Vector3 next = middle + new Vector3(dx * Cell, 0f, dz * Cell);
						long key = Key(next);
						if (seen.Contains(key))
						{
							continue;
						}
						if (Inside(zones, next))
						{
							touches.Add(next);
							continue;
						}
						if (Village.FlatDistance(next, first) > PathReach || !isPath(next))
						{
							continue;
						}
						seen.Add(key);
						open.Enqueue(key);
					}
				}
			}
			return(cells);
		}

		/// <summary>
		/// Works out about how far apart the spots a path runs into the zones at are, at their farthest
		/// </summary>
		/// <param name="touches">The spots</param>
		/// <returns>The distance, in meters, or 0 for none</returns>
		internal static float Span(List<Vector3> touches)
		{
			if (touches.Count < 2)
			{
				return(0f);
			}
			// The spot farthest from any one, then the spot farthest from that, are about as far apart as any two
			Vector3 far = Farthest(touches, touches[0]);
			return(Village.FlatDistance(far, Farthest(touches, far)));
		}

		/// <summary>
		/// Finds the spot farthest from another
		/// </summary>
		/// <param name="spots">The spots, at least one</param>
		/// <param name="from">The other</param>
		/// <returns>The farthest</returns>
		private static Vector3 Farthest(List<Vector3> spots, Vector3 from)
		{
			Vector3 farthest = spots[0];
			float best = -1f;
			foreach (Vector3 spot in spots)
			{
				float distance = Village.FlatDistance(spot, from);
				if (distance > best)
				{
					farthest = spot;
					best = distance;
				}
			}
			return(farthest);
		}

		/// <summary>
		/// Lays circles over a path's cells, so every cell is within step of a circle's middle
		/// </summary>
		/// <param name="cells">The middles of the path's cells</param>
		/// <param name="reach">How far each circle reaches, in meters</param>
		/// <param name="step">How far a cell can be from a circle's middle, in meters</param>
		/// <returns>The circles</returns>
		internal static List<Village.Zone> Cover(List<Vector3> cells, float reach, float step)
		{
			List<Village.Zone> circles = new List<Village.Zone>();
			foreach (Vector3 cell in cells)
			{
				if (!circles.Exists((Village.Zone circle) => Village.FlatDistance(circle.Center, cell) <= step))
				{
					circles.Add(new Village.Zone { Center = cell, Radius = reach });
				}
			}
			return(circles);
		}

		/// <summary>
		/// Checks whether a spot is inside any of some zones
		/// </summary>
		/// <param name="zones">The zones</param>
		/// <param name="spot">The spot</param>
		/// <returns>True when it is</returns>
		private static bool Inside(List<Village.Zone> zones, Vector3 spot)
		{
			return(zones.Exists((Village.Zone zone) => Village.FlatDistance(zone.Center, spot) <= zone.Radius));
		}

		/// <summary>
		/// Finds the grid cell a spot is in, as one number
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>The cell</returns>
		internal static long Key(Vector3 spot)
		{
			long x = Mathf.FloorToInt(spot.x / Cell);
			long z = Mathf.FloorToInt(spot.z / Cell);
			return((x << 32) ^ (z & 0xFFFFFFFFL));
		}

		/// <summary>
		/// Finds the middle of a grid cell, at ground level 0
		/// </summary>
		/// <param name="key">The cell</param>
		/// <returns>Its middle</returns>
		internal static Vector3 Middle(long key)
		{
			int x = (int)(key >> 32);
			int z = (int)(key & 0xFFFFFFFFL);
			return(new Vector3((x + 0.5f) * Cell, 0f, (z + 0.5f) * Cell));
		}
	}
}
