using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Kriona.Shared;

namespace CreatureMarker
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.CreatureMarker";
		public const string PluginName = "Creature Marker";
		public const string PluginVersion = "1.8.2";

		private readonly MarkerHud markers = new MarkerHud();
		private readonly MapMarkers mapMarkers = new MapMarkers();
		private readonly List<Target> marked = new List<Target>();
		private Harmony harmony;

		/// <summary>
		/// The loaded plugin, whose markers the camera patch places each frame
		/// </summary>
		private static Plugin instance;

		/// <summary>
		/// Watches the config file, which the creature list also writes when the scene starts
		/// </summary>
		internal static ConfigWatcher Watcher;

		/// <summary>
		/// Binds the settings for Configuration Manager, watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			instance = this;
			CreatureConfig.Init(Config);
			Watcher = new ConfigWatcher(CreatureConfig.FilePath, CreatureConfig.Reload, Logger, PluginName);
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
		/// Places the markers on screen and on the map, called once the camera has moved for the frame so the markers
		/// track the creatures' final positions
		/// </summary>
		internal static void UpdateMarkers()
		{
			Player player = Player.m_localPlayer;
			if (instance == null || player == null)
			{
				return;
			}
			CreatureConfig.GetMarked(player, instance.marked);
			instance.markers.Update(instance.marked, player);
			instance.mapMarkers.Update(instance.marked, player);
		}

		/// <summary>
		/// Removes the markers, stops watching the config file and removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			instance = null;
			markers.Destroy();
			mapMarkers.Destroy();
			Watcher?.Dispose();
			harmony?.UnpatchSelf();
		}
	}
}
