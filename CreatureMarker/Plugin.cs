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
		public const string PluginVersion = "1.2.1";

		private readonly MarkerHud markers = new MarkerHud();
		private readonly MapMarkers mapMarkers = new MapMarkers();
		private readonly List<Character> marked = new List<Character>();
		private Harmony harmony;

		/// <summary>
		/// Watches the config file, which the creature list also writes when the scene starts
		/// </summary>
		internal static ConfigWatcher Watcher;

		/// <summary>
		/// Watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
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
		/// Runs after the camera and creatures have moved so the markers track their final positions
		/// </summary>
		private void LateUpdate()
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				return;
			}
			CreatureConfig.GetMarked(player, marked);
			markers.Update(marked, player);
			mapMarkers.Update(marked, player);
		}

		/// <summary>
		/// Removes the markers, stops watching the config file and removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			markers.Destroy();
			mapMarkers.Destroy();
			Watcher?.Dispose();
			harmony?.UnpatchSelf();
		}
	}
}
