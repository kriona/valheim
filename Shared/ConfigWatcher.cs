using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace Kriona.Shared
{
	/// <summary>
	/// Watches a config file and calls back on the main thread once it has been saved, so changes take effect while the
	/// game is running
	/// </summary>
	/// <remarks>
	/// Compiled into every plugin from Directory.Build.props, so each plugin has its own copy and nothing extra has to be
	/// installed. The watcher's events arrive on another thread and only set a flag - the plugin calls Update from its
	/// own Update, which waits for the file to stop changing and then calls back. The file's contents are compared
	/// with the last ones read, so a write that leaves them the same - including the plugin's own write during the
	/// callback - doesn't call back again
	/// </remarks>
	internal class ConfigWatcher : IDisposable
	{
		/// <summary>
		/// Seconds to wait after the file last changed before calling back, since editors often write a file in
		/// several steps
		/// </summary>
		private const float ReloadDelay = 0.5f;

		private readonly string path;
		private readonly Action reload;
		private readonly ManualLogSource log;
		private readonly string pluginName;
		private readonly FileSystemWatcher watcher;

		/// <summary>
		/// The file's contents when it was last read, or null when it couldn't be read
		/// </summary>
		private string knownContents;

		/// <summary>
		/// Set from the watcher's thread when the file changes
		/// </summary>
		private volatile bool fileChanged;

		/// <summary>
		/// When to call back, as a Time.realtimeSinceStartup value, or negative when no call is waiting
		/// </summary>
		private float reloadTime = -1f;

		/// <summary>
		/// Starts watching a config file, logging a warning when it can't be watched
		/// </summary>
		/// <param name="path">The config file's full path</param>
		/// <param name="reload">Reads the file again, called on the main thread</param>
		/// <param name="log">The plugin's log</param>
		/// <param name="pluginName">Name shown in a message on screen after each reload, or null to show none</param>
		public ConfigWatcher(string path, Action reload, ManualLogSource log, string pluginName)
		{
			this.path = path;
			this.reload = reload;
			this.log = log;
			this.pluginName = pluginName;
			Remember();
			try
			{
				watcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path));
				watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
				watcher.Changed += (sender, args) => fileChanged = true;
				watcher.Created += (sender, args) => fileChanged = true;
				watcher.Renamed += (sender, args) => fileChanged = true;
				watcher.EnableRaisingEvents = true;
			}
			catch (Exception e)
			{
				watcher = null;
				log.LogWarning("Couldn't watch " + path + " for changes, so changes made to it while Valheim is running aren't read: " + e.Message);
			}
		}

		/// <summary>
		/// Starts watching a BepInEx config file, reading it again with ReloadConfig once it has been saved
		/// </summary>
		/// <param name="config">The plugin's config file</param>
		/// <param name="log">The plugin's log</param>
		/// <param name="pluginName">Name shown in a message on screen after each reload</param>
		public ConfigWatcher(ConfigFile config, ManualLogSource log, string pluginName) : this(config.ConfigFilePath, () => ReloadConfig(config), log, pluginName)
		{
		}

		/// <summary>
		/// Reads a BepInEx config file again, only writing it back when a setting's value differs from the file's -
		/// one that was out of range, couldn't be read or is missing
		/// </summary>
		/// <remarks>
		/// ConfigFile.Reload saves the whole file each time a setting changes, which makes an editor that has it open
		/// report that it was changed by another program
		/// </remarks>
		/// <param name="config">The config file to read</param>
		private static void ReloadConfig(ConfigFile config)
		{
			bool saveOnConfigSet = config.SaveOnConfigSet;
			config.SaveOnConfigSet = false;
			try
			{
				config.Reload();
			}
			finally
			{
				config.SaveOnConfigSet = saveOnConfigSet;
			}
			if (saveOnConfigSet && !MatchesFile(config))
			{
				config.Save();
			}
		}

		/// <summary>
		/// Whether every setting is in the file with the value it holds
		/// </summary>
		/// <remarks>
		/// Reads the file the same way as ConfigFile.Reload
		/// </remarks>
		/// <param name="config">The config file to check</param>
		/// <returns>True when saving would write the same values</returns>
		private static bool MatchesFile(ConfigFile config)
		{
			Dictionary<ConfigDefinition, string> values = new Dictionary<ConfigDefinition, string>();
			string section = string.Empty;
			foreach (string line in File.ReadAllLines(config.ConfigFilePath))
			{
				string text = line.Trim();
				if (text.StartsWith("#"))
				{
					continue;
				}
				if (text.StartsWith("[") && text.EndsWith("]"))
				{
					section = text.Substring(1, text.Length - 2);
					continue;
				}
				string[] parts = text.Split(new char[] { '=' }, 2);
				if (parts.Length == 2)
				{
					values[new ConfigDefinition(section, parts[0].Trim())] = parts[1].Trim();
				}
			}
			foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entry in config)
			{
				if (!values.TryGetValue(entry.Key, out string text))
				{
					return(false);
				}
				try
				{
					if (!Equals(TomlTypeConverter.ConvertToValue(text, entry.Value.SettingType), entry.Value.BoxedValue))
					{
						return(false);
					}
				}
				catch (Exception)
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Calls back once the file has stopped changing with different contents, logging the reload and showing a
		/// message on screen when the watcher has a plugin name - call it from the plugin's Update
		/// </summary>
		public void Update()
		{
			if (fileChanged)
			{
				fileChanged = false;
				reloadTime = Time.realtimeSinceStartup + ReloadDelay;
			}
			if (reloadTime < 0f || Time.realtimeSinceStartup < reloadTime)
			{
				return;
			}
			reloadTime = -1f;
			string contents;
			try
			{
				contents = File.ReadAllText(path);
			}
			catch (FileNotFoundException)
			{
				return;
			}
			catch (IOException)
			{
				// The editor may still have the file open, so try again shortly
				reloadTime = Time.realtimeSinceStartup + ReloadDelay;
				return;
			}
			if (contents == knownContents)
			{
				return;
			}
			reload();
			Remember();
			if (pluginName != null)
			{
				log.LogInfo("Reloaded " + Path.GetFileName(path));
				MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, pluginName + ": config reloaded");
			}
		}

		/// <summary>
		/// Takes the file's current contents as already read, so a write the plugin makes itself doesn't call back -
		/// call it after the plugin writes the file outside the callback
		/// </summary>
		public void Remember()
		{
			try
			{
				knownContents = File.ReadAllText(path);
			}
			catch (IOException)
			{
				knownContents = null;
			}
		}

		/// <summary>
		/// Stops watching the file
		/// </summary>
		public void Dispose()
		{
			watcher?.Dispose();
		}
	}
}
