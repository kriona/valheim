using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Sizes each farm at random as it's generated, so villages come in a range of sizes, and makes sure every village
	/// has at least MinHouses houses in its core - HardMinHouses in or by a biome harder than the Meadows - where its
	/// vikings live, placing more farmhouses in a farm when its own farmhouses and the empty houses close by them come to
	/// fewer - the outposts standing off on their own don't count (see Village.Core)
	/// </summary>
	/// <remarks>
	/// A farm's generator scatters its rooms around its middle, each a weighted pick among the farm's rooms, so a farm
	/// can come out with few farmhouses. The extra farmhouses are placed the way the generator places its own rooms -
	/// at random spots inside its fence on ground that isn't too steep, clear of every room already placed, its fence
	/// included - or, when the farm is too full for one, just outside its fence. They're placed while the generator
	/// still has its rooms loaded and its random numbers seeded, so they're saved with the farm, get their vikings and
	/// paths like the rest, and come out the same each time the farm is generated
	/// </remarks>
	internal static class VillageFarmhouses
	{
		/// <summary>
		/// The fewest houses a village has, and the fewest a village in or by a biome harder than the Meadows has (see
		/// NearHarderBiome)
		/// </summary>
		private const int MinHouses = 3;
		private const int HardMinHouses = 4;

		/// <summary>
		/// How many random spots are tried for each farmhouse inside the fence, and then outside it
		/// </summary>
		private const int TriesPerHouse = 300;

		/// <summary>
		/// How far past the fence a farmhouse that doesn't fit inside it can stand, in meters
		/// </summary>
		private const float OutsideReach = 14f;

		/// <summary>
		/// The range a farm's fence stands from its middle in, in meters - each farm's size is picked at random within
		/// it, so villages come in a range of sizes, the largest reaching into the zones around the farm's. The game's
		/// own is 18-32, which fills the farm's zone
		/// </summary>
		public const float FenceRadiusMin = 24f;
		public const float FenceRadiusMax = 44f;

		/// <summary>
		/// How far either side of a farm's picked size its generator may put its fence, in meters
		/// </summary>
		private const float FenceSpread = 2f;

		/// <summary>
		/// How far from a farm's middle a biome harder than the Meadows can be for the farm to be one of the larger ones
		/// with more houses, and for its living vikings to be that biome's, close enough for its creatures to come into
		/// the village, in meters
		/// </summary>
		private const float HardBiomeReach = 150f;
		public const float HardBiomeVikingReach = 50f;

		/// <summary>
		/// How many directions and how far apart the rings the biomes around a point are looked at in are, in meters, and
		/// how far up the range of sizes a farm by a harder biome has its smallest, from 0 to 1
		/// </summary>
		private const int BiomeDirections = 16;
		private const float BiomeRing = 25f;
		private const float HardSizeFloor = 0.6f;

		/// <summary>
		/// Finds the hardest biome at a point or within a distance of it, from the world's plan, so the answer is the same
		/// however much of the world has been generated - by VikingGear's order, from the Meadows to the Deep North, with
		/// the ocean counting as the Meadows. A farm with one harder than the Meadows within HardBiomeReach is one of the
		/// larger ones, with more houses, and its living vikings are of the hardest within HardBiomeVikingReach
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="reach">How far from it to look, in meters</param>
		/// <returns>The biome's index into VikingGear's tables (see VikingGear.IndexOf)</returns>
		public static int HardestBiomeNear(Vector3 point, float reach)
		{
			if (WorldGenerator.instance == null)
			{
				return(0);
			}
			int hardest = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(point.x, point.z));
			for (float distance = BiomeRing; distance <= reach; distance += BiomeRing)
			{
				for (int i = 0; i < BiomeDirections; i++)
				{
					Vector3 sample = point + Quaternion.Euler(0f, i * 360f / BiomeDirections, 0f) * Vector3.forward * distance;
					hardest = Mathf.Max(hardest, VikingGear.IndexOf(WorldGenerator.instance.GetBiome(sample.x, sample.z)));
				}
			}
			return(hardest);
		}

		/// <summary>
		/// Checks whether a farm is in or by a biome harder than the Meadows, within HardBiomeReach, so it's one of the
		/// larger farms with at least HardMinHouses houses
		/// </summary>
		/// <param name="farm">Where the farm stands</param>
		/// <returns>True when it is</returns>
		public static bool NearHarderBiome(Vector3 farm)
		{
			return(HardestBiomeNear(farm, HardBiomeReach) > VikingGear.IndexOf(Heightmap.Biome.Meadows));
		}

		/// <summary>
		/// On a farm's generator, how far its fence stands from its middle, in meters
		/// </summary>
		public static readonly int FenceKey = "kriona.ZombieApocalypse.villageFence".GetStableHashCode();

		/// <summary>
		/// How far past the fence the generator may place a room, in meters, with room for a farmhouse past the fence
		/// </summary>
		private const float ZoneMargin = 12f;

		/// <summary>
		/// The most times as many rooms as the game's own a widened farm gets
		/// </summary>
		private const float RoomScaleMax = 3f;

		/// <summary>
		/// A farm generator's own settings, put back after it's generated
		/// </summary>
		public struct FarmSettings
		{
			public bool Widened;
			public float RadiusMin;
			public float RadiusMax;
			public int Sections;
			public Vector3 ZoneSize;
			public int MinRooms;
			public int MaxRooms;
		}

		/// <summary>
		/// How many times as many rooms the farm being generated gets as the game would give it, for the ground its
		/// widened fence takes in - 1 for any other
		/// </summary>
		public static float RoomScale { get; private set; } = 1f;

		/// <summary>
		/// Sizes a farm about to be generated, so its fence stands at a size picked between FenceRadiusMin and
		/// FenceRadiusMax from its middle - picked from the farm's own seed, so it's the same each time the farm is
		/// generated - with as many more fence sections as it's longer than the game's, as many more rooms as it takes
		/// in more ground, and its rooms allowed past its own zone
		/// </summary>
		/// <param name="generator">The farm's generator, about to place its rooms</param>
		/// <param name="mode">How the farm is being placed</param>
		/// <returns>The generator's own settings, to put back after</returns>
		public static FarmSettings Widen(DungeonGenerator generator, ZoneSystem.SpawnMode mode)
		{
			FarmSettings own = new FarmSettings();
			if (!IsVillageFarm(generator, mode))
			{
				return(own);
			}
			own.Widened = true;
			own.RadiusMin = generator.m_campRadiusMin;
			own.RadiusMax = generator.m_campRadiusMax;
			own.Sections = generator.m_perimeterSections;
			own.ZoneSize = generator.m_zoneSize;
			FarmSize size = Size(generator.GetSeed(), NearHarderBiome(generator.transform.position), own.RadiusMin, own.RadiusMax, own.Sections);
			generator.m_campRadiusMin = size.RadiusMin;
			generator.m_campRadiusMax = size.RadiusMax;
			generator.m_perimeterSections = size.Sections;
			float zone = (generator.m_campRadiusMax + ZoneMargin) * 2f;
			generator.m_zoneSize = new Vector3(Mathf.Max(own.ZoneSize.x, zone), own.ZoneSize.y, Mathf.Max(own.ZoneSize.z, zone));
			own.MinRooms = generator.m_minRooms;
			own.MaxRooms = generator.m_maxRooms;
			RoomScale = size.RoomScale;
			generator.m_minRooms = Mathf.RoundToInt(own.MinRooms * RoomScale);
			generator.m_maxRooms = Mathf.RoundToInt(own.MaxRooms * RoomScale);
			return(own);
		}

		/// <summary>
		/// A widened farm's size - the range its fence stands from its middle in, its fence sections, and how many times
		/// as many rooms it gets as the game would give it
		/// </summary>
		internal struct FarmSize
		{
			public float RadiusMin;
			public float RadiusMax;
			public int Sections;
			public float RoomScale;
		}

		/// <summary>
		/// Works out a widened farm's size from its seed, so it's the same each time the farm is generated - its fence at
		/// a size picked between FenceRadiusMin and FenceRadiusMax, from the top of that range in or by a biome harder
		/// than the Meadows, with as many more fence sections as it's longer than the game's, rounded up, and as many more
		/// rooms as it takes in more ground, up to RoomScaleMax times
		/// </summary>
		/// <param name="seed">The farm generator's seed</param>
		/// <param name="nearHarder">Whether the farm is in or by a biome harder than the Meadows (see NearHarderBiome)</param>
		/// <param name="ownRadiusMin">The least distance the game's own fence stands from the middle, in meters</param>
		/// <param name="ownRadiusMax">The most distance the game's own fence stands from the middle, in meters</param>
		/// <param name="ownSections">The game's own number of fence sections</param>
		/// <returns>The size</returns>
		internal static FarmSize Size(int seed, bool nearHarder, float ownRadiusMin, float ownRadiusMax, int ownSections)
		{
			// Its own random numbers, so the generator's rolls come out as they would
			System.Random rolls = new System.Random(seed);
			float smallest = (nearHarder ? Mathf.Lerp(FenceRadiusMin, FenceRadiusMax, HardSizeFloor) : FenceRadiusMin);
			float picked = Mathf.Lerp(smallest, FenceRadiusMax, (float)rolls.NextDouble());
			FarmSize size = new FarmSize();
			size.RadiusMin = Mathf.Max(FenceRadiusMin, picked - FenceSpread);
			size.RadiusMax = Mathf.Min(FenceRadiusMax, picked + FenceSpread);
			float longer = (size.RadiusMin + size.RadiusMax) / Mathf.Max(1f, ownRadiusMin + ownRadiusMax);
			size.Sections = Mathf.CeilToInt(ownSections * longer);
			size.RoomScale = Mathf.Clamp(longer * longer, 1f, RoomScaleMax);
			return(size);
		}

		/// <summary>
		/// Puts a farm generator's own settings back after Widen
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="own">Its own settings</param>
		public static void Unwiden(DungeonGenerator generator, FarmSettings own)
		{
			if (!own.Widened)
			{
				return;
			}
			generator.m_campRadiusMin = own.RadiusMin;
			generator.m_campRadiusMax = own.RadiusMax;
			generator.m_perimeterSections = own.Sections;
			generator.m_zoneSize = own.ZoneSize;
			generator.m_minRooms = own.MinRooms;
			generator.m_maxRooms = own.MaxRooms;
			RoomScale = 1f;
		}

		/// <summary>
		/// Checks whether a generator is an abandoned farm's, being generated where it's kept, while VikingVillages is on
		/// </summary>
		/// <param name="generator">The generator</param>
		/// <param name="mode">How its location is being placed</param>
		/// <returns>True when it is</returns>
		public static bool IsVillageFarm(DungeonGenerator generator, ZoneSystem.SpawnMode mode)
		{
			return(mode != ZoneSystem.SpawnMode.Client && Plugin.VikingVillages.Value && generator.m_algorithm == DungeonGenerator.Algorithm.CampRadial && (generator.m_themes & Room.Theme.MeadowsFarm) != 0 && ZoneSystem.instance != null);
		}

		/// <summary>
		/// How far from a farm's planned spot its generator can stand, in meters
		/// </summary>
		private const float GeneratorReach = 16f;

		/// <summary>
		/// Notes how far a farm just generated has its fence from its middle, and places more farmhouses in it when it
		/// and the houses near it come to fewer than MinHouses - or HardMinHouses in or by a biome harder than the Meadows
		/// </summary>
		/// <param name="generator">The farm's generator, with its rooms just placed</param>
		/// <param name="mode">How the farm is being placed</param>
		public static void EnsureHouses(DungeonGenerator generator, ZoneSystem.SpawnMode mode)
		{
			if (!IsVillageFarm(generator, mode))
			{
				return;
			}
			float fence = FenceRadius(generator);
			ZDO zdo = ((generator.m_nview != null) ? generator.m_nview.GetZDO() : null);
			if (zdo != null)
			{
				zdo.Set(FenceKey, fence);
			}
			List<VillagePaths.House> farmhouseList = new List<VillagePaths.House>();
			foreach (Room room in DungeonGenerator.m_placedRooms)
			{
				if (room != null && IsFarmhouse(room))
				{
					farmhouseList.Add(new VillagePaths.House { Center = room.transform.position, HalfSize = Mathf.Max(room.m_size.x, room.m_size.z) / 2f });
				}
			}
			farmhouseList.AddRange(FarmLayout.BlueprintHouses());
			int farmhouses = farmhouseList.Count;
			// Only the houses the vikings live in count - the farmhouses and the houses close by them, not the outposts. A
			// farm with no farmhouses has no core, since its vikings live in them
			List<VillagePaths.House> all = new List<VillagePaths.House>(farmhouseList);
			all.AddRange(VikingSpawns.PlannedHouses(generator.transform.position, GeneratorReach));
			int houses = ((farmhouses > 0) ? Village.Core(farmhouseList, all).Count : 0);
			int least = (NearHarderBiome(generator.transform.position) ? HardMinHouses : MinHouses);
			if (houses >= least)
			{
				return;
			}
			List<DungeonDB.RoomData> choices = new List<DungeonDB.RoomData>();
			foreach (DungeonDB.RoomData room in DungeonGenerator.m_availableRooms)
			{
				if (room.RoomInPrefab != null && IsFarmhouse(room.RoomInPrefab))
				{
					choices.Add(room);
				}
			}
			if (choices.Count == 0)
			{
				Plugin.Log.LogWarning("Found no farmhouse rooms to give the farm at " + generator.transform.position.ToString("F0") + " more houses");
				return;
			}
			int inside = 0;
			int outside = 0;
			for (int i = houses; i < least; i++)
			{
				DungeonDB.RoomData room = choices[Random.Range(0, choices.Count)];
				if (PlaceFarmhouse(generator, room, 0f, fence - generator.m_perimeterBuffer, mode))
				{
					inside++;
				}
				else if (PlaceFarmhouse(generator, room, fence + generator.m_perimeterBuffer, fence + OutsideReach, mode))
				{
					outside++;
				}
			}
			Plugin.Log.LogInfo("Placed " + inside + " more farmhouses in the farm at " + generator.transform.position.ToString("F0") + " and " + outside + " just outside its fence, which had " + farmhouses + " and " + (houses - farmhouses) + " houses close by them");
			if (houses + inside + outside < least)
			{
				Plugin.Log.LogWarning("Found no room for " + (least - houses - inside - outside) + " more farmhouses around the farm at " + generator.transform.position.ToString("F0"));
			}
		}

		/// <summary>
		/// Works out how far a farm's fence stands from its middle, from the fence sections the generator placed around
		/// it - or the farthest the generator can put it when it placed none
		/// </summary>
		/// <param name="generator">The farm's generator, with its rooms just placed</param>
		/// <returns>The fence's distance, in meters</returns>
		private static float FenceRadius(DungeonGenerator generator)
		{
			float total = 0f;
			int sections = 0;
			foreach (Room room in DungeonGenerator.m_placedRooms)
			{
				if (room != null && room.m_perimeter)
				{
					total += Utils.DistanceXZ(room.transform.position, generator.transform.position);
					sections++;
				}
			}
			return((sections > 0) ? total / sections : generator.m_campRadiusMax);
		}

		/// <summary>
		/// Places a farmhouse at a random spot between two distances from a farm's middle, on ground that isn't too steep
		/// or low, clear of the rooms already placed and inside the generator's area - the same checks the generator
		/// makes for its own rooms
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="room">The farmhouse room</param>
		/// <param name="nearest">The least distance from the middle, in meters</param>
		/// <param name="farthest">The most distance from the middle</param>
		/// <param name="mode">How the farm is being placed</param>
		/// <returns>False when no spot had room for it</returns>
		private static bool PlaceFarmhouse(DungeonGenerator generator, DungeonDB.RoomData room, float nearest, float farthest, ZoneSystem.SpawnMode mode)
		{
			float steepest = Mathf.Cos(Mathf.Deg2Rad * generator.m_maxTilt);
			for (int i = 0; i < TriesPerHouse; i++)
			{
				float distance = Random.Range(nearest, Mathf.Max(nearest, farthest));
				Vector3 point = generator.transform.position + Quaternion.Euler(0f, Random.Range(0, 360), 0f) * Vector3.forward * distance;
				FarmLayout.GroundData(ref point, out Vector3 normal);
				if (normal.y < steepest || point.y - 30f < generator.m_minAltitude)
				{
					continue;
				}
				Quaternion rotation = generator.GetCampRoomRotation(room, point);
				if (!FarmLayout.OverlapsBlueprint(room.RoomInPrefab, point, rotation) && !generator.TestCollision(room.RoomInPrefab, point, rotation))
				{
					generator.PlaceRoom(room, point, rotation, null, mode);
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a room is a farmhouse
		/// </summary>
		/// <param name="room">The room, placed or a prefab's</param>
		/// <returns>True when it is</returns>
		private static bool IsFarmhouse(Room room)
		{
			return(room.gameObject.name.StartsWith(VikingSpawns.FarmhousePrefix, System.StringComparison.OrdinalIgnoreCase));
		}
	}
}
