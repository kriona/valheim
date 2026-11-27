using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Kriona.Shared;
using Kriona.Vikings;

namespace VikingVillages
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	[BepInDependency(VillageNameHud.ClockGuid, BepInDependency.DependencyFlags.SoftDependency)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.VikingVillages";
		public const string PluginName = "Viking Villages";
		public const string PluginVersion = "1.0.0";

		private const string VikingGearSection = "Viking Gear";
		private const string DvergrVikingsSection = "Dvergr Vikings";

		/// <summary>
		/// The plugin's log, with the time in front of each line while a test run is going (see TestLog)
		/// </summary>
		internal static StampedLog Log;

		/// <summary>
		/// The percent chance a dvergr viking drops each piece of its gear when it dies, and the most pieces it drops
		/// </summary>
		public static ConfigEntry<float> DvergrGearDropChance;
		public static ConfigEntry<int> DvergrGearDropMax;

		private Harmony harmony;
		private ConfigWatcher watcher;

		/// <summary>
		/// Binds the settings, watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			Log = new StampedLog(Logger);
			TestLog.Start(Logger);
			VikingMod.Init(PluginGuid, PluginName, PluginVersion, Logger);
			VikingSettings.HelmetChance = Config.Bind(VikingGearSection, "HelmetChance", 50, new ConfigDescription("Percent chance that a PC viking also wears its armor set's helmet - from the Mountains on, every one wears one", new AcceptableValueRange<int>(0, 100)));
			VikingSettings.CapeChance = Config.Bind(VikingGearSection, "CapeChance", 50, new ConfigDescription("Percent chance that a PC viking also wears its armor set's cape - from the Mountains on, every one wears one", new AcceptableValueRange<int>(0, 100)));
			VikingSettings.ShieldChance = Config.Bind(VikingGearSection, "ShieldChance", 40, new ConfigDescription("Percent chance that a PC viking carries a shield, unless its weapon takes both hands", new AcceptableValueRange<int>(0, 100)));
			DvergrGearDropChance = Config.Bind(DvergrVikingsSection, "GearDropChance", 5f, new ConfigDescription("Percent chance that a dvergr viking drops each piece of its gear when it dies", new AcceptableValueRange<float>(0f, 100f)));
			DvergrGearDropMax = Config.Bind(DvergrVikingsSection, "GearDropMax", 2, new ConfigDescription("Most pieces of gear a dvergr viking drops", new AcceptableValueRange<int>(0, 10)));
			watcher = new ConfigWatcher(Config, Logger, PluginName);
			ConfigSync.Init(Config);
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Reads the config file again once it has been saved, shares or puts back the server's settings after a
		/// change, lays the paths of villages as they load, keeps track of the villages this client looks after -
		/// their night watch, their records, the vikings they're missing, the gear they're working toward (see
		/// VillageLife.UpdateGoals), their quests (see VillageQuests) and their carts (see VillageCarts) - moves a player who spawned a village out of
		/// anything built on them as it goes up (see VillageSpawnCommand), starts a quest from the console once the
		/// server has planned its road (see QuestCommand), tames the farms' pen animals that came
		/// out wild, shows the villages' areas the player turns on, notes the villages' reach for running all of a village
		/// the player is in (see VillageSimulation), moves a dedicated server's own area to the village it runs (see
		/// ServerVillageRun), keeps the news the vikings tell and watches for more (see VikingNewsStore and
		/// VikingNewsWatch) and the news the villages hear of each other (see NeighborNews), has the dvergr's outposts hire their vikings (see DvergrVikingSpawns), shows the
		/// name of the village the player is in at the top of the screen (see VillageNameHud) and its debug summary
		/// in the sidebar when it's turned on (see VillageDebugPanel), and runs the test log's checks (see TestLog)
		/// </summary>
		private void Update()
		{
			watcher.Update();
			ConfigSync.Update();
			VillagePaths.Update();
			VillagePortals.Update();
			VillageWatch.Update();
			VillageRespawn.Update();
			VillageSpawnCommand.Update();
			QuestCommand.Update();
			VillageTravel.Update();
			TravelMapPins.Update();
			VillageDisband.Update();
			VillageLivestock.Update();
			VillageLife.UpdateNaming();
			VillageLife.UpdateGoals();
			VillageQuests.Update();
			VikingInventory.Update();
			VikingConcernSpeech.Update();
			VillageArea.Update();
			VillageChestUse.Update();
			VillageMapPins.Update();
			VillageNameList.Update();
			VillagePlannedNames.Update();
			VillageBases.Update();
			SleepSkip.Update();
			VillageFireSound.Update();
			VillageGroundPaths.Update();
			VillageStock.Update();
			VillageCarts.Update();
			VillageSimulation.Update();
			ServerVillageRun.Update();
			VikingNewsStore.Update();
			VikingNewsWatch.Update();
			NeighborNews.Update();
			DvergrVikingSpawns.Update();
			VillageNameHud.Update();
			VillageDebugPanel.Update();
			TestLog.Update();
		}

		/// <summary>
		/// Stops watching the config file, ends a test run and stops watching Unity's exceptions, and removes the patches
		/// when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			watcher?.Dispose();
			TestLog.Stop();
			harmony?.UnpatchSelf();
		}
	}
}
