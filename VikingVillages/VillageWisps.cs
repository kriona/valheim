using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The wisp lights of a village of the Mistlands, put up with its paths - wisp torches that push back the mist around
	/// the fire, along the inside of the fence and along the paths wherever they run on with no light, and a wisp
	/// fountain that draws wisps to it - each on open ground, off the paths
	/// </summary>
	/// <remarks>
	/// Where they go is worked out before the ground is reshaped, on the village's ground with its paths marked, and
	/// they're put up once it has been, standing on the ground as it ends up. Each is one of the village's own pieces,
	/// like those it was generated with - it breaks into what it's built of, and the vikings build it back, as long as
	/// it stands within the village
	/// </remarks>
	internal static class VillageWisps
	{
		/// <summary>
		/// The wisp torch, a standing torch that pushes back the mist, and the wisp fountain
		/// </summary>
		public const string TorchPrefab = "piece_groundtorch_mist";
		public const string FountainPrefab = "piece_wisplure";

		/// <summary>
		/// The torches around the fire - how many, and how far from its middle, past where the vikings gather, in meters
		/// </summary>
		private const int FireTorches = 4;
		private const float FireTorchReach = 8f;

		/// <summary>
		/// The torches along the fence - how far inside it they stand and how far apart, in meters
		/// </summary>
		private const float EdgeInset = 3f;
		private const float EdgeSpacing = 14f;

		/// <summary>
		/// The torches along the paths - how far along a path runs from every light before it gets one, and how often
		/// along it that's looked at, in meters
		/// </summary>
		private const float PathGap = 10f;
		private const float PathStep = 2f;

		/// <summary>
		/// How much room a torch needs around it, how far from where it's wanted it's looked for, and the most the ground
		/// under it can rise and fall, in meters
		/// </summary>
		private const float TorchRoom = 0.5f;
		private const float TorchSearch = 2.5f;
		private const float TorchFlatness = 0.5f;

		/// <summary>
		/// How much room the fountain needs around it, how far from the fire it stands at the least, and how far it's
		/// looked for, in meters
		/// </summary>
		private const float FountainRoom = 1.5f;
		private const float FountainLeast = 8f;
		private const float FountainSearch = 30f;

		/// <summary>
		/// How far around the middle of the fire nothing stands, in meters
		/// </summary>
		private const float FireKeep = 4f;

		/// <summary>
		/// Where a village's wisp lights go - its torches, and its fountain, with whether the ground under the fountain
		/// is flattened for it
		/// </summary>
		internal class Plan
		{
			public List<Vector3> Torches = new List<Vector3>();
			public bool HasFountain;
			public Vector3 Fountain;
			public bool FlattenFountain;
		}

		/// <summary>
		/// Checks whether a village gets wisp lights - one of the Mistlands
		/// </summary>
		/// <param name="biome">The biome its farm stands in</param>
		/// <returns>True when it does</returns>
		public static bool GetsWisps(Heightmap.Biome biome)
		{
			return(biome == Heightmap.Biome.Mistlands);
		}

		/// <summary>
		/// Works out where a village's wisp lights go - the fountain on open ground nearest the fire, at least
		/// FountainLeast from it, then the torches around the fire, along the fence, and along the paths wherever they
		/// run more than PathGap from every light - each on the open ground nearest where it's wanted, kept apart from the
		/// others
		/// </summary>
		/// <param name="grid">The village's ground, with its paths marked</param>
		/// <param name="fire">Where the village's fire stands or goes</param>
		/// <param name="farm">Where the farm's generator stands, the middle of its fence</param>
		/// <param name="fence">How far the fence stands from the farm's middle, in meters</param>
		/// <param name="legs">The village's paths, each as its points</param>
		/// <returns>Where they go</returns>
		public static Plan Find(VillagePaths.Grid grid, Vector3 fire, Vector3 farm, float fence, List<List<Vector3>> legs)
		{
			Plan plan = new Plan();
			List<Vector3> lights = new List<Vector3> { fire };
			Keep(grid, fire, FireKeep);
			if (VillagePaths.FindLevelSpot(grid, fire, FountainRoom, FountainLeast, FountainSearch, out Vector3 fountain, out bool flatten))
			{
				plan.HasFountain = true;
				plan.Fountain = fountain;
				plan.FlattenFountain = flatten;
				lights.Add(fountain);
				Keep(grid, fountain, FountainRoom);
			}
			float start = Random.Range(0f, 360f);
			foreach (Vector3 wanted in RingPoints(fire, FireTorchReach, FireTorches, start))
			{
				AddTorch(grid, plan, lights, wanted);
			}
			float edge = fence - EdgeInset;
			if (edge > FireTorchReach)
			{
				foreach (Vector3 wanted in RingPoints(farm, edge, RingCount(edge, EdgeSpacing), start))
				{
					AddTorch(grid, plan, lights, wanted);
				}
			}
			foreach (List<Vector3> leg in legs)
			{
				foreach (Vector3 point in Along(leg, PathStep))
				{
					if (NeedsLight(point, lights, PathGap))
					{
						AddTorch(grid, plan, lights, point);
					}
				}
			}
			return(plan);
		}

		/// <summary>
		/// Adds a torch on the open ground nearest where it's wanted, when there's any close enough, keeping the ground
		/// around it clear of the next
		/// </summary>
		/// <param name="grid">The village's ground</param>
		/// <param name="plan">The plan, given the torch</param>
		/// <param name="lights">The lights so far, given the torch</param>
		/// <param name="wanted">Where it's wanted</param>
		private static void AddTorch(VillagePaths.Grid grid, Plan plan, List<Vector3> lights, Vector3 wanted)
		{
			if (!grid.FindOpenSpot(wanted, TorchRoom, 0f, TorchSearch, TorchFlatness, out Vector3 spot, out float _))
			{
				return;
			}
			plan.Torches.Add(spot);
			lights.Add(spot);
			Keep(grid, spot, TorchRoom);
		}

		/// <summary>
		/// Keeps the ground within a radius of a point clear of anything found after
		/// </summary>
		/// <param name="grid">The village's ground</param>
		/// <param name="point">The point</param>
		/// <param name="radius">The radius, in meters</param>
		private static void Keep(VillagePaths.Grid grid, Vector3 point, float radius)
		{
			grid.Mark(new List<VillagePaths.Blocker> { new VillagePaths.Blocker { Center = point, Radius = radius, Kind = VillagePaths.Kind.Avoid } });
		}

		/// <summary>
		/// Puts a village's wisp lights up where they go, on the ground as it ends up - flattening the fountain's first
		/// when it needs it - and notes those within the village among its pieces, for its vikings to build back. Its
		/// pieces are usually noted after this, along with the rest, and are only noted here when they were noted before
		/// </summary>
		/// <param name="plan">Where they go</param>
		/// <param name="farm">Where the farm's generator stands</param>
		public static void Build(Plan plan, Vector3 farm)
		{
			List<PieceRecord> placed = new List<PieceRecord>();
			foreach (Vector3 spot in plan.Torches)
			{
				Vector3 at = spot;
				at.y = ZoneSystem.instance.GetGroundHeight(spot);
				Place(TorchPrefab, at, placed);
			}
			int torches = placed.Count;
			if (plan.HasFountain)
			{
				Vector3 at = plan.Fountain;
				if (plan.FlattenFountain)
				{
					VillagePaths.Flatten(at);
				}
				else
				{
					at.y = ZoneSystem.instance.GetGroundHeight(at);
				}
				Place(FountainPrefab, at, placed);
			}
			Village village = Village.ForFarm(farm);
			if (village != null)
			{
				VillageRecords.Add(village, placed.FindAll((PieceRecord piece) => village.OwnsPiece(piece.Position)));
			}
			Plugin.Log.LogInfo("Put up " + torches + " wisp torches" + ((placed.Count > torches) ? " and a wisp fountain" : "") + " in the village at " + farm.ToString("F0"));
		}

		/// <summary>
		/// Puts a piece up, turned any way, as one no player built
		/// </summary>
		/// <param name="prefabName">The piece's prefab</param>
		/// <param name="at">Where it stands</param>
		/// <param name="placed">The pieces put up, added to</param>
		private static void Place(string prefabName, Vector3 at, List<PieceRecord> placed)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
			if (prefab == null)
			{
				Plugin.Log.LogWarning("Couldn't find the " + prefabName + " prefab for a village's wisp lights");
				return;
			}
			Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
			GameObject instance = Object.Instantiate(prefab, at, rotation);
			WearNTear wear = instance.GetComponent<WearNTear>();
			if (wear != null)
			{
				wear.OnPlaced();
			}
			int hash = prefabName.GetStableHashCode();
			placed.Add(new PieceRecord { Prefab = hash, Position = at, Rotation = rotation, Wood = VillageRecords.WoodCost(hash) });
		}

		/// <summary>
		/// Works out how many points a ring gets, about a spacing apart around it, at least 1
		/// </summary>
		/// <param name="radius">The ring's radius, in meters</param>
		/// <param name="spacing">How far apart the points are about, in meters</param>
		/// <returns>How many points</returns>
		internal static int RingCount(float radius, float spacing)
		{
			return(Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * Mathf.Max(0f, radius) / spacing)));
		}

		/// <summary>
		/// Spreads points evenly around a ring on the ground
		/// </summary>
		/// <param name="center">The ring's middle</param>
		/// <param name="radius">Its radius, in meters</param>
		/// <param name="count">How many points</param>
		/// <param name="startDegrees">Where around it the first point is, in degrees</param>
		/// <returns>The points, at the middle's height</returns>
		internal static List<Vector3> RingPoints(Vector3 center, float radius, int count, float startDegrees)
		{
			List<Vector3> points = new List<Vector3>();
			for (int i = 0; i < count; i++)
			{
				float angle = (startDegrees + 360f * i / count) * Mathf.Deg2Rad;
				points.Add(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
			}
			return(points);
		}

		/// <summary>
		/// Lists the points along a path a step apart, its first point included
		/// </summary>
		/// <param name="path">The path's points</param>
		/// <param name="step">How far apart, in meters along the path</param>
		/// <returns>The points</returns>
		internal static List<Vector3> Along(List<Vector3> path, float step)
		{
			List<Vector3> points = new List<Vector3>();
			if (path == null || path.Count == 0)
			{
				return(points);
			}
			points.Add(path[0]);
			float carried = 0f;
			for (int i = 1; i < path.Count; i++)
			{
				Vector3 from = path[i - 1];
				Vector3 to = path[i];
				float length = Vector3.Distance(from, to);
				float at = step - carried;
				while (at <= length)
				{
					points.Add(Vector3.Lerp(from, to, at / length));
					at += step;
				}
				carried = length - (at - step);
			}
			return(points);
		}

		/// <summary>
		/// Checks whether a point is farther than a gap from every light, across the ground
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="lights">Where the lights stand</param>
		/// <param name="gap">The gap, in meters</param>
		/// <returns>True when it needs a light of its own</returns>
		internal static bool NeedsLight(Vector3 point, List<Vector3> lights, float gap)
		{
			foreach (Vector3 light in lights)
			{
				if (Utils.DistanceXZ(light, point) <= gap)
				{
					return(false);
				}
			}
			return(true);
		}
	}
}
