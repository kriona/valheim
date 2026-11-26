using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The beehives the vikings build in their village from queen bees, up to HivesMax - where one can go and what it
	/// takes
	/// </summary>
	/// <remarks>
	/// A hive goes on open ground in the village's core, where it makes honey - in a biome its bees live in, under
	/// little enough cover - on fairly flat ground with nothing in the way, off the paths and HiveSpacing from the
	/// village's other hives. It's built as a piece no player built and noted among the village's pieces, so the
	/// vikings repair and rebuild it
	/// </remarks>
	internal static class VillageHives
	{
		/// <summary>
		/// The beehive piece, and the queen bee it's built with
		/// </summary>
		public const string HivePrefab = "piece_beehive";
		public const string QueenBeePrefab = "QueenBee";
		private static readonly int HiveHash = HivePrefab.GetStableHashCode();

		/// <summary>
		/// The most beehives a village builds itself, counting those it has already
		/// </summary>
		public const int HivesMax = 6;

		/// <summary>
		/// How far apart the village's hives keep, how many spots are tried for one, how far above the ground the room
		/// for one is checked from, and the most the ground under one can rise and fall, in meters
		/// </summary>
		private const float HiveSpacing = 5f;
		private const int SpotTries = 20;
		private const float RoomLift = 0.15f;
		private const float Flatness = 0.4f;

		private static int roomMask;

		/// <summary>
		/// Counts the beehives in a village, built by anyone
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>How many</returns>
		public static int Count(Village village)
		{
			int count = 0;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Key.GetPrefab() == HiveHash && village.OwnsPiece(instance.Key.GetPosition()))
				{
					count++;
				}
			}
			return(count);
		}

		/// <summary>
		/// Works out the wood a beehive takes to build - every resource but its queen bee
		/// </summary>
		/// <returns>The wood, or -1 when the game has no beehive</returns>
		public static int WoodCost()
		{
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(HivePrefab) : null);
			Piece piece = ((prefab != null) ? prefab.GetComponent<Piece>() : null);
			if (piece == null)
			{
				return(-1);
			}
			int wood = 0;
			foreach (Piece.Requirement requirement in piece.m_resources)
			{
				if (requirement.m_resItem != null && requirement.m_resItem.gameObject.name != QueenBeePrefab)
				{
					wood += Mathf.Max(0, requirement.m_amount);
				}
			}
			return(wood);
		}

		/// <summary>
		/// Finds a spot in a village for a beehive (see the remarks)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">Where it would stand, on the ground</param>
		/// <param name="rotation">Which way it would face</param>
		/// <returns>False when no spot tried had room</returns>
		public static bool FindSpot(Village village, out Vector3 spot, out Quaternion rotation)
		{
			for (int i = 0; i < SpotTries; i++)
			{
				rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
				if (village.RandomOpenSpot(out spot) && HasRoom(village, spot, rotation))
				{
					return(true);
				}
			}
			spot = Vector3.zero;
			rotation = Quaternion.identity;
			return(false);
		}

		/// <summary>
		/// Checks whether a beehive would make honey at a spot and has room there (see the remarks)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">Where it would stand, on the ground</param>
		/// <param name="rotation">Which way it would face</param>
		/// <returns>True when it would</returns>
		public static bool HasRoom(Village village, Vector3 spot, Quaternion rotation)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(HivePrefab);
			Beehive hive = ((prefab != null) ? prefab.GetComponent<Beehive>() : null);
			if (hive == null || !VillageFurnishing.Footprint(HivePrefab, out Bounds size))
			{
				return(false);
			}
			if ((Heightmap.FindBiome(spot) & hive.m_biome) == 0)
			{
				return(false);
			}
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Key.GetPrefab() == HiveHash && Utils.DistanceXZ(instance.Key.GetPosition(), spot) < HiveSpacing)
				{
					return(false);
				}
			}
			float lowest = float.MaxValue;
			float highest = float.MinValue;
			for (int corner = 0; corner < 5; corner++)
			{
				Vector3 local = ((corner == 4) ? size.center : size.center + new Vector3(((corner & 1) != 0) ? size.extents.x : -size.extents.x, 0f, ((corner & 2) != 0) ? size.extents.z : -size.extents.z));
				Vector3 point = spot + rotation * local;
				float height = ZoneSystem.instance.GetGroundHeight(point);
				lowest = Mathf.Min(lowest, height);
				highest = Mathf.Max(highest, height);
				Heightmap heightmap = Heightmap.FindHeightmap(point);
				if (village.InHouse(point, 0.5f) || (heightmap != null && heightmap.IsCleared(point)))
				{
					return(false);
				}
			}
			if (highest - lowest > Flatness)
			{
				return(false);
			}
			if (roomMask == 0)
			{
				roomMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
			}
			Vector3 center = spot + rotation * size.center;
			center.y = highest + RoomLift + size.extents.y;
			if (Physics.CheckBox(center, size.extents, rotation, roomMask, QueryTriggerInteraction.Ignore))
			{
				return(false);
			}
			// The bees need open sky - the same check the hive makes, from its cover point
			Vector3 coverPoint = ((hive.m_coverPoint != null) ? prefab.transform.InverseTransformPoint(hive.m_coverPoint.position) : Vector3.up);
			Cover.GetCoverForPoint(spot + rotation * coverPoint, out float cover, out bool _);
			return(cover < hive.m_maxCover);
		}

		/// <summary>
		/// Builds a beehive in a village, as a piece no player built, noting it among the village's pieces
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">Where it stands, on the ground</param>
		/// <param name="rotation">Which way it faces</param>
		public static void Build(Village village, Vector3 spot, Quaternion rotation)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(HivePrefab);
			if (prefab == null)
			{
				return;
			}
			GameObject instance = Object.Instantiate(prefab, spot, rotation);
			VillageRecords.MarkBuilt(instance);
			WearNTear wear = instance.GetComponent<WearNTear>();
			if (wear != null)
			{
				wear.OnPlaced();
			}
			Piece piece = instance.GetComponent<Piece>();
			if (piece != null)
			{
				piece.m_placeEffect.Create(spot, rotation, instance.transform);
			}
			VillageRecords.Add(village, new List<PieceRecord> { new PieceRecord { Prefab = HiveHash, Position = spot, Rotation = rotation, Wood = VillageRecords.WoodCost(HiveHash) } });
			Plugin.Log.LogInfo("A viking built a beehive at " + spot.ToString("F1") + " in the village at " + village.Farm.ToString("F0"));
		}
	}
}
