using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Flattens the ground around a village and lays dirt paths between its houses - an abandoned farm's farmhouses and
	/// the abandoned houses near it that its living vikings have taken in - with a fire in its middle
	/// </summary>
	/// <remarks>
	/// A farm notes its farmhouses in its generator's ZDO as it's generated. The paths can only be laid once everything
	/// around the village has loaded, since they wind around what stands there, so whoever owns the generator lays them
	/// the first time the whole village is loaded around them, and marks the generator so they're only laid once. The
	/// houses are joined by the shortest set of paths that connects them all, and each path is found on a grid that
	/// steers around buildings, fences, large trees, bushes and large rocks, prefers flat ground and keeps out of the
	/// water and the Ashlands' lava, which it treats as water - or, when there's no way around them, goes through as few
	/// of them and as little water as it can. It's
	/// painted with the hoe's path, and the branches, stones, small trees and small rocks on it are cleared. A farm with
	/// no open ground for its bonfire stays abandoned - nothing is built or painted, and no living vikings come to it.
	/// An Ashlands or Deep North village, and the village its portal leads to, get the portal as their paths are laid,
	/// with a path of its own (see VillagePortals)
	/// </remarks>
	internal static partial class VillagePaths
	{
		/// <summary>
		/// On a farm's generator, its farmhouses, and that its paths have been laid
		/// </summary>
		public static readonly int HousesKey = "kriona.VikingVillages.villageHouses".GetStableHashCode();
		private static readonly int PathsLaidKey = "kriona.VikingVillages.villageJoined".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, that it had no open ground for its bonfire, so it stays an abandoned farm rather than a
		/// village
		/// </summary>
		public static readonly int AbandonedKey = "kriona.VikingVillages.villageAbandoned".GetStableHashCode();

		/// <summary>
		/// How far past the area a village's paths are laid across its living vikings are looked for, when it stays
		/// abandoned and they're taken away, in meters
		/// </summary>
		private const float AbandonReach = 50f;

		/// <summary>
		/// On a farm's generator, that its paths have their bridges over the water, so a village whose paths were laid
		/// without them gets them once
		/// </summary>
		private static readonly int BridgedKey = "kriona.VikingVillages.villageBridged".GetStableHashCode();

		/// <summary>
		/// On a bonfire or campfire, that it's a village's
		/// </summary>
		public static readonly int BonfireKey = "kriona.VikingVillages.villageBonfire".GetStableHashCode();

		/// <summary>
		/// The bonfire built in the middle of a village, how much open ground it needs around its middle, and how far
		/// from the middle to look for that, in meters
		/// </summary>
		private const string BonfirePrefab = "bonfire";
		private const float BonfireRadius = 2.5f;
		private const float BonfireSearch = 30f;

		/// <summary>
		/// The most the ground under a fire can rise and fall as it is, and before it's flattened, in meters
		/// </summary>
		private const float FireFlatness = 0.75f;
		private const float FlattenLimit = 2f;

		/// <summary>
		/// How far a fire is set into the ground so it touches it, in meters
		/// </summary>
		private const float FireSink = 0.1f;

		/// <summary>
		/// The terrain operation that flattens the ground for a fire, built from the hoe's path - it levels the ground
		/// out to LevelRadius, smooths it into the slope around out to LevelSmoothRadius and paints it dirt, in meters
		/// </summary>
		private const string LevelOpName = "kriona_VikingVillages_villageLevel";
		private const float LevelRadius = 3.5f;
		private const float LevelSmoothRadius = 5f;
		private const float LevelSmoothPower = 3f;

		/// <summary>
		/// The terrain operation that paints the ground under a house's floor pieces the way the hoe's path does, so no
		/// grass grows through them, without moving the ground under them - its radius, in meters, and the part of the
		/// name of every floor piece
		/// </summary>
		private const string FloorOpName = "kriona_VikingVillages_villageFloor";
		private const float FloorPaintRadius = 1.5f;
		private const string FloorName = "floor";

		/// <summary>
		/// The terrain operation that clears the Deep North's deep snow off the ground, setting its paint back to the
		/// ground's own - its radius, how far apart the spots it's put down at are, and how far past a farm's fence and
		/// its houses' walls it clears, in meters
		/// </summary>
		private const string SnowOpName = "kriona_VikingVillages_villageSnow";
		private const float SnowPaintRadius = 4f;
		private const float SnowSpacing = 5f;
		private const float SnowFenceMargin = 8f;
		private const float SnowHouseMargin = 8f;

		private static GameObject levelOp;
		private static GameObject floorOp;
		private static GameObject snowOp;
		private static GameObject opHolder;

		/// <summary>
		/// How far from a farm's planned spot its generator can stand, in meters
		/// </summary>
		private const float GeneratorReach = 16f;

		internal const string GeneratorPrefab = "DG_MeadowsFarm";
		public static readonly int GeneratorHash = GeneratorPrefab.GetStableHashCode();

		/// <summary>
		/// The hoe's path, painted along each path
		/// </summary>
		private const string PathPiece = "path_v2";

		/// <summary>
		/// Seconds between looks for a village to lay paths in, and between looks while one is waiting for what's around
		/// it to load, and the fewest milliseconds a try at laying them has to take to be written to the log
		/// </summary>
		private const float CheckInterval = 5f;
		private const float WaitingInterval = 1f;
		private const double TimingLogMin = 10.0;

		/// <summary>
		/// How far past a farm's fence the trees, bushes, rocks and logs are cleared, in meters
		/// </summary>
		private const float FarmClearMargin = 6f;

		/// <summary>
		/// The grid's cell size, how far past the houses it reaches, and how far apart the zones checked for being loaded
		/// are, in meters
		/// </summary>
		private const float CellSize = 1f;
		private const float Margin = 24f;
		private const float ZoneCheckSpacing = 32f;

		/// <summary>
		/// How far a path keeps from what it steers around, how far from it its clutter is cleared, and how far apart
		/// its painted spots are, in meters
		/// </summary>
		private const float PathClearance = 1f;
		private const float ClearRadius = 1.5f;
		private const float PaintSpacing = 1.5f;

		/// <summary>
		/// How the ground around a village is flattened - fully out to the core distance from the farm's middle and
		/// from each house, fading out by the fade distance and before the edge of the ground measured, in meters
		/// </summary>
		private const float FarmFlatCore = 30f;
		private const float FarmFlatFade = 44f;
		private const float HouseFlatCore = 10f;
		private const float HouseFlatFade = 20f;
		private const float EdgeFade = 8f;

		/// <summary>
		/// The ground is moved toward a blurred version of itself, taking out its bumps, and that is pulled part of the
		/// way toward the village's average height, taking out some of its slope - FlatStrength of the way there, by no
		/// more than MaxFlatten, with the blur reaching FlatBlur meters
		/// </summary>
		private const float FlatBlur = 10f;
		private const float FlatMeanPull = 0.4f;
		private const float FlatStrength = 0.85f;
		private const float MaxFlatten = 3f;

		/// <summary>
		/// How far above the water flattening leaves the ground it lowers, and raises a swamp village's ground to, so it
		/// stays dry, in meters, and how far short of that ground raised to it can come for rounding and still be dry
		/// </summary>
		private const float WaterClearance = 0.5f;
		private const float RaisedSlack = 0.01f;

		/// <summary>
		/// The ground within StructureKeep of a building piece is left as it is, so nothing built is left floating or
		/// buried, blending into the flattened ground over StructureFade, in meters
		/// </summary>
		private const float StructureKeep = 1.5f;
		private const float StructureFade = 5f;

		/// <summary>
		/// The least a spot moves for the ground there to be changed and what stands on it moved with it, in meters
		/// </summary>
		private const float MinFlatten = 0.02f;
		private const float MinMove = 0.05f;

		/// <summary>
		/// How far past a house's walls its doors can stand, and how far out from a door a path can start, in meters
		/// </summary>
		private const float DoorReach = 3f;
		private const float DoorstepReach = 8f;

		/// <summary>
		/// The extra cost of a step that climbs or drops a meter, of a step through clutter that will be cleared, and of
		/// a step closer to something steered around than PathClearance, which a path only takes to squeeze through a
		/// gap like a gate
		/// </summary>
		private const float SlopeCost = 4f;
		private const float ClutterCost = 2f;
		private const float ClearanceCost = 4f;

		/// <summary>
		/// The extra cost of a step through something a path steers around and of a step through water, which a path
		/// only takes when there's no way around, so two houses are always joined
		/// </summary>
		private const float ForcedCost = 30f;
		private const float WetCost = 100f;

		/// <summary>
		/// How far above the water level the ground still counts as water, in meters
		/// </summary>
		private const float WetHeight = 0.5f;

		/// <summary>
		/// The extra cost of a step over water a bridge can cross, so a path crosses a river where it's narrow and goes
		/// around a pond it can walk around
		/// </summary>
		private const float BridgeCost = 6f;

		/// <summary>
		/// The widest water a wood bridge crosses, from bank to bank, in meters - a wood floor holds up 4 more floors out
		/// from one resting on the ground, so a bridge resting on both banks spans a little more than this. A bridge of
		/// another piece spans what its floors hold up (see BridgeSpan)
		/// </summary>
		internal const float BridgeSpanMax = 14f;

		/// <summary>
		/// How much of the reach of a bridge's floors rests on its banks rather than over the water, in meters - so a
		/// wood bridge spans BridgeSpanMax
		/// </summary>
		private const float BankRest = 3.1f;

		/// <summary>
		/// The widest water a bridge of each piece crosses, by the piece's name, worked out the first time (see
		/// BridgeSpanOf)
		/// </summary>
		private static readonly Dictionary<string, float> bridgeSpans = new Dictionary<string, float>();

		/// <summary>
		/// How many times a path is looked for again around water too wide to bridge
		/// </summary>
		private const int BridgeTries = 4;

		/// <summary>
		/// The piece bridges are built of - a grausten floor in a village of the Ashlands or the Deep North, and a wood
		/// floor anywhere else - how far apart its floors are laid, so each overlaps the next a little, how far above the
		/// bank a bridge's ends are, in meters, and the steepest a bridge can be, in degrees
		/// </summary>
		private const string BridgePiece = "wood_floor";
		private const string StoneBridgePiece = "Piece_grausten_floor_2x2";
		private const float BridgeStep = 1.9f;
		private const float BridgeLift = 0.05f;
		private const float BridgeSlopeMax = 25f;

		/// <summary>
		/// The shortest a bridge can be and still be built, from end to end, in meters
		/// </summary>
		private const float BridgeSpanMin = 0.1f;

		/// <summary>
		/// The largest an object can be across and still be cleared off a path as a small tree or rock, in meters
		/// </summary>
		private const float SmallSize = 2.5f;

		/// <summary>
		/// The largest an object can be across and still be steered around, in meters - anything bigger holds other
		/// objects, which are measured on their own
		/// </summary>
		private const float MaxSize = 40f;

		/// <summary>
		/// The pickables cleared off a path
		/// </summary>
		private static readonly HashSet<string> ClutterPickables = new HashSet<string> { "Pickable_Branch", "Pickable_Stone", "Pickable_Flint" };

		/// <summary>
		/// Parts of names that mark a bush, which a path steers around
		/// </summary>
		private static readonly string[] BushNames = new[] { "bush", "shrub" };

		private static float nextCheck;

		/// <summary>
		/// When each farm generated on this client was, by Time.time, by where it stands (see SinceGenerated)
		/// </summary>
		private static readonly Dictionary<Vector3, float> generatedAt = new Dictionary<Vector3, float>();

		/// <summary>
		/// What a path does about an object in its way
		/// </summary>
		internal enum Kind
		{
			Ignore,
			Avoid,
			Clear
		}

		/// <summary>
		/// An object near a village's paths, with how far it reaches from its middle
		/// </summary>
		internal struct Blocker
		{
			public ZNetView View;
			public Vector3 Center;
			public float Radius;
			public Kind Kind;

			/// <summary>
			/// Its solid colliders, which give its true shape, or empty when it's measured as a circle - by what's drawn
			/// of it, or an oak by its trunk
			/// </summary>
			public List<Collider> Colliders;

			/// <summary>
			/// Whether it's a building piece or an oak, which the ground around isn't flattened under
			/// </summary>
			public bool Structure;
		}

		/// <summary>
		/// A bridge a path crosses water on, from the ground on one bank to the ground on the other
		/// </summary>
		internal struct Bridge
		{
			public Vector3 Start;
			public Vector3 End;
		}

		/// <summary>
		/// A house a path leads to, with how far its walls reach from its middle
		/// </summary>
		public struct House
		{
			public Vector3 Center;
			public float HalfSize;

			/// <summary>
			/// Where the house's doors stand, or null before they're found
			/// </summary>
			public List<Vector3> Doors;
		}

		/// <summary>
		/// Checks whether a village's paths have been laid and its bonfire built, so it's set up and its vikings come
		/// </summary>
		/// <param name="generator">The farm's generator's ZDO</param>
		/// <returns>True once they have</returns>
		public static bool Laid(ZDO generator)
		{
			return(generator.GetBool(PathsLaidKey));
		}

		/// <summary>
		/// Sets up a village a player started with no farmhouses, and with its paths noted as laid and bridged, since
		/// none are laid for it (see VillageFounding)
		/// </summary>
		/// <param name="anchor">The village's anchor's ZDO, owned by this client</param>
		public static void RecordFounded(ZDO anchor)
		{
			anchor.Set(HousesKey, Village.WriteHouses(new List<House>()));
			anchor.Set(PathsLaidKey, true);
			anchor.Set(BridgedKey, true);
		}

		/// <summary>
		/// Notes a farm's farmhouses in its generator's ZDO as it's generated, for its paths to be laid later
		/// </summary>
		/// <param name="farm">Where the farm stands</param>
		/// <param name="houses">Its farmhouses</param>
		public static void RecordVillage(Vector3 farm, List<House> houses)
		{
			DungeonGenerator generator = FindGenerator(farm);
			ZNetView view = ((generator != null) ? generator.GetComponent<ZNetView>() : null);
			if (view == null || view.GetZDO() == null)
			{
				Plugin.Log.LogWarning("Couldn't find the generator of the farm at " + farm.ToString("F0") + " to note its farmhouses for paths");
				return;
			}
			view.GetZDO().Set(HousesKey, Village.WriteHouses(houses));
			// Its paths are looked for on the next update, rather than waiting out CheckInterval
			generatedAt[farm] = Time.time;
			nextCheck = 0f;
		}

		/// <summary>
		/// Finds how long ago a farm was generated on this client, for timing how long its village takes to be set up
		/// </summary>
		/// <param name="farm">Where the farm stands</param>
		/// <returns>The seconds since, or -1 when it wasn't generated here in this session</returns>
		public static float SinceGenerated(Vector3 farm)
		{
			foreach (KeyValuePair<Vector3, float> entry in generatedAt)
			{
				if (Utils.DistanceXZ(entry.Key, farm) <= GeneratorReach)
				{
					return(Time.time - entry.Value);
				}
			}
			return(-1f);
		}

		/// <summary>
		/// Finds the farm's dungeon generator nearest to a point, among the objects just placed
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The generator, or null</returns>
		private static DungeonGenerator FindGenerator(Vector3 point)
		{
			DungeonGenerator nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (DungeonGenerator generator in Object.FindObjectsByType<DungeonGenerator>(FindObjectsSortMode.None))
			{
				float distance = Utils.DistanceXZ(generator.transform.position, point);
				if (distance < nearestDistance && (generator.m_themes & Room.Theme.MeadowsFarm) != 0)
				{
					nearest = generator;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Looks for a loaded village owned by this client that still needs its paths, every CheckInterval seconds, and
		/// lays them once everything around it has loaded - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (Time.time < nextCheck || ZNetScene.instance == null || ZoneSystem.instance == null || !ServerCheck.Active)
			{
				return;
			}
			List<ZNetView> waiting = new List<ZNetView>();
			List<ZNetView> unbridged = new List<ZNetView>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZDO zdo = instance.Key;
				if (zdo.GetPrefab() != GeneratorHash || instance.Value == null || !instance.Value.IsOwner() || zdo.GetByteArray(HousesKey) == null || zdo.GetBool(AbandonedKey))
				{
					continue;
				}
				if (!zdo.GetBool(PathsLaidKey))
				{
					waiting.Add(instance.Value);
				}
				else if (!zdo.GetBool(BridgedKey))
				{
					unbridged.Add(instance.Value);
				}
			}
			// A farm waiting on its paths or bridges is looked at again every WaitingInterval, so they follow as soon as
			// what's around it has loaded
			nextCheck = Time.time + ((waiting.Count + unbridged.Count > 0) ? WaitingInterval : CheckInterval);
			foreach (ZNetView view in waiting)
			{
				Vector3 farm = view.transform.position;
				System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
				TryLay(view);
				watch.Stop();
				bool laid = (view.GetZDO() != null && view.GetZDO().GetBool(PathsLaidKey));
				if (watch.Elapsed.TotalMilliseconds >= TimingLogMin || laid)
				{
					Plugin.Log.LogInfo("Timing: " + (laid ? "laying" : "trying to lay") + " the paths of the village at " + farm.ToString("F0") + " took " + watch.Elapsed.TotalMilliseconds.ToString("F0") + "ms" + ((laid && SinceGenerated(farm) >= 0f) ? ", " + SinceGenerated(farm).ToString("F1") + "s after its farm was generated" : ""));
				}
				if (laid)
				{
					// Setting the village up follows on the next update
					VillageRespawn.CheckSoon();
				}
			}
			foreach (ZNetView view in unbridged)
			{
				System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
				TryBridge(view);
				watch.Stop();
				if (watch.Elapsed.TotalMilliseconds >= TimingLogMin)
				{
					Plugin.Log.LogInfo("Timing: bridging the paths of the village at " + view.transform.position.ToString("F0") + " took " + watch.Elapsed.TotalMilliseconds.ToString("F0") + "ms");
				}
			}
		}

		/// <summary>
		/// Lays a village's paths when everything around it has loaded
		/// </summary>
		/// <param name="view">The farm's generator</param>
		private static void TryLay(ZNetView view)
		{
			ZDO zdo = view.GetZDO();
			Vector3 farm = view.transform.position;
			List<House> houses = ReadHouses(zdo.GetByteArray(HousesKey));
			Vector3 middle = Middle(houses);
			if (!VillageLocations.FindOutposts(zdo, farm, houses))
			{
				return;
			}
			Village.RecordHouses(zdo, houses);
			if (houses.Count == 0)
			{
				zdo.Set(PathsLaidKey, true);
				zdo.Set(BridgedKey, true);
				return;
			}
			if (!VillagePortals.Prepare(zdo, farm, out VillagePortals.Plan portal))
			{
				return;
			}
			if (!VillageLocations.FindNearbyFarms(zdo, farm, out Vector3 planned, out List<Vector3> neighbors))
			{
				return;
			}
			Vector3 min = houses[0].Center;
			Vector3 max = houses[0].Center;
			foreach (House house in houses)
			{
				min = Vector3.Min(min, house.Center);
				max = Vector3.Max(max, house.Center);
			}
			// Only the first of two villages by position joins them, so the path is only laid once
			List<House> neighborHouses = new List<House>();
			foreach (Vector3 neighbor in neighbors)
			{
				if (!ComesFirst(planned, neighbor))
				{
					continue;
				}
				if (!ZoneSystem.instance.IsZoneLoaded(neighbor))
				{
					return;
				}
				House nearest;
				if (FindNearestFarmhouse(neighbor, middle, out nearest))
				{
					neighborHouses.Add(nearest);
					min = Vector3.Min(min, nearest.Center);
					max = Vector3.Max(max, nearest.Center);
				}
			}
			min -= new Vector3(Margin, 0f, Margin);
			max += new Vector3(Margin, 0f, Margin);
			float clearRadius = FarmClearRadius(zdo);
			min = Vector3.Min(min, farm - new Vector3(clearRadius, 0f, clearRadius));
			max = Vector3.Max(max, farm + new Vector3(clearRadius, 0f, clearRadius));
			if (!AreaLoaded(min, max))
			{
				return;
			}
			int wilds = ClearWilds(farm, clearRadius);
			Grid grid = new Grid(min, max, farm);
			List<Blocker> blockers = FindBlockers(min, max);
			VillagePortals.LeaveOut(blockers, portal);
			grid.Flatten(farm, houses, blockers);
			// A village in the Swamp, or the Plains by the sea, can stand in the water, so its ground is raised out of it
			// before the paths are found - ground that's already dry is left as it is
			Heightmap.Biome biome = ((WorldGenerator.instance != null) ? WorldGenerator.instance.GetBiome(farm) : Heightmap.Biome.None);
			int raised = ((biome == Heightmap.Biome.Swamp || biome == Heightmap.Biome.Plains) ? grid.RaiseOutOfWater(farm, houses, blockers) : 0);
			if (raised > 0)
			{
				Plugin.Log.LogInfo("Raised " + raised + " cells of the ground of the village at " + farm.ToString("F0") + " in the " + biome + " out of the water");
			}
			grid.Mark(blockers);
			grid.MarkBridgeable();
			FindDoors(houses, blockers);
			FindDoors(neighborHouses, blockers);
			// The paths and the bonfire's spot are worked out before the ground is reshaped or anything is built or
			// painted, so a farm with no room for its bonfire gets none of it
			List<List<Vector3>> legs = new List<List<Vector3>>();
			List<Vector3> path = new List<Vector3>();
			List<Bridge> bridges = new List<Bridge>();
			List<Vector3> linked = new List<Vector3>();
			int laid = 0;
			foreach (KeyValuePair<int, int> edge in SpanningTree(houses))
			{
				List<Vector3> leg = grid.FindPath(houses[edge.Key], houses[edge.Value]);
				if (leg == null)
				{
					Plugin.Log.LogWarning("Couldn't find a path between the houses at " + houses[edge.Key].Center.ToString("F0") + " and " + houses[edge.Value].Center.ToString("F0"));
					continue;
				}
				bridges.AddRange(grid.FindBridges(leg));
				legs.Add(leg);
				path.AddRange(leg);
				laid++;
			}
			foreach (House neighbor in neighborHouses)
			{
				House own = houses[0];
				foreach (House house in houses)
				{
					if (Utils.DistanceXZ(house.Center, neighbor.Center) < Utils.DistanceXZ(own.Center, neighbor.Center))
					{
						own = house;
					}
				}
				List<Vector3> link = grid.FindPath(own, neighbor);
				if (link == null)
				{
					Plugin.Log.LogWarning("Couldn't find a path from the village at " + farm.ToString("F0") + " to the one at " + neighbor.Center.ToString("F0"));
					continue;
				}
				bridges.AddRange(grid.FindBridges(link));
				legs.Add(link);
				path.AddRange(link);
				linked.Add(neighbor.Center);
			}
			grid.MarkPath(path);
			bool hasFire = FindBonfire(farm, out Vector3 standingFire);
			Vector3 fireSpot = farm;
			bool flatten = false;
			if (!hasFire && !FindFireSpot(grid, farm, out fireSpot, out flatten))
			{
				Abandon(zdo, farm, min, max);
				return;
			}
			VillagePortals.Site portalSite = null;
			if (portal != null)
			{
				portalSite = VillagePortals.FindSite(grid, farm, (hasFire ? (Vector3?)null : fireSpot), path, houses);
				if (portalSite != null && portalSite.Leg != null)
				{
					bridges.AddRange(grid.FindBridges(portalSite.Leg));
					legs.Add(portalSite.Leg);
					path.AddRange(portalSite.Leg);
				}
			}
			// A village of the Mistlands lights its ground with wisps, found once the portal's path is in
			VillageWisps.Plan wisps = null;
			if (VillageWisps.GetsWisps(biome))
			{
				wisps = VillageWisps.Find(grid, (hasFire ? standingFire : fireSpot), farm, zdo.GetFloat(VillageFarmhouses.FenceKey, VillageFarmhouses.FenceRadiusMax), legs);
			}
			Reshape(grid, min, max);
			// Before the paths, so their paint goes on over the cleared ground
			int snow = ClearSnow(zdo, farm, houses, min, max);
			foreach (List<Vector3> leg in legs)
			{
				Paint(leg);
			}
			foreach (Vector3 neighbor in linked)
			{
				Plugin.Log.LogInfo("Laid a path from the village at " + farm.ToString("F0") + " to the farmhouse at " + neighbor.ToString("F0"));
			}
			int built = BuildBridges(farm, bridges, BridgePieceAt(farm));
			int floors = PaintFloors(houses, blockers);
			int cleared = Clear(blockers, path);
			if (portal != null)
			{
				VillagePortals.Build(portal, portalSite, zdo, farm);
			}
			if (!hasFire)
			{
				PlaceBonfire(fireSpot, flatten);
			}
			if (wisps != null)
			{
				VillageWisps.Build(wisps, farm);
			}
			zdo.Set(PathsLaidKey, true);
			zdo.Set(BridgedKey, true);
			// Its pieces are put right on the ground the paths reshaped, once it's in place (see FarmSupport)
			FarmSupport.NoteLaid(zdo);
			Plugin.Log.LogInfo("Laid " + laid + " paths between " + houses.Count + " houses of the village at " + farm.ToString("F0") + ", building " + built + " bridges over the water, clearing " + cleared + " things off them and painting under " + floors + " floor pieces, after clearing " + wilds + " trees, bushes, rocks and logs around the farm" + ((snow > 0) ? " and the deep snow at " + snow + " spots" : ""));
		}

		/// <summary>
		/// Checks whether one village comes before another by position, west to east and then south to north, which
		/// decides which of two neighbors joins them
		/// </summary>
		/// <param name="farm">One village's farm</param>
		/// <param name="other">The other's</param>
		/// <returns>True when the first comes first</returns>
		internal static bool ComesFirst(Vector3 farm, Vector3 other)
		{
			if (Mathf.Abs(farm.x - other.x) > 0.5f)
			{
				return(farm.x < other.x);
			}
			return(farm.z < other.z);
		}

		/// <summary>
		/// Finds the farmhouse of a neighboring village nearest a point, from what its generator noted
		/// </summary>
		/// <param name="farm">Where the neighbor's farm stands</param>
		/// <param name="toward">The point</param>
		/// <param name="nearest">The farmhouse</param>
		/// <returns>False when the neighbor's generator isn't loaded or noted no farmhouses - a farm without vikings - or
		/// it stays abandoned</returns>
		private static bool FindNearestFarmhouse(Vector3 farm, Vector3 toward, out House nearest)
		{
			nearest = new House();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZDO zdo = instance.Key;
				if (zdo.GetPrefab() != GeneratorHash || Utils.DistanceXZ(zdo.GetPosition(), farm) > GeneratorReach)
				{
					continue;
				}
				byte[] bytes = zdo.GetByteArray(HousesKey);
				if (bytes == null || zdo.GetBool(AbandonedKey))
				{
					return(false);
				}
				List<House> houses = ReadHouses(bytes);
				if (houses.Count == 0)
				{
					return(false);
				}
				nearest = houses[0];
				foreach (House house in houses)
				{
					if (Utils.DistanceXZ(house.Center, toward) < Utils.DistanceXZ(nearest.Center, toward))
					{
						nearest = house;
					}
				}
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Leaves a farm with no open ground for its bonfire as an abandoned farm - marking its generator so it's no
		/// village, its paths are never laid and no other village's path leads to it, and taking away any living vikings
		/// that came to it before it was found to have no room
		/// </summary>
		/// <param name="generator">The generator's ZDO, owned by this client</param>
		/// <param name="farm">Where the generator stands</param>
		/// <param name="min">The lowest corner of the area its paths would have been laid across</param>
		/// <param name="max">The highest corner of it</param>
		private static void Abandon(ZDO generator, Vector3 farm, Vector3 min, Vector3 max)
		{
			generator.Set(AbandonedKey, true);
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear((min + max) / 2f, Utils.DistanceXZ(min, max) / 2f + AbandonReach, zdos);
			int removed = 0;
			foreach (ZDO zdo in zdos)
			{
				Vector3 village = zdo.GetVec3(Village.VillageKey, Vector3.zero);
				if (zdo.GetPrefab() != VikingCore.LivingPrefabHash || village == Vector3.zero || Utils.DistanceXZ(village, farm) > GeneratorReach)
				{
					continue;
				}
				ZNetView view = ZNetScene.instance.FindInstance(zdo);
				if (view != null)
				{
					view.ClaimOwnership();
					ZNetScene.instance.Destroy(view.gameObject);
				}
				else
				{
					zdo.SetOwner(ZDOMan.GetSessionID());
					ZDOMan.instance.DestroyZDO(zdo);
				}
				removed++;
			}
			Plugin.Log.LogWarning("Found no open ground for a bonfire near " + farm.ToString("F0") + ", so the farm stays abandoned - took away its " + removed + " living vikings");
		}

		/// <summary>
		/// Finds a village's bonfire already burning within BonfireSearch of a point
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="at">Set to where the bonfire stands, or the point when there's none</param>
		/// <returns>True when one does</returns>
		private static bool FindBonfire(Vector3 point, out Vector3 at)
		{
			at = point;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Key.GetBool(BonfireKey) && Utils.DistanceXZ(instance.Key.GetPosition(), point) <= BonfireSearch)
				{
					at = instance.Key.GetPosition();
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// How many bytes a list of noted houses' count takes, and each house - its middle and half size
		/// </summary>
		private const int HouseCountBytes = 4;
		private const int HouseBytes = 16;

		/// <summary>
		/// Reads the farmhouses noted in a farm's generator, as Village.WriteHouses writes them - none from nothing, and
		/// only the houses fully written from a list cut short
		/// </summary>
		/// <param name="bytes">The noted farmhouses, or null</param>
		/// <returns>The farmhouses</returns>
		public static List<House> ReadHouses(byte[] bytes)
		{
			List<House> houses = new List<House>();
			if (bytes == null || bytes.Length < HouseCountBytes)
			{
				return(houses);
			}
			ZPackage pkg = new ZPackage(bytes);
			int count = Mathf.Min(pkg.ReadInt(), (bytes.Length - HouseCountBytes) / HouseBytes);
			for (int i = 0; i < count; i++)
			{
				House house = new House();
				house.Center = pkg.ReadVector3();
				house.HalfSize = pkg.ReadSingle();
				houses.Add(house);
			}
			return(houses);
		}
	}
}
