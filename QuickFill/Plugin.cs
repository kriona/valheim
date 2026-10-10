using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace QuickFill
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.QuickFill";
		public const string PluginName = "Quick Fill";
		public const string PluginVersion = "1.2.0";

		/// <summary>
		/// A KeyboardShortcut rather than a KeyCode so BepInEx doesn't write every KeyCode name into the config file
		/// as the acceptable values - only its main key is used
		/// </summary>
		public static ConfigEntry<KeyboardShortcut> ModifierKey;

		private Harmony harmony;

		/// <summary>
		/// The loaded plugin, for reloading its config file from a patch
		/// </summary>
		private static Plugin instance;

		/// <summary>
		/// Binds the settings and applies the patches
		/// </summary>
		private void Awake()
		{
			instance = this;
			ModifierKey = Config.Bind("General", "ModifierKey", new KeyboardShortcut(KeyCode.LeftShift),"Key to hold while pressing the use key to fill a station - a Unity KeyCode name, e.g. LeftShift, LeftControl or LeftAlt. Either side of Ctrl, Shift, Alt and Command counts, and None turns the fill off");
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Reads the config file again, so changes made to it while the game is running take effect
		/// </summary>
		public static void ReloadConfig()
		{
			instance?.Config.Reload();
		}

		/// <summary>
		/// Removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			harmony?.UnpatchSelf();
		}
	}
}
