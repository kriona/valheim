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
		public const string PluginVersion = "1.2.0";

		private ConfigEntry<int> fontSize;
		private ConfigEntry<int> margin;
		private readonly WeightLabel label = new WeightLabel();
		private Harmony harmony;
		private ConfigWatcher watcher;

		/// <summary>
		/// Binds the display settings, watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			fontSize = Config.Bind("Display", "FontSize", 18, "Size of the weight text");
			margin = Config.Bind("Display", "Margin", 6, "Gap between the minimap (or the screen corner when there is no minimap) and the text");
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
			label.Update(fontSize.Value, margin.Value);
		}

		/// <summary>
		/// Removes the label, stops watching the config file and removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			label.Destroy();
			watcher?.Dispose();
			harmony?.UnpatchSelf();
		}
	}
}
