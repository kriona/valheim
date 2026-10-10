using System;
using System.IO;
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
	/// own Update, which waits for the file to stop changing and then calls back
	/// </remarks>
	internal class ConfigWatcher : IDisposable
	{
		/// <summary>
		/// Seconds to wait after the file last changed before calling back, since editors often write a file in
		/// several steps
		/// </summary>
		private const float ReloadDelay = 0.5f;

		private readonly Action reload;
		private readonly FileSystemWatcher watcher;

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
		public ConfigWatcher(string path, Action reload, ManualLogSource log)
		{
			this.reload = reload;
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
		/// Calls back once the file has stopped changing - call it from the plugin's Update
		/// </summary>
		public void Update()
		{
			if (fileChanged)
			{
				fileChanged = false;
				reloadTime = Time.realtimeSinceStartup + ReloadDelay;
			}
			if (reloadTime >= 0f && Time.realtimeSinceStartup >= reloadTime)
			{
				reloadTime = -1f;
				reload();
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
