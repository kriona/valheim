using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Kriona.Shared;
using UnityEngine;

namespace ZombieApocalypse
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.ZombieApocalypse";
		public const string PluginName = "Zombie Apocalypse";
		public const string PluginVersion = "1.0.0";

		private const string RisingSection = "Rising";
		private const string ZombiesSection = "Zombies";

		/// <summary>
		/// Undead creatures outside the Undead faction, and creatures that were never alive
		/// </summary>
		private const string DefaultExcludedCreatures = "Charred_*, Skeleton_DeepNorth, BonemawSerpent, FallenValkyrie, ShadowPerson, StoneGolem, Mistile";

		public static ConfigEntry<int> RiseChance;
		public static ConfigEntry<float> RiseDelay;
		public static ConfigEntry<float> RiseDuration;
		public static ConfigEntry<bool> WakeUpAnimation;
		public static ConfigEntry<bool> TamedCreaturesRise;
		public static ConfigEntry<bool> AnimalsRise;
		public static ConfigEntry<float> AnimalDamage;
		public static ConfigEntry<string> ExcludedCreatures;
		public static ConfigEntry<int> ZombieHealth;
		public static ConfigEntry<bool> UndeadWeaknesses;
		public static ConfigEntry<string> ZombieName;
		public static ConfigEntry<Color> ZombieColor;
		public static ConfigEntry<float> ZombieSaturation;
		public static ConfigEntry<float> ZombieMottling;
		public static ConfigEntry<float> ZombieMottleSize;
		public static ConfigEntry<bool> ZombieEyes;
		public static ConfigEntry<Color> ZombieEyeColor;
		public static ConfigEntry<bool> ZombieFlies;
		public static ConfigEntry<float> ZombieBuzzVolume;

		/// <summary>
		/// The plugin's log
		/// </summary>
		internal static BepInEx.Logging.ManualLogSource Log;

		private Harmony harmony;
		private ConfigWatcher watcher;

		/// <summary>
		/// Binds the settings, watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			RiseChance = Config.Bind(RisingSection, "RiseChance", 100, new ConfigDescription("Percent chance that a slain creature rises as a zombie", new AcceptableValueRange<int>(0, 100)));
			RiseDelay = Config.Bind(RisingSection, "RiseDelay", 5f, new ConfigDescription("Seconds a corpse lies on the ground before it starts to get up", new AcceptableValueRange<float>(0f, 120f)));
			RiseDuration = Config.Bind(RisingSection, "RiseDuration", 2.5f, new ConfigDescription("Seconds a corpse takes to get up", new AcceptableValueRange<float>(0.5f, 10f)));
			WakeUpAnimation = Config.Bind(RisingSection, "WakeUpAnimation", true, "Creatures that can sleep (e.g., trolls and bears) get up with their own wake-up animation");
			TamedCreaturesRise = Config.Bind(RisingSection, "TamedCreaturesRise", true, "Tamed creatures rise too, as wild zombies that attack their old owners - false leaves them dead");
			ExcludedCreatures = Config.Bind(RisingSection, "ExcludedCreatures", DefaultExcludedCreatures, "Creatures that never rise, as a comma-separated list of prefab names - a name ending in * matches every prefab starting with it. Creatures in the Undead faction, bosses and summons never rise either way");
			AnimalsRise = Config.Bind(RisingSection, "AnimalsRise", true, "Animals that only flee (e.g., deer and hares) rise too, as zombies that run at their enemies and ram them");
			AnimalDamage = Config.Bind(ZombiesSection, "AnimalDamage", 10f, new ConfigDescription("Blunt damage a zombie animal's ram does, plus half as much again for each star", new AcceptableValueRange<float>(0f, 500f)));
			ZombieHealth = Config.Bind(ZombiesSection, "ZombieHealth", 100, new ConfigDescription("A zombie's health as a percent of the creature's", new AcceptableValueRange<int>(10, 1000)));
			UndeadWeaknesses = Config.Bind(ZombiesSection, "UndeadWeaknesses", true, "Zombies take damage like the undead - immune to poison, weak to spirit (silver) and fire, and tougher against arrows and other pierce damage. Takes effect for zombies that rise or load after it's changed");
			ZombieName = Config.Bind(ZombiesSection, "ZombieName", "Zombie", "Word put in front of a zombie's name, or empty to leave its name as it is");
			ZombieColor = Config.Bind(ZombiesSection, "ZombieColor", (Color)new Color32(178, 217, 153, 255), "Color a zombie's skin is multiplied by, as RRGGBBAA hex - white leaves the color as it is");
			ZombieSaturation = Config.Bind(ZombiesSection, "ZombieSaturation", -1f, new ConfigDescription("How much a zombie's colors are washed out - 0 leaves them as they are and -1 makes them gray", new AcceptableValueRange<float>(-1f, 1f)));
			ZombieMottling = Config.Bind(ZombiesSection, "ZombieMottling", 0.25f, new ConfigDescription("How dark the rotten patches on a zombie's skin are - 0 leaves none and 1 makes them black", new AcceptableValueRange<float>(0f, 1f)));
			ZombieMottleSize = Config.Bind(ZombiesSection, "ZombieMottleSize", 6f, new ConfigDescription("How many rotten patches fit across a creature's texture - higher makes smaller patches", new AcceptableValueRange<float>(1f, 32f)));
			ZombieEyes = Config.Bind(ZombiesSection, "ZombieEyes", true, "Zombies' eyes glow ZombieEyeColor - creatures with glowing eyes (e.g., greydwarfs, dvergr and necks) have them recolored, and ones with eyes painted on (e.g., boars, wolves and trolls) get glowing eyeballs");
			ZombieEyeColor = Config.Bind(ZombiesSection, "ZombieEyeColor", (Color)new Color32(255, 26, 13, 255), "Color zombies' eyes glow, as RRGGBBAA hex");
			ZombieFlies = Config.Bind(ZombiesSection, "ZombieFlies", true, "Zombies carry the draugr's swarm of flies and faint green haze");
			ZombieBuzzVolume = Config.Bind(ZombiesSection, "ZombieBuzzVolume", 0.4f, new ConfigDescription("How loud zombies' flies buzz, from 0 for silent to 1 - a beehive is 0.8. Takes effect for zombies that rise or load after it's changed", new AcceptableValueRange<float>(0f, 1f)));			watcher = new ConfigWatcher(Config, Logger, PluginName);
			Log = Logger;
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Reads the config file again once it has been saved
		/// </summary>
		private void Update()
		{
			watcher.Update();
		}

		/// <summary>
		/// Stops watching the config file and removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			watcher?.Dispose();
			harmony?.UnpatchSelf();
		}
	}
}
