using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TweakStats
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.TweakStats";
		public const string PluginName = "Tweak Stats";
		public const string PluginVersion = "1.0.0";

		/// <summary>
		/// Seconds to wait after the config file last changed before reading it, since editors often write a file in
		/// several steps
		/// </summary>
		private const float ReloadDelay = 0.5f;

		public static ManualLogSource Log;

		/// <summary>
		/// The tweaks config file, which shares its name with the file BepInEx would create for the plugin so it sits
		/// with the other kriona mods - the plugin never binds a BepInEx setting, so BepInEx never writes to it
		/// </summary>
		public static string ConfigPath;

		private Harmony harmony;
		private FileSystemWatcher watcher;

		/// <summary>
		/// Set from the watcher's thread when the config file changes
		/// </summary>
		private volatile bool fileChanged;

		/// <summary>
		/// When to read the config file, as a Time.realtimeSinceStartup value, or negative when no read is waiting
		/// </summary>
		private float reloadTime = -1f;

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
			WatchConfig();
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Reads the config file again and applies it once it has stopped changing
		/// </summary>
		private void Update()
		{
			if (fileChanged)
			{
				fileChanged = false;
				reloadTime = Time.realtimeSinceStartup + ReloadDelay;
			}
			if (reloadTime >= 0f && Time.realtimeSinceStartup >= reloadTime)
			{
				reloadTime = -1f;
				Tweaks.Reload();
			}
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
		/// Flags the config file to be read again whenever it is saved, created or renamed into place
		/// </summary>
		private void WatchConfig()
		{
			try
			{
				watcher = new FileSystemWatcher(Path.GetDirectoryName(ConfigPath), Path.GetFileName(ConfigPath));
				watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
				watcher.Changed += (sender, args) => fileChanged = true;
				watcher.Created += (sender, args) => fileChanged = true;
				watcher.Renamed += (sender, args) => fileChanged = true;
				watcher.EnableRaisingEvents = true;
			}
			catch (Exception e)
			{
				Logger.LogWarning("Couldn't watch " + ConfigPath + " for changes, so it is only read when Valheim starts: " + e.Message);
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
