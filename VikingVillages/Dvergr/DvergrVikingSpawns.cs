using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Where dvergr vikings come from - the dvergr's outposts, which each hire a few once, and the dvergr's patrols,
	/// which now and then have one along
	/// </summary>
	/// <remarks>
	/// An outpost is a location whose prefab spawns dvergr. Its dvergr only spawn once a player comes near, so its
	/// vikings are hired once a player has been within the outpost for SettleTime seconds and some of its dvergr are
	/// standing - up to a share of the dvergr still alive there (see DvergrVikingSpawnRules.Cap), which in an outpost
	/// whose dvergr were killed before the mod came along is none. The outpost's location proxy notes that it has
	/// hired them, so it never does again, whether they live or die - the client that owns the proxy does the hiring.
	/// A patrol is a group of the world's dvergr spawns, and one in PatrolChance has a viking spawn with it
	/// </remarks>
	internal static class DvergrVikingSpawns
	{
		/// <summary>
		/// On an outpost's location proxy, that it has hired its dvergr vikings
		/// </summary>
		private static readonly int HiredKey = "kriona.VikingVillages.dvergrVikingsHired".GetStableHashCode();

		/// <summary>
		/// How likely a dvergr patrol has a viking along
		/// </summary>
		private const float PatrolChance = 0.5f;

		/// <summary>
		/// How long a player has to be within an outpost before it hires its vikings, in seconds, so its dvergr have
		/// spawned, and how often the outposts are looked at
		/// </summary>
		private const float SettleTime = 5f;
		private const float CheckInterval = 2f;

		/// <summary>
		/// How far past an outpost's reach its dvergr are counted, in meters, for one standing at its edge
		/// </summary>
		private const float CountMargin = 5f;

		/// <summary>
		/// How far from the dvergr it stands beside a viking spawns, at least and at most, in meters
		/// </summary>
		private const float SpreadMin = 1.5f;
		private const float SpreadMax = 3f;

		/// <summary>
		/// How far above the ground a viking spawning looks down for it, in meters, so it lands on the floor the dvergr
		/// beside it stands on rather than a roof above them
		/// </summary>
		private const float FloorLookHeight = 0.5f;

		/// <summary>
		/// Whether each location, by its prefab's name hash, is an outpost of the dvergr
		/// </summary>
		private static readonly Dictionary<int, bool> outposts = new Dictionary<int, bool>();

		/// <summary>
		/// Since when a player has been within each outpost loaded on this client, in Time.time seconds
		/// </summary>
		private static readonly Dictionary<Location, float> nearSince = new Dictionary<Location, float>();

		/// <summary>
		/// When the outposts are next looked at
		/// </summary>
		private static float nextCheck;

		/// <summary>
		/// The spawn and frame of the last dvergr the world spawned, so the rest of its group are known as the same
		/// patrol
		/// </summary>
		private static SpawnSystem.SpawnData lastPatrol;
		private static int lastPatrolFrame = -1;

		/// <summary>
		/// Has each outpost a player has been within for SettleTime seconds hire its vikings, every CheckInterval
		/// seconds, on the client that owns its location proxy while the server has the mod - call it from the plugin's
		/// Update
		/// </summary>
		public static void Update()
		{
			if (Time.time < nextCheck)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			if (!ServerCheck.Active || ZNetScene.instance == null || ZoneSystem.instance == null)
			{
				nearSince.Clear();
				return;
			}
			List<Location> gone = new List<Location>();
			foreach (Location location in nearSince.Keys)
			{
				if (location == null)
				{
					gone.Add(location);
				}
			}
			foreach (Location location in gone)
			{
				nearSince.Remove(location);
			}
			foreach (Location location in Location.s_allLocations)
			{
				if (location != null)
				{
					CheckOutpost(location);
				}
			}
		}

		/// <summary>
		/// Forgets the outposts players were within and the last patrol spawned, as the world is left
		/// </summary>
		public static void Forget()
		{
			nearSince.Clear();
			lastPatrol = null;
			lastPatrolFrame = -1;
		}

		/// <summary>
		/// Has an outpost hire its vikings once a player has been within it for SettleTime seconds with some of its dvergr
		/// standing, unless it has already, on the client that owns its location proxy
		/// </summary>
		/// <param name="location">The location</param>
		private static void CheckOutpost(Location location)
		{
			LocationProxy proxy = location.GetComponentInParent<LocationProxy>();
			ZNetView view = ((proxy != null) ? proxy.m_nview : null);
			if (view == null || !view.IsValid() || !view.IsOwner())
			{
				nearSince.Remove(location);
				return;
			}
			ZDO zdo = view.GetZDO();
			if (zdo.GetBool(HiredKey) || !IsOutpost(zdo.GetInt(ZDOVars.s_location)))
			{
				return;
			}
			Vector3 center = location.transform.position;
			float reach = location.GetMaxRadius();
			if (!Player.IsPlayerInRange(center, reach))
			{
				nearSince.Remove(location);
				return;
			}
			if (!nearSince.TryGetValue(location, out float since))
			{
				nearSince[location] = Time.time;
				return;
			}
			if (Time.time - since < SettleTime)
			{
				return;
			}
			List<Character> dvergr = DvergrNear(center, reach + CountMargin);
			if (dvergr.Count == 0)
			{
				return;
			}
			int hired = DvergrVikingSpawnRules.Roll(DvergrVikingSpawnRules.Cap(dvergr.Count), Random.value);
			float levelUp = SpawnSystem.GetLevelUpChance(center, Mathf.Max(0f, location.m_enemyLevelUpOverride));
			for (int i = 0; i < hired; i++)
			{
				Character beside = dvergr[Random.Range(0, dvergr.Count)];
				BaseAI ai = beside.GetBaseAI();
				Spawn(beside.transform.position, SpreadMin, SpreadMax, levelUp, ai != null && ai.m_patrol);
			}
			zdo.Set(HiredKey, true);
			nearSince.Remove(location);
			if (Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Dvergr outpost at " + center.ToString("F0") + " hired " + hired + " dvergr vikings, with " + dvergr.Count + " dvergr there");
			}
		}

		/// <summary>
		/// Checks whether a location is an outpost of the dvergr - one whose prefab spawns dvergr - looking at its prefab
		/// the first time
		/// </summary>
		/// <param name="hash">The location's prefab name hash</param>
		/// <returns>True when it is</returns>
		private static bool IsOutpost(int hash)
		{
			if (outposts.TryGetValue(hash, out bool known))
			{
				return(known);
			}
			bool outpost = false;
			ZoneSystem.ZoneLocation zoneLocation = ZoneSystem.instance.GetLocation(hash);
			if (zoneLocation != null && zoneLocation.m_prefab.IsValid)
			{
				zoneLocation.m_prefab.Load();
				GameObject asset = zoneLocation.m_prefab.Asset;
				if (asset != null)
				{
					foreach (CreatureSpawner spawner in asset.GetComponentsInChildren<CreatureSpawner>(true))
					{
						if (spawner.m_creaturePrefab != null && DvergrVikingSpawnRules.IsDvergrName(spawner.m_creaturePrefab.name))
						{
							outpost = true;
							break;
						}
					}
				}
				zoneLocation.m_prefab.Release();
			}
			outposts[hash] = outpost;
			return(outpost);
		}

		/// <summary>
		/// Lists the dvergr alive within a distance of a point
		/// </summary>
		/// <param name="center">The point</param>
		/// <param name="range">The distance, in meters</param>
		/// <returns>The dvergr</returns>
		private static List<Character> DvergrNear(Vector3 center, float range)
		{
			List<Character> found = new List<Character>();
			foreach (Character character in Character.GetAllCharacters())
			{
				if (!character.IsDead() && DvergrVikingSpawnRules.IsDvergrName(Utils.GetPrefabName(character.gameObject)) && Vector3.Distance(character.transform.position, center) <= range)
				{
					found.Add(character);
				}
			}
			return(found);
		}

		/// <summary>
		/// Has one in PatrolChance of the world's dvergr patrols spawn with a viking along - rolled as the first of a
		/// group spawns, while the server has the mod
		/// </summary>
		/// <param name="critter">The spawn</param>
		/// <param name="spawnPoint">Where the dvergr spawned</param>
		/// <param name="eventSpawner">Whether an event spawned it</param>
		public static void OnSpawned(SpawnSystem.SpawnData critter, Vector3 spawnPoint, bool eventSpawner)
		{
			if (SpawnSystem.m_nospawn || eventSpawner || critter == null || critter.m_prefab == null || !DvergrVikingSpawnRules.IsDvergrName(critter.m_prefab.name) || !ServerCheck.Active)
			{
				return;
			}
			// The rest of the group spawns in the same frame, from the same spawn
			if (critter == lastPatrol && Time.frameCount == lastPatrolFrame)
			{
				return;
			}
			lastPatrol = critter;
			lastPatrolFrame = Time.frameCount;
			if (Random.value >= PatrolChance)
			{
				return;
			}
			Spawn(spawnPoint, 0f, Mathf.Max(SpreadMax, critter.m_groupRadius), SpawnSystem.GetLevelUpChance(spawnPoint, critter), false);
			if (Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Dvergr patrol at " + spawnPoint.ToString("F0") + " spawned with a dvergr viking");
			}
		}

		/// <summary>
		/// Spawns a dvergr viking near a spot, on the floor there, with one star at the level up chance the dvergr spawn
		/// with
		/// </summary>
		/// <param name="near">The spot</param>
		/// <param name="spreadMin">How far from the spot it stands at least, in meters</param>
		/// <param name="spreadMax">How far from the spot it stands at most, in meters</param>
		/// <param name="levelUpChance">The percent chance it has a star</param>
		/// <param name="patrol">True to have it keep to where it spawned, the way a dvergr standing guard does</param>
		private static void Spawn(Vector3 near, float spreadMin, float spreadMax, float levelUpChance, bool patrol)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(VikingCore.DvergrPrefabHash);
			if (prefab == null)
			{
				return;
			}
			Vector2 heading = Random.insideUnitCircle.normalized;
			Vector2 offset = heading * Random.Range(spreadMin, spreadMax);
			Vector3 spot = near + new Vector3(offset.x, 0f, offset.y);
			if (ZoneSystem.instance.FindFloor(spot + Vector3.up * FloorLookHeight, out float height))
			{
				spot.y = height;
			}
			GameObject spawned = Object.Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			Character character = spawned.GetComponent<Character>();
			int level = DvergrVikingSpawnRules.Level(levelUpChance, Random.Range(0f, 100f));
			if (character != null && level > 1)
			{
				character.SetLevel(level);
			}
			BaseAI ai = spawned.GetComponent<BaseAI>();
			if (ai != null && patrol)
			{
				ai.SetPatrolPoint();
			}
		}
	}

	/// <summary>
	/// What decides which creatures are dvergr, how many dvergr vikings an outpost hires, and their stars
	/// </summary>
	internal static class DvergrVikingSpawnRules
	{
		/// <summary>
		/// The start of the names of the dvergr's prefabs - the dvergr and their mages
		/// </summary>
		private const string DvergrPrefix = "Dverger";

		/// <summary>
		/// Checks whether a prefab is one of the dvergr's own - a dvergr or one of their mages, and not a dvergr viking
		/// </summary>
		/// <param name="name">The prefab's name</param>
		/// <returns>True when it is</returns>
		internal static bool IsDvergrName(string name)
		{
			return(name != null && name.StartsWith(DvergrPrefix, System.StringComparison.Ordinal));
		}

		/// <summary>
		/// The most vikings an outpost hires, and the share of its dvergr it hires at most
		/// </summary>
		internal const int OutpostMax = 4;
		internal const float OutpostShare = 0.25f;

		/// <summary>
		/// A dvergr viking's highest level, where 2 is one star - the same as a zombie viking's by day
		/// </summary>
		internal const int MaxLevel = 2;

		/// <summary>
		/// Works out the most vikings an outpost hires - OutpostShare of its dvergr alive, rounded to the nearest, up to
		/// OutpostMax
		/// </summary>
		/// <param name="dvergr">How many dvergr are alive there</param>
		/// <returns>The most it hires</returns>
		internal static int Cap(int dvergr)
		{
			return(Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, dvergr) * OutpostShare + 0.5f), 0, OutpostMax));
		}

		/// <summary>
		/// Works out how many vikings an outpost hires - any number from none to its cap, each as likely
		/// </summary>
		/// <param name="cap">The most it hires</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>How many it hires</returns>
		internal static int Roll(int cap, float roll)
		{
			if (cap <= 0)
			{
				return(0);
			}
			return(Mathf.Min(cap, Mathf.FloorToInt(Mathf.Clamp01(roll) * (cap + 1))));
		}

		/// <summary>
		/// Works out a viking's level - one star when the roll is within the level up chance, the way the game levels up
		/// the creatures it spawns, up to MaxLevel
		/// </summary>
		/// <param name="levelUpChance">The percent chance it levels up</param>
		/// <param name="roll">A random number from 0 to 100</param>
		/// <returns>Its level, where 1 is no stars</returns>
		internal static int Level(float levelUpChance, float roll)
		{
			return((MaxLevel > 1 && roll <= levelUpChance) ? 2 : 1);
		}
	}
}
