using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Kriona.Shared;

namespace SafeSneak
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.SafeSneak";
		public const string PluginName = "Safe Sneak";
		public const string PluginVersion = "1.0.0";

		/// <summary>
		/// How far the ground can drop away in front of the player, in meters, before sneaking stops them at the edge
		/// </summary>
		public static ConfigEntry<float> MaxDrop;

		public static ManualLogSource Log;

		private Harmony harmony;
		private ConfigWatcher watcher;

		/// <summary>
		/// Binds the settings, watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			Log = Logger;
			MaxDrop = Config.Bind("General", "MaxDrop", 1f, new ConfigDescription("How far the ground can drop away in front of you, in meters, before sneaking stops you at the edge - lower stops you at smaller steps, higher lets you sneak down them", new AcceptableValueRange<float>(0.1f, 20f)));
			watcher = new ConfigWatcher(Config, Logger, PluginName);
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
