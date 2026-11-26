using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The paths from a house added to a village for a player's bed to the rest of the village, planned and painted
	/// by its vikings (see VillageLife.PathJob)
	/// </summary>
	internal static partial class VillagePaths
	{
		/// <summary>
		/// On a farm's generator, the houses added to its village for players' beds that have a path to the rest of it -
		/// made by a viking with a hoe, or given up on when there's no way there
		/// </summary>
		private static readonly int PathedHousesKey = "kriona.VikingVillages.villagePathedHouses".GetStableHashCode();

		/// <summary>
		/// The message a viking's client sends to whoever runs its village once it has made a house's path
		/// </summary>
		private const string PathedRpc = "kriona.VikingVillages.VillagePathed";

		/// <summary>
		/// The message the client laying a village's paths sends every client, with the objects it moved with the ground
		/// and where they went
		/// </summary>
		private const string MovedRpc = "kriona.VikingVillages.VillageMoved";

		/// <summary>
		/// How close two houses' middles are to be the same house, and how far from a farm's middle its generator
		/// stands, in meters
		/// </summary>
		private const float SameHouseReach = 1f;
		private const float FarmReach = 30f;

		/// <summary>
		/// A path a viking makes with a hoe from a house added to its village to the nearest house that has one - the
		/// spots it paints the hoe's path at, in order, and the clutter lying at each, which it clears as it gets there
		/// </summary>
		public class HousePath
		{
			public House House;
			public readonly List<Vector3> Spots = new List<Vector3>();
			public readonly List<List<ZNetView>> Clutter = new List<List<ZNetView>>();
			public readonly List<HouseBridge> Bridges = new List<HouseBridge>();
		}

		/// <summary>
		/// A bridge a viking builds over the water on a house's path - from the ground on one bank to the ground on the
		/// other, how many of the path's spots come before it, so it's built once those are made, and the wood it takes
		/// </summary>
		public class HouseBridge
		{
			public Vector3 Start;
			public Vector3 End;
			public int Before;
			public int Wood;
		}

		/// <summary>
		/// Registers the message a viking's client sends once it has made a house's path, and the one telling every
		/// client where the objects moved with a village's ground went, with a world's routed RPC, forgetting the farms
		/// generated in the world being left
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance?.Register<Vector3, Vector3>(PathedRpc, OnPathed);
			ZRoutedRpc.instance?.Register<ZPackage>(MovedRpc, OnMoved);
			generatedAt.Clear();
		}

		/// <summary>
		/// Checks whether a village has a house added for a player's bed with no path to the rest of it yet
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="added">The houses added to it for players' beds</param>
		/// <returns>True when it has</returns>
		public static bool NeedsPath(Village village, List<House> added)
		{
			List<House> pathed = PathedHouses(village.Zdo);
			return(added.Exists((House house) => !HasHouse(pathed, house)));
		}

		/// <summary>
		/// Checks whether one house added to a village for a player's bed still needs its path - it's still among the
		/// added houses, and its path hasn't been made
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="added">The houses added to it for players' beds</param>
		/// <param name="house">The house</param>
		/// <returns>True when it does</returns>
		public static bool StillNeedsPath(Village village, List<House> added, House house)
		{
			return(HasHouse(added, house) && !HasHouse(PathedHouses(village.Zdo), house));
		}

		/// <summary>
		/// Plans the path a viking makes with a hoe for the first house added to a village for a player's bed that has
		/// none yet - to the nearest of the village's other houses that has one, steering around what the village's own
		/// paths steer around, from door to door, once everything between them has loaded, and crossing water a bridge can
		/// span the way they do, with the bridges for the viking to build (see HouseBridge). A house there's no way to is
		/// given up on
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="added">The houses added to it for players' beds</param>
		/// <returns>The path, or null when there's none to make now</returns>
		public static HousePath PlanHousePath(Village village, List<House> added)
		{
			List<House> pathed = PathedHouses(village.Zdo);
			foreach (House house in added)
			{
				if (HasHouse(pathed, house))
				{
					continue;
				}
				bool found = false;
				House nearest = new House();
				foreach (House other in village.Houses)
				{
					bool waiting = (HasHouse(added, other) && !HasHouse(pathed, other));
					if (waiting || Utils.DistanceXZ(other.Center, house.Center) <= SameHouseReach)
					{
						continue;
					}
					if (!found || Utils.DistanceXZ(other.Center, house.Center) < Utils.DistanceXZ(nearest.Center, house.Center))
					{
						nearest = other;
						found = true;
					}
				}
				if (!found)
				{
					continue;
				}
				Vector3 margin = new Vector3(Margin, 0f, Margin);
				Vector3 min = Vector3.Min(house.Center, nearest.Center) - margin;
				Vector3 max = Vector3.Max(house.Center, nearest.Center) + margin;
				if (!AreaLoaded(min, max))
				{
					continue;
				}
				Grid grid = new Grid(min, max, village.Farm);
				List<Blocker> blockers = FindBlockers(min, max);
				grid.Mark(blockers);
				grid.MarkBridgeable();
				List<House> ends = new List<House> { house, nearest };
				FindDoors(ends, blockers);
				List<Vector3> path = grid.FindPath(ends[0], ends[1]);
				if (path == null)
				{
					Plugin.Log.LogWarning("Couldn't find a way for a path from the house at " + house.Center.ToString("F0") + " to the village at " + village.Farm.ToString("F0") + ", so none is made");
					Pathed(village, house);
					continue;
				}
				HousePath plan = new HousePath { House = house };
				List<int> spotSteps = PaintSteps(path, PaintSpacing, ZoneSystem.instance.m_waterLevel + WetHeight);
				foreach (int i in spotSteps)
				{
					plan.Spots.Add(path[i]);
					plan.Clutter.Add(ClutterAt(blockers, path[i]));
				}
				// Each bridge goes in once the spots before the bank it starts from are made
				foreach (Bridge crossing in grid.FindBridges(path))
				{
					int step = 0;
					for (int i = 1; i < path.Count; i++)
					{
						if (Utils.DistanceXZ(path[i], crossing.Start) < Utils.DistanceXZ(path[step], crossing.Start))
						{
							step = i;
						}
					}
					int before = spotSteps.FindAll((int spotStep) => spotStep <= step).Count;
					plan.Bridges.Add(new HouseBridge { Start = crossing.Start, End = crossing.End, Before = Mathf.Min(before, Mathf.Max(0, plan.Spots.Count - 1)), Wood = BridgeWood(crossing, BridgePieceAt(village.Farm)) });
				}
				return(plan);
			}
			return(null);
		}

		/// <summary>
		/// Paints the hoe's path at a spot and clears the clutter lying there, the way the village's own paths are made
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <param name="clutter">The clutter lying at it</param>
		public static void PaintSpot(Vector3 spot, List<ZNetView> clutter)
		{
			if (ObjectDB.instance != null && ObjectDB.instance.TryGetTerrainOp(PathPiece, out TerrainOp op))
			{
				Object.Instantiate(op.gameObject, spot, Quaternion.identity);
			}
			foreach (ZNetView view in clutter)
			{
				if (view != null && view.IsValid())
				{
					view.ClaimOwnership();
					ZNetScene.instance.Destroy(view.gameObject);
				}
			}
		}

		/// <summary>
		/// Notes that a house of a village has its path - on this client when it runs the village, and otherwise on the
		/// client that does
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="house">The house</param>
		public static void Pathed(Village village, House house)
		{
			if (village.Zdo.IsOwner())
			{
				NotePathed(village.Zdo, house.Center);
			}
			else if (ZRoutedRpc.instance != null)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, PathedRpc, village.Farm, house.Center);
			}
		}

		/// <summary>
		/// Notes a house's path for a viking on another client, on the client that runs its village
		/// </summary>
		/// <param name="sender">The viking's client</param>
		/// <param name="farm">Where its village's farm stands</param>
		/// <param name="center">The house's middle</param>
		private static void OnPathed(long sender, Vector3 farm, Vector3 center)
		{
			if (ZNetScene.instance == null)
			{
				return;
			}
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZDO zdo = instance.Key;
				if ((zdo.GetPrefab() == GeneratorHash || VillageFounding.IsAnchor(zdo.GetPrefab())) && zdo.IsOwner() && Utils.DistanceXZ(zdo.GetPosition(), farm) <= FarmReach)
				{
					NotePathed(zdo, center);
					return;
				}
			}
		}

		/// <summary>
		/// Adds a house to those noted as having their path in a farm's generator, once
		/// </summary>
		/// <param name="zdo">The generator, owned by this client</param>
		/// <param name="center">The house's middle</param>
		private static void NotePathed(ZDO zdo, Vector3 center)
		{
			List<House> pathed = PathedHouses(zdo);
			House house = new House { Center = center };
			if (HasHouse(pathed, house))
			{
				return;
			}
			pathed.Add(house);
			zdo.Set(PathedHousesKey, Village.WriteHouses(pathed));
		}

		/// <summary>
		/// Reads the houses noted as having their path in a farm's generator
		/// </summary>
		/// <param name="zdo">The generator</param>
		/// <returns>The houses</returns>
		private static List<House> PathedHouses(ZDO zdo)
		{
			byte[] bytes = zdo.GetByteArray(PathedHousesKey);
			return((bytes != null) ? ReadHouses(bytes) : new List<House>());
		}

		/// <summary>
		/// Checks whether a list of houses has a house, by its middle
		/// </summary>
		/// <param name="houses">The houses</param>
		/// <param name="house">The house</param>
		/// <returns>True when it has</returns>
		private static bool HasHouse(List<House> houses, House house)
		{
			return(houses.Exists((House other) => Utils.DistanceXZ(other.Center, house.Center) <= SameHouseReach));
		}
	}
}
