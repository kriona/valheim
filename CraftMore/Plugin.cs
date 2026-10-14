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
		public const string PluginVersion = "1.0.0";

		private Harmony harmony;
		private ConfigWatcher watcher;

		/// <summary>
		/// Reads the config file, watches it for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			CraftConfig.Load();
			watcher = new ConfigWatcher(CraftConfig.FilePath, CraftConfig.Load, Logger, PluginName);
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
