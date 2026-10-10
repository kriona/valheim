using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Kriona.Shared;
using UnityEngine;

namespace QuickFill
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.QuickFill";
		public const string PluginName = "Quick Fill";
		public const string PluginVersion = "1.3.1";

		/// <summary>
		/// A KeyboardShortcut rather than a KeyCode so BepInEx doesn't write every KeyCode name into the config file
		/// as the acceptable values - only its main key is used
		/// </summary>
		public static ConfigEntry<KeyboardShortcut> ModifierKey;

		private Harmony harmony;
		private ConfigWatcher watcher;

		/// <summary>
		/// Binds the settings, watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			ModifierKey = Config.Bind("General", "ModifierKey", new KeyboardShortcut(KeyCode.LeftShift),"Key to hold while pressing the use key to fill a station - a Unity KeyCode name, e.g. LeftShift, LeftControl or LeftAlt. Either side of Ctrl, Shift, Alt and Command counts, and None turns the fill off");
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
