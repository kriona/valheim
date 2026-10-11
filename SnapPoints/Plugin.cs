using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Kriona.Shared;

namespace SnapPoints
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.SnapPoints";
		public const string PluginName = "Snap Points";
		public const string PluginVersion = "1.1.0";

		/// <summary>
		/// The plugin's log, for the classes outside the plugin
		/// </summary>
		internal static ManualLogSource Log;

		/// <summary>
		/// Watches the config file, which the build piece list also writes when the scene starts
		/// </summary>
		internal static ConfigWatcher Watcher;

		private Harmony harmony;

		/// <summary>
		/// Watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			Log = Logger;
			Watcher = new ConfigWatcher(PieceConfig.FilePath, PieceConfig.Reload, Logger, PluginName);
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
