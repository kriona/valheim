using System.Collections.Generic;
using System.Linq;
using Kriona.Vikings;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Where zombie vikings of the world come from - the world's spawns by day in the Swamp, the Ashlands, Dark Meadows
	/// and Ruin Black Forest and by night anywhere, the draugr's body piles, and abandoned houses as they're generated
	/// </summary>
	/// <remarks>
	/// The world's spawns copy the draugr's Swamp spawn and count against their own cap the same way, a little more
	/// often than draugr by day and less often at night, least in the Meadows. A night spawn flees at dawn like the
	/// game's other night spawns
	/// </remarks>
	internal static class VikingSpawns
	{
		/// <summary>
		/// The start of the name of every spawn added here, which marks a spawn list as done
		/// </summary>
		private const string SpawnPrefix = "ZombieViking";

		private const string TemplateCreature = "Draugr";
		private const string BodyPile = "Spawner_DraugrPile";

		/// <summary>
		/// The alt biomes vikings spawn in by day, named as the game names them with spaces and case ignored
		/// </summary>
		private static readonly string[] DayAltBiomes = new[] { "darkmeadows", "ruinblackforest" };

		/// <summary>
		/// A viking's highest level by day and at night, where 2 is one star
		/// </summary>
		private const int DayMaxLevel = 2;
		private const int NightMaxLevel = 3;

		/// <summary>
		/// How often vikings spawn compared to the draugr spawn they copy - by day outside the Meadows, and at night in
		/// the Meadows and everywhere else
		/// </summary>
		private const float DayChanceScale = 1.25f;
		private const float MeadowsNightChanceScale = 0.5f;
		private const float NightChanceScale = 0.75f;

		/// <summary>
		/// The most night vikings near a player in the Meadows and the Black Forest, which come one at a time, and the
		/// size of the groups that spawn from the Swamp on
		/// </summary>
		private const int MeadowsNightMax = 1;
		private const int BlackForestNightMax = 2;
		private const int NightGroupMax = 3;

		/// <summary>
		/// How likely a body pile spawns a viking compared to each of its usual creatures
		/// </summary>
		private const float BodyPileWeightScale = 0.5f;

		/// <summary>
		/// The start of the name of the abandoned houses, how likely an empty one holds vikings, and how many
		/// </summary>
		private const string HousePrefix = "WoodHouse";
		private const float HouseChance = 0.66f;
		private const int HouseVikingsMin = 1;
		private const int HouseVikingsMax = 2;

		/// <summary>
		/// Whether the missing draugr spawn has been logged, so it's only logged once
		/// </summary>
		private static bool warnedNoTemplate;

		/// <summary>
		/// Adds the viking to the spawns of the body piles that spawn draugr, so vikings count toward the piles' caps
		/// alongside the draugr
		/// </summary>
		/// <param name="scene">The world's ZNetScene, with the viking registered</param>
		public static void AddToBodyPiles(ZNetScene scene)
		{
			GameObject viking = scene.GetPrefab(VikingCore.ZombiePrefabHash);
			GameObject pile = scene.GetPrefab(BodyPile);
			if (viking == null || pile == null)
			{
				return;
			}
			foreach (SpawnArea area in pile.GetComponentsInChildren<SpawnArea>(true))
			{
				if (area.m_prefabs.Count == 0 || area.m_prefabs.Any((SpawnArea.SpawnData spawn) => spawn.m_prefab == viking))
				{
					continue;
				}
				SpawnArea.SpawnData added = new SpawnArea.SpawnData();
				added.m_prefab = viking;
				added.m_weight = area.m_prefabs.Average((SpawnArea.SpawnData spawn) => spawn.m_weight) * BodyPileWeightScale;
				added.m_minLevel = 1;
				added.m_maxLevel = area.m_prefabs.Max((SpawnArea.SpawnData spawn) => spawn.m_maxLevel);
				area.m_prefabs.Add(added);
			}
		}

		/// <summary>
		/// Checks whether a prefab is the zombie viking's
		/// </summary>
		/// <param name="prefab">The prefab, or null</param>
		/// <returns>True when it is</returns>
		public static bool IsVikingPrefab(GameObject prefab)
		{
			return(prefab != null && prefab.name == VikingCore.ZombiePrefabName);
		}

		/// <summary>
		/// Adds the viking's spawns to the world's spawn lists and its alt biomes' lists, once each
		/// </summary>
		/// <param name="system">A zone's spawn system as it wakes</param>
		public static void AddToSpawnLists(SpawnSystem system)
		{
			GameObject viking = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(VikingCore.ZombiePrefabHash) : null);
			SpawnSystem.SpawnData template = FindTemplate(system);
			if (viking == null || template == null)
			{
				if (viking != null && !warnedNoTemplate)
				{
					warnedNoTemplate = true;
					Plugin.Log.LogWarning("Couldn't find the draugr's Swamp spawn to copy, so zombie vikings won't spawn in the world");
				}
				return;
			}
			SpawnSystemList first = system.m_spawnLists.FirstOrDefault((SpawnSystemList list) => list != null);
			if (first != null && !HasViking(first.m_spawners))
			{
				SpawnSystem.SpawnData swamp = MakeSpawn(template, viking, "Swamp", Heightmap.Biome.Swamp);
				swamp.m_spawnChance = template.m_spawnChance * DayChanceScale;
				first.m_spawners.Add(swamp);
				SpawnSystem.SpawnData ashlands = AnyGround(MakeSpawn(template, viking, "Ashlands", Heightmap.Biome.AshLands));
				ashlands.m_spawnChance = template.m_spawnChance * DayChanceScale;
				first.m_spawners.Add(ashlands);
				SpawnSystem.SpawnData meadows = MakeNightSpawn(template, viking, "NightMeadows", Heightmap.Biome.Meadows, MeadowsNightChanceScale, 1, MeadowsNightMax);
				first.m_spawners.Add(meadows);
				SpawnSystem.SpawnData forest = MakeNightSpawn(template, viking, "NightBlackForest", Heightmap.Biome.BlackForest, NightChanceScale, 1, BlackForestNightMax);
				first.m_spawners.Add(forest);
				Heightmap.Biome later = Heightmap.Biome.Land & ~(Heightmap.Biome.Meadows | Heightmap.Biome.BlackForest);
				first.m_spawners.Add(MakeNightSpawn(template, viking, "NightGroups", later, NightChanceScale, NightGroupMax, NightGroupMax));
				Plugin.Log.LogInfo("Zombie viking spawns added - every " + template.m_spawnInterval + "s, by day at " + swamp.m_spawnChance + "%, and at night at " + meadows.m_spawnChance + "% in the Meadows (one at a time) and " + forest.m_spawnChance + "% elsewhere (up to " + BlackForestNightMax + " in the Black Forest, groups of 1-" + NightGroupMax + " from the Swamp on)");
			}
			foreach (AltBiome alt in AltBiomeList.m_altBiomes)
			{
				if (alt != null && DayAltBiomes.Contains(Simplify(alt.m_name)) && !HasViking(alt.m_spawn))
				{
					SpawnSystem.SpawnData spawn = AnyGround(MakeSpawn(template, viking, Simplify(alt.m_name), alt.m_biome));
					if ((alt.m_biome & Heightmap.Biome.Meadows) == 0)
					{
						spawn.m_spawnChance = template.m_spawnChance * DayChanceScale;
					}
					alt.m_spawn.Add(spawn);
					Plugin.Log.LogInfo("Zombie viking spawn added to " + alt.m_name + " at " + spawn.m_spawnChance + "%");
				}
			}
		}

		/// <summary>
		/// Makes a viking spawn for the night only, up to two stars, on any dry ground of its biomes
		/// </summary>
		/// <param name="template">The draugr's spawn</param>
		/// <param name="viking">The viking prefab</param>
		/// <param name="label">What the spawn is for, added to its name</param>
		/// <param name="biome">Where it spawns</param>
		/// <param name="chanceScale">How often it spawns compared to the draugr</param>
		/// <param name="groupMax">The most vikings it spawns at once</param>
		/// <param name="maxSpawned">The most near a player</param>
		/// <returns>The spawn</returns>
		private static SpawnSystem.SpawnData MakeNightSpawn(SpawnSystem.SpawnData template, GameObject viking, string label, Heightmap.Biome biome, float chanceScale, int groupMax, int maxSpawned)
		{
			SpawnSystem.SpawnData night = AnyGround(MakeSpawn(template, viking, label, biome));
			night.m_spawnAtDay = false;
			night.m_spawnAtNight = true;
			night.m_maxLevel = NightMaxLevel;
			night.m_spawnChance = template.m_spawnChance * chanceScale;
			night.m_groupSizeMin = 1;
			night.m_groupSizeMax = groupMax;
			night.m_maxSpawned = maxSpawned;
			return(night);
		}

		/// <summary>
		/// Lets a spawn place vikings on any dry ground of its biome, in place of the draugr's limits for the low,
		/// flat Swamp
		/// </summary>
		/// <param name="spawn">The spawn</param>
		/// <returns>The same spawn</returns>
		private static SpawnSystem.SpawnData AnyGround(SpawnSystem.SpawnData spawn)
		{
			spawn.m_minAltitude = 0f;
			spawn.m_maxAltitude = 1000f;
			spawn.m_minTilt = 0f;
			spawn.m_maxTilt = 35f;
			spawn.m_inForest = true;
			spawn.m_outsideForest = true;
			spawn.m_minDistanceFromCenter = 0f;
			spawn.m_maxDistanceFromCenter = 0f;
			return(spawn);
		}

		/// <summary>
		/// Finds the draugr's Swamp spawn that the viking's spawns copy
		/// </summary>
		/// <param name="system">A zone's spawn system</param>
		/// <returns>The spawn, or null when the game has none</returns>
		private static SpawnSystem.SpawnData FindTemplate(SpawnSystem system)
		{
			foreach (SpawnSystemList list in system.m_spawnLists)
			{
				if (list == null)
				{
					continue;
				}
				foreach (SpawnSystem.SpawnData spawn in list.m_spawners)
				{
					if (spawn.m_prefab != null && spawn.m_prefab.name == TemplateCreature && (spawn.m_biome & Heightmap.Biome.Swamp) != 0 && !spawn.m_name.StartsWith(SpawnPrefix))
					{
						return(spawn);
					}
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether a spawn list already has the viking's spawns
		/// </summary>
		/// <param name="spawns">The list</param>
		/// <returns>True when it has</returns>
		private static bool HasViking(List<SpawnSystem.SpawnData> spawns)
		{
			return(spawns.Any((SpawnSystem.SpawnData spawn) => spawn.m_name != null && spawn.m_name.StartsWith(SpawnPrefix)));
		}

		/// <summary>
		/// Makes a viking spawn from the draugr's, for any time of day, up to one star
		/// </summary>
		/// <param name="template">The draugr's spawn</param>
		/// <param name="viking">The viking prefab</param>
		/// <param name="label">What the spawn is for, added to its name</param>
		/// <param name="biome">Where it spawns</param>
		/// <returns>The spawn</returns>
		private static SpawnSystem.SpawnData MakeSpawn(SpawnSystem.SpawnData template, GameObject viking, string label, Heightmap.Biome biome)
		{
			SpawnSystem.SpawnData spawn = template.Clone();
			spawn.m_name = SpawnPrefix + "_" + label;
			spawn.m_enabled = true;
			spawn.m_devDisabled = false;
			spawn.m_prefab = viking;
			spawn.m_biome = biome;
			spawn.m_biomeArea = Heightmap.BiomeArea.Everything;
			spawn.m_requiredGlobalKey = "";
			spawn.m_requiredEnvironments = new List<string>();
			spawn.m_requiredPersistentEvent = "";
			spawn.m_spawnAtDay = true;
			spawn.m_spawnAtNight = true;
			spawn.m_inLava = false;
			spawn.m_outsideLava = true;
			spawn.m_huntPlayer = false;
			spawn.m_minLevel = 1;
			spawn.m_maxLevel = DayMaxLevel;
			return(spawn);
		}

		/// <summary>
		/// Lowercases a name and takes out its spaces
		/// </summary>
		/// <param name="name">The name, or null</param>
		/// <returns>The simplified name</returns>
		internal static string Simplify(string name)
		{
			return((name ?? "").Replace(" ", "").Replace("_", "").ToLowerInvariant());
		}

		/// <summary>
		/// Puts one or two zombie vikings in an abandoned house with no creatures of its own, by HouseChance, as it's
		/// generated - which happens once, the first time its zone is - unless Viking Villages' living vikings take the
		/// house in, as one near an abandoned farm (see VillageLink.ClaimsHouse)
		/// </summary>
		/// <param name="location">The location just placed</param>
		/// <param name="position">Where it stands</param>
		/// <param name="mode">How it was placed - in full or as a ghost being saved without being shown, or for a client
		/// showing a location that's already there</param>
		/// <param name="ghosts">The objects of a ghost placement, which the game saves and removes</param>
		public static void OnLocationSpawned(ZoneSystem.ZoneLocation location, Vector3 position, ZoneSystem.SpawnMode mode, List<GameObject> ghosts)
		{
			if (mode == ZoneSystem.SpawnMode.Client || location == null || ZNetScene.instance == null)
			{
				return;
			}
			string name = (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name);
			if (name == null || !name.StartsWith(HousePrefix, System.StringComparison.OrdinalIgnoreCase) || !IsEmpty(location) || VillageLink.ClaimsHouse(position) || Random.value >= HouseChance)
			{
				return;
			}
			GameObject viking = ZNetScene.instance.GetPrefab(VikingCore.ZombiePrefabHash);
			if (viking == null)
			{
				return;
			}
			SpawnGroup(viking, position, Random.Range(HouseVikingsMin, HouseVikingsMax + 1), mode, ghosts);
			Plugin.Log.LogInfo("Zombie vikings in " + name + " at " + position.ToString("F0"));
		}

		/// <summary>
		/// Spawns vikings around a point in a location being placed, each with a chance of a star and patrolling where it
		/// stands
		/// </summary>
		/// <param name="viking">The viking prefab</param>
		/// <param name="position">Where they gather</param>
		/// <param name="count">How many</param>
		/// <param name="mode">How the location was placed</param>
		/// <param name="ghosts">The objects of a ghost placement, which the game saves and removes</param>
		private static void SpawnGroup(GameObject viking, Vector3 position, int count, ZoneSystem.SpawnMode mode, List<GameObject> ghosts)
		{
			for (int i = 0; i < count; i++)
			{
				Vector2 offset = Random.insideUnitCircle * 1.5f;
				Spawn(viking, position + new Vector3(offset.x, 0.5f, offset.y), mode, ghosts);
			}
		}

		/// <summary>
		/// Spawns a zombie viking at a point in a location being placed, with a chance of a star, patrolling where it
		/// stands
		/// </summary>
		/// <param name="viking">The viking prefab</param>
		/// <param name="point">Where it stands</param>
		/// <param name="mode">How the location was placed</param>
		/// <param name="ghosts">The objects of a ghost placement, which the game saves and removes</param>
		private static void Spawn(GameObject viking, Vector3 point, ZoneSystem.SpawnMode mode, List<GameObject> ghosts)
		{
			if (mode == ZoneSystem.SpawnMode.Ghost)
			{
				ZNetView.StartGhostInit();
			}
			GameObject instance = Object.Instantiate(viking, point, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			Character character = instance.GetComponent<Character>();
			if (character != null && Random.Range(0f, 100f) <= SpawnSystem.GetLevelUpChance(point))
			{
				character.SetLevel(DayMaxLevel);
			}
			BaseAI ai = instance.GetComponent<BaseAI>();
			if (ai != null)
			{
				ai.SetPatrolPoint();
			}
			if (mode == ZoneSystem.SpawnMode.Ghost)
			{
				ghosts.Add(instance);
				ZNetView.FinishGhostInit();
			}
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
