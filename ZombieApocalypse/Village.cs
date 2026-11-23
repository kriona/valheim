using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// A village of living vikings - an abandoned farm's farmhouses and the empty houses near it they've taken in, read
	/// from the farm's generator's ZDO, which also holds what the village keeps track of - or a village a player started
	/// at a bonfire of theirs, with the buildings its beds brought in, read from its anchor's ZDO the same way (see
	/// VillageFounding)
	/// </summary>
	/// <remarks>
	/// A village is known by where its generator stands, which each of its vikings notes in its ZDO along with the
	/// house it calls home. Only villages whose generator is loaded are found. Its core is its farmhouses and the houses
	/// close by them, where its vikings live and keep to at night - the rest are outposts, standing off on their own,
	/// which the vikings only go out to by day (see Core)
	/// </remarks>
	internal class Village
	{
		/// <summary>
		/// On a living viking, where its village's generator stands and the middle of the house it calls home
		/// </summary>
		public static readonly int VillageKey = "kriona.ZombieApocalypse.vikingVillage".GetStableHashCode();
		public static readonly int HomeKey = "kriona.ZombieApocalypse.vikingHome".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, every house of its village - its farmhouses and the houses near it the vikings have
		/// taken in - once they're known, written the same way as its farmhouses
		/// </summary>
		public static readonly int AllHousesKey = "kriona.ZombieApocalypse.villageAllHouses".GetStableHashCode();

		/// <summary>
		/// How far a village reaches past each of its houses' walls and around each of its pieces, in meters
		/// </summary>
		private const float Margin = 8f;

		/// <summary>
		/// How far a village reaches either side of the line a path runs between two of its houses, how far apart the
		/// circles laying that band out stand along it, and how near its pieces have to be to each other to share a
		/// circle, in meters
		/// </summary>
		public const float PathMargin = 4f;
		public const float PathStep = 3f;
		private const float PieceMerge = 3f;

		/// <summary>
		/// How far around its fire a village a player started reaches before Margin, as though the fire were a house of
		/// that half size, in meters
		/// </summary>
		private const float FoundedFireReach = 3f;

		/// <summary>
		/// How far past a farm's fence its core reaches, so the fence itself is in it, in meters
		/// </summary>
		private const float FenceMargin = 2f;

		/// <summary>
		/// How near a house's walls have to be to the walls of a house in the village's core for it to be in the core
		/// too, rather than an outpost, and how near two points have to be to be the same house's middle, in meters
		/// </summary>
		public const float NeighborGap = 15f;
		private const float HouseMatch = 1f;

		/// <summary>
		/// How far outside a village a point can be and still belong to it, for finding a viking's village, in meters
		/// </summary>
		public const float FindReach = 60f;

		/// <summary>
		/// How far past the village's edge a building piece no player built still belongs to the village, like a pen's
		/// fence, in meters
		/// </summary>
		public const float PieceMargin = 12f;

		/// <summary>
		/// How far from the spot a viking notes as its village the village's generator can stand, in meters - a viking
		/// spawned as its village is generated notes the farm's planned spot, which the generator stands near
		/// </summary>
		private const float FarmReach = 16f;

		/// <summary>
		/// How far from a house's walls an open spot keeps, how far from a random point one can be found, and how far
		/// past the flames of a fire one keeps, in meters
		/// </summary>
		private const float HouseClearance = 1.5f;
		private const float SpotSnap = 2f;
		private const float FireClearance = 1.5f;

		/// <summary>
		/// How far from the village's fire its anchor stands, in meters, and how many spots around the fire are tried for
		/// one that isn't in a pen
		/// </summary>
		private const float AnchorFromFire = 4f;
		private const int AnchorSides = 8;

		/// <summary>
		/// How many random points are tried for an open spot
		/// </summary>
		private const int SpotTries = 30;

		/// <summary>
		/// How far past a house's walls a living viking spawns or comes back at most, in meters
		/// </summary>
		public const float SpawnReach = 6f;

		/// <summary>
		/// How far past the farm's fence a viking coming to the village starts, in meters - past the cleared ground
		/// around it, among the trees
		/// </summary>
		private const float ArrivalMin = 8f;
		private const float ArrivalMax = 16f;

		/// <summary>
		/// Seconds between looks for the villages that have loaded or unloaded
		/// </summary>
		private const float RefreshInterval = 2f;

		/// <summary>
		/// How a spot is judged fenced in, like a pen - how many level rays are cast around it at what height, how far
		/// they reach, in meters, and how many have to hit something
		/// </summary>
		private const int EnclosureRays = 8;
		private const float EnclosureHeight = 0.5f;
		private const float EnclosureReach = 8f;
		private const int EnclosureHits = 7;

		/// <summary>
		/// How high above a fenced-in spot a roof is looked for from, and how far up, in meters - a spot with a roof is in
		/// a house rather than a pen
		/// </summary>
		private const float EnclosureRoofFrom = 1f;
		private const float EnclosureRoofReach = 8f;

		/// <summary>
		/// How far out from a door its doorstep is, and how far past a house's walls its doors can stand, in meters
		/// </summary>
		private const float DoorstepDistance = 2f;
		private const float DoorReach = 3f;

		/// <summary>
		/// The layers of what fences a spot in - buildings, fences, trees and rocks
		/// </summary>
		private static int enclosureMask;

		private static readonly Dictionary<ZDOID, Village> loaded = new Dictionary<ZDOID, Village>();
		private static float nextRefresh;

		/// <summary>
		/// The farm's generator
		/// </summary>
		public ZDO Zdo;
		public ZNetView View;

		/// <summary>
		/// Where the generator stands, which the village's vikings note as their village
		/// </summary>
		public Vector3 Farm;

		/// <summary>
		/// The village's houses
		/// </summary>
		public List<VillagePaths.House> Houses = new List<VillagePaths.House>();

		/// <summary>
		/// The middle of its houses, and how far from it the farthest of its zones reaches - a circle around every part of
		/// it, for looking through what's around it, rather than for telling what's in it (see Zones)
		/// </summary>
		public Vector3 Middle;
		public float Radius;

		/// <summary>
		/// A part of the ground a village reaches over - a circle around a farm's fenced ground, a house, a few of its
		/// pieces or a stretch of a path - its middle and how far from it it reaches
		/// </summary>
		public struct Zone
		{
			public Vector3 Center;
			public float Radius;
		}

		/// <summary>
		/// The parts of the ground the village reaches over (see MakeZones), the largest first - a point in any of them
		/// is in the village, so it reaches over what it has built and the paths between, rather than the empty ground
		/// around them - and those of them around what it has built, before its paths
		/// </summary>
		public List<Zone> Zones = new List<Zone>();
		public List<Zone> BaseZones = new List<Zone>();

		/// <summary>
		/// The zones along the village's paths as last traced on the ground (see VillageGroundPaths), or null before
		/// they have been
		/// </summary>
		private List<Zone> groundPaths;

		/// <summary>
		/// The houses of the village's core, where its vikings live and keep to at night - its farmhouses and the houses
		/// close by them, leaving out the outposts standing off on their own - with their middle and how far from it
		/// they reach
		/// </summary>
		public List<VillagePaths.House> CoreHouses = new List<VillagePaths.House>();
		public Vector3 CoreMiddle;
		public float CoreRadius;

		/// <summary>
		/// The generator's data revision the houses were last read at
		/// </summary>
		private uint readRevision = uint.MaxValue;

		/// <summary>
		/// The village's fire as last found, and when to look for it again
		/// </summary>
		private Fireplace fire;
		private float nextFireCheck;

		/// <summary>
		/// On a fire in a village, that a player made it one of the village's gathering fires (see VillageArea)
		/// </summary>
		public static readonly int GatherFireKey = "kriona.ZombieApocalypse.gatherFire".GetStableHashCode();

		/// <summary>
		/// The village's fires its vikings gather at as last found - its own fire first - and when to look for them
		/// again
		/// </summary>
		private List<Fireplace> gatherFires = new List<Fireplace>();
		private float nextGatherCheck;

		/// <summary>
		/// On a farm's generator, its village's fire - its prefab, where it stands and which way it faces - so it can be
		/// built back when it's destroyed
		/// </summary>
		public static readonly int FireKey = "kriona.ZombieApocalypse.villageFire".GetStableHashCode();

		/// <summary>
		/// How far from the village's middle its fire can stand, in meters - as far as the paths look for open ground
		/// for it
		/// </summary>
		private const float FireReach = 30f;

		/// <summary>
		/// How near where the village's destroyed fire stood a bonfire or campfire someone built has to be for the
		/// village to take it up as its fire, in meters
		/// </summary>
		private const float AdoptRange = 6f;

		/// <summary>
		/// Seconds between looks for the village's fire
		/// </summary>
		private const float FireCheckInterval = 3f;

		/// <summary>
		/// Lists the villages whose generators are loaded
		/// </summary>
		/// <returns>The villages</returns>
		public static IEnumerable<Village> Loaded()
		{
			Refresh();
			return(loaded.Values);
		}

		/// <summary>
		/// Finds the loaded village a point belongs to - the nearest one it's within FindReach of
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The village, or null</returns>
		public static Village Find(Vector3 point)
		{
			Village nearest = null;
			float nearestOutside = float.MaxValue;
			foreach (Village village in Loaded())
			{
				float outside = village.DistanceOutside(point);
				if (outside <= FindReach && outside < nearestOutside)
				{
					nearest = village;
					nearestOutside = outside;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Finds the loaded village whose generator stands at a point
		/// </summary>
		/// <param name="farm">Where its generator stands, or the farm's planned spot</param>
		/// <returns>The village, or null when it isn't loaded</returns>
		public static Village ForFarm(Vector3 farm)
		{
			foreach (Village village in Loaded())
			{
				if (Utils.DistanceXZ(village.Farm, farm) <= FarmReach)
				{
					return(village);
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds the village a living viking belongs to, by what its ZDO notes
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The village, or null when the viking has none or it isn't loaded</returns>
		public static Village Of(ZDO viking)
		{
			if (viking == null)
			{
				return(null);
			}
			Vector3 farm = viking.GetVec3(VillageKey, Vector3.zero);
			return((farm == Vector3.zero) ? null : ForFarm(farm));
		}

		/// <summary>
		/// Forgets a village whose anchor was just taken away, rather than waiting for the next look for the loaded
		/// villages
		/// </summary>
		/// <param name="village">The village</param>
		public static void Drop(Village village)
		{
			loaded.Remove(village.Zdo.m_uid);
		}

		/// <summary>
		/// Notes a living viking as belonging to a village, calling one of its houses home
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		/// <param name="house">The house it calls home</param>
		public void Adopt(ZDO viking, VillagePaths.House house)
		{
			viking.Set(VillageKey, Farm);
			viking.Set(HomeKey, house.Center);
			// One coming in with a name it was given that another viking here goes by gets a byname after it - a new
			// viking, not rolled yet, is named as it's rolled (see VikingCore.Generate)
			VikingData data = VikingData.FromBytes(viking.GetByteArray(VikingCore.DataKey));
			if (data != null)
			{
				VikingNames.GiveName(viking, data, true);
			}
		}

		/// <summary>
		/// Notes every house of a village in its generator, once the houses the vikings have taken in are known - leaving
		/// the generator as it is when it already notes them, since a new array always counts as a change and the ZDO
		/// would be sent again
		/// </summary>
		/// <param name="generator">The generator's ZDO</param>
		/// <param name="houses">The village's houses</param>
		public static void RecordHouses(ZDO generator, List<VillagePaths.House> houses)
		{
			byte[] bytes = WriteHouses(houses);
			byte[] old = generator.GetByteArray(AllHousesKey);
			if (old != null && System.Linq.Enumerable.SequenceEqual(old, bytes))
			{
				return;
			}
			generator.Set(AllHousesKey, bytes);
		}

		/// <summary>
		/// Writes a list of houses the way a generator notes them - how many there are, then each one's middle and half
		/// size - which VillagePaths.ReadHouses reads back
		/// </summary>
		/// <param name="houses">The houses</param>
		/// <returns>The houses, written</returns>
		internal static byte[] WriteHouses(List<VillagePaths.House> houses)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(houses.Count);
			foreach (VillagePaths.House house in houses)
			{
				pkg.Write(house.Center);
				pkg.Write(house.HalfSize);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Finds the loaded villages again, every RefreshInterval seconds, reading each one's houses when they've changed
		/// </summary>
		private static void Refresh()
		{
			if (Time.time < nextRefresh && loaded.Count > 0)
			{
				return;
			}
			nextRefresh = Time.time + RefreshInterval;
			if (ZNetScene.instance == null)
			{
				loaded.Clear();
				return;
			}
			HashSet<ZDOID> seen = new HashSet<ZDOID>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZDO zdo = instance.Key;
				if (zdo == null || (zdo.GetPrefab() != VillagePaths.GeneratorHash && !VillageFounding.IsAnchor(zdo.GetPrefab())) || instance.Value == null)
				{
					continue;
				}
				// A farm with no room for its bonfire stays abandoned
				if (zdo.GetByteArray(VillagePaths.HousesKey) == null || zdo.GetBool(VillagePaths.AbandonedKey))
				{
					continue;
				}
				if (!loaded.TryGetValue(zdo.m_uid, out Village village))
				{
					village = new Village();
					loaded[zdo.m_uid] = village;
				}
				village.Zdo = zdo;
				village.View = instance.Value;
				village.Read();
				seen.Add(zdo.m_uid);
			}
			List<ZDOID> gone = new List<ZDOID>();
			foreach (ZDOID id in loaded.Keys)
			{
				if (!seen.Contains(id))
				{
					gone.Add(id);
				}
			}
			foreach (ZDOID id in gone)
			{
				loaded.Remove(id);
			}
		}

		/// <summary>
		/// Reads the village's houses from its generator again right away, for houses just noted in it
		/// </summary>
		public void ReadAgain()
		{
			readRevision = uint.MaxValue;
			Read();
		}

		/// <summary>
		/// Reads the village's houses from its generator when they've changed, and works out its zones, and its middle and
		/// the reach around all of them
		/// </summary>
		private void Read()
		{
			Farm = Zdo.GetPosition();
			if (Zdo.DataRevision == readRevision)
			{
				return;
			}
			readRevision = Zdo.DataRevision;
			byte[] farmhouses = Zdo.GetByteArray(VillagePaths.HousesKey);
			byte[] bytes = Zdo.GetByteArray(AllHousesKey) ?? farmhouses;
			Houses = VillagePaths.ReadHouses(bytes);
			CoreHouses = Core(VillagePaths.ReadHouses(farmhouses), Houses);
			// A village a player started reaches over its fire, where its anchor stands, as well as its houses - which
			// may stand off to one side of it, or not be built yet
			List<VillagePaths.House> reached = new List<VillagePaths.House>(Houses);
			List<VillagePaths.House> coreReached = new List<VillagePaths.House>(CoreHouses);
			VillagePaths.House fireSpot = new VillagePaths.House { Center = Farm, HalfSize = FoundedFireReach };
			if (IsFounded())
			{
				reached.Add(fireSpot);
				coreReached.Add(fireSpot);
			}
			Reach(reached, Farm, out Middle, out float _);
			Reach(coreReached, Middle, out CoreMiddle, out CoreRadius);
			Zones = MakeZones(fireSpot);
			Radius = 0f;
			foreach (Zone zone in Zones)
			{
				Radius = Mathf.Max(Radius, Utils.DistanceXZ(zone.Center, Middle) + zone.Radius);
			}
		}

		/// <summary>
		/// Works out the village's zones - a farm's fenced ground whole, out FenceMargin past its fence when the farm
		/// noted how far its fence stands as it was generated, with its pens and fields; Margin past each house's walls,
		/// and around a village a player started's fire; Margin around each of its own building pieces (see
		/// VillageRecords), a few close together sharing a circle - all of them its BaseZones; and PathMargin either side
		/// of its paths, as traced on the ground (see VillageGroundPaths), or until they've been, the lines its paths run
		/// between its houses (see VillagePaths.PathLines) - leaving out any that lie wholly inside another, the largest
		/// first
		/// </summary>
		/// <param name="fireSpot">The fire of a village a player started, as though it were a house</param>
		/// <returns>The zones, or one Margin around the generator when the village has none</returns>
		private List<Zone> MakeZones(VillagePaths.House fireSpot)
		{
			bool founded = IsFounded();
			float fence = (founded ? 0f : Zdo.GetFloat(VillageFarmhouses.FenceKey, 0f));
			List<PieceRecord> pieces = VillageRecords.Find(this, (PieceRecord record) => true);
			List<Zone> paths = groundPaths;
			if (paths == null)
			{
				paths = new List<Zone>();
				foreach (KeyValuePair<Vector3, Vector3> line in VillagePaths.PathLines(this))
				{
					paths.AddRange(BandZones(line.Key, line.Value, PathMargin, PathStep));
				}
			}
			List<Zone> zones = MakeZones(Farm, fence, (founded ? fireSpot : (VillagePaths.House?)null), Houses, pieces, paths, out List<Zone> baseZones);
			BaseZones = baseZones;
			return(zones);
		}

		/// <summary>
		/// Works out a village's zones from what it has - FenceMargin past its fence, Margin past its fire and each
		/// house's walls and around its pieces, which are its base zones, and its paths' zones - leaving out any that lie
		/// wholly inside another, the largest first
		/// </summary>
		/// <param name="farm">Where its generator stands</param>
		/// <param name="fence">How far its fence stands from the generator, or 0 when it has none noted</param>
		/// <param name="fireSpot">The fire of a village a player started, as though it were a house, or null for a
		/// farm's</param>
		/// <param name="houses">Its houses</param>
		/// <param name="pieces">Its own building pieces</param>
		/// <param name="paths">The zones along its paths</param>
		/// <param name="baseZones">The zones around what it has built, leaving out its paths</param>
		/// <returns>The zones, or one Margin around the generator when the village has none</returns>
		internal static List<Zone> MakeZones(Vector3 farm, float fence, VillagePaths.House? fireSpot, List<VillagePaths.House> houses, List<PieceRecord> pieces, List<Zone> paths, out List<Zone> baseZones)
		{
			List<Zone> zones = new List<Zone>();
			if (fence > 0f)
			{
				zones.Add(new Zone { Center = farm, Radius = fence + FenceMargin });
			}
			if (fireSpot.HasValue)
			{
				zones.Add(new Zone { Center = fireSpot.Value.Center, Radius = fireSpot.Value.HalfSize + Margin });
			}
			foreach (VillagePaths.House house in houses)
			{
				zones.Add(new Zone { Center = house.Center, Radius = house.HalfSize + Margin });
			}
			zones.AddRange(PieceZones(pieces));
			baseZones = Outermost(zones);
			zones.AddRange(paths);
			zones = Outermost(zones);
			if (zones.Count == 0)
			{
				zones.Add(new Zone { Center = farm, Radius = Margin });
			}
			return(zones);
		}

		/// <summary>
		/// Takes the zones along the village's paths as traced on the ground (see VillageGroundPaths), working its zones
		/// out again when they've changed
		/// </summary>
		/// <param name="paths">The zones along its paths</param>
		public void SetGroundPaths(List<Zone> paths)
		{
			if (groundPaths != null && SameZones(groundPaths, paths))
			{
				return;
			}
			groundPaths = paths;
			ReadAgain();
		}

		/// <summary>
		/// Checks whether two lists of zones are the same, to within a meter
		/// </summary>
		/// <param name="a">One list</param>
		/// <param name="b">The other</param>
		/// <returns>True when they are</returns>
		internal static bool SameZones(List<Zone> a, List<Zone> b)
		{
			if (a.Count != b.Count)
			{
				return(false);
			}
			for (int i = 0; i < a.Count; i++)
			{
				if (FlatDistance(a[i].Center, b[i].Center) > 1f || Mathf.Abs(a[i].Radius - b[i].Radius) > 1f)
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Works out the zones around a village's pieces - Margin around each, with the pieces within PieceMerge of the
		/// first of them sharing its circle, made wide enough to reach Margin around every one of them
		/// </summary>
		/// <param name="pieces">The pieces</param>
		/// <returns>The zones</returns>
		private static List<Zone> PieceZones(List<PieceRecord> pieces)
		{
			List<Zone> zones = new List<Zone>();
			foreach (PieceRecord piece in pieces)
			{
				int shared = zones.FindIndex((Zone zone) => FlatDistance(zone.Center, piece.Position) <= PieceMerge);
				if (shared < 0)
				{
					zones.Add(new Zone { Center = piece.Position, Radius = Margin });
					continue;
				}
				Zone grown = zones[shared];
				grown.Radius = Mathf.Max(grown.Radius, FlatDistance(grown.Center, piece.Position) + Margin);
				zones[shared] = grown;
			}
			return(zones);
		}

		/// <summary>
		/// Lays a band either side of a line out as circles along it
		/// </summary>
		/// <param name="from">One end</param>
		/// <param name="to">The other end</param>
		/// <param name="reach">How far the band reaches either side of the line, in meters</param>
		/// <param name="step">How far apart the circles stand, in meters - less than reach, so the band has no gaps</param>
		/// <returns>The circles, one at each end and as many as it takes between</returns>
		internal static List<Zone> BandZones(Vector3 from, Vector3 to, float reach, float step)
		{
			List<Zone> zones = new List<Zone>();
			float length = FlatDistance(from, to);
			int steps = Mathf.Max(1, Mathf.CeilToInt(length / step));
			for (int i = 0; i <= steps; i++)
			{
				zones.Add(new Zone { Center = Vector3.Lerp(from, to, i / (float)steps), Radius = reach });
			}
			return(zones);
		}

		/// <summary>
		/// Leaves out the zones that lie wholly inside another, so the same ground isn't covered twice - keeping the
		/// largest, which go first
		/// </summary>
		/// <param name="zones">The zones</param>
		/// <returns>The zones left, the largest first</returns>
		internal static List<Zone> Outermost(List<Zone> zones)
		{
			List<Zone> sorted = new List<Zone>(zones);
			sorted.Sort((Zone a, Zone b) => b.Radius.CompareTo(a.Radius));
			List<Zone> kept = new List<Zone>();
			foreach (Zone zone in sorted)
			{
				if (!kept.Exists((Zone outer) => FlatDistance(outer.Center, zone.Center) + zone.Radius <= outer.Radius))
				{
					kept.Add(zone);
				}
			}
			return(kept);
		}

		/// <summary>
		/// Works out how far apart two points are across the ground, leaving out how high each is
		/// </summary>
		/// <param name="a">One point</param>
		/// <param name="b">The other</param>
		/// <returns>The distance, in meters</returns>
		internal static float FlatDistance(Vector3 a, Vector3 b)
		{
			float x = a.x - b.x;
			float z = a.z - b.z;
			return(Mathf.Sqrt(x * x + z * z));
		}

		/// <summary>
		/// Moves each house that stands within NeighborGap of a house of a group, wall to wall, into the group, until no
		/// more do
		/// </summary>
		/// <param name="group">The group, added to</param>
		/// <param name="rest">The houses not in it, taken from</param>
		private static void Gather(List<VillagePaths.House> group, List<VillagePaths.House> rest)
		{
			bool grew = true;
			while (grew)
			{
				grew = false;
				for (int i = rest.Count - 1; i >= 0; i--)
				{
					VillagePaths.House house = rest[i];
					if (group.Exists((VillagePaths.House inner) => Gap(inner, house) <= NeighborGap))
					{
						group.Add(house);
						rest.RemoveAt(i);
						grew = true;
					}
				}
			}
		}

		/// <summary>
		/// Checks whether a player started the village at a bonfire of theirs, rather than it being a farm's (see
		/// VillageFounding)
		/// </summary>
		/// <returns>True when they did</returns>
		public bool IsFounded()
		{
			return(Zdo != null && VillageFounding.IsAnchor(Zdo.GetPrefab()));
		}

		/// <summary>
		/// Works out the middle of some houses and how far from it they reach, with Margin to spare
		/// </summary>
		/// <param name="houses">The houses</param>
		/// <param name="fallback">The middle when there are no houses</param>
		/// <param name="middle">Their middle</param>
		/// <param name="radius">How far from it they reach</param>
		internal static void Reach(List<VillagePaths.House> houses, Vector3 fallback, out Vector3 middle, out float radius)
		{
			Vector3 sum = Vector3.zero;
			foreach (VillagePaths.House house in houses)
			{
				sum += house.Center;
			}
			middle = ((houses.Count > 0) ? sum / houses.Count : fallback);
			radius = 0f;
			foreach (VillagePaths.House house in houses)
			{
				radius = Mathf.Max(radius, Utils.DistanceXZ(house.Center, middle) + house.HalfSize);
			}
			radius += Margin;
		}

		/// <summary>
		/// Works out a village's core - its farmhouses, and each house that stands within NeighborGap of a house already
		/// in the core, wall to wall - leaving out the outposts, which stand off on their own or with only each other
		/// </summary>
		/// <param name="farmhouses">The village's farmhouses</param>
		/// <param name="houses">Every house of the village, farmhouses included</param>
		/// <returns>The houses in the core - every house when there are no farmhouses among them</returns>
		public static List<VillagePaths.House> Core(List<VillagePaths.House> farmhouses, List<VillagePaths.House> houses)
		{
			List<VillagePaths.House> core = new List<VillagePaths.House>();
			List<VillagePaths.House> rest = new List<VillagePaths.House>();
			foreach (VillagePaths.House house in houses)
			{
				bool farmhouse = farmhouses.Exists((VillagePaths.House own) => Utils.DistanceXZ(own.Center, house.Center) <= HouseMatch);
				if (farmhouse)
				{
					core.Add(house);
				}
				else
				{
					rest.Add(house);
				}
			}
			if (core.Count == 0)
			{
				return(new List<VillagePaths.House>(houses));
			}
			Gather(core, rest);
			return(core);
		}

		/// <summary>
		/// Works out how far apart two houses' walls are
		/// </summary>
		/// <param name="a">One house</param>
		/// <param name="b">The other</param>
		/// <returns>The distance, or 0 when they overlap</returns>
		internal static float Gap(VillagePaths.House a, VillagePaths.House b)
		{
			return(Mathf.Max(0f, Utils.DistanceXZ(a.Center, b.Center) - a.HalfSize - b.HalfSize));
		}

		/// <summary>
		/// Checks whether a house is in the village's core, rather than an outpost - a house the vikings live in and
		/// keep to at night
		/// </summary>
		/// <param name="house">The house</param>
		/// <returns>True when it's in the core</returns>
		public bool IsCore(VillagePaths.House house)
		{
			return(CoreHouses.Exists((VillagePaths.House inner) => Utils.DistanceXZ(inner.Center, house.Center) <= HouseMatch));
		}

		/// <summary>
		/// Checks whether a point is inside one of the houses of the village's core
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="padding">How far past the walls still counts, in meters</param>
		/// <returns>True when it is</returns>
		public bool InCoreHouse(Vector3 point, float padding)
		{
			return(CoreHouses.Exists((VillagePaths.House house) => InHouse(house, point, padding)));
		}

		/// <summary>
		/// Finds the house of the village's core nearest a point
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="house">The house</param>
		/// <returns>False when the village has no houses</returns>
		public bool NearestCoreHouse(Vector3 point, out VillagePaths.House house)
		{
			house = new VillagePaths.House();
			if (CoreHouses.Count == 0)
			{
				return(false);
			}
			house = CoreHouses[0];
			foreach (VillagePaths.House candidate in CoreHouses)
			{
				if (Utils.DistanceXZ(candidate.Center, point) < Utils.DistanceXZ(house.Center, point))
				{
					house = candidate;
				}
			}
			return(true);
		}

		/// <summary>
		/// Checks whether this client owns the village's generator, so it's the one that keeps track of the village
		/// </summary>
		/// <returns>True when it does</returns>
		public bool IsOwner()
		{
			return(View != null && View.IsValid() && View.IsOwner());
		}

		/// <summary>
		/// Works out how far outside the village a point is - past the edge of the zone nearest it (see Zones)
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The distance past its edge, or 0 inside it</returns>
		public float DistanceOutside(Vector3 point)
		{
			float nearest = float.MaxValue;
			foreach (Zone zone in Zones)
			{
				nearest = Mathf.Min(nearest, Utils.DistanceXZ(point, zone.Center) - zone.Radius);
			}
			return((nearest == float.MaxValue) ? Mathf.Max(0f, Utils.DistanceXZ(point, Middle) - Radius) : Mathf.Max(0f, nearest));
		}

		/// <summary>
		/// Checks whether a point is inside the village
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when it is</returns>
		public bool Contains(Vector3 point)
		{
			// Every zone lies within Radius of Middle, so a point past that is outside without looking through them
			if (Zones.Count > 0 && FlatDistance(point, Middle) > Radius)
			{
				return(false);
			}
			return(DistanceOutside(point) <= 0f);
		}

		/// <summary>
		/// Finds the house whose middle is at a point, or else the house nearest it
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="house">The house</param>
		/// <returns>False when the village has no houses</returns>
		public bool NearestHouse(Vector3 point, out VillagePaths.House house)
		{
			house = new VillagePaths.House();
			if (Houses.Count == 0)
			{
				return(false);
			}
			house = Houses[0];
			foreach (VillagePaths.House candidate in Houses)
			{
				if (Utils.DistanceXZ(candidate.Center, point) < Utils.DistanceXZ(house.Center, point))
				{
					house = candidate;
				}
			}
			return(true);
		}

		/// <summary>
		/// Checks whether a building piece at a point belongs to the village, when no player built it - a piece of its
		/// houses, or of the fences, pens and the rest of the village around them
		/// </summary>
		/// <param name="point">Where the piece stands</param>
		/// <returns>True when it does</returns>
		public bool OwnsPiece(Vector3 point)
		{
			return(DistanceOutside(point) <= PieceMargin || InHouse(point, 1f));
		}

		/// <summary>
		/// Checks whether a point is inside one of the village's houses
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="padding">How far past the walls still counts, in meters</param>
		/// <returns>True when it is</returns>
		public bool InHouse(Vector3 point, float padding)
		{
			foreach (VillagePaths.House house in Houses)
			{
				if (InHouse(house, point, padding))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a point is inside a house's walls
		/// </summary>
		/// <param name="house">The house</param>
		/// <param name="point">The point</param>
		/// <param name="padding">How far past the walls still counts, in meters</param>
		/// <returns>True when it is</returns>
		public static bool InHouse(VillagePaths.House house, Vector3 point, float padding)
		{
			float reach = house.HalfSize + padding;
			return(Mathf.Abs(point.x - house.Center.x) <= reach && Mathf.Abs(point.z - house.Center.z) <= reach);
		}

		/// <summary>
		/// Finds the village's bonfire or campfire, looking again every FireCheckInterval seconds
		/// </summary>
		/// <returns>The fire, or null when it has none loaded</returns>
		public Fireplace Fire()
		{
			if (Time.time < nextFireCheck)
			{
				return((fire != null) ? fire : null);
			}
			nextFireCheck = Time.time + FireCheckInterval;
			fire = FindFire();
			if (fire == null && IsOwner())
			{
				fire = AdoptFire();
			}
			if (fire != null && IsOwner() && Zdo.GetByteArray(FireKey) == null)
			{
				RecordFire(fire);
			}
			return(fire);
		}

		/// <summary>
		/// Lists the fires the village's vikings gather at - its own fire first, then the loaded fires in it a player
		/// made gathering fires - looking again every FireCheckInterval seconds
		/// </summary>
		/// <returns>The fires, which aren't to be changed</returns>
		public List<Fireplace> GatherFires()
		{
			if (Time.time < nextGatherCheck)
			{
				gatherFires.RemoveAll((Fireplace found) => found == null);
				return(gatherFires);
			}
			nextGatherCheck = Time.time + FireCheckInterval;
			List<Fireplace> fires = new List<Fireplace>();
			Fireplace own = Fire();
			if (own != null)
			{
				fires.Add(own);
			}
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Value == null || !instance.Key.GetBool(GatherFireKey) || instance.Key.GetBool(VillagePaths.BonfireKey) || !Contains(instance.Key.GetPosition()))
				{
					continue;
				}
				Fireplace found = instance.Value.GetComponent<Fireplace>();
				if (found != null && found != own)
				{
					fires.Add(found);
				}
			}
			gatherFires = fires;
			return(gatherFires);
		}

		/// <summary>
		/// Looks for the fires the village's vikings gather at again the next time they're wanted, for one just made or
		/// unmade a gathering fire
		/// </summary>
		public void ForgetGatherFires()
		{
			nextGatherCheck = 0f;
		}

		/// <summary>
		/// Notes the village's fire in its generator, so it can be built back where it stood
		/// </summary>
		/// <param name="found">The fire</param>
		private void RecordFire(Fireplace found)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(Utils.GetPrefabName(found.gameObject).GetStableHashCode());
			pkg.Write(found.transform.position);
			pkg.Write(found.transform.rotation);
			Zdo.Set(FireKey, pkg.GetArray());
		}

		/// <summary>
		/// Reads where the village's fire stood and what it was - the vikings build it back with nothing, the way it
		/// breaks into nothing (see VillageFireDropPatch)
		/// </summary>
		/// <param name="record">The fire</param>
		/// <returns>False when it was never noted</returns>
		public bool FireRecord(out PieceRecord record)
		{
			record = new PieceRecord();
			byte[] bytes = Zdo.GetByteArray(FireKey);
			if (bytes == null)
			{
				return(false);
			}
			ZPackage pkg = new ZPackage(bytes);
			record.Prefab = pkg.ReadInt();
			record.Position = pkg.ReadVector3();
			record.Rotation = pkg.ReadQuaternion();
			record.Wood = 0;
			return(true);
		}

		/// <summary>
		/// Checks whether the village's fire has been destroyed - it was noted, its zone has finished loading and the
		/// village has no fire standing
		/// </summary>
		/// <param name="record">The fire, to build back</param>
		/// <returns>True when it's gone</returns>
		public bool FireMissing(out PieceRecord record)
		{
			if (!FireRecord(out record) || !VillageRespawn.ZoneSettled(record.Position))
			{
				return(false);
			}
			return(Fire() == null);
		}

		/// <summary>
		/// Builds the village's fire back where it stood, as the village's, with its place effect
		/// </summary>
		/// <param name="record">The fire</param>
		/// <returns>False when it couldn't be - its prefab is unknown, its zone hasn't finished loading, it already
		/// stands or an active ward covers it</returns>
		public bool RebuildFire(PieceRecord record)
		{
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(record.Prefab) : null);
			// The fire is looked for afresh, and the ZDOs where it stood checked, since a viking owned by another client
			// may have built it back since it was last looked for
			nextFireCheck = 0f;
			if (prefab == null || !FireMissing(out PieceRecord _) || VillageRecords.StandsAt(record) || VillageLife.Warded(record.Position))
			{
				return(false);
			}
			GameObject instance = Object.Instantiate(prefab, record.Position, record.Rotation);
			VillageRecords.MarkBuilt(instance);
			ZNetView view = instance.GetComponent<ZNetView>();
			if (view != null && view.GetZDO() != null)
			{
				view.GetZDO().Set(VillagePaths.BonfireKey, true);
			}
			WearNTear wear = instance.GetComponent<WearNTear>();
			if (wear != null)
			{
				wear.OnPlaced();
			}
			Piece piece = instance.GetComponent<Piece>();
			if (piece != null)
			{
				piece.m_placeEffect.Create(record.Position, record.Rotation, instance.transform);
			}
			fire = instance.GetComponent<Fireplace>();
			nextFireCheck = Time.time + FireCheckInterval;
			return(true);
		}

		/// <summary>
		/// Makes a bonfire or campfire someone built within AdoptRange of where the village's destroyed fire stood the
		/// village's fire, so the vikings gather at it, cook over it and keep it stacked rather than building their own
		/// back - noting it as the fire to build back if it's destroyed too
		/// </summary>
		/// <returns>The fire, or null when there's none, or the village's fire was never noted or its zone hasn't
		/// finished loading</returns>
		private Fireplace AdoptFire()
		{
			if (!FireRecord(out PieceRecord record) || !VillageRespawn.ZoneSettled(record.Position))
			{
				return(null);
			}
			Fireplace nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Value == null)
				{
					continue;
				}
				float distance = Utils.DistanceXZ(instance.Key.GetPosition(), record.Position);
				if (distance > AdoptRange || distance >= nearestDistance)
				{
					continue;
				}
				Fireplace candidate = instance.Value.GetComponent<Fireplace>();
				string prefab = Utils.GetPrefabName(instance.Value.gameObject);
				if (candidate != null && (prefab.StartsWith("bonfire", System.StringComparison.OrdinalIgnoreCase) || prefab.StartsWith("fire_pit", System.StringComparison.OrdinalIgnoreCase)))
				{
					nearest = candidate;
					nearestDistance = distance;
				}
			}
			if (nearest == null)
			{
				return(null);
			}
			ZNetView view = nearest.GetComponent<ZNetView>();
			if (!view.IsOwner())
			{
				view.ClaimOwnership();
			}
			view.GetZDO().Set(VillagePaths.BonfireKey, true);
			RecordFire(nearest);
			VillageCooking.FireMoved(this, record.Position);
			VillageSeats.FireMoved(this, record.Position);
			Plugin.Log.LogInfo("Village at " + Farm.ToString("F0") + " took up the " + Utils.GetPrefabName(nearest.gameObject) + " at " + nearest.transform.position.ToString("F1") + " as its fire");
			return(nearest);
		}

		/// <summary>
		/// Looks for the village's bonfire or campfire among the loaded objects
		/// </summary>
		/// <returns>The fire, or null when it has none loaded</returns>
		private Fireplace FindFire()
		{
			Fireplace nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Value == null || !instance.Key.GetBool(VillagePaths.BonfireKey))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(instance.Key.GetPosition(), Middle);
				if (distance <= Mathf.Max(Radius, FireReach) && distance < nearestDistance)
				{
					Fireplace fire = instance.Value.GetComponent<Fireplace>();
					if (fire != null)
					{
						nearest = fire;
						nearestDistance = distance;
					}
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Lists the loaded living vikings that belong to the village
		/// </summary>
		/// <returns>The vikings, alive</returns>
		public List<Character> Vikings()
		{
			List<Character> vikings = new List<Character>();
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character.IsDead() || !VikingCore.IsLivingViking(character))
				{
					continue;
				}
				// One leaving a village that was disbanded belongs to none, even a village started where it stood
				ZDO zdo = character.m_nview.GetZDO();
				Vector3 farm = zdo.GetVec3(VillageKey, Vector3.zero);
				if (farm != Vector3.zero && Utils.DistanceXZ(farm, Farm) <= FarmReach && !zdo.GetBool(VillageFounding.LeavingKey))
				{
					vikings.Add(character);
				}
			}
			return(vikings);
		}

		/// <summary>
		/// Finds a random spot on open ground in the village's core - outside its houses and pens, out of the water and
		/// where a viking can walk to the rest of the village from
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>False when none was found</returns>
		public bool RandomOpenSpot(out Vector3 spot)
		{
			bool hasAnchor = Anchor(out Vector3 anchor);
			for (int i = 0; i < SpotTries; i++)
			{
				Vector2 offset = Random.insideUnitCircle * CoreRadius;
				if (OpenSpotAt(CoreMiddle + new Vector3(offset.x, 0f, offset.y), hasAnchor, anchor, out spot))
				{
					return(true);
				}
			}
			spot = Vector3.zero;
			return(false);
		}

		/// <summary>
		/// Finds a random spot on open ground beside one of the houses of the village's core, no more than SpawnReach past
		/// its walls, for a living viking to come back at - outside its houses and pens, out of the water and where a
		/// viking can walk to the rest of the village from
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>False when none was found</returns>
		public bool SpawnSpot(out Vector3 spot)
		{
			if (CoreHouses.Count == 0)
			{
				return(RandomOpenSpot(out spot));
			}
			bool hasAnchor = Anchor(out Vector3 anchor);
			for (int i = 0; i < SpotTries; i++)
			{
				if (OpenSpotAt(PointNearHouse(CoreHouses, HouseClearance), hasAnchor, anchor, out spot))
				{
					return(true);
				}
			}
			spot = Vector3.zero;
			return(false);
		}

		/// <summary>
		/// Finds a random spot on open ground outside the farm's fence - or the edge of a village a player started, which
		/// has none - ArrivalMin to ArrivalMax past it, for a viking coming to the village to start from and walk in - out
		/// of the water, outside any pen, where its zone has finished loading and with a way into the village
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>False when none was found</returns>
		public bool ArrivalSpot(out Vector3 spot)
		{
			bool hasAnchor = Anchor(out Vector3 anchor);
			float fence = (IsFounded() ? Utils.DistanceXZ(Middle, Farm) + Radius : Zdo.GetFloat(VillageFarmhouses.FenceKey, VillageFarmhouses.FenceRadiusMax));
			for (int i = 0; i < SpotTries; i++)
			{
				Vector3 outward = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
				Vector3 point = Farm + outward * (fence + Random.Range(ArrivalMin, ArrivalMax));
				if (VillageRespawn.ZoneSettled(point) && OpenSpotAt(point, hasAnchor, anchor, out spot) && Utils.DistanceXZ(spot, Farm) > fence)
				{
					return(true);
				}
			}
			spot = Vector3.zero;
			return(false);
		}

		/// <summary>
		/// Picks a random point beside one of a list of houses, in the band from clearance to SpawnReach past its walls
		/// </summary>
		/// <param name="houses">The houses, at least one</param>
		/// <param name="clearance">How far from the house's walls the point keeps, in meters</param>
		/// <returns>The point, at the house's height</returns>
		public static Vector3 PointNearHouse(List<VillagePaths.House> houses, float clearance)
		{
			VillagePaths.House house = houses[Random.Range(0, houses.Count)];
			float inner = house.HalfSize + clearance;
			float outer = house.HalfSize + Mathf.Max(SpawnReach, clearance + 1f);
			float along = Random.Range(-outer, outer);
			float across = Random.Range(inner, outer) * ((Random.value < 0.5f) ? -1f : 1f);
			Vector3 offset = ((Random.value < 0.5f) ? new Vector3(along, 0f, across) : new Vector3(across, 0f, along));
			return(house.Center + offset);
		}

		/// <summary>
		/// Checks whether a point is on open ground a viking can stand on - outside the village's houses and pens, out of
		/// the water, on the navmesh and not on a roof, clear of the flames of a bonfire or any other fire by
		/// FireClearance, with a way to the village's anchor
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="hasAnchor">Whether the village has an anchor</param>
		/// <param name="anchor">The spot on the village's open ground every viking walks to</param>
		/// <param name="spot">The point on the navmesh</param>
		/// <returns>True when it's open</returns>
		private bool OpenSpotAt(Vector3 point, bool hasAnchor, Vector3 anchor, out Vector3 spot)
		{
			spot = Vector3.zero;
			if (InHouse(point, HouseClearance))
			{
				return(false);
			}
			point.y = ZoneSystem.instance.GetSolidHeight(point);
			if (point.y < ZoneSystem.instance.m_waterLevel + 0.3f || Enclosed(point))
			{
				return(false);
			}
			if (Pathfinding.instance == null || !NavmeshPoints.FindValidPoint(out Vector3 valid, point, SpotSnap, Pathfinding.AgentType.Humanoid) || InHouse(valid, HouseClearance) || NavmeshRoofs.OnRoof(valid))
			{
				return(false);
			}
			if (EffectArea.IsPointInsideArea(valid, EffectArea.Type.Burning, FireClearance) != null)
			{
				return(false);
			}
			// A spot with no way to the fire or a doorstep is shut in, like a pen
			if (hasAnchor && !Pathfinding.instance.HavePath(valid, anchor, Pathfinding.AgentType.Humanoid))
			{
				return(false);
			}
			spot = valid;
			return(true);
		}

		/// <summary>
		/// Finds a spot on the village's open ground every viking walks to - beside its fire, or else the doorstep of a
		/// house of its core, whichever isn't in a pen - which a spot has to have a way to
		/// </summary>
		/// <param name="anchor">The spot</param>
		/// <returns>False when the village has neither</returns>
		private bool Anchor(out Vector3 anchor)
		{
			// A spot in a pen would only lead to more spots in it, so the anchor is somewhere out in the open
			Fireplace found = Fire();
			if (found != null)
			{
				for (int i = 0; i < AnchorSides; i++)
				{
					Vector3 side = found.transform.position + Quaternion.Euler(0f, i * 360f / AnchorSides, 0f) * Vector3.forward * AnchorFromFire;
					side.y = ZoneSystem.instance.GetSolidHeight(side);
					if (!FencedIn(side))
					{
						anchor = side;
						return(true);
					}
				}
			}
			foreach (VillagePaths.House house in CoreHouses)
			{
				foreach (Vector3 step in Doorsteps(house))
				{
					if (!FencedIn(step))
					{
						anchor = step;
						return(true);
					}
				}
			}
			anchor = CoreMiddle;
			return(false);
		}

		/// <summary>
		/// Finds where the village's vikings run to for help and walk back to - the spot on its open ground every viking
		/// walks to, out of any pen, or else the middle of its core
		/// </summary>
		/// <returns>The spot</returns>
		public Vector3 RallyPoint()
		{
			Anchor(out Vector3 rally);
			return(rally);
		}

		/// <summary>
		/// Checks whether a spot on the ground is fenced in on nearly every side, like a pen, by casting level rays
		/// around it at knee height
		/// </summary>
		/// <remarks>
		/// It uses the colliders that are there, so it works as a village is generated, before it has a navmesh
		/// </remarks>
		/// <param name="ground">The spot, on the ground</param>
		/// <returns>True when it's fenced in</returns>
		public static bool Enclosed(Vector3 ground)
		{
			if (enclosureMask == 0)
			{
				enclosureMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
			}
			Vector3 origin = ground + Vector3.up * EnclosureHeight;
			int hits = 0;
			for (int i = 0; i < EnclosureRays; i++)
			{
				Vector3 direction = Quaternion.Euler(0f, i * 360f / EnclosureRays, 0f) * Vector3.forward;
				if (Physics.Raycast(origin, direction, EnclosureReach, enclosureMask, QueryTriggerInteraction.Ignore))
				{
					hits++;
				}
			}
			return(hits >= EnclosureHits);
		}

		/// <summary>
		/// Checks whether a spot on the ground is in a fenced-in area open to the sky, like a pen - fenced in on nearly
		/// every side with nothing overhead, which tells it from the inside of a house
		/// </summary>
		/// <param name="ground">The spot, on the ground</param>
		/// <returns>True when it's in a pen</returns>
		public static bool FencedIn(Vector3 ground)
		{
			if (!Enclosed(ground))
			{
				return(false);
			}
			return(!Physics.Raycast(ground + Vector3.up * EnclosureRoofFrom, Vector3.up, EnclosureRoofReach, enclosureMask, QueryTriggerInteraction.Ignore));
		}

		/// <summary>
		/// Finds the doorsteps of a house's doors - a little outside each door
		/// </summary>
		/// <param name="house">The house</param>
		/// <returns>The doorsteps</returns>
		public static List<Vector3> Doorsteps(VillagePaths.House house)
		{
			List<Vector3> steps = new List<Vector3>();
			foreach (ZNetView view in DoorPieces(house))
			{
				Vector3 outward = view.transform.forward;
				outward.y = 0f;
				if (Vector3.Dot(outward, view.transform.position - house.Center) < 0f)
				{
					outward = -outward;
				}
				steps.Add(view.transform.position + outward.normalized * DoorstepDistance);
			}
			return(steps);
		}

		/// <summary>
		/// Finds where a house's doors stand
		/// </summary>
		/// <param name="house">The house</param>
		/// <returns>The doors' positions</returns>
		public static List<Vector3> Doors(VillagePaths.House house)
		{
			return(DoorPieces(house).ConvertAll((ZNetView view) => view.transform.position));
		}

		/// <summary>
		/// Finds a house's doors - the doors loaded within its walls or just outside them
		/// </summary>
		/// <param name="house">The house</param>
		/// <returns>The doors' pieces</returns>
		private static List<ZNetView> DoorPieces(VillagePaths.House house)
		{
			List<ZNetView> doors = new List<ZNetView>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				if (view != null && Utils.DistanceXZ(view.transform.position, house.Center) <= house.HalfSize + DoorReach && view.GetComponent<Door>() != null)
				{
					doors.Add(view);
				}
			}
			return(doors);
		}

		/// <summary>
		/// Checks whether any enemy of the living vikings - a zombie or a monster - is within a distance of the village's
		/// edge
		/// </summary>
		/// <param name="reach">How far past the edge to look, in meters</param>
		/// <returns>True when one is</returns>
		public bool EnemyNear(float reach)
		{
			return(FindEnemy(reach) != null);
		}

		/// <summary>
		/// Finds a hostile creature within a reach of the village (see EnemyNear)
		/// </summary>
		/// <param name="reach">How far outside the village counts, in meters</param>
		/// <returns>The creature, or null when there's none</returns>
		public Character FindEnemy(float reach)
		{
			foreach (Character character in Character.GetAllCharacters())
			{
				if (IsHostile(character) && DistanceOutside(character.transform.position) <= reach)
				{
					return(character);
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds a hostile creature within a range of a point (see IsHostile)
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="range">The range, in meters</param>
		/// <returns>The creature, or null when there's none</returns>
		public static Character FindEnemyNear(Vector3 point, float range)
		{
			foreach (Character character in Character.GetAllCharacters())
			{
				if (IsHostile(character) && Vector3.Distance(character.transform.position, point) <= range)
				{
					return(character);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether a creature is hostile to a village - alive, and not a player, tamed, an animal, a living viking
		/// or one of the dvergr
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it is</returns>
		private static bool IsHostile(Character character)
		{
			if (character == null || character.IsDead() || character.IsPlayer() || character.IsTamed())
			{
				return(false);
			}
			Character.Faction faction = character.GetFaction();
			return(faction != Character.Faction.Dverger && faction != Character.Faction.AnimalsVeg && faction != Character.Faction.PlayerSpawned);
		}

		/// <summary>
		/// Checks whether the village's houses still need noting with the houses the vikings took in
		/// </summary>
		/// <returns>True when only its farmhouses are noted</returns>
		public bool NeedsAllHouses()
		{
			return(Zdo.GetByteArray(AllHousesKey) == null);
		}
	}
}
