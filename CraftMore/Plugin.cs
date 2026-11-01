using BepInEx;
using HarmonyLib;
using Kriona.Shared;

namespace CraftMore
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.CraftMore";
		public const string PluginName = "Craft More";
		public const string PluginVersion = "1.0.1";

		/// <summary>
		/// Watches the config file, which Configuration Manager changes also write
		/// </summary>
		internal static ConfigWatcher Watcher;

		private Harmony harmony;

		/// <summary>
		/// Mirrors the settings for Configuration Manager, reads the config file, watches it for changes and applies
		/// the patches
		/// </summary>
		private void Awake()
		{
			CraftConfig.Init(Config);
			CraftConfig.Load();
			Watcher = new ConfigWatcher(CraftConfig.FilePath, CraftConfig.Load, Logger, PluginName);
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Reads the config file again once it has been saved
		/// </summary>
		private void Update()
		{
			Watcher.Update();
		}

		/// <summary>
		/// Stops watching the config file and removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			Watcher?.Dispose();
			harmony?.UnpatchSelf();
		}
	}
}
