using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Lays out an abandoned farm with its farmhouses in the middle and its pens out toward its fence, in place of the
	/// generator scattering every room anywhere inside it
	/// </summary>
	/// <remarks>
	/// It places the rooms the way the generator's radial camp does - as many as its widened room count, but no more
	/// farmhouses than the game's own count would have, each a weighted pick among the farm's
	/// rooms, at a random spot inside the fence on ground that isn't too steep or low, facing the way the room asks,
	/// clear of the rooms already placed, then the fence around them - except that the farmhouses are placed first,
	/// within HouseShare of the fence's radius from the middle, then the fields and the rest, then the pens, beyond
	/// PenShare of it, and no room covers the open ground within PlazaRadius of the middle, where the bonfire goes. A pen
	/// is a room with an animal spawner in it. It runs with the generator's random numbers seeded, so a farm comes out
	/// the same each time
	/// </remarks>
	internal static class FarmLayout
	{
		/// <summary>
		/// How far out from the middle a farmhouse can stand, and how far out a pen has to, as shares of the fence's
		/// radius
		/// </summary>
		private const float HouseShare = 0.45f;
		private const float PenShare = 0.65f;

		/// <summary>
		/// The least distance from the middle a farmhouse can stand out to, in meters, however small the farm, so a small
		/// farm still has room for its farmhouses past the plaza - no farther than the farm's reach
		/// </summary>
		private const float HouseReachMin = 15f;

		/// <summary>
		/// How far from the middle the ground is kept clear of rooms for the bonfire and the vikings gathered around it,
		/// in meters
		/// </summary>
		private const float PlazaRadius = 7f;

		/// <summary>
		/// The order the kinds of room are placed in, and their names in the log
		/// </summary>
		private const int FarmhouseKind = 0;
		private const int OtherKind = 1;
		private const int PenKind = 2;
		private static readonly string[] kindNames = { "farmhouse", "other", "pen" };

		/// <summary>
		/// How many spots are tried for each room the farm gets, the same as the generator
		/// </summary>
		private const int TriesPerRoom = 20;

		/// <summary>
		/// How many picks are made for a room past the game's own count before giving up on one that isn't a farmhouse
		/// </summary>
		private const int RepickTries = 10;

		/// <summary>
		/// How far below a spot's ground can be at the most for a room, the same as the generator, in meters
		/// </summary>
		private const float AltitudeOffset = 30f;

		/// <summary>
		/// How far either side of a spot the world's heights are read to work out how the ground slopes there, in meters
		/// </summary>
		private const float SlopeStep = 1f;

		/// <summary>
		/// Whether each room is a pen, by its room data
		/// </summary>
		private static readonly Dictionary<DungeonDB.RoomData, bool> pens = new Dictionary<DungeonDB.RoomData, bool>();

		/// <summary>
		/// The share of a farm's farmhouses built from a house blueprint instead, while there are any
		/// </summary>
		private const float BlueprintShare = 0.5f;

		/// <summary>
		/// The most the ground can rise and fall under a house built from a blueprint, in meters - it stands on the
		/// highest of it, so its low side stands that far off the ground at most
		/// </summary>
		private const float MaxUnevenness = 1f;

		/// <summary>
		/// The ground a room or house takes up, seen from above - where its middle is, which way it faces, and half its
		/// size across and deep
		/// </summary>
		internal struct Footprint
		{
			public Vector3 Center;
			public Quaternion Rotation;
			public Vector2 Half;
		}

		/// <summary>
		/// The houses built from blueprints in the farm being generated
		/// </summary>
		private static readonly List<Footprint> blueprintHouses = new List<Footprint>();

		/// <summary>
		/// Forgets which rooms are pens when leaving a world, since its room data goes with it
		/// </summary>
		public static void Forget()
		{
			pens.Clear();
		}

		/// <summary>
		/// Lays out a village's farm being generated, in place of the generator's radial camp
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="mode">How the farm is being placed</param>
		/// <returns>False when it isn't a village's farm, so the generator lays it out itself</returns>
		public static bool Generate(DungeonGenerator generator, ZoneSystem.SpawnMode mode)
		{
			if (!VillageFarmhouses.IsVillageFarm(generator, mode))
			{
				return(false);
			}
			Generating = true;
			try
			{
				Lay(generator, mode);
			}
			finally
			{
				Generating = false;
			}
			return(true);
		}

		/// <summary>
		/// Whether a village's farm is being laid out, while the ground the game's own placing reads is taken from the
		/// world's heights where its terrain isn't loaded (see GroundHeight and FarmGroundPatch)
		/// </summary>
		public static bool Generating { get; private set; }

		/// <summary>
		/// Lays out a village's farm - its rooms, farmhouses first, then its fence (see Generate)
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="mode">How the farm is being placed</param>
		private static void Lay(DungeonGenerator generator, ZoneSystem.SpawnMode mode)
		{
			float radius = Random.Range(generator.m_campRadiusMin, generator.m_campRadiusMax);
			float reach = Mathf.Max(0f, radius - generator.m_perimeterBuffer);
			float steepest = Mathf.Cos(Mathf.Deg2Rad * generator.m_maxTilt);
			int wanted = Random.Range(generator.m_minRooms, generator.m_maxRooms);
			// The rooms are picked first and placed farmhouses first, so the fields don't take the middle before them
			List<DungeonDB.RoomData> rooms = PickRooms<DungeonDB.RoomData>(wanted, VillageFarmhouses.RoomScale, () => generator.GetRandomWeightedRoom(false), (DungeonDB.RoomData room) => room.RoomInPrefab != null, IsFarmhouse);
			int[] picked = new int[kindNames.Length];
			int[] placed = new int[kindNames.Length];
			float[] nearest = new float[kindNames.Length];
			for (int kind = 0; kind < kindNames.Length; kind++)
			{
				nearest[kind] = float.MaxValue;
			}
			Vector3 middle = generator.transform.position;
			blueprintHouses.Clear();
			List<VillageBlueprints.Design> designs = VillageBlueprints.Usable();
			int blueprintsBuilt = 0;
			for (int kind = FarmhouseKind; kind <= PenKind; kind++)
			{
				float near = ((kind == PenKind) ? reach * PenShare : 0f);
				float far = ((kind == FarmhouseKind) ? HouseReach(reach) : reach);
				foreach (DungeonDB.RoomData room in rooms)
				{
					if (Kind(room) != kind)
					{
						continue;
					}
					picked[kind]++;
					// A farmhouse can be built from one of the house designs instead, falling back on the farmhouse when
					// the design finds no room
					if (kind == FarmhouseKind && designs.Count > 0 && Random.value < BlueprintShare)
					{
						VillageBlueprints.Design design = designs[Random.Range(0, designs.Count)];
						if (BuildBlueprint(generator, design, middle, far, mode))
						{
							blueprintsBuilt++;
							continue;
						}
					}
					for (int tries = 0; tries < TriesPerRoom; tries++)
					{
						Vector3 point = middle + Quaternion.Euler(0f, Random.Range(0, 360), 0f) * Vector3.forward * Random.Range(near, far);
						GroundData(ref point, out Vector3 normal);
						if (normal.y < steepest || point.y - AltitudeOffset < generator.m_minAltitude)
						{
							continue;
						}
						Quaternion rotation = generator.GetCampRoomRotation(room, point);
						if (CoversPlaza(RoomFootprint(room.RoomInPrefab, point, rotation), middle) || OverlapsBlueprint(room.RoomInPrefab, point, rotation) || generator.TestCollision(room.RoomInPrefab, point, rotation))
						{
							continue;
						}
						generator.PlaceRoom(room, point, rotation, null, mode);
						placed[kind]++;
						nearest[kind] = Mathf.Min(nearest[kind], Utils.DistanceXZ(point, middle));
						break;
					}
				}
			}
			if (generator.m_perimeterSections > 0)
			{
				generator.PlaceWall(radius, generator.m_perimeterSections, mode);
			}
			string summary = "Laid out the farm at " + middle.ToString("F0") + " with its fence " + radius.ToString("F0") + "m out:";
			for (int kind = 0; kind < kindNames.Length; kind++)
			{
				summary += " " + placed[kind] + "/" + picked[kind] + " " + kindNames[kind] + ((placed[kind] > 0) ? " (nearest " + nearest[kind].ToString("F0") + "m)" : "");
			}
			summary += ", and " + blueprintsBuilt + " farmhouses built from the " + designs.Count + " house blueprints";
			Plugin.Log.LogInfo(summary);
		}

		/// <summary>
		/// Picks a farm's rooms, as many as it wants - a widened farm gets more rooms to fill its ground, but only as many
		/// farmhouses as its first CapIndex picks come to, the game's own count of rooms, since each brings more vikings. A
		/// farmhouse picked past that is picked again, up to RepickTries times before the room is given up on, and a
		/// pick that isn't usable gives the room up at once
		/// </summary>
		/// <typeparam name="T">The kind of room</typeparam>
		/// <param name="wanted">How many rooms the farm wants</param>
		/// <param name="roomScale">How many times as many rooms the farm gets as the game would give it</param>
		/// <param name="pick">Picks a room at random, or null when it found none</param>
		/// <param name="usable">Checks whether a room picked can be placed</param>
		/// <param name="isFarmhouse">Checks whether a usable room is a farmhouse</param>
		/// <returns>The rooms, in the order they were picked</returns>
		internal static List<T> PickRooms<T>(int wanted, float roomScale, System.Func<T> pick, System.Func<T, bool> usable, System.Func<T, bool> isFarmhouse) where T : class
		{
			List<T> rooms = new List<T>();
			int capAt = CapIndex(wanted, roomScale);
			int farmhousesMax = int.MaxValue;
			int farmhouses = 0;
			for (int i = 0; i < wanted; i++)
			{
				// Set as each pick starts, so the cap comes in after the game's own count of picks even when one of them
				// found no room
				if (i == capAt)
				{
					farmhousesMax = farmhouses;
				}
				T room = null;
				for (int tries = 0; tries < RepickTries && room == null; tries++)
				{
					room = pick();
					if (room != null && usable(room) && isFarmhouse(room) && farmhouses >= farmhousesMax)
					{
						room = null;
					}
				}
				if (room == null || !usable(room))
				{
					continue;
				}
				rooms.Add(room);
				if (isFarmhouse(room))
				{
					farmhouses++;
				}
			}
			return(rooms);
		}

		/// <summary>
		/// Works out how many of a widened farm's picks come to the game's own count of rooms, after which it gets no more
		/// farmhouses than it has
		/// </summary>
		/// <param name="wanted">How many rooms the farm wants</param>
		/// <param name="roomScale">How many times as many rooms the farm gets as the game would give it</param>
		/// <returns>The number of picks, at least 1</returns>
		internal static int CapIndex(int wanted, float roomScale)
		{
			return(Mathf.Max(1, Mathf.RoundToInt(wanted / roomScale)));
		}

		/// <summary>
		/// Works out how far from a farm's middle its farmhouses can stand - HouseShare of its reach, or HouseReachMin when
		/// that's more and the farm reaches that far, and always past the plaza
		/// </summary>
		/// <param name="reach">How far from its middle the farm's rooms can stand, in meters</param>
		/// <returns>The distance, in meters</returns>
		internal static float HouseReach(float reach)
		{
			return(Mathf.Max(PlazaRadius + 1f, Mathf.Max(reach * HouseShare, Mathf.Min(HouseReachMin, reach))));
		}

		/// <summary>
		/// Finds the ground at a spot and how it slopes, the way the generator does - from the terrain where it's loaded,
		/// and from the world's own heights where it isn't (see GroundHeight)
		/// </summary>
		/// <param name="point">The spot, set to the ground's height there</param>
		/// <param name="normal">Set to the ground's slope there, as its normal</param>
		public static void GroundData(ref Vector3 point, out Vector3 normal)
		{
			ZoneSystem.instance.GetGroundData(ref point, out normal, out Heightmap.Biome _, out Heightmap.BiomeArea _, out Heightmap terrain);
			if (terrain == null)
			{
				GroundFromWorld(ref point, ref normal);
			}
		}

		/// <summary>
		/// Sets a spot's height and slope from the world's own heights, which the terrain is built from, for a spot whose
		/// terrain isn't loaded (see GroundHeight)
		/// </summary>
		/// <param name="point">The spot, set to the ground's height there</param>
		/// <param name="normal">Set to the ground's slope there, as its normal</param>
		public static void GroundFromWorld(ref Vector3 point, ref Vector3 normal)
		{
			WorldGenerator world = WorldGenerator.instance;
			if (world == null)
			{
				return;
			}
			point.y = world.GetHeight(point.x, point.z);
			float east = world.GetHeight(point.x + SlopeStep, point.z);
			float west = world.GetHeight(point.x - SlopeStep, point.z);
			float north = world.GetHeight(point.x, point.z + SlopeStep);
			float south = world.GetHeight(point.x, point.z - SlopeStep);
			normal = new Vector3(west - east, 2f * SlopeStep, south - north).normalized;
		}

		/// <summary>
		/// Finds the ground's height at a spot - from the terrain where it's loaded, and from the world's own heights where
		/// it isn't
		/// </summary>
		/// <remarks>
		/// A farm being generated can reach past its zone into one whose terrain isn't loaded - always on a dedicated
		/// server, which generates each zone with only its own terrain - where the game's raycast finds nothing and leaves
		/// the height where it was, the farm's middle. A room placed there levels the ground to that height, cutting a
		/// hole into higher ground or raising lower ground, so the world's own heights, which the terrain is built from,
		/// are read there instead
		/// </remarks>
		/// <param name="point">The spot</param>
		/// <returns>The height</returns>
		public static float GroundHeight(Vector3 point)
		{
			GroundData(ref point, out Vector3 _);
			return(point.y);
		}

		/// <summary>
		/// Lists the houses built from blueprints in the farm being generated, as farmhouses
		/// </summary>
		/// <returns>The houses</returns>
		public static List<VillagePaths.House> BlueprintHouses()
		{
			List<VillagePaths.House> houses = new List<VillagePaths.House>();
			foreach (Footprint house in blueprintHouses)
			{
				houses.Add(new VillagePaths.House { Center = house.Center, HalfSize = Mathf.Max(house.Half.x, house.Half.y) });
			}
			return(houses);
		}

		/// <summary>
		/// Checks whether a room placed at a spot would overlap a house built from a blueprint in the farm being
		/// generated
		/// </summary>
		/// <param name="room">The room</param>
		/// <param name="point">Where its middle would go</param>
		/// <param name="rotation">Which way it would face</param>
		/// <returns>True when it would</returns>
		public static bool OverlapsBlueprint(Room room, Vector3 point, Quaternion rotation)
		{
			Footprint footprint = RoomFootprint(room, point, rotation);
			return(blueprintHouses.Exists((Footprint house) => Overlaps(footprint, house)));
		}

		/// <summary>
		/// Builds a house from a design in the farm's house band, on ground even enough for it, clear of the rooms placed
		/// and the farm's middle, with its doors facing the middle - noting it as a farmhouse for vikings to live in
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="design">The design</param>
		/// <param name="middle">The farm's middle</param>
		/// <param name="far">How far from the middle a farmhouse can stand, in meters</param>
		/// <param name="mode">How the farm is being placed</param>
		/// <returns>True when it found room and was built</returns>
		private static bool BuildBlueprint(DungeonGenerator generator, VillageBlueprints.Design design, Vector3 middle, float far, ZoneSystem.SpawnMode mode)
		{
			float near = PlazaRadius + Mathf.Min(design.Half.x, design.Half.y);
			for (int tries = 0; tries < TriesPerRoom; tries++)
			{
				Vector3 point = middle + Quaternion.Euler(0f, Random.Range(0, 360), 0f) * Vector3.forward * Random.Range(near, Mathf.Max(near, far));
				Vector3 toMiddle = middle - point;
				toMiddle.y = 0f;
				Quaternion rotation = Quaternion.Euler(0f, Vector3.SignedAngle(design.DoorDirection, toMiddle, Vector3.up), 0f);
				Footprint footprint = new Footprint { Center = point, Rotation = rotation, Half = design.Half };
				if (CoversPlaza(footprint, middle) || blueprintHouses.Exists((Footprint house) => Overlaps(footprint, house)))
				{
					continue;
				}
				if (DungeonGenerator.m_placedRooms.Exists((Room placed) => placed != null && Overlaps(footprint, RoomFootprint(placed, placed.transform.position, placed.transform.rotation))))
				{
					continue;
				}
				if (!GroundUnder(footprint, out float ground) || ground - AltitudeOffset < generator.m_minAltitude)
				{
					continue;
				}
				point.y = ground;
				footprint.Center = point;
				VillageBlueprints.Build(design, point, rotation, mode);
				VillageBlueprints.RecordHouse(generator, point);
				VikingSpawns.AddFarmhouse(point, Mathf.Max(design.Half.x, design.Half.y));
				blueprintHouses.Add(footprint);
				Plugin.Log.LogInfo("Built the house blueprint " + design.Name + " at " + point.ToString("F0"));
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Finds the height a house stands at over its footprint - the highest ground under its middle and corners, so
		/// none of it is buried - when the ground is even enough for it
		/// </summary>
		/// <param name="footprint">The house's footprint</param>
		/// <param name="ground">The height</param>
		/// <returns>False when the ground rises and falls too much under it</returns>
		private static bool GroundUnder(Footprint footprint, out float ground)
		{
			float low = float.MaxValue;
			float high = float.MinValue;
			for (int x = -1; x <= 1; x++)
			{
				for (int z = -1; z <= 1; z++)
				{
					Vector3 point = footprint.Center + footprint.Rotation * new Vector3(x * footprint.Half.x, 0f, z * footprint.Half.y);
					float height = GroundHeight(point);
					low = Mathf.Min(low, height);
					high = Mathf.Max(high, height);
				}
			}
			ground = high;
			return(high - low <= MaxUnevenness);
		}

		/// <summary>
		/// Works out the footprint a room takes up at a spot
		/// </summary>
		/// <param name="room">The room</param>
		/// <param name="point">Where its middle is</param>
		/// <param name="rotation">Which way it faces</param>
		/// <returns>Its footprint</returns>
		private static Footprint RoomFootprint(Room room, Vector3 point, Quaternion rotation)
		{
			return(new Footprint { Center = point, Rotation = rotation, Half = new Vector2(room.m_size.x / 2f, room.m_size.z / 2f) });
		}

		/// <summary>
		/// Checks whether two footprints overlap, seen from above
		/// </summary>
		/// <param name="a">One footprint</param>
		/// <param name="b">The other</param>
		/// <returns>True when they do</returns>
		internal static bool Overlaps(Footprint a, Footprint b)
		{
			Vector2 offset = new Vector2(b.Center.x - a.Center.x, b.Center.z - a.Center.z);
			Vector2[] axes = { Axis(a.Rotation, Vector3.right), Axis(a.Rotation, Vector3.forward), Axis(b.Rotation, Vector3.right), Axis(b.Rotation, Vector3.forward) };
			foreach (Vector2 axis in axes)
			{
				if (Mathf.Abs(Vector2.Dot(offset, axis)) >= Reach(a, axis) + Reach(b, axis))
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Works out how far a footprint reaches from its middle along a direction, seen from above
		/// </summary>
		/// <param name="footprint">The footprint</param>
		/// <param name="axis">The direction</param>
		/// <returns>The reach, in meters</returns>
		private static float Reach(Footprint footprint, Vector2 axis)
		{
			return(footprint.Half.x * Mathf.Abs(Vector2.Dot(Axis(footprint.Rotation, Vector3.right), axis)) + footprint.Half.y * Mathf.Abs(Vector2.Dot(Axis(footprint.Rotation, Vector3.forward), axis)));
		}

		/// <summary>
		/// Turns a direction and flattens it onto the ground, seen from above
		/// </summary>
		/// <param name="rotation">The turn</param>
		/// <param name="direction">The direction</param>
		/// <returns>The flattened direction</returns>
		private static Vector2 Axis(Quaternion rotation, Vector3 direction)
		{
			Vector3 turned = rotation * direction;
			return(new Vector2(turned.x, turned.z).normalized);
		}

		/// <summary>
		/// Works out which kind of room a room is, for the order the rooms are placed in
		/// </summary>
		/// <param name="room">The room</param>
		/// <returns>FarmhouseKind, PenKind or OtherKind</returns>
		private static int Kind(DungeonDB.RoomData room)
		{
			if (IsFarmhouse(room))
			{
				return(FarmhouseKind);
			}
			return(IsPen(room) ? PenKind : OtherKind);
		}

		/// <summary>
		/// Checks whether a footprint would cover any of the open ground kept around the farm's middle
		/// </summary>
		/// <param name="footprint">The footprint</param>
		/// <param name="middle">The farm's middle</param>
		/// <returns>True when it would</returns>
		internal static bool CoversPlaza(Footprint footprint, Vector3 middle)
		{
			// A footprint's turn is a unit quaternion, whose inverse is its conjugate
			Quaternion unturn = new Quaternion(-footprint.Rotation.x, -footprint.Rotation.y, -footprint.Rotation.z, footprint.Rotation.w);
			Vector3 local = unturn * (middle - footprint.Center);
			Vector2 nearestPoint = new Vector2(Mathf.Clamp(local.x, -footprint.Half.x, footprint.Half.x), Mathf.Clamp(local.z, -footprint.Half.y, footprint.Half.y));
			return(Vector2.Distance(nearestPoint, new Vector2(local.x, local.z)) < PlazaRadius);
		}

		/// <summary>
		/// Checks whether a room is a farmhouse
		/// </summary>
		/// <param name="room">The room</param>
		/// <returns>True when it is</returns>
		private static bool IsFarmhouse(DungeonDB.RoomData room)
		{
			return(room.RoomInPrefab.gameObject.name.StartsWith(VikingSpawns.FarmhousePrefix, System.StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Checks whether a room is a pen - one with an animal spawner in it - remembering the answer
		/// </summary>
		/// <param name="room">The room</param>
		/// <returns>True when it is</returns>
		private static bool IsPen(DungeonDB.RoomData room)
		{
			if (!pens.TryGetValue(room, out bool pen))
			{
				pen = (room.RoomInPrefab.GetComponentsInChildren<CreatureSpawner>(true).Length > 0);
				pens[room] = pen;
			}
			return(pen);
		}
	}
}
