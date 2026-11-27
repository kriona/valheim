using System.Collections.Generic;
using System.Linq;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The abandoned farms PC vikings come to - noted as they're generated, for PC vikings to come to once each
	/// has its bonfire (see VillageRespawn.Populated) - the empty abandoned houses near them the vikings take in, the
	/// livestock their pens spawn, and the chests they're generated with, which are the vikings' own
	/// </summary>
	internal static class FarmSpawns
	{
		/// <summary>
		/// On an animal a farm's pen spawned tamed, that it's the vikings' livestock
		/// </summary>
		public static readonly int LivestockKey = "kriona.VikingVillages.livestock".GetStableHashCode();

		/// <summary>
		/// The start of the name of the abandoned houses
		/// </summary>
		private const string HousePrefix = "WoodHouse";

		/// <summary>
		/// The start of the name of the abandoned farms, and of their farmhouses
		/// </summary>
		private const string FarmPrefix = "WoodFarm";
		public const string FarmhousePrefix = "woodfarm_farmhouse";

		/// <summary>
		/// How far past a widened farm's farthest fence its pens' spawners can stand and still be the farm's, in meters
		/// </summary>
		private const float FenceMargin = 4f;

		/// <summary>
		/// How close a PC viking has to be to a farm's pen for the animals it spawns to be tamed, in meters - about
		/// the farm's size
		/// </summary>
		private const float FarmKeeperRange = 40f;

		/// <summary>
		/// How far from an abandoned farm an empty abandoned house is taken in by its PC vikings, in meters
		/// </summary>
		private const float FarmGuardRange = 100f;

		/// <summary>
		/// How close two abandoned farms have to be for a path to join their villages, in meters
		/// </summary>
		private const float FarmLinkRange = 100f;

		/// <summary>
		/// How far an abandoned house's walls reach from its middle, in meters, for placing its beehive outside
		/// </summary>
		private const float HouseHalfSize = 4f;

		/// <summary>
		/// The start of the names of the building pieces of a farm or a house its vikings have taken in - its roofs,
		/// walls, floors and doors, and the sections of roof a house leaves out as a group - and of the vines a house
		/// is overgrown with, which they clear
		/// </summary>
		private const string BuildingPiecePrefix = "wood_";
		private const string RoofGroupPrefix = "random_roof";
		private const string VinesPrefix = "vines";

		/// <summary>
		/// The wild beehive a farmhouse may hang inside, whose bees sting anything near it, the beehive piece a farm gets
		/// in its place, and how far outside the farmhouse's walls that piece stands, in meters
		/// </summary>
		private const string WildHivePrefix = "Beehive";
		private const string HivePiece = "piece_beehive";
		private const float HiveClearance = 2f;

		/// <summary>
		/// A farmhouse of the farm being placed - where its middle stands and how far its walls reach from it
		/// </summary>
		internal struct Farmhouse
		{
			public Vector3 Center;
			public float HalfSize;
		}

		/// <summary>
		/// An abandoned farm the world plans - where it stands, and whether its zone has been generated
		/// </summary>
		internal struct PlannedFarm
		{
			public Vector3 Position;
			public bool Placed;
		}

		/// <summary>
		/// The farmhouses of the farm being placed, gathered as its rooms are generated
		/// </summary>
		private static readonly List<Farmhouse> farmhouses = new List<Farmhouse>();

		/// <summary>
		/// Where the wild beehives the farm being placed would have had hang, gathered as its rooms are generated
		/// </summary>
		private static readonly List<Vector3> hives = new List<Vector3>();

		/// <summary>
		/// Whether an abandoned farm, or an empty abandoned house near one that its PC vikings take in, is being
		/// generated for the first time, so its pieces start out whole
		/// </summary>
		private static bool generatingFarm;
		private static bool generatingOutpost;

		/// <summary>
		/// How many locations are being shown to a client inside the placement of another
		/// </summary>
		/// <remarks>
		/// Placing a location in full makes its proxy, which shows the location at once by placing it again for a client,
		/// inside the first placement - so a client placement leaves the first one's state alone and isn't fixed up
		/// </remarks>
		private static int clientDepth;

		/// <summary>
		/// Gets the hover text of the vikings' livestock - its name, and its village's name after it, or that it's the
		/// vikings' when it stands in no loaded village or the village has no name
		/// </summary>
		/// <param name="animal">The animal's name</param>
		/// <param name="village">Its village's name, or ""</param>
		/// <returns>The hover text</returns>
		internal static string LivestockHover(string animal, string village)
		{
			return(animal + " (" + (string.IsNullOrEmpty(village) ? "Viking livestock" : village) + ")");
		}

		/// <summary>
		/// Checks whether an animal is a farm's livestock, which the vikings keep rather than players
		/// </summary>
		/// <param name="part">Any component on the animal</param>
		/// <returns>True when it's livestock</returns>
		public static bool IsLivestock(Component part)
		{
			ZNetView nview = part.GetComponent<ZNetView>();
			return(nview != null && nview.IsValid() && nview.GetZDO().GetBool(LivestockKey));
		}

		/// <summary>
		/// Checks whether a point is inside an abandoned farm - within its location's radius, or within its widened
		/// fence, which reaches past that radius
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when it is</returns>
		public static bool InFarm(Vector3 point)
		{
			foreach (Location location in Location.s_allLocations)
			{
				if (location == null || !Utils.GetPrefabName(location.gameObject).StartsWith(FarmPrefix, System.StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				float radius = Mathf.Max(location.GetMaxRadius(), VillageFarmhouses.FenceRadiusMax + FenceMargin);
				if (Utils.DistanceXZ(location.transform.position, point) < radius)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether an abandoned farm stands, or will stand, within FarmGuardRange of a point, so its PC vikings
		/// take in an empty abandoned house there
		/// </summary>
		/// <remarks>
		/// The world plans where every location goes when it's made, so a farm counts before its zone is generated, and
		/// a house generated first is taken in the same as one generated after
		/// </remarks>
		/// <param name="point">The point</param>
		/// <returns>True when a farm is that close</returns>
		public static bool FarmNear(Vector3 point)
		{
			return(NearestFarm(point, out _));
		}

		/// <summary>
		/// Lists the empty abandoned houses the world plans near a farm that its PC vikings take in - those whose
		/// nearest farm it is - whether or not they've been generated yet
		/// </summary>
		/// <param name="farm">Where the farm stands, its planned spot or near it</param>
		/// <param name="reach">How far from its planned spot the farm can stand, in meters</param>
		/// <returns>The houses</returns>
		public static List<VillagePaths.House> PlannedHouses(Vector3 farm, float reach)
		{
			List<VillagePaths.House> houses = new List<VillagePaths.House>();
			foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
			{
				ZoneSystem.ZoneLocation location = instance.m_location;
				string name = ((location != null) ? (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name) : null);
				if (name == null || !name.StartsWith(HousePrefix, System.StringComparison.OrdinalIgnoreCase) || Utils.DistanceXZ(instance.m_position, farm) > FarmGuardRange + reach)
				{
					continue;
				}
				if (NearestFarm(instance.m_position, out Vector3 nearest) && Utils.DistanceXZ(nearest, farm) <= reach && IsEmpty(location))
				{
					houses.Add(new VillagePaths.House { Center = instance.m_position, HalfSize = HouseHalfSize });
				}
			}
			return(houses);
		}

		/// <summary>
		/// Finds the planned spot of the abandoned farm nearest a point, within FarmGuardRange of it - the farm whose
		/// PC vikings take in an empty house there
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="farm">The farm's planned spot, or the point when there's none</param>
		/// <returns>False when no farm is that close</returns>
		public static bool NearestFarm(Vector3 point, out Vector3 farm)
		{
			farm = point;
			float nearest = float.MaxValue;
			foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
			{
				ZoneSystem.ZoneLocation location = instance.m_location;
				string name = ((location != null) ? (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name) : null);
				float distance = Utils.DistanceXZ(instance.m_position, point);
				if (name != null && name.StartsWith(FarmPrefix, System.StringComparison.OrdinalIgnoreCase) && distance <= FarmGuardRange && distance < nearest)
				{
					farm = instance.m_position;
					nearest = distance;
				}
			}
			return(nearest < float.MaxValue);
		}

		/// <summary>
		/// Lists where every farm the world was planned with stands - generated or not yet - from the world's plan of its
		/// locations, which only the server has
		/// </summary>
		/// <returns>The farms' planned spots</returns>
		public static List<Vector3> PlannedFarms()
		{
			List<Vector3> farms = new List<Vector3>();
			if (ZoneSystem.instance == null)
			{
				return(farms);
			}
			foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
			{
				if (IsFarm(instance.m_location))
				{
					farms.Add(instance.m_position);
				}
			}
			return(farms);
		}

		/// <summary>
		/// Checks whether a location is one of the game's abandoned farms
		/// </summary>
		/// <param name="location">The location, or null</param>
		/// <returns>True when it is</returns>
		public static bool IsFarm(ZoneSystem.ZoneLocation location)
		{
			string name = ((location != null) ? (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name) : null);
			return(name != null && name.StartsWith(FarmPrefix, System.StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Adds the empty abandoned houses within FarmGuardRange of a farm, which its PC vikings have taken in, to a
		/// village's houses
		/// </summary>
		/// <param name="farm">Where the farm stands</param>
		/// <param name="houses">The village's houses, added to</param>
		/// <returns>False when a house near the farm hasn't been generated yet, so the village isn't finished</returns>
		public static bool FindOutposts(Vector3 farm, List<VillagePaths.House> houses)
		{
			List<VillagePaths.House> found = new List<VillagePaths.House>();
			foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
			{
				ZoneSystem.ZoneLocation location = instance.m_location;
				string name = ((location != null) ? (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name) : null);
				if (name == null || !name.StartsWith(HousePrefix, System.StringComparison.OrdinalIgnoreCase) || Utils.DistanceXZ(instance.m_position, farm) > FarmGuardRange)
				{
					continue;
				}
				if (!instance.m_placed)
				{
					return(false);
				}
				if (IsEmpty(location))
				{
					found.Add(new VillagePaths.House { Center = instance.m_position, HalfSize = HouseHalfSize });
				}
			}
			houses.AddRange(found);
			return(true);
		}

		/// <summary>
		/// Finds a farm's own planned spot and the other abandoned farms within FarmLinkRange of it, whose villages a
		/// path joins it to
		/// </summary>
		/// <param name="farm">Where the farm's generator stands, near its planned spot</param>
		/// <param name="reach">How far from its planned spot the generator can stand</param>
		/// <param name="self">The farm's planned spot</param>
		/// <param name="farms">The other farms' planned spots</param>
		/// <returns>False when one of them hasn't been generated yet</returns>
		public static bool FindNearbyFarms(Vector3 farm, float reach, out Vector3 self, out List<Vector3> farms)
		{
			List<PlannedFarm> planned = new List<PlannedFarm>();
			foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
			{
				ZoneSystem.ZoneLocation location = instance.m_location;
				string name = ((location != null) ? (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name) : null);
				if (name != null && name.StartsWith(FarmPrefix, System.StringComparison.OrdinalIgnoreCase))
				{
					planned.Add(new PlannedFarm { Position = instance.m_position, Placed = instance.m_placed });
				}
			}
			return(NearbyFarms(farm, reach, planned, out self, out farms));
		}

		/// <summary>
		/// Picks a farm's own planned spot and the other farms within FarmLinkRange of it, looking among the farms the
		/// world plans within FarmLinkRange and the generator's reach of where its generator stands - its own being the one
		/// nearest the generator
		/// </summary>
		/// <param name="farm">Where the farm's generator stands, near its planned spot</param>
		/// <param name="reach">How far from its planned spot the generator can stand</param>
		/// <param name="planned">The farms the world plans</param>
		/// <param name="self">The farm's planned spot, or where its generator stands when no farm is near enough</param>
		/// <param name="farms">The other farms' planned spots</param>
		/// <returns>False when one of the farms looked among hasn't been generated yet</returns>
		internal static bool NearbyFarms(Vector3 farm, float reach, List<PlannedFarm> planned, out Vector3 self, out List<Vector3> farms)
		{
			farms = new List<Vector3>();
			self = farm;
			List<Vector3> near = new List<Vector3>();
			float nearest = float.MaxValue;
			foreach (PlannedFarm candidate in planned)
			{
				float distance = Utils.DistanceXZ(candidate.Position, farm);
				if (distance > FarmLinkRange + reach)
				{
					continue;
				}
				if (!candidate.Placed)
				{
					return(false);
				}
				if (distance < nearest)
				{
					nearest = distance;
					self = candidate.Position;
				}
				near.Add(candidate.Position);
			}
			foreach (Vector3 spot in near)
			{
				float distance = Utils.DistanceXZ(spot, self);
				if (distance > 0.5f && distance <= FarmLinkRange)
				{
					farms.Add(spot);
				}
			}
			return(true);
		}

		/// <summary>
		/// Checks whether a PC viking is within FarmKeeperRange of a point
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when one is</returns>
		public static bool PCVikingNear(Vector3 point)
		{
			List<Character> nearby = new List<Character>();
			Character.GetCharactersInRange(point, FarmKeeperRange, nearby);
			return(nearby.Any((Character character) => !character.IsDead() && VikingCore.IsPCViking(character)));
		}

		/// <summary>
		/// Forgets the farmhouses of the last location placed as a new one starts, and notes whether it's a farm, or an
		/// empty house within FarmGuardRange of one, being generated for the first time
		/// </summary>
		/// <param name="location">The location being placed</param>
		/// <param name="position">Where it's being placed</param>
		/// <param name="mode">How it's being placed</param>
		public static void OnLocationStarting(ZoneSystem.ZoneLocation location, Vector3 position, ZoneSystem.SpawnMode mode)
		{
			if (mode == ZoneSystem.SpawnMode.Client)
			{
				clientDepth++;
				return;
			}
			farmhouses.Clear();
			hives.Clear();
			generatingFarm = false;
			generatingOutpost = false;
			string name = ((location != null) ? (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name) : null);
			if (name == null)
			{
				return;
			}
			generatingFarm = name.StartsWith(FarmPrefix, System.StringComparison.OrdinalIgnoreCase);
			generatingOutpost = (name.StartsWith(HousePrefix, System.StringComparison.OrdinalIgnoreCase) && FarmNear(position) && IsEmpty(location));
		}

		/// <summary>
		/// Forgets what was noted as a location started being placed, when placing it threw
		/// </summary>
		/// <param name="mode">How it was being placed</param>
		public static void OnLocationFailed(ZoneSystem.SpawnMode mode)
		{
			if (mode == ZoneSystem.SpawnMode.Client)
			{
				clientDepth = Mathf.Max(0, clientDepth - 1);
				return;
			}
			farmhouses.Clear();
			hives.Clear();
			generatingFarm = false;
			generatingOutpost = false;
		}

		/// <summary>
		/// For a farm or house its PC vikings keep in repair, puts back a building piece it left out by chance, like
		/// a roof tile or a door, clears the vines it's overgrown with, and takes out the wild beehive, noting where it
		/// would have hung so a beehive piece can stand outside instead - its other furnishings are still left to chance
		/// </summary>
		/// <remarks>
		/// The chance is still rolled, so the rest of the location's random choices come out as they would have
		/// </remarks>
		/// <param name="spawn">The piece's random spawn, just rolled</param>
		/// <param name="position">Where the piece stands in the world</param>
		public static void OnRandomized(RandomSpawn spawn, Vector3 position)
		{
			if ((!generatingFarm && !generatingOutpost) || clientDepth > 0)
			{
				return;
			}
			string name = spawn.gameObject.name;
			if (name.StartsWith(WildHivePrefix, System.StringComparison.OrdinalIgnoreCase))
			{
				if (spawn.gameObject.activeSelf)
				{
					hives.Add(position);
				}
				SetSpawned(spawn, false);
			}
			else if (name.StartsWith(VinesPrefix, System.StringComparison.OrdinalIgnoreCase))
			{
				SetSpawned(spawn, false);
			}
			else if ((name.StartsWith(BuildingPiecePrefix, System.StringComparison.OrdinalIgnoreCase) || name.StartsWith(RoofGroupPrefix, System.StringComparison.OrdinalIgnoreCase)) && !spawn.gameObject.activeSelf)
			{
				SetSpawned(spawn, true);
			}
		}

		/// <summary>
		/// Shows or leaves out a randomly spawned piece and its networked parts, overriding its roll
		/// </summary>
		/// <param name="spawn">The piece's random spawn</param>
		/// <param name="spawned">True to show it</param>
		private static void SetSpawned(RandomSpawn spawn, bool spawned)
		{
			spawn.gameObject.SetActive(spawned);
			if (spawn.m_childNetViews != null)
			{
				foreach (ZNetView child in spawn.m_childNetViews)
				{
					child.gameObject.SetActive(spawned);
				}
			}
			if (spawn.m_OffObject != null)
			{
				spawn.m_OffObject.SetActive(!spawned);
			}
		}

		/// <summary>
		/// Keeps the pieces of a farm, or a house its PC vikings have taken in, from starting out damaged as it's
		/// generated, since the vikings have kept it in repair
		/// </summary>
		public static void OnPieceAwake()
		{
			if ((generatingFarm || generatingOutpost) && clientDepth == 0)
			{
				WearNTear.m_randomInitialDamage = false;
			}
		}

		/// <summary>
		/// Makes one of the game's treasure chests that a farm, or a house its PC vikings take in, is generated with
		/// one of the village's own chests - starting empty, as a chest built as a piece does, and no longer counted as a
		/// treasure chest (see VillageLife.IsTreasureChest) - so its vikings keep their things in it. A buried one, which
		/// the vikings can't reach and the wishbone finds, stays the world's
		/// </summary>
		/// <remarks>
		/// It's called as the chest wakes, before it fills itself with its loot, which it does only while that hasn't
		/// been noted as done
		/// </remarks>
		/// <param name="chest">The chest, waking</param>
		public static void OnChestAwake(Container chest)
		{
			if ((!generatingFarm && !generatingOutpost) || clientDepth > 0 || chest.m_rootObjectOverride != null || !VillageLife.HasDefaultLoot(chest) || chest.GetComponent<Beacon>() != null)
			{
				return;
			}
			ZNetView view = chest.GetComponent<ZNetView>();
			ZDO zdo = ((view != null) ? view.GetZDO() : null);
			if (zdo == null)
			{
				return;
			}
			zdo.Set(ZDOVars.s_addedDefaultItems, true);
			zdo.Set(VillageLife.OwnChestKey, true);
		}

		/// <summary>
		/// Notes a house built in an abandoned farm being generated as one of its farmhouses, for PC vikings to live
		/// in
		/// </summary>
		/// <param name="center">Where the house's middle stands</param>
		/// <param name="halfSize">Half its size, in meters</param>
		public static void AddFarmhouse(Vector3 center, float halfSize)
		{
			if (clientDepth > 0)
			{
				return;
			}
			Farmhouse house = new Farmhouse();
			house.Center = center;
			house.HalfSize = halfSize;
			farmhouses.Add(house);
		}

		/// <summary>
		/// Notes where an abandoned farm's farmhouses stand as its rooms are generated, for PC vikings to live in
		/// </summary>
		/// <param name="rooms">The rooms just placed</param>
		public static void OnRoomsGenerated(List<Room> rooms)
		{
			if (clientDepth > 0)
			{
				return;
			}
			foreach (Room room in rooms)
			{
				if (room != null && room.gameObject.name.StartsWith(FarmhousePrefix, System.StringComparison.OrdinalIgnoreCase))
				{
					Farmhouse house = new Farmhouse();
					house.Center = room.transform.position;
					house.HalfSize = Mathf.Max(room.m_size.x, room.m_size.z) / 2f;
					farmhouses.Add(house);
				}
			}
		}

		/// <summary>
		/// Notes an abandoned farm's farmhouses for PC vikings to come to as the location is generated - which
		/// happens once, the first time its zone is - and fixes up an empty house near a farm for the farm's vikings
		/// </summary>
		/// <param name="location">The location just placed</param>
		/// <param name="position">Where it stands</param>
		/// <param name="mode">How it was placed - in full or as a ghost being saved without being shown, or for a client
		/// showing a location that's already there</param>
		/// <param name="ghosts">The objects of a ghost placement, which the game saves and removes</param>
		public static void OnLocationSpawned(ZoneSystem.ZoneLocation location, Vector3 position, ZoneSystem.SpawnMode mode, List<GameObject> ghosts)
		{
			if (mode == ZoneSystem.SpawnMode.Client)
			{
				clientDepth = Mathf.Max(0, clientDepth - 1);
				return;
			}
			List<Farmhouse> houses = new List<Farmhouse>(farmhouses);
			farmhouses.Clear();
			List<Vector3> hiveSpots = new List<Vector3>(hives);
			hives.Clear();
			bool farm = generatingFarm;
			bool outpost = generatingOutpost;
			generatingFarm = false;
			generatingOutpost = false;
			if (outpost)
			{
				Farmhouse house = new Farmhouse();
				house.Center = position;
				house.HalfSize = HouseHalfSize;
				houses.Add(house);
			}
			if (farm && houses.Count == 0)
			{
				Plugin.Log.LogWarning("Found no farmhouses for PC vikings in " + location.m_prefabName + " at " + position.ToString("F0"));
			}
			if (location == null || ZNetScene.instance == null || (!farm && !outpost) || houses.Count == 0)
			{
				return;
			}
			string name = (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name);
			List<VillagePaths.House> villageHouses = houses.Select((Farmhouse house) => new VillagePaths.House { Center = house.Center, HalfSize = house.HalfSize }).ToList();
			foreach (Vector3 hive in hiveSpots)
			{
				PlaceHive(hive, houses, mode, ghosts);
			}
			// No vikings come as the farm is generated - only once its bonfire has been built, which needs everything
			// around the farm loaded to find room for it (see VillageRespawn.Populated)
			if (farm)
			{
				VillagePaths.RecordVillage(position, villageHouses);
				VillageRespawn.MarkPlanned(position);
			}
			Plugin.Log.LogInfo((farm ? "Farm for PC vikings: " : "Fixed up for PC vikings: ") + name + " at " + position.ToString("F0"));
		}

		/// <summary>
		/// Puts a beehive piece on the ground outside the farmhouse a wild beehive would have hung in, past the wall it
		/// hung by and facing away from the house
		/// </summary>
		/// <param name="hive">Where the wild beehive would have hung</param>
		/// <param name="houses">The farm's farmhouses</param>
		/// <param name="mode">How the farm was placed</param>
		/// <param name="ghosts">The objects of a ghost placement, which the game saves and removes</param>
		private static void PlaceHive(Vector3 hive, List<Farmhouse> houses, ZoneSystem.SpawnMode mode, List<GameObject> ghosts)
		{
			GameObject piece = ZNetScene.instance.GetPrefab(HivePiece);
			if (piece == null || houses.Count == 0)
			{
				return;
			}
			Vector3 point = HiveSpot(hive, houses, out Vector3 outward);
			point.y = ZoneSystem.instance.GetGroundHeight(point);
			if (mode == ZoneSystem.SpawnMode.Ghost)
			{
				ZNetView.StartGhostInit();
			}
			GameObject instance = Object.Instantiate(piece, point, Quaternion.LookRotation(outward));
			VillageRecords.MarkBuilt(instance);
			if (mode == ZoneSystem.SpawnMode.Ghost)
			{
				ghosts.Add(instance);
				ZNetView.FinishGhostInit();
			}
		}

		/// <summary>
		/// Works out where a beehive piece stands for a wild beehive - HiveClearance outside the walls of the farmhouse
		/// nearest it, on the side it hung by, or along the world's forward axis when it hung right over its middle
		/// </summary>
		/// <param name="hive">Where the wild beehive would have hung</param>
		/// <param name="houses">The farm's farmhouses, at least one</param>
		/// <param name="outward">The way from the farmhouse's middle to the spot, flat on the ground</param>
		/// <returns>The spot, at the height of the farmhouse's middle</returns>
		internal static Vector3 HiveSpot(Vector3 hive, List<Farmhouse> houses, out Vector3 outward)
		{
			Farmhouse house = houses.OrderBy((Farmhouse candidate) => Utils.DistanceXZ(candidate.Center, hive)).First();
			outward = hive - house.Center;
			outward.y = 0f;
			if (outward.sqrMagnitude < 0.01f)
			{
				outward = Vector3.forward;
			}
			outward.Normalize();
			return(house.Center + outward * (house.HalfSize + HiveClearance));
		}

		/// <summary>
		/// Checks whether a location has no creature spawners or spawn areas of its own
		/// </summary>
		/// <param name="location">The location</param>
		/// <returns>True when it's empty</returns>
		private static bool IsEmpty(ZoneSystem.ZoneLocation location)
		{
			location.m_prefab.Load();
			GameObject asset = location.m_prefab.Asset;
			bool empty = (asset != null && asset.GetComponentsInChildren<CreatureSpawner>(true).Length == 0 && asset.GetComponentsInChildren<SpawnArea>(true).Length == 0);
			location.m_prefab.Release();
			return(empty);
		}
	}
}
