using System;
using BepInEx.Configuration;

namespace Kriona.Shared
{
	/// <summary>
	/// Mirrors the settings of a plugin's own config file as entries in its BepInEx config, so Configuration Manager can
	/// list and change them
	/// </summary>
	/// <remarks>
	/// The entries are never saved: BepInEx's file for a plugin is named after its GUID, the same as the plugin's own
	/// file, and saving would write BepInEx's format over it. The plugin gives the entries the values it reads from its
	/// file inside Sync, which doesn't call back, and is called back when an entry is changed anywhere else, such as in
	/// Configuration Manager, to write the change to its file
	/// </remarks>
	internal class ConfigMirror
	{
		private readonly ConfigFile config;
		private readonly Action changed;

		/// <summary>
		/// Set during Sync, so the entries the plugin sets don't call back
		/// </summary>
		private bool syncing;

		/// <summary>
		/// Stops the BepInEx config from saving and starts listening for changes to its entries
		/// </summary>
		/// <param name="config">The plugin's BepInEx config</param>
		/// <param name="changed">Called after an entry is changed outside Sync</param>
		public ConfigMirror(ConfigFile config, Action changed)
		{
			this.config = config;
			this.changed = changed;
			config.SaveOnConfigSet = false;
			config.SettingChanged += OnSettingChanged;
		}

		/// <summary>
		/// Binds an entry, or returns the one already bound with the same section and key
		/// </summary>
		/// <param name="section">The Configuration Manager category</param>
		/// <param name="key">The setting's key, the same as in the plugin's file</param>
		/// <param name="defaultValue">The setting's default</param>
		/// <param name="description">Shown when hovering over the setting</param>
		/// <param name="acceptableValues">The values the setting can take, or null for any</param>
		/// <param name="order">Position in its category, higher first, or null for Configuration Manager's default</param>
		/// <param name="displayName">Name shown in place of the key, or null to show the key</param>
		/// <returns>The entry</returns>
		/// <exception cref="ArgumentException">The section or key has a character BepInEx doesn't allow, like = [ ] or a quote</exception>
		public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description, AcceptableValueBase acceptableValues, int? order, string displayName)
		{
			ConfigurationManagerAttributes attributes = new ConfigurationManagerAttributes();
			attributes.Order = order;
			attributes.DispName = displayName;
			return(config.Bind(section, key, defaultValue, new ConfigDescription(description, acceptableValues, attributes)));
		}

		/// <summary>
		/// Removes an entry, so Configuration Manager no longer lists it
		/// </summary>
		/// <param name="entry">The entry</param>
		public void Remove(ConfigEntryBase entry)
		{
			config.Remove(entry.Definition);
		}

		/// <summary>
		/// Runs code that sets entries to the values read from the plugin's file, without calling back
		/// </summary>
		/// <param name="apply">Sets the entries</param>
		public void Sync(Action apply)
		{
			syncing = true;
			try
			{
				apply();
			}
			finally
			{
				syncing = false;
			}
		}

		/// <summary>
		/// Changes the name Configuration Manager shows for an entry bound by Bind
		/// </summary>
		/// <remarks>
		/// Configuration Manager reads the name each time it builds its list, so the change shows the next time it is opened
		/// </remarks>
		/// <param name="entry">The entry</param>
		/// <param name="displayName">Name shown in place of the key, or null to show the key</param>
		public static void SetDisplayName(ConfigEntryBase entry, string displayName)
		{
			foreach (object tag in entry.Description.Tags)
			{
				if (tag is ConfigurationManagerAttributes attributes)
				{
					attributes.DispName = displayName;
				}
			}
		}

		/// <summary>
		/// Calls back when an entry changes outside Sync
		/// </summary>
		/// <param name="sender">The BepInEx config</param>
		/// <param name="args">The entry that changed</param>
		private void OnSettingChanged(object sender, SettingChangedEventArgs args)
		{
			if (!syncing)
			{
				changed();
			}
		}
	}
}
