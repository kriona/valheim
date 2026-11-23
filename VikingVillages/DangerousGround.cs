using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace VikingVillages
{
	/// <summary>
	/// The ground that burns whoever stands on it, which living vikings keep off - the Ashlands' lava, and the boiling
	/// sea around the Ashlands
	/// </summary>
	/// <remarks>
	/// The lava's and the sea's ground is built into navmesh areas of their own, costing what the ground and the water
	/// cost every other creature, so their paths are the game's. A living viking's AI leaves both areas out (see
	/// NavmeshRoofs.StartAvoiding), so its paths go around them and the spots it picks are never on them, and a move
	/// that would take it from safe ground onto them stops short (see VikingDangerStepPatch). Only the terrain is
	/// dangerous, as in Character.UpdateLava - a building piece or a rock standing in the lava or the sea is safe to
	/// stand on, and keeps its place in the navmesh
	/// </remarks>
	internal static class DangerousGround
	{
		/// <summary>
		/// The navmesh areas lava's ground and the sea's are built into, after NavmeshRoofs' roof and fire areas
		/// </summary>
		public const int LavaArea = 6;
		public const int SeaArea = 7;

		/// <summary>
		/// Both areas, as a navmesh area mask
		/// </summary>
		public const int AreaMask = (1 << LavaArea) | (1 << SeaArea);

		/// <summary>
		/// How the lava's heat follows the lava in the ground's paint - as Character.UpdateLava has it, the heat rises
		/// from nothing at LavaHeatLow to full at LavaHeatHigh, and burns once it's above MinLavaHeat
		/// </summary>
		private const float LavaHeatLow = 0.1f;
		private const float LavaHeatHigh = 1f;
		private const float MinLavaHeat = 0.05f;

		/// <summary>
		/// The side of the squares a navmesh tile's ground is looked at in, in meters, and how far past dangerous ground
		/// its area reaches - farther than StepAhead, so a viking on a path at the edge of the area never looks ahead
		/// onto the danger itself
		/// </summary>
		private const float CellSize = 1f;
		private const float Margin = 1f;

		/// <summary>
		/// How far below the ground an area reaches and how far above the ground, or above the sea, in meters - low
		/// enough that a bridge or floor built over the lava stays out of it
		/// </summary>
		private const float DepthBelow = 1f;
		private const float HeightAbove = 0.5f;

		/// <summary>
		/// How much the tops of a row's dangerous squares can differ before they're split into areas of their own, in
		/// meters, so an area on a slope doesn't reach up over what's built beside it
		/// </summary>
		private const float TopSpread = 1f;

		/// <summary>
		/// How far ahead of a viking a step is looked at, in meters
		/// </summary>
		private const float StepAhead = 0.75f;

		/// <summary>
		/// How far above a point the ground under it is looked for from, and how far down, in meters
		/// </summary>
		private const float FootingRise = 2f;
		private const float FootingDrop = 50f;

		/// <summary>
		/// The area the game builds walkable sources into
		/// </summary>
		private const int DefaultArea = 0;

		/// <summary>
		/// The layers of what can be stood on, looked up on first use
		/// </summary>
		private static int groundMask;

		/// <summary>
		/// Whether the areas' costs have been set
		/// </summary>
		private static bool costSet;

		/// <summary>
		/// A run of dangerous squares along a row, from its first to its last, the area it's built into, and how low
		/// and high the area reaches
		/// </summary>
		internal struct Span
		{
			public int Start;
			public int End;
			public int Area;
			public float Bottom;
			public float Top;
		}

		/// <summary>
		/// Works out how hot lava is from how much lava is in the ground's paint, as Character.UpdateLava does
		/// </summary>
		/// <param name="lava">The lava in the paint, from 0 to 1</param>
		/// <returns>The heat, from 0 to 1</returns>
		internal static float LavaHeat(float lava)
		{
			float t = Mathf.Clamp01((lava - LavaHeatLow) / (LavaHeatHigh - LavaHeatLow));
			return(t * t * (3f - 2f * t));
		}

		/// <summary>
		/// Works out which dangerous area a spot of the Ashlands' ground belongs to - the sea's where the ground is
		/// under water, which boils all around the Ashlands, and the lava's where it's hot enough to burn
		/// </summary>
		/// <param name="ground">The ground's height</param>
		/// <param name="waterLevel">The water's height</param>
		/// <param name="lava">The lava in the ground's paint, from 0 to 1</param>
		/// <returns>SeaArea, LavaArea, or -1 for safe ground</returns>
		internal static int AreaOf(float ground, float waterLevel, float lava)
		{
			if (ground < waterLevel)
			{
				return(SeaArea);
			}
			return((LavaHeat(lava) > MinLavaHeat) ? LavaArea : -1);
		}

		/// <summary>
		/// Works out how high a dangerous spot's area reaches - from DepthBelow under the ground to HeightAbove over
		/// it, or over the water for the sea, which a viking swims at the top of
		/// </summary>
		/// <param name="area">The spot's area</param>
		/// <param name="ground">The ground's height</param>
		/// <param name="waterLevel">The water's height</param>
		/// <returns>The height the area reaches up to</returns>
		internal static float TopOf(int area, float ground, float waterLevel)
		{
			return(((area == SeaArea) ? Mathf.Max(ground, waterLevel) : ground) + HeightAbove);
		}

		/// <summary>
		/// Joins a row's dangerous squares into runs - each of squares side by side in the same area, whose tops are
		/// within a spread of its first square's
		/// </summary>
		/// <param name="areas">Each square's area, -1 for safe ground</param>
		/// <param name="bottoms">How low each square's area reaches</param>
		/// <param name="tops">How high each square's area reaches</param>
		/// <param name="spread">How far a square's top can be from the run's first before it starts a run of its own</param>
		/// <returns>The runs, along the row</returns>
		internal static List<Span> Spans(int[] areas, float[] bottoms, float[] tops, float spread)
		{
			List<Span> spans = new List<Span>();
			Span run = new Span();
			bool open = false;
			float firstTop = 0f;
			for (int i = 0; i < areas.Length; i++)
			{
				if (open && (areas[i] != run.Area || Mathf.Abs(tops[i] - firstTop) > spread))
				{
					spans.Add(run);
					open = false;
				}
				if (areas[i] < 0)
				{
					continue;
				}
				if (!open)
				{
					run = new Span { Start = i, End = i, Area = areas[i], Bottom = bottoms[i], Top = tops[i] };
					firstTop = tops[i];
					open = true;
					continue;
				}
				run.End = i;
				run.Bottom = Mathf.Min(run.Bottom, bottoms[i]);
				run.Top = Mathf.Max(run.Top, tops[i]);
			}
			if (open)
			{
				spans.Add(run);
			}
			return(spans);
		}

		/// <summary>
		/// Checks whether a move takes a creature from safe ground onto dangerous ground within StepAhead - one
		/// already on dangerous ground is left to move as it likes, so it can get off it
		/// </summary>
		/// <param name="character">The creature moving</param>
		/// <param name="dir">The way it's moving</param>
		/// <returns>True when the step ahead is dangerous and where it stands isn't</returns>
		public static bool StepsIntoDanger(Character character, Vector3 dir)
		{
			Vector3 heading = new Vector3(dir.x, 0f, dir.z);
			Vector3 here = character.transform.position;
			if (heading.sqrMagnitude < 0.01f || !WorldGenerator.IsAshlands(here.x, here.z))
			{
				return(false);
			}
			return(IsDangerous(here + heading.normalized * StepAhead) && !IsDangerous(here));
		}

		/// <summary>
		/// Checks whether standing at a point would burn - the first thing under it is the Ashlands' terrain, under the
		/// boiling sea or hot with lava
		/// </summary>
		/// <param name="point">The point, at or a little above where something would stand</param>
		/// <returns>True when it's dangerous</returns>
		public static bool IsDangerous(Vector3 point)
		{
			if (ZoneSystem.instance == null || !WorldGenerator.IsAshlands(point.x, point.z))
			{
				return(false);
			}
			if (groundMask == 0)
			{
				groundMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
			}
			if (!Physics.Raycast(point + Vector3.up * FootingRise, Vector3.down, out RaycastHit hit, FootingDrop, groundMask, QueryTriggerInteraction.Ignore))
			{
				return(false);
			}
			Heightmap hmap = hit.collider.GetComponent<Heightmap>();
			return(hmap != null && AreaOf(hit.point.y, ZoneSystem.instance.m_waterLevel, hmap.GetLava(hit.point)) >= 0);
		}

		/// <summary>
		/// Puts the dangerous ground in a navmesh tile into the lava and sea areas, setting the areas' costs the first
		/// time - leaving out the tiles of a creature that can't walk, which have no walkable sources, and those
		/// nowhere near the Ashlands
		/// </summary>
		/// <param name="sources">The tile's sources</param>
		/// <param name="bounds">The tile's bounds, in the world</param>
		public static void Mark(List<NavMeshBuildSource> sources, Bounds bounds)
		{
			if (ZoneSystem.instance == null || Pathfinding.instance == null || !NearAshlands(bounds) || !sources.Exists((NavMeshBuildSource source) => source.area == DefaultArea))
			{
				return;
			}
			if (!costSet)
			{
				NavMesh.SetAreaCost(LavaArea, Pathfinding.instance.m_defaultCost);
				NavMesh.SetAreaCost(SeaArea, Pathfinding.instance.m_waterCost);
				costSet = true;
			}
			float waterLevel = ZoneSystem.instance.m_waterLevel;
			float minX = bounds.min.x - Margin;
			float minZ = bounds.min.z - Margin;
			int columns = Mathf.CeilToInt((bounds.size.x + Margin * 2f) / CellSize);
			int rows = Mathf.CeilToInt((bounds.size.z + Margin * 2f) / CellSize);
			int[] areas = new int[columns];
			float[] bottoms = new float[columns];
			float[] tops = new float[columns];
			Heightmap hmap = null;
			for (int row = 0; row < rows; row++)
			{
				float z = minZ + (row + 0.5f) * CellSize;
				for (int column = 0; column < columns; column++)
				{
					Vector3 point = new Vector3(minX + (column + 0.5f) * CellSize, 0f, z);
					areas[column] = -1;
					if (!WorldGenerator.IsAshlands(point.x, point.z))
					{
						continue;
					}
					if (hmap == null || !hmap.IsPointInside(point))
					{
						hmap = Heightmap.FindHeightmap(point);
					}
					if (hmap == null || !hmap.GetWorldHeight(point, out float ground))
					{
						continue;
					}
					point.y = ground;
					areas[column] = AreaOf(ground, waterLevel, hmap.GetLava(point));
					bottoms[column] = ground - DepthBelow;
					tops[column] = TopOf(areas[column], ground, waterLevel);
				}
				foreach (Span span in Spans(areas, bottoms, tops, TopSpread))
				{
					NavMeshBuildSource box = new NavMeshBuildSource();
					box.shape = NavMeshBuildSourceShape.ModifierBox;
					Vector3 center = new Vector3(minX + (span.Start + span.End + 1) * CellSize / 2f, (span.Bottom + span.Top) / 2f, z);
					box.transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);
					box.size = new Vector3((span.End - span.Start + 1) * CellSize + Margin * 2f, span.Top - span.Bottom, CellSize + Margin * 2f);
					box.area = span.Area;
					sources.Add(box);
				}
			}
		}

		/// <summary>
		/// Checks whether any of a tile's corners or its middle, with the margin around it, is in the Ashlands
		/// </summary>
		/// <param name="bounds">The tile's bounds, in the world</param>
		/// <returns>True when one is</returns>
		private static bool NearAshlands(Bounds bounds)
		{
			float minX = bounds.min.x - Margin;
			float maxX = bounds.max.x + Margin;
			float minZ = bounds.min.z - Margin;
			float maxZ = bounds.max.z + Margin;
			return(WorldGenerator.IsAshlands(bounds.center.x, bounds.center.z) || WorldGenerator.IsAshlands(minX, minZ) || WorldGenerator.IsAshlands(minX, maxZ) || WorldGenerator.IsAshlands(maxX, minZ) || WorldGenerator.IsAshlands(maxX, maxZ));
		}
	}
}
