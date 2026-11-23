using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Kriona.Shared;
using Kriona.Vikings;

namespace VikingVillages
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.VikingVillages";
		public const string PluginName = "Viking Villages";
		public const string PluginVersion = "1.0.0";

		private const string VikingGearSection = "Viking Gear";

		/// <summary>
		/// The plugin's log, with the time in front of each line while a test run is going (see TestLog)
		/// </summary>
		internal static StampedLog Log;

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
			VikingSettings.HelmetChance = Config.Bind(VikingGearSection, "HelmetChance", 50, new ConfigDescription("Percent chance that a living viking also wears its armor set's helmet - from the Mountains on, every one wears one", new AcceptableValueRange<int>(0, 100)));
			VikingSettings.CapeChance = Config.Bind(VikingGearSection, "CapeChance", 50, new ConfigDescription("Percent chance that a living viking also wears its armor set's cape - from the Mountains on, every one wears one", new AcceptableValueRange<int>(0, 100)));
			VikingSettings.ShieldChance = Config.Bind(VikingGearSection, "ShieldChance", 40, new ConfigDescription("Percent chance that a living viking carries a shield, unless its weapon takes both hands", new AcceptableValueRange<int>(0, 100)));
			watcher = new ConfigWatcher(Config, Logger, PluginName);
			ConfigSync.Init(Config);
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Reads the config file again once it has been saved, shares or puts back the server's settings after a
		/// change, lays the paths of villages as they load, keeps track of the villages this client looks after -
		/// their night watch, their records and the vikings they're missing - tames the farms' pen animals that came
		/// out wild, shows the villages' areas the player turns on, and runs the test log's checks (see TestLog)
		/// </summary>
		private void Update()
		{
			watcher.Update();
			ConfigSync.Update();
			VillagePaths.Update();
			VillageWatch.Update();
			VillageRespawn.Update();
			VillageLivestock.Update();
			VillageLife.UpdateNaming();
			VikingInventory.Update();
			VikingConcernSpeech.Update();
			VillageArea.Update();
			VillageMapPins.Update();
			VillageNameList.Update();
			VillagePlannedNames.Update();
			VillageBases.Update();
			SleepSkip.Update();
			VillageFireSound.Update();
			VillageGroundPaths.Update();
			VillageStock.Update();
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
