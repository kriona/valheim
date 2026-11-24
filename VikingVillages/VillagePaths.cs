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
	internal static class VillagePaths
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
		/// Clears the Deep North's deep snow off the ground within SnowPaintRadius of a spot, the way a village's ground
		/// is cleared as it's set up
		/// </summary>
		/// <param name="spot">The spot, on the ground</param>
		/// <returns>False when the terrain operation isn't set up</returns>
		public static bool ClearSnowAt(Vector3 spot)
		{
			if (snowOp == null)
			{
				return(false);
			}
			Object.Instantiate(snowOp, spot, Quaternion.identity);
			return(true);
		}

		/// <summary>
		/// How far from a spot clearing the ground's snow reaches (see ClearSnowAt), in meters
		/// </summary>
		public const float SnowReach = SnowPaintRadius;

		/// <summary>
		/// Clears the Deep North's deep snow off a village's ground - within SnowFenceMargin past its farm's fence and
		/// SnowHouseMargin past its houses' walls, its outposts' included - putting the snow-clearing paint down every
		/// SnowSpacing meters where the ground is the Deep North's
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="farm">Where the farm stands</param>
		/// <param name="houses">The village's houses</param>
		/// <param name="min">The lowest corner of the ground around the village</param>
		/// <param name="max">The highest corner of it</param>
		/// <returns>How many spots it was put down at</returns>
		private static int ClearSnow(ZDO generator, Vector3 farm, List<House> houses, Vector3 min, Vector3 max)
		{
			if (snowOp == null)
			{
				return(0);
			}
			float fence = generator.GetFloat(VillageFarmhouses.FenceKey, VillageFarmhouses.FenceRadiusMax) + SnowFenceMargin;
			int spots = 0;
			for (float x = min.x; x <= max.x; x += SnowSpacing)
			{
				for (float z = min.z; z <= max.z; z += SnowSpacing)
				{
					Vector3 point = new Vector3(x, 0f, z);
					if (!WorldGenerator.IsDeepnorth(x, z))
					{
						continue;
					}
					if (Utils.DistanceXZ(point, farm) > fence && !houses.Exists((House house) => Village.InHouse(house, point, SnowHouseMargin)))
					{
						continue;
					}
					point.y = ZoneSystem.instance.GetGroundHeight(point);
					Object.Instantiate(snowOp, point, Quaternion.identity);
					spots++;
				}
			}
			return(spots);
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
		/// Reshapes the ground around a village to the grid's flattened heights, the way a level operation does, and
		/// moves what stands on the ground - trees, rocks, bushes and the rest - up or down with it
		/// </summary>
		/// <remarks>
		/// Each zone's terrain is changed through its own terrain compiler, which this client takes over so it can save
		/// the change for everyone. Building pieces stand where the ground isn't changed, so they're left alone
		/// </remarks>
		/// <param name="grid">The village's ground, flattened</param>
		/// <param name="min">The area's lowest corner</param>
		/// <param name="max">The area's highest corner</param>
		private static void Reshape(Grid grid, Vector3 min, Vector3 max)
		{
			Vector3 center = (min + max) / 2f;
			float radius = Utils.DistanceXZ(min, max) / 2f;
			List<Heightmap> maps = new List<Heightmap>();
			Heightmap.FindHeightmap(center, radius, maps);
			foreach (Heightmap map in maps)
			{
				TerrainComp terrain = map.GetAndCreateTerrainCompiler();
				if (terrain == null || !terrain.m_initialized || terrain.m_nview == null || !terrain.m_nview.IsValid())
				{
					continue;
				}
				terrain.m_nview.ClaimOwnership();
				int size = terrain.m_width + 1;
				int half = map.m_width / 2;
				bool changed = false;
				for (int i = 0; i < size; i++)
				{
					for (int j = 0; j < size; j++)
					{
						Vector3 vertex = map.transform.position + new Vector3((j - half) * map.m_scale, 0f, (i - half) * map.m_scale);
						if (!grid.TryGetChange(vertex, out float change) || Mathf.Abs(change) < MinFlatten)
						{
							continue;
						}
						// The same as a level operation - the smoothing is folded into the level, which takes the change
						int index = i * size + j;
						terrain.m_levelDelta[index] = Mathf.Clamp(terrain.m_levelDelta[index] + change + terrain.m_smoothDelta[index], -8f, 8f);
						terrain.m_smoothDelta[index] = 0f;
						terrain.m_modifiedHeight[index] = true;
						changed = true;
					}
				}
				if (changed)
				{
					terrain.Save();
					map.Poke(1, false);
				}
			}
			if (ClutterSystem.instance != null)
			{
				ClutterSystem.instance.ResetGrass(center, radius);
			}
			MoveObjects(grid, min, max);
		}

		/// <summary>
		/// Moves the objects standing on the ground around a village up or down with the ground under them, leaving
		/// building pieces, which stand where it isn't changed, and creatures and dropped items, which find the ground
		/// on their own
		/// </summary>
		/// <param name="grid">The village's ground, flattened</param>
		/// <param name="min">The area's lowest corner</param>
		/// <param name="max">The area's highest corner</param>
		private static void MoveObjects(Grid grid, Vector3 min, Vector3 max)
		{
			List<ZNetView> moving = new List<ZNetView>();
			foreach (ZNetView view in ZNetScene.instance.m_instances.Values)
			{
				if (view == null)
				{
					continue;
				}
				Vector3 position = view.transform.position;
				if (position.x < min.x || position.x > max.x || position.z < min.z || position.z > max.z)
				{
					continue;
				}
				GameObject target = view.gameObject;
				if (target.GetComponent<WearNTear>() != null || target.GetComponent<Character>() != null || target.GetComponent<ItemDrop>() != null || target.GetComponent<Ragdoll>() != null || target.GetComponent<TerrainComp>() != null || target.GetComponent<DungeonGenerator>() != null || target.GetComponent<LocationProxy>() != null)
				{
					continue;
				}
				moving.Add(view);
			}
			List<ZNetView> moved = new List<ZNetView>();
			foreach (ZNetView view in moving)
			{
				Vector3 position = view.transform.position;
				if (!grid.TryGetChange(position, out float change) || Mathf.Abs(change) < MinMove)
				{
					continue;
				}
				view.ClaimOwnership();
				position.y += change;
				view.transform.position = position;
				view.GetZDO().SetPosition(position);
				moved.Add(view);
			}
			// Objects that stand still don't follow their ZDO's position, so the other clients with them loaded are told
			// where they went
			if (moved.Count == 0 || ZRoutedRpc.instance == null)
			{
				return;
			}
			ZPackage pkg = new ZPackage();
			pkg.Write(moved.Count);
			foreach (ZNetView view in moved)
			{
				pkg.Write(view.GetZDO().m_uid);
				pkg.Write(view.transform.position);
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, MovedRpc, pkg);
		}

		/// <summary>
		/// Moves the objects another client moved with the ground around a village to where they went, on the clients
		/// that have them loaded
		/// </summary>
		/// <param name="sender">The client that moved them</param>
		/// <param name="pkg">Each object's ZDO and where it went</param>
		private static void OnMoved(long sender, ZPackage pkg)
		{
			if (ZNetScene.instance == null)
			{
				return;
			}
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				ZDOID id = pkg.ReadZDOID();
				Vector3 position = pkg.ReadVector3();
				GameObject found = ZNetScene.instance.FindInstance(id);
				ZNetView view = ((found != null) ? found.GetComponent<ZNetView>() : null);
				if (view != null && view.IsValid() && !view.IsOwner())
				{
					view.transform.position = position;
				}
			}
		}

		/// <summary>
		/// Finds each house's doors among the objects around the village - the doors within DoorReach of its walls
		/// </summary>
		/// <param name="houses">The houses, given their doors</param>
		/// <param name="blockers">The objects around the village</param>
		private static void FindDoors(List<House> houses, List<Blocker> blockers)
		{
			for (int i = 0; i < houses.Count; i++)
			{
				House house = houses[i];
				house.Doors = new List<Vector3>();
				foreach (Blocker blocker in blockers)
				{
					if (blocker.View != null && blocker.View.GetComponent<Door>() != null && Utils.DistanceXZ(blocker.View.transform.position, house.Center) <= house.HalfSize + DoorReach)
					{
						house.Doors.Add(blocker.View.transform.position);
					}
				}
				houses[i] = house;
			}
		}

		/// <summary>
		/// Finds the middle of some houses
		/// </summary>
		/// <param name="houses">The houses</param>
		/// <returns>The average of their middles</returns>
		private static Vector3 Middle(List<House> houses)
		{
			Vector3 sum = Vector3.zero;
			foreach (House house in houses)
			{
				sum += house.Center;
			}
			return((houses.Count > 0) ? sum / houses.Count : sum);
		}

		/// <summary>
		/// Finds the open ground for a bonfire nearest the middle of a farm, which its layout keeps clear of rooms, clear
		/// of its buildings and paths - flat ground if there's any, or else gently sloping ground to flatten for it
		/// </summary>
		/// <param name="grid">The village's ground, with its paths marked</param>
		/// <param name="middle">The middle of the farm</param>
		/// <param name="spot">Where the bonfire goes - at the height the ground is flattened to, when it is</param>
		/// <param name="flatten">Whether the ground there is flattened first</param>
		/// <returns>False when there's no ground for it</returns>
		private static bool FindFireSpot(Grid grid, Vector3 middle, out Vector3 spot, out bool flatten)
		{
			return(FindLevelSpot(grid, middle, BonfireRadius, 0f, BonfireSearch, out spot, out flatten));
		}

		/// <summary>
		/// Finds the open ground nearest a point with room all around it, clear of buildings and paths, the way a fire's
		/// is found - flat ground if there's any, or else gently sloping ground to flatten for it (see Flatten)
		/// </summary>
		/// <param name="grid">The village's ground, with its paths marked</param>
		/// <param name="middle">The point</param>
		/// <param name="radius">How much room it needs around it, no more than FlattenReach, in meters</param>
		/// <param name="least">How far from the point it stands at the least, in meters</param>
		/// <param name="search">How far from the point to look, in meters</param>
		/// <param name="spot">The spot - at the height the ground is flattened to, when it is</param>
		/// <param name="flatten">Whether the ground there is flattened first</param>
		/// <returns>False when there's no ground for it</returns>
		internal static bool FindLevelSpot(Grid grid, Vector3 middle, float radius, float least, float search, out Vector3 spot, out bool flatten)
		{
			flatten = false;
			if (grid.FindOpenSpot(middle, radius, least, search, FireFlatness, out spot, out float level))
			{
				return(true);
			}
			if (levelOp == null || !grid.FindOpenSpot(middle, radius, least, search, FlattenLimit, out spot, out level))
			{
				return(false);
			}
			// Levels the ground halfway between its highest and lowest points, so it's cut and filled evenly
			spot.y = level;
			flatten = true;
			return(true);
		}

		/// <summary>
		/// How far from a spot flattening it levels the ground (see Flatten), in meters
		/// </summary>
		public const float FlattenReach = LevelRadius;

		/// <summary>
		/// Levels the ground within FlattenReach of a spot to the spot's height, smoothing it into the ground around, the
		/// way the ground under a village's fire is
		/// </summary>
		/// <param name="spot">The spot, at the height to level to</param>
		/// <returns>False when the terrain operation isn't set up</returns>
		public static bool Flatten(Vector3 spot)
		{
			if (levelOp == null)
			{
				return(false);
			}
			Object.Instantiate(levelOp, spot, Quaternion.identity);
			return(true);
		}

		/// <summary>
		/// Builds a village's bonfire at the spot found for it, flattening the ground there first when it needs it
		/// </summary>
		/// <param name="spot">Where it goes (see FindFireSpot)</param>
		/// <param name="flatten">Whether the ground there is flattened first</param>
		private static void PlaceBonfire(Vector3 spot, bool flatten)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(BonfirePrefab);
			if (prefab == null)
			{
				Plugin.Log.LogWarning("Couldn't find the " + BonfirePrefab + " prefab to build a village's fire at " + spot.ToString("F0"));
				return;
			}
			if (flatten)
			{
				Object.Instantiate(levelOp, spot, Quaternion.identity);
			}
			// A fire is a building piece, which breaks without support unless it touches the ground
			spot.y -= FireSink;
			GameObject instance = Object.Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			VillageRecords.MarkBuilt(instance);
			ZNetView view = instance.GetComponent<ZNetView>();
			if (view != null && view.GetZDO() != null)
			{
				view.GetZDO().Set(BonfireKey, true);
				// The vikings stack it with as much wood as it holds
				Fireplace fire = instance.GetComponent<Fireplace>();
				if (fire != null)
				{
					view.GetZDO().Set(ZDOVars.s_fuel, fire.m_maxFuel);
				}
			}
			Plugin.Log.LogInfo("Built a " + BonfirePrefab + " at " + spot.ToString("F1") + (flatten ? " on ground flattened for it" : ""));
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
		/// Adds the terrain operations that flatten the ground for a fire and paint the ground under a house to the
		/// game's terrain operations, building them the first time
		/// </summary>
		/// <remarks>
		/// Whoever owns a piece of ground applies the operations on it, finding each by its name, so the operations have
		/// to be known on every client
		/// </remarks>
		/// <param name="db">The ObjectDB, as it wakes</param>
		public static void RegisterLevelOp(ObjectDB db)
		{
			TerrainOp path = db.m_terrainOps.Find((TerrainOp op) => op != null && op.name == PathPiece);
			if (levelOp == null)
			{
				if (path == null)
				{
					return;
				}
				levelOp = CopyOp(path, LevelOpName);
				TerrainOp level = levelOp.GetComponent<TerrainOp>();
				TerrainOp.Settings settings = new TerrainOp.Settings();
				settings.m_level = true;
				settings.m_levelRadius = LevelRadius;
				settings.m_square = false;
				settings.m_smooth = true;
				settings.m_smoothRadius = LevelSmoothRadius;
				settings.m_smoothPower = LevelSmoothPower;
				settings.m_paintCleared = true;
				settings.m_paintType = TerrainModifier.PaintType.Dirt;
				settings.m_paintRadius = LevelRadius;
				settings.m_paintCurve = path.m_settings.m_paintCurve;
				level.m_settings = settings;
				level.m_spawnOnPlaced = null;
			}
			if (floorOp == null && path != null)
			{
				floorOp = CopyOp(path, FloorOpName);
				TerrainOp floor = floorOp.GetComponent<TerrainOp>();
				TerrainOp.Settings settings = new TerrainOp.Settings();
				settings.m_level = false;
				settings.m_raise = false;
				settings.m_smooth = false;
				settings.m_paintCleared = true;
				settings.m_paintType = path.m_settings.m_paintType;
				settings.m_paintRadius = FloorPaintRadius;
				settings.m_paintCurve = path.m_settings.m_paintCurve;
				floor.m_settings = settings;
				floor.m_spawnOnPlaced = null;
			}
			if (snowOp == null && path != null)
			{
				snowOp = CopyOp(path, SnowOpName);
				TerrainOp snow = snowOp.GetComponent<TerrainOp>();
				TerrainOp.Settings settings = new TerrainOp.Settings();
				settings.m_level = false;
				settings.m_raise = false;
				settings.m_smooth = false;
				settings.m_paintCleared = true;
				settings.m_paintType = TerrainModifier.PaintType.Reset;
				settings.m_paintRadius = SnowPaintRadius;
				settings.m_paintCurve = path.m_settings.m_paintCurve;
				snow.m_settings = settings;
				snow.m_spawnOnPlaced = null;
			}
			Register(db, levelOp, LevelOpName);
			Register(db, floorOp, FloorOpName);
			Register(db, snowOp, SnowOpName);
		}

		/// <summary>
		/// Copies the hoe's path as a terrain operation of the mod's own, kept inactive and without the path's piece
		/// </summary>
		/// <param name="path">The hoe's path</param>
		/// <param name="name">The copy's name</param>
		/// <returns>The copy, whose settings are the caller's to set</returns>
		private static GameObject CopyOp(TerrainOp path, string name)
		{
			if (opHolder == null)
			{
				opHolder = new GameObject("VikingVillages terrain ops");
				opHolder.SetActive(false);
				Object.DontDestroyOnLoad(opHolder);
			}
			GameObject copy = Object.Instantiate(path.gameObject, opHolder.transform, false);
			copy.name = name;
			Piece piece = copy.GetComponent<Piece>();
			if (piece != null)
			{
				Object.DestroyImmediate(piece);
			}
			return(copy);
		}

		/// <summary>
		/// Adds one of the mod's terrain operations to the game's, by its name
		/// </summary>
		/// <param name="db">The ObjectDB</param>
		/// <param name="holder">The operation's object, or null when it couldn't be built</param>
		/// <param name="name">Its name</param>
		private static void Register(ObjectDB db, GameObject holder, string name)
		{
			TerrainOp op = ((holder != null) ? holder.GetComponent<TerrainOp>() : null);
			if (op == null)
			{
				return;
			}
			if (!db.m_terrainOps.Contains(op))
			{
				db.m_terrainOps.Add(op);
			}
			db.m_terrainOpsByHash[name.GetStableHashCode()] = op;
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

		/// <summary>
		/// Finds the clutter a path clears lying at a spot - branches, stones, small trees and small rocks
		/// </summary>
		/// <param name="blockers">The objects around the path</param>
		/// <param name="spot">The spot</param>
		/// <returns>The clutter</returns>
		private static List<ZNetView> ClutterAt(List<Blocker> blockers, Vector3 spot)
		{
			List<ZNetView> clutter = new List<ZNetView>();
			foreach (Blocker blocker in blockers)
			{
				if (blocker.Kind == Kind.Clear && blocker.View != null && Utils.DistanceXZ(spot, blocker.Center) <= blocker.Radius + ClearRadius)
				{
					clutter.Add(blocker.View);
				}
			}
			return(clutter);
		}

		/// <summary>
		/// Checks whether every zone across an area has loaded
		/// </summary>
		/// <param name="min">The area's lowest corner</param>
		/// <param name="max">The area's highest corner</param>
		/// <returns>True when they all have</returns>
		private static bool AreaLoaded(Vector3 min, Vector3 max)
		{
			for (float x = min.x; x <= max.x + ZoneCheckSpacing; x += ZoneCheckSpacing)
			{
				for (float z = min.z; z <= max.z + ZoneCheckSpacing; z += ZoneCheckSpacing)
				{
					if (!ZoneSystem.instance.IsZoneLoaded(new Vector3(Mathf.Min(x, max.x), 0f, Mathf.Min(z, max.z))))
					{
						return(false);
					}
				}
			}
			return(true);
		}

		/// <summary>
		/// Lists the straight lines a village's paths run between its houses, for how far the village reaches (see
		/// Village.Zones) until its paths have been traced on the ground (see VillageGroundPaths) - the houses with a
		/// path joined the way the paths are laid between them (see SpanningTree): a
		/// farm's own houses once its paths are laid, the houses added for players' beds once theirs is made, and a
		/// village a player started's fire, which its houses' paths lead back to
		/// </summary>
		/// <remarks>
		/// The paths themselves wind around what stands in the way and aren't kept, so their lines stand in for them
		/// </remarks>
		/// <param name="village">The village, its houses read</param>
		/// <returns>Each line's ends</returns>
		public static List<KeyValuePair<Vector3, Vector3>> PathLines(Village village)
		{
			List<KeyValuePair<Vector3, Vector3>> lines = new List<KeyValuePair<Vector3, Vector3>>();
			if (!Laid(village.Zdo))
			{
				return(lines);
			}
			bool founded = village.IsFounded();
			List<House> pathed = PathedHouses(village.Zdo);
			List<House> joined = village.Houses.FindAll((House house) => (VillageBeds.IsAdded(village, house) ? HasHouse(pathed, house) : !founded));
			if (founded && joined.Count > 0)
			{
				joined.Insert(0, new House { Center = village.Farm });
			}
			if (joined.Count < 2)
			{
				return(lines);
			}
			foreach (KeyValuePair<int, int> edge in SpanningTree(joined))
			{
				lines.Add(new KeyValuePair<Vector3, Vector3>(joined[edge.Key].Center, joined[edge.Value].Center));
			}
			return(lines);
		}

		/// <summary>
		/// Joins the houses with the shortest set of straight lines across the ground that connects them all, starting
		/// from the first
		/// </summary>
		/// <param name="houses">The houses</param>
		/// <returns>The pairs of houses to join, by index</returns>
		internal static List<KeyValuePair<int, int>> SpanningTree(List<House> houses)
		{
			List<KeyValuePair<int, int>> edges = new List<KeyValuePair<int, int>>();
			HashSet<int> joined = new HashSet<int> { 0 };
			while (joined.Count < houses.Count)
			{
				int bestFrom = -1;
				int bestTo = -1;
				float bestDistance = float.MaxValue;
				foreach (int from in joined)
				{
					for (int to = 0; to < houses.Count; to++)
					{
						if (joined.Contains(to))
						{
							continue;
						}
						float distance = Utils.DistanceXZ(houses[from].Center, houses[to].Center);
						if (distance < bestDistance)
						{
							bestFrom = from;
							bestTo = to;
							bestDistance = distance;
						}
					}
				}
				// The houses left have no distance to measure to them - a middle that isn't a number or is infinitely
				// far - so they're left out
				if (bestTo < 0)
				{
					break;
				}
				joined.Add(bestTo);
				edges.Add(new KeyValuePair<int, int>(bestFrom, bestTo));
			}
			return(edges);
		}

		/// <summary>
		/// Finds the loaded objects across an area that a path steers around or clears
		/// </summary>
		/// <param name="min">The area's lowest corner</param>
		/// <param name="max">The area's highest corner</param>
		/// <returns>The objects</returns>
		private static List<Blocker> FindBlockers(Vector3 min, Vector3 max)
		{
			List<Blocker> blockers = new List<Blocker>();
			foreach (ZNetView view in ZNetScene.instance.m_instances.Values)
			{
				if (view == null)
				{
					continue;
				}
				Vector3 position = view.transform.position;
				if (position.x < min.x || position.x > max.x || position.z < min.z || position.z > max.z)
				{
					continue;
				}
				Bounds bounds;
				List<Collider> colliders = new List<Collider>();
				if (!Measure(view.gameObject, out bounds, colliders))
				{
					continue;
				}
				// An oak is measured by its trunk alone - its branches spread far wider, and the vikings walk under them
				bool oak = VillageOaks.IsOak(view);
				Blocker blocker = new Blocker();
				blocker.View = view;
				blocker.Colliders = (oak ? new List<Collider>() : colliders);
				blocker.Center = (oak ? position : bounds.center);
				blocker.Radius = (oak ? VillageOaks.TrunkRadius * view.transform.localScale.x : Mathf.Max(bounds.extents.x, bounds.extents.z));
				blocker.Kind = Classify(view, blocker.Radius * 2f);
				blocker.Structure = (blocker.Kind == Kind.Avoid && (oak || view.GetComponent<WearNTear>() != null));
				if (blocker.Kind != Kind.Ignore)
				{
					blockers.Add(blocker);
				}
			}
			return(blockers);
		}

		/// <summary>
		/// Measures an object by its solid colliders, or by what's drawn of it when it has none
		/// </summary>
		/// <param name="target">The object</param>
		/// <param name="bounds">How far it reaches</param>
		/// <param name="colliders">Its solid colliders, filled in</param>
		/// <returns>False when it has nothing to measure</returns>
		private static bool Measure(GameObject target, out Bounds bounds, List<Collider> colliders)
		{
			bounds = new Bounds();
			bool found = false;
			foreach (Collider collider in target.GetComponentsInChildren<Collider>())
			{
				if (collider.isTrigger || !collider.enabled)
				{
					continue;
				}
				colliders.Add(collider);
				if (found)
				{
					bounds.Encapsulate(collider.bounds);
				}
				else
				{
					bounds = collider.bounds;
					found = true;
				}
			}
			if (found)
			{
				return(true);
			}
			foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
			{
				if (renderer is ParticleSystemRenderer)
				{
					continue;
				}
				if (found)
				{
					bounds.Encapsulate(renderer.bounds);
				}
				else
				{
					bounds = renderer.bounds;
					found = true;
				}
			}
			return(found);
		}

		/// <summary>
		/// Decides whether a path steers around an object, clears it or ignores it
		/// </summary>
		/// <param name="view">The object</param>
		/// <param name="size">How far across it is</param>
		/// <returns>What the path does about it</returns>
		private static Kind Classify(ZNetView view, float size)
		{
			GameObject target = view.gameObject;
			// A location's proxy and generator hold the whole location, which is measured piece by piece instead
			if (size > MaxSize || target.GetComponent<Character>() != null || target.GetComponent<ItemDrop>() != null || target.GetComponent<Ragdoll>() != null || target.GetComponent<DungeonGenerator>() != null || target.GetComponent<LocationProxy>() != null || target.GetComponent<TerrainComp>() != null)
			{
				return(Kind.Ignore);
			}
			string name = Utils.GetPrefabName(target);
			string lower = name.ToLowerInvariant();
			if (target.GetComponent<WearNTear>() != null)
			{
				return(Kind.Avoid);
			}
			if (Contains(lower, BushNames))
			{
				return(Kind.Avoid);
			}
			if (target.GetComponent<Pickable>() != null)
			{
				return(ClutterPickables.Contains(name) ? Kind.Clear : Kind.Ignore);
			}
			// Grown trees are TreeBase, while small trees, saplings and stumps are Destructible like rocks
			if (target.GetComponent<TreeBase>() != null)
			{
				return(Kind.Avoid);
			}
			if (target.GetComponent<MineRock>() != null || target.GetComponent<MineRock5>() != null)
			{
				return((size <= SmallSize) ? Kind.Clear : Kind.Avoid);
			}
			if (target.GetComponent<Destructible>() != null)
			{
				return((size <= SmallSize) ? Kind.Clear : Kind.Avoid);
			}
			return(Kind.Avoid);
		}

		/// <summary>
		/// Checks whether a name holds any of some parts
		/// </summary>
		/// <param name="name">The name, in lowercase</param>
		/// <param name="parts">The parts, in lowercase</param>
		/// <returns>True when it holds one</returns>
		private static bool Contains(string name, string[] parts)
		{
			foreach (string part in parts)
			{
				if (name.Contains(part))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Paints the hoe's path along a path, leaving out where it crosses water
		/// </summary>
		/// <param name="path">The path's points</param>
		private static void Paint(List<Vector3> path)
		{
			// The hoe's terrain pieces are kept in ObjectDB rather than among the networked prefabs
			if (ObjectDB.instance == null || !ObjectDB.instance.TryGetTerrainOp(PathPiece, out TerrainOp op))
			{
				Plugin.Log.LogWarning("Couldn't find the hoe's path to paint village paths with");
				return;
			}
			GameObject piece = op.gameObject;
			if (path.Count == 0)
			{
				return;
			}
			foreach (int i in PaintSteps(path, PaintSpacing, ZoneSystem.instance.m_waterLevel + WetHeight))
			{
				Object.Instantiate(piece, path[i], Quaternion.identity);
			}
		}

		/// <summary>
		/// Picks the points along a path the hoe's path is painted at - the first out of the water, then each at least a
		/// spacing across the ground from the last painted, and the last - leaving out those under water, though the
		/// distance across them still counts
		/// </summary>
		/// <param name="path">The path's points</param>
		/// <param name="spacing">How far apart the painted points are, in meters</param>
		/// <param name="water">The height a point under water is below</param>
		/// <returns>The painted points' places in the path, in order</returns>
		internal static List<int> PaintSteps(List<Vector3> path, float spacing, float water)
		{
			List<int> steps = new List<int>();
			float travelled = spacing;
			for (int i = 0; i < path.Count; i++)
			{
				if (i > 0)
				{
					travelled += Utils.DistanceXZ(path[i - 1], path[i]);
				}
				if (path[i].y < water)
				{
					continue;
				}
				if (travelled >= spacing || i == path.Count - 1)
				{
					travelled = 0f;
					steps.Add(i);
				}
			}
			return(steps);
		}

		/// <summary>
		/// Paints the ground under each floor piece of the village's houses the way the hoe's path does, so no grass
		/// grows through the floors - leaving the ground where it is
		/// </summary>
		/// <param name="houses">The village's houses</param>
		/// <param name="blockers">The objects around the village</param>
		/// <returns>How many floor pieces it painted under</returns>
		private static int PaintFloors(List<House> houses, List<Blocker> blockers)
		{
			if (floorOp == null)
			{
				Plugin.Log.LogWarning("Couldn't find the hoe's path to paint under the village's houses with");
				return(0);
			}
			int painted = 0;
			foreach (Blocker blocker in blockers)
			{
				if (!blocker.Structure || blocker.View == null || !blocker.View.IsValid())
				{
					continue;
				}
				string name = Utils.GetPrefabName(blocker.View.gameObject);
				if (name.IndexOf(FloorName, System.StringComparison.OrdinalIgnoreCase) < 0 || !houses.Exists((House house) => Village.InHouse(house, blocker.Center, 1f)))
				{
					continue;
				}
				// A floor piece is up to 2 meters square, which one paint just covers to its corners
				Vector3 point = blocker.Center;
				point.y = ZoneSystem.instance.GetGroundHeight(point);
				Object.Instantiate(floorOp, point, Quaternion.identity);
				painted++;
			}
			return(painted);
		}

		/// <summary>
		/// Works out how far from a farm's middle the trees, bushes, rocks and logs are cleared - FarmClearMargin past
		/// its fence, or past the largest fence a farm gets when its generator didn't note its own
		/// </summary>
		/// <param name="generator">The farm's generator's ZDO</param>
		/// <returns>The distance, in meters</returns>
		private static float FarmClearRadius(ZDO generator)
		{
			return(generator.GetFloat(VillageFarmhouses.FenceKey, VillageFarmhouses.FenceRadiusMax) + FarmClearMargin);
		}

		/// <summary>
		/// Removes the trees, bushes, rocks and logs the world grew within a distance of a farm's middle - in its own
		/// zone and the zones around it, which its widened fields, pens and houses reach into - leaving what's picked,
		/// like branches, stones and berries, and the oaks, which the village is built around (see VillageOaks)
		/// </summary>
		/// <remarks>
		/// A location only clears what grows in its own zone, as it's generated, so this is done once everything around
		/// the village has loaded, whichever zone was generated first
		/// </remarks>
		/// <param name="farm">The farm's generator</param>
		/// <param name="radius">How far from its middle to clear, in meters</param>
		/// <returns>How many things were cleared</returns>
		private static int ClearWilds(Vector3 farm, float radius)
		{
			HashSet<int> wilds = new HashSet<int>();
			foreach (ZoneSystem.ZoneVegetation vegetation in ZoneSystem.instance.m_vegetation)
			{
				if (vegetation.m_prefab != null)
				{
					wilds.Add(vegetation.m_prefab.name.GetStableHashCode());
				}
			}
			List<ZNetView> found = new List<ZNetView>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				if (view == null || !view.IsValid() || !wilds.Contains(instance.Key.GetPrefab()) || view.GetComponent<Pickable>() != null || VillageOaks.IsOak(view))
				{
					continue;
				}
				if (Utils.DistanceXZ(view.transform.position, farm) <= radius)
				{
					found.Add(view);
				}
			}
			foreach (ZNetView view in found)
			{
				view.ClaimOwnership();
				ZNetScene.instance.Destroy(view.gameObject);
			}
			return(found.Count);
		}

		/// <summary>
		/// Removes the clutter lying on the paths - branches, stones, small trees and small rocks
		/// </summary>
		/// <param name="blockers">The objects around the village</param>
		/// <param name="path">Every path's points</param>
		/// <returns>How many things were cleared</returns>
		private static int Clear(List<Blocker> blockers, List<Vector3> path)
		{
			int cleared = 0;
			foreach (Blocker blocker in blockers)
			{
				if (blocker.Kind != Kind.Clear || blocker.View == null || !blocker.View.IsValid())
				{
					continue;
				}
				float reach = blocker.Radius + ClearRadius;
				foreach (Vector3 point in path)
				{
					if (Utils.DistanceXZ(point, blocker.Center) <= reach)
					{
						blocker.View.ClaimOwnership();
						ZNetScene.instance.Destroy(blocker.View.gameObject);
						cleared++;
						break;
					}
				}
			}
			return(cleared);
		}

		/// <summary>
		/// The ground across a village, in cells, with what's blocked and what costs more to walk through
		/// </summary>
		internal class Grid
		{
			private readonly Vector3 origin;
			private readonly float waterLevel;
			private readonly int width;
			private readonly int depth;
			private readonly float[] heights;
			private readonly bool[] blocked;
			private readonly bool[] wet;
			private readonly float[] extra;

			/// <summary>
			/// The cells under something a path steers around, the water a bridge can cross, and the water under a
			/// bridge already found
			/// </summary>
			private readonly bool[] solid;
			private readonly bool[] bridgeable;
			private readonly bool[] bridged;
			private readonly bool[] onPath;

			/// <summary>
			/// How far the ground at each cell was moved by flattening, in meters
			/// </summary>
			private readonly float[] changes;

			/// <summary>
			/// The widest water a bridge crosses, from bank to bank, in meters
			/// </summary>
			private readonly float spanMax;

			/// <summary>
			/// Reads the world's ground across an area, blocking the water, and the Ashlands' lava and the boiling sea
			/// around it as water too, for the bridges of a village's floor piece to cross (see BridgePieceAt)
			/// </summary>
			/// <param name="min">The area's lowest corner</param>
			/// <param name="max">The area's highest corner</param>
			/// <param name="farm">Where the village's farm stands</param>
			public Grid(Vector3 min, Vector3 max, Vector3 farm) : this(min, max, ZoneSystem.instance.m_waterLevel, (Vector3 point) => ZoneSystem.instance.GetGroundHeight(point), DangerousGround.IsDangerous, BridgeSpanOf(BridgePieceAt(farm)))
			{
			}

			/// <summary>
			/// Reads the ground across an area from the height of the ground at each cell, blocking the water, for wood
			/// bridges to cross
			/// </summary>
			/// <param name="min">The area's lowest corner</param>
			/// <param name="max">The area's highest corner</param>
			/// <param name="waterLevel">The water's height</param>
			/// <param name="groundHeight">The ground's height at a point</param>
			internal Grid(Vector3 min, Vector3 max, float waterLevel, System.Func<Vector3, float> groundHeight) : this(min, max, waterLevel, groundHeight, null, BridgeSpanMax)
			{
			}

			/// <summary>
			/// Reads the ground across an area from the height of the ground at each cell, blocking the water and the
			/// ground that burns whoever stands on it, which paths cross and bridge the way they do the water
			/// </summary>
			/// <param name="min">The area's lowest corner</param>
			/// <param name="max">The area's highest corner</param>
			/// <param name="waterLevel">The water's height</param>
			/// <param name="groundHeight">The ground's height at a point</param>
			/// <param name="dangerous">Whether standing on the ground at a point burns, or null when none does</param>
			/// <param name="spanMax">The widest water a bridge crosses, from bank to bank, in meters</param>
			internal Grid(Vector3 min, Vector3 max, float waterLevel, System.Func<Vector3, float> groundHeight, System.Func<Vector3, bool> dangerous, float spanMax)
			{
				this.waterLevel = waterLevel;
				this.spanMax = spanMax;
				// The cells sit on whole meters, where the terrain's vertices are
				origin = new Vector3(Mathf.Floor(min.x), 0f, Mathf.Floor(min.z));
				width = Mathf.CeilToInt((max.x - origin.x) / CellSize) + 1;
				depth = Mathf.CeilToInt((max.z - origin.z) / CellSize) + 1;
				heights = new float[width * depth];
				blocked = new bool[width * depth];
				wet = new bool[width * depth];
				solid = new bool[width * depth];
				bridgeable = new bool[width * depth];
				bridged = new bool[width * depth];
				extra = new float[width * depth];
				onPath = new bool[width * depth];
				changes = new float[width * depth];
				for (int z = 0; z < depth; z++)
				{
					for (int x = 0; x < width; x++)
					{
						int index = z * width + x;
						float height = groundHeight(Position(x, z, 0f));
						heights[index] = height;
						wet[index] = (height < waterLevel + WetHeight || (dangerous != null && dangerous(Position(x, z, height))));
						blocked[index] = wet[index];
					}
				}
			}

			/// <summary>
			/// Blocks the cells under what a path steers around and makes the cells close around it cost more, and makes
			/// the cells under clutter cost more
			/// </summary>
			/// <param name="blockers">The objects around the village</param>
			public void Mark(List<Blocker> blockers)
			{
				foreach (Blocker blocker in blockers)
				{
					if (blocker.Kind == Kind.Avoid && blocker.Colliders != null && blocker.Colliders.Count > 0)
					{
						MarkShape(blocker);
						continue;
					}
					float reach = blocker.Radius + ((blocker.Kind == Kind.Avoid) ? PathClearance : 0f);
					int minX = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.x - reach - origin.x) / CellSize));
					int maxX = Mathf.Min(width - 1, Mathf.CeilToInt((blocker.Center.x + reach - origin.x) / CellSize));
					int minZ = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.z - reach - origin.z) / CellSize));
					int maxZ = Mathf.Min(depth - 1, Mathf.CeilToInt((blocker.Center.z + reach - origin.z) / CellSize));
					for (int z = minZ; z <= maxZ; z++)
					{
						for (int x = minX; x <= maxX; x++)
						{
							if (Utils.DistanceXZ(Position(x, z, 0f), blocker.Center) > reach)
							{
								continue;
							}
							int index = z * width + x;
							if (blocker.Kind == Kind.Clear)
							{
								extra[index] = Mathf.Max(extra[index], ClutterCost);
							}
							else if (Utils.DistanceXZ(Position(x, z, 0f), blocker.Center) <= blocker.Radius)
							{
								blocked[index] = true;
								solid[index] = true;
							}
							else
							{
								extra[index] = Mathf.Max(extra[index], ClearanceCost);
							}
						}
					}
				}
			}

			/// <summary>
			/// Works out flatter ground for the village - each cell moved toward the blurred ground around it, pulled
			/// part of the way toward the village's average height, fully near the farm's middle and its houses and
			/// fading out away from them, and left alone right around building pieces and under water
			/// </summary>
			/// <param name="farm">The farm's middle</param>
			/// <param name="houses">The village's houses</param>
			/// <param name="blockers">The objects around the village</param>
			public void Flatten(Vector3 farm, List<House> houses, List<Blocker> blockers)
			{
				int count = width * depth;
				float[] weight = new float[count];
				double sum = 0d;
				double total = 0d;
				for (int z = 0; z < depth; z++)
				{
					for (int x = 0; x < width; x++)
					{
						int index = z * width + x;
						Vector3 point = Position(x, z, 0f);
						float w = Falloff(Utils.DistanceXZ(point, farm), FarmFlatCore, FarmFlatFade);
						foreach (House house in houses)
						{
							w = Mathf.Max(w, Falloff(Utils.DistanceXZ(point, house.Center), HouseFlatCore, HouseFlatFade));
						}
						float edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(z, depth - 1 - z)) * CellSize;
						w *= Mathf.Clamp01(edge / EdgeFade);
						if (heights[index] < waterLevel)
						{
							w = 0f;
						}
						weight[index] = w;
						sum += heights[index] * w;
						total += w;
					}
				}
				if (total <= 0d)
				{
					return;
				}
				float mean = (float)(sum / total);
				float[] blurred = Blur(heights, Mathf.RoundToInt(FlatBlur / CellSize));
				float[] near = StructureDistance(blockers);
				for (int i = 0; i < count; i++)
				{
					float keep = Mathf.Clamp01((near[i] - StructureKeep) / StructureFade);
					float strength = weight[i] * keep * FlatStrength;
					if (strength <= 0f)
					{
						continue;
					}
					float target = Mathf.Lerp(blurred[i], mean, FlatMeanPull);
					float change = AboveWater(heights[i], Mathf.Clamp((target - heights[i]) * strength, -MaxFlatten, MaxFlatten), waterLevel);
					changes[i] = change;
					heights[i] += change;
				}
			}

			/// <summary>
			/// Raises a village's ground out of the water, as in the Swamp or the Plains by the sea - ground lower than
			/// WaterClearance above the water is raised to it, fully near the farm's middle and its houses and fading out
			/// away from them, so the raised ground slopes back down into the water, left alone right around building pieces
			/// the way flattening is, and by no more than MaxFlatten. A cell raised out of the water is no longer water for the paths, so they cross
			/// it rather than bridge it
			/// </summary>
			/// <param name="farm">The farm's middle</param>
			/// <param name="houses">The village's houses</param>
			/// <param name="blockers">The objects around the village</param>
			/// <returns>How many cells were raised</returns>
			public int RaiseOutOfWater(Vector3 farm, List<House> houses, List<Blocker> blockers)
			{
				float[] near = StructureDistance(blockers);
				int raised = 0;
				for (int z = 0; z < depth; z++)
				{
					for (int x = 0; x < width; x++)
					{
						int index = z * width + x;
						Vector3 point = Position(x, z, 0f);
						float w = Falloff(Utils.DistanceXZ(point, farm), FarmFlatCore, FarmFlatFade);
						foreach (House house in houses)
						{
							w = Mathf.Max(w, Falloff(Utils.DistanceXZ(point, house.Center), HouseFlatCore, HouseFlatFade));
						}
						float edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(z, depth - 1 - z)) * CellSize;
						w *= Mathf.Clamp01(edge / EdgeFade) * Mathf.Clamp01((near[index] - StructureKeep) / StructureFade);
						float change = RaisedOutOfWater(heights[index], w, waterLevel);
						if (change <= 0f)
						{
							continue;
						}
						changes[index] += change;
						heights[index] += change;
						raised++;
						if (heights[index] >= waterLevel + WetHeight - RaisedSlack)
						{
							wet[index] = false;
							blocked[index] = false;
						}
					}
				}
				return(raised);
			}

			/// <summary>
			/// Works out how far a cell of a village's ground is raised out of the water (see RaiseOutOfWater) - up to
			/// WaterClearance above it, by the share it's raised by, and no more than MaxFlatten
			/// </summary>
			/// <param name="height">The ground's height</param>
			/// <param name="weight">How fully it's raised, from 1 near the farm and its houses to 0 away from them</param>
			/// <param name="waterLevel">The water's height</param>
			/// <returns>How far it's raised, or 0 when it's high enough already</returns>
			internal static float RaisedOutOfWater(float height, float weight, float waterLevel)
			{
				float target = waterLevel + WaterClearance;
				if (height >= target || weight <= 0f)
				{
					return(0f);
				}
				return(Mathf.Min((target - height) * Mathf.Clamp01(weight), MaxFlatten));
			}

			/// <summary>
			/// Keeps flattening from lowering dry ground into the water, as it could on low ground like the Plains' by the
			/// sea - ground lowered stops WaterClearance above the water, and ground already that low isn't lowered at all,
			/// though it can still be raised
			/// </summary>
			/// <param name="height">The ground's height</param>
			/// <param name="change">How far flattening would move it, in meters - lower for less than 0</param>
			/// <param name="waterLevel">The water's height</param>
			/// <returns>How far it's moved</returns>
			internal static float AboveWater(float height, float change, float waterLevel)
			{
				if (change >= 0f)
				{
					return(change);
				}
				return(Mathf.Max(change, Mathf.Min(0f, waterLevel + WaterClearance - height)));
			}

			/// <summary>
			/// Finds how far flattening moved the ground at a point
			/// </summary>
			/// <param name="point">The point</param>
			/// <param name="change">How far it moved, in meters</param>
			/// <returns>False when the point is off the grid</returns>
			public bool TryGetChange(Vector3 point, out float change)
			{
				int cell = Cell(point);
				change = ((cell >= 0) ? changes[cell] : 0f);
				return(cell >= 0);
			}

			/// <summary>
			/// Works out how strongly a spot is flattened by its distance from what it's flattened around
			/// </summary>
			/// <param name="distance">The distance, in meters</param>
			/// <param name="core">How far it's flattened fully</param>
			/// <param name="fade">How far it's flattened at all</param>
			/// <returns>From 1 for fully to 0 for not at all, easing between</returns>
			internal static float Falloff(float distance, float core, float fade)
			{
				if (distance <= core)
				{
					return(1f);
				}
				if (distance >= fade)
				{
					return(0f);
				}
				float t = 1f - (distance - core) / (fade - core);
				return(t * t * (3f - 2f * t));
			}

			/// <summary>
			/// Averages each cell's value with the cells around it, out to a reach
			/// </summary>
			/// <param name="values">The values</param>
			/// <param name="reach">How many cells out to average</param>
			/// <returns>The averaged values</returns>
			internal float[] Blur(float[] values, int reach)
			{
				// A running sum of every cell above and to the left of each, so any rectangle's sum takes four lookups
				double[] sums = new double[(width + 1) * (depth + 1)];
				for (int z = 0; z < depth; z++)
				{
					double row = 0d;
					for (int x = 0; x < width; x++)
					{
						row += values[z * width + x];
						sums[(z + 1) * (width + 1) + x + 1] = sums[z * (width + 1) + x + 1] + row;
					}
				}
				float[] blurred = new float[values.Length];
				for (int z = 0; z < depth; z++)
				{
					int z0 = Mathf.Max(0, z - reach);
					int z1 = Mathf.Min(depth - 1, z + reach) + 1;
					for (int x = 0; x < width; x++)
					{
						int x0 = Mathf.Max(0, x - reach);
						int x1 = Mathf.Min(width - 1, x + reach) + 1;
						double area = sums[z1 * (width + 1) + x1] - sums[z0 * (width + 1) + x1] - sums[z1 * (width + 1) + x0] + sums[z0 * (width + 1) + x0];
						blurred[z * width + x] = (float)(area / ((x1 - x0) * (z1 - z0)));
					}
				}
				return(blurred);
			}

			/// <summary>
			/// Measures how far each cell is from the nearest building piece, across the cells
			/// </summary>
			/// <param name="blockers">The objects around the village</param>
			/// <returns>The distances, in meters</returns>
			private float[] StructureDistance(List<Blocker> blockers)
			{
				float[] distance = new float[width * depth];
				for (int i = 0; i < distance.Length; i++)
				{
					distance[i] = float.MaxValue;
				}
				foreach (Blocker blocker in blockers)
				{
					if (!blocker.Structure)
					{
						continue;
					}
					int minX = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.x - blocker.Radius - origin.x) / CellSize));
					int maxX = Mathf.Min(width - 1, Mathf.CeilToInt((blocker.Center.x + blocker.Radius - origin.x) / CellSize));
					int minZ = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.z - blocker.Radius - origin.z) / CellSize));
					int maxZ = Mathf.Min(depth - 1, Mathf.CeilToInt((blocker.Center.z + blocker.Radius - origin.z) / CellSize));
					for (int z = minZ; z <= maxZ; z++)
					{
						for (int x = minX; x <= maxX; x++)
						{
							bool under = ((blocker.Colliders != null && blocker.Colliders.Count > 0) ? Gap(Position(x, z, 0f), blocker.Colliders) <= CellSize / 2f : Utils.DistanceXZ(Position(x, z, 0f), blocker.Center) <= blocker.Radius);
							if (under)
							{
								distance[z * width + x] = 0f;
							}
						}
					}
				}
				// Two sweeps, forward and back, carry each cell's distance to its neighbors
				float diagonal = 1.4142f * CellSize;
				for (int z = 0; z < depth; z++)
				{
					for (int x = 0; x < width; x++)
					{
						int index = z * width + x;
						if (x > 0)
						{
							distance[index] = Mathf.Min(distance[index], distance[index - 1] + CellSize);
						}
						if (z > 0)
						{
							distance[index] = Mathf.Min(distance[index], distance[index - width] + CellSize);
							if (x > 0)
							{
								distance[index] = Mathf.Min(distance[index], distance[index - width - 1] + diagonal);
							}
							if (x < width - 1)
							{
								distance[index] = Mathf.Min(distance[index], distance[index - width + 1] + diagonal);
							}
						}
					}
				}
				for (int z = depth - 1; z >= 0; z--)
				{
					for (int x = width - 1; x >= 0; x--)
					{
						int index = z * width + x;
						if (x < width - 1)
						{
							distance[index] = Mathf.Min(distance[index], distance[index + 1] + CellSize);
						}
						if (z < depth - 1)
						{
							distance[index] = Mathf.Min(distance[index], distance[index + width] + CellSize);
							if (x < width - 1)
							{
								distance[index] = Mathf.Min(distance[index], distance[index + width + 1] + diagonal);
							}
							if (x > 0)
							{
								distance[index] = Mathf.Min(distance[index], distance[index + width - 1] + diagonal);
							}
						}
					}
				}
				return(distance);
			}

			/// <summary>
			/// Blocks the cells under an object a path steers around by the true shape of its colliders, so a long thin
			/// piece like a fence or wall blocks only the cells it stands on and leaves the gaps between pieces open, and
			/// makes the cells close around it cost more
			/// </summary>
			/// <param name="blocker">The object</param>
			private void MarkShape(Blocker blocker)
			{
				float reach = blocker.Radius + PathClearance;
				int minX = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.x - reach - origin.x) / CellSize));
				int maxX = Mathf.Min(width - 1, Mathf.CeilToInt((blocker.Center.x + reach - origin.x) / CellSize));
				int minZ = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.z - reach - origin.z) / CellSize));
				int maxZ = Mathf.Min(depth - 1, Mathf.CeilToInt((blocker.Center.z + reach - origin.z) / CellSize));
				for (int z = minZ; z <= maxZ; z++)
				{
					for (int x = minX; x <= maxX; x++)
					{
						float gap = Gap(Position(x, z, 0f), blocker.Colliders);
						int index = z * width + x;
						if (gap <= CellSize / 2f)
						{
							blocked[index] = true;
							solid[index] = true;
						}
						else if (gap <= PathClearance)
						{
							extra[index] = Mathf.Max(extra[index], ClearanceCost);
						}
					}
				}
			}

			/// <summary>
			/// Measures how far a point is across the ground from the nearest of some colliders
			/// </summary>
			/// <remarks>
			/// The point is taken at each collider's middle height, so a wall or trunk is measured where it stands. A
			/// collider whose nearest point can't be found - a mesh that isn't convex - is measured by its bounds
			/// </remarks>
			/// <param name="point">The point</param>
			/// <param name="colliders">The colliders</param>
			/// <returns>The distance, 0 when the point is inside one</returns>
			private static float Gap(Vector3 point, List<Collider> colliders)
			{
				float nearest = float.MaxValue;
				foreach (Collider collider in colliders)
				{
					if (collider == null)
					{
						continue;
					}
					Vector3 probe = new Vector3(point.x, collider.bounds.center.y, point.z);
					MeshCollider mesh = collider as MeshCollider;
					Vector3 closest = ((mesh != null && !mesh.convex) ? collider.bounds.ClosestPoint(probe) : collider.ClosestPoint(probe));
					nearest = Mathf.Min(nearest, Utils.DistanceXZ(probe, closest));
				}
				return(nearest);
			}

			/// <summary>
			/// Notes the cells the paths were laid across, and the cells beside them the paint reaches
			/// </summary>
			/// <param name="path">Every path's points</param>
			public void MarkPath(List<Vector3> path)
			{
				int reach = Mathf.CeilToInt(PaintSpacing / CellSize);
				foreach (Vector3 point in path)
				{
					int cell = Cell(point);
					if (cell < 0)
					{
						continue;
					}
					int cx = cell % width;
					int cz = cell / width;
					for (int z = Mathf.Max(0, cz - reach); z <= Mathf.Min(depth - 1, cz + reach); z++)
					{
						for (int x = Mathf.Max(0, cx - reach); x <= Mathf.Min(width - 1, cx + reach); x++)
						{
							onPath[z * width + x] = true;
						}
					}
				}
			}

			/// <summary>
			/// Finds the open ground nearest a point with room all around it - nothing blocked, nothing too close to
			/// what paths steer around, no clutter, no path and no water within a radius, on ground that rises and falls
			/// no more than a limit
			/// </summary>
			/// <param name="near">The point</param>
			/// <param name="radius">How much room it needs around it, in meters</param>
			/// <param name="least">How far from the point it stands at the least, in meters</param>
			/// <param name="search">How far from the point to look, in meters</param>
			/// <param name="flatness">The most the ground there can rise and fall, in meters</param>
			/// <param name="spot">The spot found, on the ground</param>
			/// <param name="level">Halfway between the ground's highest and lowest points around the spot</param>
			/// <returns>False when there's no such spot that close</returns>
			public bool FindOpenSpot(Vector3 near, float radius, float least, float search, float flatness, out Vector3 spot, out float level)
			{
				spot = near;
				level = near.y;
				int center = Cell(near);
				if (center < 0)
				{
					return(false);
				}
				int reach = Mathf.CeilToInt(radius / CellSize);
				int limit = Mathf.CeilToInt(search / CellSize);
				int cx = center % width;
				int cz = center / width;
				List<int> candidates = new List<int>();
				for (int z = Mathf.Max(reach, cz - limit); z <= Mathf.Min(depth - 1 - reach, cz + limit); z++)
				{
					for (int x = Mathf.Max(reach, cx - limit); x <= Mathf.Min(width - 1 - reach, cx + limit); x++)
					{
						candidates.Add(z * width + x);
					}
				}
				candidates.Sort((a, b) => Distance(a, center).CompareTo(Distance(b, center)));
				foreach (int candidate in candidates)
				{
					if (Distance(candidate, center) * CellSize < least || !IsOpen(candidate, reach, flatness, out level))
					{
						continue;
					}
					// The open ground inside a pen is no place for a fire the whole village gathers at
					Vector3 found = Position(candidate % width, candidate / width, heights[candidate]);
					if (!Village.FencedIn(found))
					{
						spot = found;
						return(true);
					}
				}
				return(false);
			}

			/// <summary>
			/// Checks whether the ground around a cell is open and flat enough
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <param name="reach">How many cells around it to check</param>
			/// <param name="flatness">The most the ground can rise and fall, in meters</param>
			/// <param name="level">Halfway between the ground's highest and lowest points</param>
			/// <returns>True when it is</returns>
			private bool IsOpen(int cell, int reach, float flatness, out float level)
			{
				int cx = cell % width;
				int cz = cell / width;
				float low = float.MaxValue;
				float high = float.MinValue;
				level = heights[cell];
				for (int z = cz - reach; z <= cz + reach; z++)
				{
					for (int x = cx - reach; x <= cx + reach; x++)
					{
						if ((x - cx) * (x - cx) + (z - cz) * (z - cz) > reach * reach)
						{
							continue;
						}
						int index = z * width + x;
						if (blocked[index] || extra[index] > 0f || onPath[index])
						{
							return(false);
						}
						low = Mathf.Min(low, heights[index]);
						high = Mathf.Max(high, heights[index]);
					}
				}
				level = (low + high) / 2f;
				return(high - low <= flatness);
			}

			/// <summary>
			/// Measures the distance between two cells
			/// </summary>
			/// <param name="a">One cell</param>
			/// <param name="b">The other</param>
			/// <returns>The distance, in cells</returns>
			private float Distance(int a, int b)
			{
				int dx = a % width - b % width;
				int dz = a / width - b / width;
				return(Mathf.Sqrt(dx * dx + dz * dz));
			}

			/// <summary>
			/// Finds the cheapest path between two houses, from the door of each nearest the other, or from the open
			/// ground nearest each toward the other when it has no door - through as little of what paths steer around
			/// and as little water as it can when there's no way around them
			/// </summary>
			/// <remarks>
			/// The path runs between the open cells just outside the doors, which are blocked like the rest of the
			/// house, then on up to each door's threshold so it's painted right to the door
			/// </remarks>
			/// <param name="from">The house it starts at</param>
			/// <param name="to">The house it ends at</param>
			/// <returns>The path's points on the ground, or null when there's no way through</returns>
			public List<Vector3> FindPath(House from, House to)
			{
				Vector3? fromDoor = NearestDoor(from, to.Center);
				Vector3? toDoor = NearestDoor(to, from.Center);
				int start = Doorstep(from, fromDoor, to.Center);
				int goal = Doorstep(to, toDoor, from.Center);
				if (start < 0 || goal < 0)
				{
					return(null);
				}
				List<Vector3> path = null;
				for (int i = 0; i < BridgeTries && path == null; i++)
				{
					path = Search(start, goal, false);
					if (path == null)
					{
						break;
					}
					if (ShutWideWater(path))
					{
						path = null;
					}
				}
				path = (path ?? Search(start, goal, true));
				if (path == null)
				{
					return(null);
				}
				if (fromDoor.HasValue)
				{
					path.Insert(0, OnGround(fromDoor.Value));
				}
				if (toDoor.HasValue)
				{
					path.Add(OnGround(toDoor.Value));
				}
				return(path);
			}

			/// <summary>
			/// Notes the water a bridge can cross - with nothing standing in it, and no farther from dry ground than half
			/// the widest a bridge spans
			/// </summary>
			public void MarkBridgeable()
			{
				int count = width * depth;
				float reach = spanMax / 2f;
				float[] distance = new float[count];
				Queue<int> queue = new Queue<int>();
				for (int i = 0; i < count; i++)
				{
					distance[i] = (wet[i] ? float.MaxValue : 0f);
					if (!wet[i])
					{
						queue.Enqueue(i);
					}
				}
				while (queue.Count > 0)
				{
					int current = queue.Dequeue();
					int cx = current % width;
					int cz = current / width;
					for (int dz = -1; dz <= 1; dz++)
					{
						for (int dx = -1; dx <= 1; dx++)
						{
							int nx = cx + dx;
							int nz = cz + dz;
							if ((dx == 0 && dz == 0) || nx < 0 || nz < 0 || nx >= width || nz >= depth)
							{
								continue;
							}
							int next = nz * width + nx;
							float through = distance[current] + ((dx != 0 && dz != 0) ? 1.4142f : 1f) * CellSize;
							if (wet[next] && through <= reach && through < distance[next])
							{
								distance[next] = through;
								queue.Enqueue(next);
							}
						}
					}
				}
				for (int i = 0; i < count; i++)
				{
					bridgeable[i] = (wet[i] && !solid[i] && distance[i] <= reach);
				}
			}

			/// <summary>
			/// Finds the bridges a path needs over the water it crosses - one from bank to bank over each stretch of
			/// water no bridge found before crosses, when its banks are from BridgeSpanMin to spanMax apart, it's no
			/// steeper than BridgeSlopeMax and has nothing standing in the way - and notes the water under them, so the
			/// paths found after cross on them
			/// </summary>
			/// <param name="path">The path's points on the ground</param>
			/// <returns>The bridges</returns>
			public List<Bridge> FindBridges(List<Vector3> path)
			{
				List<Bridge> bridges = new List<Bridge>();
				foreach (KeyValuePair<int, int> run in WaterRuns(path))
				{
					Vector3 start = path[run.Key - 1];
					Vector3 end = path[run.Value + 1];
					float span = Utils.DistanceXZ(start, end);
					if (span < BridgeSpanMin || span > spanMax || Mathf.Atan2(Mathf.Abs(end.y - start.y), span) * Mathf.Rad2Deg > BridgeSlopeMax)
					{
						continue;
					}
					bool crossed = true;
					for (int i = run.Key; i <= run.Value && crossed; i++)
					{
						int cell = Cell(path[i]);
						crossed = (cell < 0 || !wet[cell] || bridged[cell]);
					}
					if (crossed || Crosses(start, end, solid))
					{
						continue;
					}
					Bridge bridge = new Bridge();
					bridge.Start = start;
					bridge.End = end;
					bridges.Add(bridge);
					MarkBridged(start, end);
				}
				return(bridges);
			}

			/// <summary>
			/// Keeps paths off water too wide to bridge that a path crosses, and the water right beside it
			/// </summary>
			/// <param name="path">The path's points on the ground</param>
			/// <returns>True when the path crosses such water, so it's looked for again</returns>
			private bool ShutWideWater(List<Vector3> path)
			{
				bool shut = false;
				foreach (KeyValuePair<int, int> run in WaterRuns(path))
				{
					if (Utils.DistanceXZ(path[run.Key - 1], path[run.Value + 1]) <= spanMax)
					{
						continue;
					}
					for (int i = run.Key; i <= run.Value; i++)
					{
						int cell = Cell(path[i]);
						if (cell < 0)
						{
							continue;
						}
						int cx = cell % width;
						int cz = cell / width;
						for (int z = Mathf.Max(0, cz - 1); z <= Mathf.Min(depth - 1, cz + 1); z++)
						{
							for (int x = Mathf.Max(0, cx - 1); x <= Mathf.Min(width - 1, cx + 1); x++)
							{
								bridgeable[z * width + x] = false;
							}
						}
					}
					shut = true;
				}
				return(shut);
			}

			/// <summary>
			/// Finds the stretches of water a path crosses with dry ground on both sides
			/// </summary>
			/// <param name="path">The path's points on the ground</param>
			/// <returns>The first and last point in the water of each stretch</returns>
			internal List<KeyValuePair<int, int>> WaterRuns(List<Vector3> path)
			{
				List<KeyValuePair<int, int>> runs = new List<KeyValuePair<int, int>>();
				int first = -1;
				for (int i = 0; i < path.Count; i++)
				{
					int cell = Cell(path[i]);
					bool inWater = (cell >= 0 && wet[cell]);
					if (inWater && first < 0)
					{
						first = i;
					}
					else if (!inWater && first >= 0)
					{
						if (first > 0)
						{
							runs.Add(new KeyValuePair<int, int>(first, i - 1));
						}
						first = -1;
					}
				}
				return(runs);
			}

			/// <summary>
			/// Checks whether a straight line crosses any of a set of cells
			/// </summary>
			/// <param name="start">Where the line starts</param>
			/// <param name="end">Where it ends</param>
			/// <param name="cells">The cells</param>
			/// <returns>True when it does</returns>
			private bool Crosses(Vector3 start, Vector3 end, bool[] cells)
			{
				int steps = Mathf.CeilToInt(Utils.DistanceXZ(start, end) / (CellSize / 2f));
				for (int i = 0; i <= steps; i++)
				{
					int cell = Cell(Vector3.Lerp(start, end, (steps > 0) ? i / (float)steps : 0f));
					if (cell >= 0 && cells[cell])
					{
						return(true);
					}
				}
				return(false);
			}

			/// <summary>
			/// Notes the water under a bridge and beside it, as wide as the bridge, so the paths found after cross on it
			/// </summary>
			/// <param name="start">Where the bridge starts</param>
			/// <param name="end">Where it ends</param>
			private void MarkBridged(Vector3 start, Vector3 end)
			{
				int steps = Mathf.CeilToInt(Utils.DistanceXZ(start, end) / (CellSize / 2f));
				for (int i = 0; i <= steps; i++)
				{
					int cell = Cell(Vector3.Lerp(start, end, (steps > 0) ? i / (float)steps : 0f));
					if (cell < 0)
					{
						continue;
					}
					int cx = cell % width;
					int cz = cell / width;
					for (int z = Mathf.Max(0, cz - 1); z <= Mathf.Min(depth - 1, cz + 1); z++)
					{
						for (int x = Mathf.Max(0, cx - 1); x <= Mathf.Min(width - 1, cx + 1); x++)
						{
							int index = z * width + x;
							if (wet[index])
							{
								bridged[index] = true;
							}
						}
					}
				}
			}

			/// <summary>
			/// Checks whether a path can step into a cell without being forced - open ground, water a bridge can cross,
			/// or water under a bridge already found
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <returns>True when it can</returns>
			private bool Passable(int cell)
			{
				return(!blocked[cell] || bridged[cell] || bridgeable[cell]);
			}

			/// <summary>
			/// Works out the extra cost of a step into a cell for what's there - nothing for open ground or a bridge
			/// already found, BridgeCost for water a bridge can cross, WetCost for other water and ForcedCost for
			/// something a path steers around
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <returns>The extra cost, for each meter</returns>
			private float StepCost(int cell)
			{
				if (!blocked[cell] || bridged[cell])
				{
					return(0f);
				}
				if (bridgeable[cell])
				{
					return(BridgeCost);
				}
				return(wet[cell] ? WetCost : ForcedCost);
			}

			/// <summary>
			/// Finds a house's door nearest a point
			/// </summary>
			/// <param name="house">The house</param>
			/// <param name="toward">The point</param>
			/// <returns>Where the door stands, or null when the house has none</returns>
			private static Vector3? NearestDoor(House house, Vector3 toward)
			{
				if (house.Doors == null || house.Doors.Count == 0)
				{
					return(null);
				}
				Vector3 nearest = house.Doors[0];
				foreach (Vector3 door in house.Doors)
				{
					if (Utils.DistanceXZ(door, toward) < Utils.DistanceXZ(nearest, toward))
					{
						nearest = door;
					}
				}
				return(nearest);
			}

			/// <summary>
			/// Puts a point on the ground of the grid
			/// </summary>
			/// <param name="point">The point</param>
			/// <returns>The point at the ground's height</returns>
			private Vector3 OnGround(Vector3 point)
			{
				int cell = Cell(point);
				point.y = ((cell >= 0) ? heights[cell] : ZoneSystem.instance.GetGroundHeight(point));
				return(point);
			}

			/// <summary>
			/// Finds the cheapest way across the grid between two cells
			/// </summary>
			/// <param name="start">The cell it starts at</param>
			/// <param name="goal">The cell it ends at</param>
			/// <param name="forced">True to step through blocked cells, at ForcedCost, or WetCost in water no bridge
			/// crosses</param>
			/// <returns>The way's points on the ground, or null when there's no way through</returns>
			internal List<Vector3> Search(int start, int goal, bool forced)
			{
				float[] cost = new float[width * depth];
				int[] came = new int[width * depth];
				bool[] closed = new bool[width * depth];
				for (int i = 0; i < cost.Length; i++)
				{
					cost[i] = float.MaxValue;
					came[i] = -1;
				}
				cost[start] = 0f;
				SortedSet<KeyValuePair<float, int>> open = new SortedSet<KeyValuePair<float, int>>(Comparer<KeyValuePair<float, int>>.Create((a, b) => ((a.Key != b.Key) ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value))));
				open.Add(new KeyValuePair<float, int>(Estimate(start, goal), start));
				while (open.Count > 0)
				{
					KeyValuePair<float, int> best = open.Min;
					open.Remove(best);
					int current = best.Value;
					if (current == goal)
					{
						return(Trace(came, goal));
					}
					if (closed[current])
					{
						continue;
					}
					closed[current] = true;
					int cx = current % width;
					int cz = current / width;
					for (int dz = -1; dz <= 1; dz++)
					{
						for (int dx = -1; dx <= 1; dx++)
						{
							int nx = cx + dx;
							int nz = cz + dz;
							if ((dx == 0 && dz == 0) || nx < 0 || nz < 0 || nx >= width || nz >= depth)
							{
								continue;
							}
							int next = nz * width + nx;
							if (closed[next] || (!forced && !Passable(next)))
							{
								continue;
							}
							// A diagonal step can't cut the corner of a blocked cell
							if (!forced && dx != 0 && dz != 0 && (!Passable(cz * width + nx) || !Passable(nz * width + cx)))
							{
								continue;
							}
							float through = StepCost(next);
							float step = ((dx != 0 && dz != 0) ? 1.4142f : 1f) * CellSize;
							float climb = Mathf.Abs(heights[next] - heights[current]);
							float total = cost[current] + step * (1f + extra[next] + through) + climb * SlopeCost;
							if (total < cost[next])
							{
								cost[next] = total;
								came[next] = current;
								open.Add(new KeyValuePair<float, int>(total + Estimate(next, goal), next));
							}
						}
					}
				}
				return(null);
			}

			/// <summary>
			/// Finds the open cell a path leaves a house from - the first one out from its door, or without a door the
			/// one just outside its walls toward where the path heads, or else the nearest open cell to the house
			/// </summary>
			/// <param name="house">The house</param>
			/// <param name="door">Where its door stands, or null</param>
			/// <param name="toward">Where the path heads</param>
			/// <returns>The cell's index, or -1 when there's none</returns>
			private int Doorstep(House house, Vector3? door, Vector3 toward)
			{
				if (door.HasValue)
				{
					Vector3 away = door.Value - house.Center;
					away.y = 0f;
					if (away.sqrMagnitude > 0.01f)
					{
						away.Normalize();
						for (float distance = 0f; distance <= DoorstepReach; distance += CellSize / 2f)
						{
							int cell = Cell(door.Value + away * distance);
							if (cell >= 0 && !blocked[cell])
							{
								return(cell);
							}
						}
					}
				}
				Vector3 outward = toward - house.Center;
				outward.y = 0f;
				if (outward.sqrMagnitude > 0.01f)
				{
					outward.Normalize();
					for (float distance = house.HalfSize; distance <= house.HalfSize + Margin; distance += CellSize)
					{
						int cell = Cell(house.Center + outward * distance);
						if (cell >= 0 && !blocked[cell])
						{
							return(cell);
						}
					}
				}
				int nearest = -1;
				float nearestDistance = float.MaxValue;
				for (int i = 0; i < blocked.Length; i++)
				{
					if (blocked[i])
					{
						continue;
					}
					float distance = Utils.DistanceXZ(Position(i % width, i / width, 0f), house.Center);
					if (distance < nearestDistance)
					{
						nearest = i;
						nearestDistance = distance;
					}
				}
				return(nearest);
			}

			/// <summary>
			/// Estimates the cost left from a cell to the goal, as the straight distance between them
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <param name="goal">The goal's cell</param>
			/// <returns>The estimate</returns>
			private float Estimate(int cell, int goal)
			{
				int dx = Mathf.Abs(cell % width - goal % width);
				int dz = Mathf.Abs(cell / width - goal / width);
				return((Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz)) * CellSize);
			}

			/// <summary>
			/// Follows a found path back from its goal to its start
			/// </summary>
			/// <param name="came">The cell each cell was reached from</param>
			/// <param name="goal">The goal's cell</param>
			/// <returns>The path's points on the ground, from start to goal</returns>
			private List<Vector3> Trace(int[] came, int goal)
			{
				List<Vector3> path = new List<Vector3>();
				for (int cell = goal; cell >= 0; cell = came[cell])
				{
					path.Add(Position(cell % width, cell / width, heights[cell]));
				}
				path.Reverse();
				return(path);
			}

			/// <summary>
			/// Finds the cell a point is in
			/// </summary>
			/// <param name="point">The point</param>
			/// <returns>The cell's index, or -1 off the grid</returns>
			internal int Cell(Vector3 point)
			{
				int x = Mathf.RoundToInt((point.x - origin.x) / CellSize);
				int z = Mathf.RoundToInt((point.z - origin.z) / CellSize);
				return((x < 0 || z < 0 || x >= width || z >= depth) ? -1 : z * width + x);
			}

			/// <summary>
			/// Works out where a cell is in the world
			/// </summary>
			/// <param name="x">The cell's column</param>
			/// <param name="z">The cell's row</param>
			/// <param name="height">The height to give it</param>
			/// <returns>The cell's middle</returns>
			private Vector3 Position(int x, int z, float height)
			{
				return(new Vector3(origin.x + x * CellSize, height, origin.z + z * CellSize));
			}
		}
	}
}
