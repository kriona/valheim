using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Kriona.Shared;

namespace WeightDisplay
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.WeightDisplay";
		public const string PluginName = "Weight Display";
		public const string PluginVersion = "1.3.0";

		private ConfigEntry<int> fontSize;
		private ConfigEntry<int> margin;
		private ConfigEntry<bool> showWeight;
		private ConfigEntry<bool> showSlots;
		private WarningSettings warnings;
		private readonly WeightLabel label = new WeightLabel();
		private Harmony harmony;
		private ConfigWatcher watcher;

		/// <summary>
		/// Binds the display and warning settings, watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			showWeight = Config.Bind("Display", "ShowWeight", true, "Show your current and maximum carry weight");
			showSlots = Config.Bind("Display", "ShowSlots", true, "Show how many inventory slots are in use");
			fontSize = Config.Bind("Display", "FontSize", 18, "Size of the weight text");
			margin = Config.Bind("Display", "Margin", 6, "Gap between the minimap (or the screen corner when there is no minimap) and the text");
			warnings = new WarningSettings(Config);
			Config.SettingChanged += OnSettingChanged;
			watcher = new ConfigWatcher(Config.ConfigFilePath, Config.Reload, Logger);
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
		/// Runs after the HUD and minimap have updated so the label follows their final positions
		/// </summary>
		private void LateUpdate()
		{
			label.Update(fontSize.Value, margin.Value, showWeight.Value, showSlots.Value, warnings);
		}

		/// <summary>
		/// Rebuilds the label's text so a changed color setting shows straight away
		/// </summary>
		/// <param name="sender">The config file</param>
		/// <param name="e">The setting that changed</param>
		private void OnSettingChanged(object sender, SettingChangedEventArgs e)
		{
			label.Refresh();
		}

		/// <summary>
		/// Removes the label, stops watching the config file and removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			Config.SettingChanged -= OnSettingChanged;
			label.Destroy();
			watcher?.Dispose();
			harmony?.UnpatchSelf();
		}
	}
}
