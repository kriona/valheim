using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// The snow built up on a village's building pieces - its own and the ones players built in it - which in the Deep
	/// North piles up on any piece out in the open and, once heavy, wears down a piece that isn't held up well, so the
	/// vikings clear it off by day (see VillageLife's snow work)
	/// </summary>
	/// <remarks>
	/// A piece's snow is kept in its ZDO by the client that owns it, so it's read from there, and cleared by asking that
	/// client to (see WearNTear.ChangeSnow)
	/// </remarks>
	internal static class VillageSnow
	{
		/// <summary>
		/// How much snow a piece has to have built up, from 0 to 1, for the vikings to clear it off
		/// </summary>
		public const float ClearAt = 0.6f;

		/// <summary>
		/// Seconds each village's snowed-over pieces are kept before they're looked for again
		/// </summary>
		private const float RefreshInterval = 5f;

		/// <summary>
		/// Each village's snowed-over pieces and when they were looked for, by its generator's or anchor's ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<float, List<WearNTear>>> snowy = new Dictionary<ZDOID, KeyValuePair<float, List<WearNTear>>>();

		/// <summary>
		/// Lists a village's pieces in the Deep North with ClearAt or more snow on them, nearest a point first
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="from">The point</param>
		/// <returns>The pieces</returns>
		public static List<WearNTear> Snowy(Village village, Vector3 from)
		{
			if (village == null || village.Zdo == null || !WorldGenerator.IsDeepnorth(village.Farm.x, village.Farm.z))
			{
				return(new List<WearNTear>());
			}
			ZDOID id = village.Zdo.m_uid;
			if (!snowy.TryGetValue(id, out KeyValuePair<float, List<WearNTear>> kept) || Time.time - kept.Key >= RefreshInterval)
			{
				kept = new KeyValuePair<float, List<WearNTear>>(Time.time, Find(village));
				snowy[id] = kept;
			}
			List<WearNTear> pieces = kept.Value.FindAll((WearNTear piece) => piece != null && Snow(piece) >= ClearAt);
			pieces.Sort((WearNTear a, WearNTear b) => Vector3.Distance(a.transform.position, from).CompareTo(Vector3.Distance(b.transform.position, from)));
			return(pieces);
		}

		/// <summary>
		/// Gets how much snow is built up on a piece, from what its owner keeps
		/// </summary>
		/// <param name="piece">The piece</param>
		/// <returns>The snow, from 0 to 1</returns>
		public static float Snow(WearNTear piece)
		{
			return((piece != null && piece.m_nview != null && piece.m_nview.IsValid()) ? piece.m_nview.GetZDO().GetFloat(ZDOVars.s_snow) : 0f);
		}

		/// <summary>
		/// Forgets the villages' pieces and ground of the world being left
		/// </summary>
		public static void Forget()
		{
			snowy.Clear();
			deepSpots.Clear();
			claimedSpots.Clear();
		}

		/// <summary>
		/// How deep the ground's snow has to be, from 0 to 1, for the vikings to clear it - where the game counts it as
		/// deep snow, which slows whoever walks through it - how far apart the spots it's looked for at are, in meters,
		/// and seconds between looks for each village
		/// </summary>
		public const float DeepAt = 0.12f;
		private const float GroundSpacing = 5f;
		private const float GroundRefreshInterval = 30f;

		/// <summary>
		/// Each village's spots of deep snow on its ground and when they were looked for, by its generator's or anchor's
		/// ZDO, and the spots a viking is clearing, by where they are on the ground grid
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<float, List<Vector3>>> deepSpots = new Dictionary<ZDOID, KeyValuePair<float, List<Vector3>>>();
		private static readonly HashSet<Vector2Int> claimedSpots = new HashSet<Vector2Int>();

		/// <summary>
		/// Finds the spot of deep snow on a village's ground in the Deep North nearest a point that no other viking is
		/// clearing, and claims it - inside the village's rings, on the ground grid every GroundSpacing meters
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="from">The point</param>
		/// <param name="spot">Set to the spot, on the ground</param>
		/// <returns>False when there's none</returns>
		public static bool ClaimDeepSpot(Village village, Vector3 from, out Vector3 spot)
		{
			spot = Vector3.zero;
			if (village == null || village.Zdo == null || !WorldGenerator.IsDeepnorth(village.Farm.x, village.Farm.z))
			{
				return(false);
			}
			ZDOID id = village.Zdo.m_uid;
			if (!deepSpots.TryGetValue(id, out KeyValuePair<float, List<Vector3>> kept) || Time.time - kept.Key >= GroundRefreshInterval)
			{
				kept = new KeyValuePair<float, List<Vector3>>(Time.time, FindDeep(village));
				deepSpots[id] = kept;
			}
			float nearest = float.MaxValue;
			bool found = false;
			foreach (Vector3 candidate in kept.Value)
			{
				float distance = Utils.DistanceXZ(candidate, from);
				if (distance < nearest && !claimedSpots.Contains(Cell(candidate)))
				{
					spot = candidate;
					nearest = distance;
					found = true;
				}
			}
			if (found)
			{
				claimedSpots.Add(Cell(spot));
			}
			return(found);
		}

		/// <summary>
		/// Lets go of a spot of deep snow a viking claimed, taking it off its village's spots once it's cleared
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">The spot</param>
		/// <param name="cleared">Whether it was cleared</param>
		public static void ReleaseDeepSpot(Village village, Vector3 spot, bool cleared)
		{
			claimedSpots.Remove(Cell(spot));
			if (cleared && village != null && village.Zdo != null && deepSpots.TryGetValue(village.Zdo.m_uid, out KeyValuePair<float, List<Vector3>> kept))
			{
				kept.Value.RemoveAll((Vector3 candidate) => Cell(candidate) == Cell(spot));
			}
		}

		/// <summary>
		/// Gets the cell of the ground grid a spot is at
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>The cell</returns>
		private static Vector2Int Cell(Vector3 spot)
		{
			return(new Vector2Int(Mathf.RoundToInt(spot.x / GroundSpacing), Mathf.RoundToInt(spot.z / GroundSpacing)));
		}

		/// <summary>
		/// Looks over a village's ground inside its rings for deep snow in the Deep North, every GroundSpacing meters
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The spots, on the ground</returns>
		private static List<Vector3> FindDeep(Village village)
		{
			List<Vector3> found = new List<Vector3>();
			foreach (Village.Zone zone in village.Zones)
			{
				for (float x = Mathf.Floor((zone.Center.x - zone.Radius) / GroundSpacing) * GroundSpacing; x <= zone.Center.x + zone.Radius; x += GroundSpacing)
				{
					for (float z = Mathf.Floor((zone.Center.z - zone.Radius) / GroundSpacing) * GroundSpacing; z <= zone.Center.z + zone.Radius; z += GroundSpacing)
					{
						Vector3 point = new Vector3(x, 0f, z);
						if (Utils.DistanceXZ(point, zone.Center) > zone.Radius || !WorldGenerator.IsDeepnorth(x, z) || village.InHouse(point, 0f))
						{
							continue;
						}
						Heightmap heightmap = Heightmap.FindHeightmap(point);
						if (heightmap == null || heightmap.GetCultivationMask(point) < DeepAt)
						{
							continue;
						}
						point.y = ZoneSystem.instance.GetGroundHeight(point);
						if (!found.Exists((Vector3 other) => Cell(other) == Cell(point)))
						{
							found.Add(point);
						}
					}
				}
			}
			return(found);
		}

		/// <summary>
		/// Looks through the loaded pieces for a village's with ClearAt or more snow on them
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The pieces</returns>
		private static List<WearNTear> Find(Village village)
		{
			List<WearNTear> found = new List<WearNTear>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Key.GetFloat(ZDOVars.s_snow) < ClearAt || instance.Value == null || !village.Contains(instance.Key.GetPosition()))
				{
					continue;
				}
				WearNTear piece = instance.Value.GetComponent<WearNTear>();
				if (piece != null)
				{
					found.Add(piece);
				}
			}
			return(found);
		}
	}
}
