using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kriona.Vikings;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// A raid of zombie vikings, possible at a base in any biome from the start of the game
	/// </summary>
	/// <remarks>
	/// The raid copies the draugr raid's music, length and spawns, without its boss requirement and in every land
	/// biome, and leaves the weather and light as they are. A viking takes its gear, health and skill from the biome it spawns in, so the raid is as hard as the
	/// biome the base is in. Vikings are a little tougher than the creatures of other raids, so fewer come at once. Every
	/// game with the mod adds the raid, since a client looks the raid up by name to show its messages, and a player only
	/// reports the raids their own game has for player-based raids
	/// </remarks>
	internal static class VikingRaid
	{
		public const string EventName = "army_zombievikings";

		/// <summary>
		/// The draugr raid the viking raid copies, and the creature in it whose spawn the vikings take
		/// </summary>
		private const string TemplateEvent = "army_bonemass";
		private const string TemplateCreature = "Draugr";

		private const string StartMessage = "The fallen rise to claim your soul";
		private const string EndMessage = "The fallen rest again";

		/// <summary>
		/// How many vikings come compared to the draugr of the raid it copies
		/// </summary>
		private const float SpawnScale = 0.6f;

		/// <summary>
		/// The raid added to the world's raid system, or null before it's added
		/// </summary>
		private static RandomEvent raid;

		/// <summary>
		/// Turns the raid on or off by VikingRaids, which a raid already underway doesn't notice since it runs as a copy
		/// </summary>
		public static void UpdateEnabled()
		{
			if (raid != null)
			{
				raid.m_enabled = Enabled();
			}
		}

		/// <summary>
		/// Checks whether the raid is on (see Plugin.VikingRaids)
		/// </summary>
		/// <returns>True when it is</returns>
		private static bool Enabled()
		{
			return(Plugin.VikingRaids.Value);
		}

		/// <summary>
		/// Adds the raid to the game's raids, once, turned on by VikingRaids and Zombies
		/// </summary>
		/// <param name="system">The raid system as it starts</param>
		public static void Add(RandEventSystem system)
		{
			GameObject viking = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(VikingCore.ZombiePrefabHash) : null);
			if (viking == null || system.m_events.Any((RandomEvent existing) => existing.m_name == EventName))
			{
				return;
			}
			RandomEvent template = FindTemplate(system);
			SpawnSystem.SpawnData spawnTemplate = ((template != null) ? FindSpawn(template) : null);
			if (spawnTemplate == null)
			{
				Plugin.Log.LogWarning("Couldn't find a draugr raid to copy, so there's no zombie viking raid");
				return;
			}
			raid = template.Clone();
			raid.m_name = EventName;
			raid.m_enabled = Enabled();
			raid.m_devDisabled = false;
			raid.m_random = true;
			raid.m_standaloneInterval = 0f;
			raid.m_biome = Heightmap.Biome.Land;
			raid.m_requiredGlobalKeys = new List<string>();
			raid.m_notRequiredGlobalKeys = new List<string>();
			raid.m_altRequiredKnownItems = new List<ItemDrop>();
			raid.m_altRequiredNotKnownItems = new List<ItemDrop>();
			raid.m_altRequiredPlayerKeysAny = new List<string>();
			raid.m_altRequiredPlayerKeysAll = new List<string>();
			raid.m_altNotRequiredPlayerKeys = new List<string>();
			raid.m_startMessage = StartMessage;
			raid.m_endMessage = EndMessage;
			raid.m_forceEnvironment = "";
			raid.m_spawn = new List<SpawnSystem.SpawnData>();
			raid.m_spawn.Add(MakeSpawn(spawnTemplate, viking));
			system.m_events.Add(raid);
			SpawnSystem.SpawnData spawn = raid.m_spawn[0];
			Plugin.Log.LogInfo("Zombie viking raid added - up to " + spawn.m_maxSpawned + " at once in groups of " + spawn.m_groupSizeMin + "-" + spawn.m_groupSizeMax + ", where the " + template.m_name + " raid has up to " + spawnTemplate.m_maxSpawned + " draugr and the " + template.m_forceEnvironment + " weather");
		}

		/// <summary>
		/// Finds the draugr raid, or else any raid that spawns draugr
		/// </summary>
		/// <param name="system">The raid system</param>
		/// <returns>The raid, or null when the game has none</returns>
		private static RandomEvent FindTemplate(RandEventSystem system)
		{
			RandomEvent named = system.m_events.FirstOrDefault((RandomEvent existing) => existing.m_name == TemplateEvent);
			if (named != null && FindSpawn(named) != null)
			{
				return(named);
			}
			return(system.m_events.FirstOrDefault((RandomEvent existing) => existing.m_name != EventName && FindSpawn(existing) != null));
		}

		/// <summary>
		/// Finds a raid's draugr spawn
		/// </summary>
		/// <param name="raid">The raid</param>
		/// <returns>The spawn, or null when the raid has none</returns>
		private static SpawnSystem.SpawnData FindSpawn(RandomEvent raid)
		{
			return(raid.m_spawn.FirstOrDefault((SpawnSystem.SpawnData spawn) => spawn.m_prefab != null && spawn.m_prefab.name == TemplateCreature));
		}

		/// <summary>
		/// Makes the raid's viking spawn from its draugr spawn, with fewer at once, on any dry ground of any land biome
		/// </summary>
		/// <param name="template">The draugr spawn</param>
		/// <param name="viking">The viking prefab</param>
		/// <returns>The spawn</returns>
		private static SpawnSystem.SpawnData MakeSpawn(SpawnSystem.SpawnData template, GameObject viking)
		{
			SpawnSystem.SpawnData spawn = template.Clone();
			spawn.m_name = "ZombieViking_Raid";
			spawn.m_enabled = true;
			spawn.m_devDisabled = false;
			spawn.m_prefab = viking;
			spawn.m_biome = Heightmap.Biome.Land;
			spawn.m_biomeArea = Heightmap.BiomeArea.Everything;
			spawn.m_requiredGlobalKey = "";
			spawn.m_requiredEnvironments = new List<string>();
			spawn.m_requiredPersistentEvent = "";
			spawn.m_spawnAtDay = true;
			spawn.m_spawnAtNight = true;
			spawn.m_inLava = false;
			spawn.m_outsideLava = true;
			spawn.m_minAltitude = 0f;
			spawn.m_maxAltitude = 1000f;
			spawn.m_minTilt = 0f;
			spawn.m_maxTilt = 35f;
			spawn.m_inForest = true;
			spawn.m_outsideForest = true;
			spawn.m_minDistanceFromCenter = 0f;
			spawn.m_maxDistanceFromCenter = 0f;
			spawn.m_maxSpawned = Scaled(template.m_maxSpawned);
			spawn.m_groupSizeMin = 1;
			spawn.m_groupSizeMax = Scaled(template.m_groupSizeMax);
			return(spawn);
		}

		/// <summary>
		/// Works out how many vikings come for a number of the draugr of the raid it copies - SpawnScale as many, rounded,
		/// and at least one
		/// </summary>
		/// <param name="draugr">How many draugr</param>
		/// <returns>How many vikings</returns>
		internal static int Scaled(int draugr)
		{
			return(Mathf.Max(1, Mathf.RoundToInt(draugr * SpawnScale)));
		}
	}

	/// <summary>
	/// Adds the zombie viking raid as the raid system starts
	/// </summary>
	[HarmonyPatch(typeof(RandEventSystem), nameof(RandEventSystem.Start))]
	public static class VikingRaidPatch
	{
		/// <summary>
		/// Adds the raid to the game's raids, once every object has woken, so the viking prefab is registered
		/// </summary>
		/// <param name="__instance">The raid system</param>
		private static void Postfix(RandEventSystem __instance)
		{
			VikingRaid.Add(__instance);
		}
	}
}
