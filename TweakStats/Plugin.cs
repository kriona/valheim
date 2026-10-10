using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Kriona.Shared;

namespace TweakStats
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.TweakStats";
		public const string PluginName = "Tweak Stats";
		public const string PluginVersion = "1.0.0";

		public static ManualLogSource Log;

		/// <summary>
		/// The tweaks config file, which shares its name with the file BepInEx would create for the plugin so it sits
		/// with the other kriona mods - the plugin never binds a BepInEx setting, so BepInEx never writes to it
		/// </summary>
		public static string ConfigPath;

		private Harmony harmony;
		private ConfigWatcher watcher;

		/// <summary>
		/// Writes the default config file when there isn't one, reads it, watches it for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			Log = Logger;
			ConfigPath = Path.Combine(Paths.ConfigPath, PluginGuid + ".cfg");
			if (!File.Exists(ConfigPath))
			{
				WriteDefaultConfig();
			}
			Tweaks.Load();
			watcher = new ConfigWatcher(ConfigPath, Tweaks.Reload, Logger, null);
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Shows any message waiting for the player and reads the config file again once it has been saved
		/// </summary>
		private void Update()
		{
			Tweaks.ShowPendingMessage();
			watcher.Update();
		}

		/// <summary>
		/// Copies the default config file, with its explanation and examples, out of the plugin
		/// </summary>
		private void WriteDefaultConfig()
		{
			try
			{
				using (Stream stream = typeof(Plugin).Assembly.GetManifestResourceStream("DefaultConfig.cfg"))
				using (StreamReader reader = new StreamReader(stream))
				{
					File.WriteAllText(ConfigPath, reader.ReadToEnd());
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Couldn't write " + ConfigPath + ": " + e.Message);
			}
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
