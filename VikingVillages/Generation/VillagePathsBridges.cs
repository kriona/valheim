using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The bridges a village's paths cross water on - where a path needs one, how long its span is, and building it
	/// of wood or stone
	/// </summary>
	internal static partial class VillagePaths
	{
		/// <summary>
		/// Builds the bridges a village's paths would cross water on, for a village whose paths were laid without them,
		/// once everything around its houses has loaded - painting the paths that lead to them, without reshaping the
		/// ground again
		/// </summary>
		/// <param name="view">The farm's generator</param>
		private static void TryBridge(ZNetView view)
		{
			ZDO zdo = view.GetZDO();
			Vector3 farm = view.transform.position;
			List<House> houses = ReadHouses(zdo.GetByteArray(HousesKey));
			if (houses.Count < 2)
			{
				zdo.Set(BridgedKey, true);
				return;
			}
			Vector3 min = houses[0].Center;
			Vector3 max = houses[0].Center;
			foreach (House house in houses)
			{
				min = Vector3.Min(min, house.Center);
				max = Vector3.Max(max, house.Center);
			}
			min -= new Vector3(Margin, 0f, Margin);
			max += new Vector3(Margin, 0f, Margin);
			if (!AreaLoaded(min, max))
			{
				return;
			}
			Grid grid = new Grid(min, max, farm);
			List<Blocker> blockers = FindBlockers(min, max);
			grid.Mark(blockers);
			grid.MarkBridgeable();
			FindDoors(houses, blockers);
			List<Bridge> bridges = new List<Bridge>();
			foreach (KeyValuePair<int, int> edge in SpanningTree(houses))
			{
				List<Vector3> leg = grid.FindPath(houses[edge.Key], houses[edge.Value]);
				if (leg == null)
				{
					continue;
				}
				List<Bridge> crossings = grid.FindBridges(leg);
				if (crossings.Count > 0)
				{
					Paint(leg);
					bridges.AddRange(crossings);
				}
			}
			int built = BuildBridges(farm, bridges, BridgePieceAt(farm));
			zdo.Set(BridgedKey, true);
			if (built > 0)
			{
				Plugin.Log.LogInfo("Built " + built + " bridges over the water between the houses of the village at " + farm.ToString("F0"));
			}
		}

		/// <summary>
		/// Builds bridges over the water for a village's paths, noting those within the village among its pieces so its
		/// vikings repair and rebuild them
		/// </summary>
		/// <param name="farm">Where the farm's generator stands</param>
		/// <param name="bridges">The bridges</param>
		/// <param name="piece">The floor piece they're built of (see BridgePieceAt)</param>
		/// <returns>How many were built</returns>
		private static int BuildBridges(Vector3 farm, List<Bridge> bridges, string piece)
		{
			if (bridges.Count == 0)
			{
				return(0);
			}
			GameObject prefab = ZNetScene.instance.GetPrefab(piece);
			if (prefab == null)
			{
				Plugin.Log.LogWarning("Couldn't find the " + piece + " piece to build village bridges with");
				return(0);
			}
			List<PieceRecord> pieces = new List<PieceRecord>();
			foreach (Bridge bridge in bridges)
			{
				Build(prefab, bridge, pieces);
			}
			Village village = Village.ForFarm(farm);
			if (village != null)
			{
				VillageRecords.Add(village, pieces.FindAll((PieceRecord piece) => village.OwnsPiece(piece.Position)));
			}
			return(bridges.Count);
		}

		/// <summary>
		/// Builds a bridge of floors laid end to end from bank to bank, as pieces no player built - each end rests on the
		/// ground, which holds up the floors out over the water
		/// </summary>
		/// <param name="prefab">The floor piece</param>
		/// <param name="bridge">Where it runs</param>
		/// <param name="pieces">The pieces built, added to</param>
		private static void Build(GameObject prefab, Bridge bridge, List<PieceRecord> pieces)
		{
			Vector3 lift = Vector3.up * (BridgeLift - FloorTop(prefab));
			Vector3 start = bridge.Start + lift;
			Vector3 end = bridge.End + lift;
			Vector3 along = end - start;
			int count = BridgeFloors(along.magnitude);
			if (count == 0)
			{
				return;
			}
			Quaternion rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
			int hash = prefab.name.GetStableHashCode();
			int wood = VillageRecords.WoodCost(hash);
			for (int i = 0; i < count; i++)
			{
				Vector3 position = Vector3.Lerp(start, end, i / (float)(count - 1));
				GameObject instance = Object.Instantiate(prefab, position, rotation);
				VillageRecords.MarkBuilt(instance);
				WearNTear wear = instance.GetComponent<WearNTear>();
				if (wear != null)
				{
					wear.OnPlaced();
				}
				pieces.Add(new PieceRecord { Prefab = hash, Position = position, Rotation = rotation, Wood = wood });
			}
		}

		/// <summary>
		/// Builds a bridge a viking has got the wood for over the water on a house's path, the way the village's own
		/// bridges are built, and notes its pieces among the village's so its vikings repair and rebuild them
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="span">The bridge</param>
		public static void BuildHouseBridge(Village village, HouseBridge span)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(BridgePieceAt(village.Farm));
			if (prefab == null)
			{
				return;
			}
			List<PieceRecord> pieces = new List<PieceRecord>();
			Build(prefab, new Bridge { Start = span.Start, End = span.End }, pieces);
			Piece piece = prefab.GetComponent<Piece>();
			foreach (PieceRecord record in pieces)
			{
				if (piece != null)
				{
					piece.m_placeEffect.Create(record.Position, record.Rotation);
				}
			}
			VillageRecords.Add(village, pieces.FindAll((PieceRecord built) => village.OwnsPiece(built.Position)));
			Plugin.Log.LogInfo("A viking built a bridge of " + pieces.Count + " floors from " + span.Start.ToString("F0") + " to " + span.End.ToString("F0") + " on a path in the village at " + village.Farm.ToString("F0"));
		}

		/// <summary>
		/// Works out the wood a bridge takes - its floors, laid as Build lays them, each what the floor piece takes
		/// </summary>
		/// <param name="bridge">The bridge</param>
		/// <param name="piece">The floor piece it's built of</param>
		/// <returns>The wood</returns>
		private static int BridgeWood(Bridge bridge, string piece)
		{
			return(BridgeFloors(Vector3.Distance(bridge.Start, bridge.End)) * Mathf.Max(1, VillageRecords.WoodCost(piece.GetStableHashCode())));
		}

		/// <summary>
		/// Picks the floor piece a village's bridges are built of - grausten in the Ashlands and the Deep North, and
		/// wood everywhere else
		/// </summary>
		/// <param name="farm">Where the village's farm stands</param>
		/// <returns>The piece's name</returns>
		internal static string BridgePieceAt(Vector3 farm)
		{
			Heightmap.Biome biome = ((WorldGenerator.instance != null) ? WorldGenerator.instance.GetBiome(farm) : Heightmap.Biome.None);
			return(UsesStoneBridges(biome) ? StoneBridgePiece : BridgePiece);
		}

		/// <summary>
		/// Checks whether a biome's villages build their bridges of grausten
		/// </summary>
		/// <param name="biome">The village's biome</param>
		/// <returns>True for the Ashlands and the Deep North</returns>
		internal static bool UsesStoneBridges(Heightmap.Biome biome)
		{
			return(biome == Heightmap.Biome.AshLands || biome == Heightmap.Biome.DeepNorth);
		}

		/// <summary>
		/// Works out the widest water a bridge of a floor piece crosses, from what its floors hold up (see BridgeSpan),
		/// the first time for each piece - or BridgeSpanMax, a wood bridge's, when the piece can't be found
		/// </summary>
		/// <param name="piece">The floor piece's name</param>
		/// <returns>The widest water, from bank to bank, in meters</returns>
		internal static float BridgeSpanOf(string piece)
		{
			if (bridgeSpans.TryGetValue(piece, out float span))
			{
				return(span);
			}
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(piece) : null);
			WearNTear wear = ((prefab != null) ? prefab.GetComponent<WearNTear>() : null);
			if (wear == null)
			{
				return(BridgeSpanMax);
			}
			wear.GetMaterialProperties(out float maxSupport, out float minSupport, out float horizontalLoss, out float _);
			int floors = FloorsHeldOut(maxSupport, minSupport, horizontalLoss);
			span = BridgeSpan(floors);
			bridgeSpans[piece] = span;
			Plugin.Log.LogInfo("Bridges of " + piece + " hold up " + floors + " floors out from each bank, so they cross water up to " + span.ToString("F1") + " meters across");
			return(span);
		}

		/// <summary>
		/// Works out how many floors laid BridgeStep apart one resting on the ground holds up out over the water, the way
		/// WearNTear.UpdateSupport works out a piece's support - each floor out has its neighbor's support, less the
		/// material's horizontal loss of it for every meter between their middles and a tenth more, and breaks below the
		/// material's least
		/// </summary>
		/// <param name="maxSupport">The support of a floor resting on the ground</param>
		/// <param name="minSupport">The least support a floor stands with</param>
		/// <param name="horizontalLoss">How much of its support is lost for every meter out</param>
		/// <returns>The floors held up</returns>
		internal static int FloorsHeldOut(float maxSupport, float minSupport, float horizontalLoss)
		{
			float keep = 1f - horizontalLoss * (BridgeStep + 0.1f);
			float support = maxSupport;
			int floors = 0;
			while (keep > 0f && floors < 100)
			{
				support *= keep;
				if (support < minSupport)
				{
					break;
				}
				floors++;
			}
			return(floors);
		}

		/// <summary>
		/// Works out the widest water a bridge crosses, from bank to bank, when each bank's floor holds up some floors out
		/// over it - the floors held up from both banks, the floor between them and what rests on the banks (see
		/// BankRest)
		/// </summary>
		/// <param name="floorsHeldOut">How many floors each bank's floor holds up (see FloorsHeldOut)</param>
		/// <returns>The widest water, in meters - below BridgeSpanMin when no bridge can be built</returns>
		internal static float BridgeSpan(int floorsHeldOut)
		{
			return((2 * floorsHeldOut + 1) * BridgeStep - BankRest);
		}

		/// <summary>
		/// Works out how many floors a bridge is laid of - BridgeStep apart from end to end, so each overlaps the next,
		/// and at least one on each bank, or none for a bridge shorter than BridgeSpanMin, which isn't built
		/// </summary>
		/// <param name="length">How far the bridge reaches from end to end, in meters</param>
		/// <returns>The floors</returns>
		internal static int BridgeFloors(float length)
		{
			if (length < BridgeSpanMin)
			{
				return(0);
			}
			return(Mathf.Max(2, Mathf.CeilToInt(length / BridgeStep) + 1));
		}

		/// <summary>
		/// Finds how far above its pivot a floor piece's walking surface is
		/// </summary>
		/// <param name="prefab">The floor piece</param>
		/// <returns>The height of its collider's top over its pivot, in meters</returns>
		private static float FloorTop(GameObject prefab)
		{
			BoxCollider box = prefab.GetComponentInChildren<BoxCollider>(true);
			if (box == null)
			{
				return(0f);
			}
			Transform holder = box.transform;
			float top = (box.center.y + box.size.y / 2f) * holder.localScale.y;
			return((holder == prefab.transform) ? top : holder.localPosition.y + top);
		}
	}
}
