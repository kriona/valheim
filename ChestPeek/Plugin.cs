using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Kriona.Shared;

namespace ChestPeek
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.ChestPeek";
		public const string PluginName = "Chest Peek";
		public const string PluginVersion = "1.1.0";

		public static ConfigEntry<string> EmptyColor;
		public static ConfigEntry<string> FullColor;

		internal static ManualLogSource Log;

		private Harmony harmony;
		private ConfigWatcher watcher;

		/// <summary>
		/// Binds the settings, watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			Log = Logger;
			EmptyColor = Config.Bind("Chest Colors", "EmptyColor", "","Color of a chest's metal fittings, or the wood chest's rope rings and front circle, when it holds nothing - a hex color like #00FF00 or a name like green, or blank to leave them unchanged");
			FullColor = Config.Bind("Chest Colors", "FullColor", "#FFFF00", "Color of a chest's metal fittings, or the wood chest's rope rings and front circle, when every slot holds a full stack - a hex color like #FFFF00 or a name like yellow, or blank to leave them unchanged");
			ChestColors.Refresh();
			Config.SettingChanged += OnSettingChanged;
			watcher = new ConfigWatcher(Config, Logger, PluginName);
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Reads the config file again once it has been saved, and recolors the chests whose contents changed
		/// </summary>
		private void Update()
		{
			watcher.Update();
			ChestColors.Update();
		}

		/// <summary>
		/// Recolors the chests when a color setting changes, from Configuration Manager or the config file
		/// </summary>
		/// <param name="sender">The config file</param>
		/// <param name="args">The setting that changed</param>
		private void OnSettingChanged(object sender, SettingChangedEventArgs args)
		{
			ChestColors.Refresh();
		}

		/// <summary>
		/// Stops watching the config file and removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			Config.SettingChanged -= OnSettingChanged;
			watcher?.Dispose();
			harmony?.UnpatchSelf();
		}
	}
}
