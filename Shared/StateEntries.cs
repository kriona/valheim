using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace Kriona.Shared
{
	/// <summary>
	/// Configuration Manager entries for a plugin file's "Prefab = inherit/true/false ## Name" lines, one dropdown per
	/// line, named by the name in game
	/// </summary>
	/// <remarks>
	/// The plugin calls Set for every line inside ConfigMirror.Sync each time it reads its file, then RemoveUnset, so
	/// lines that are gone from the file are gone from Configuration Manager too
	/// </remarks>
	internal class StateEntries
	{
		public const string Inherit = "inherit";

		private static readonly AcceptableValueList<string> values = new AcceptableValueList<string>(Inherit, "true", "false");

		private readonly ConfigMirror mirror;
		private readonly string help;

		/// <summary>
		/// The entry of each line, by prefab name
		/// </summary>
		private readonly Dictionary<string, ConfigEntry<string>> entries = new Dictionary<string, ConfigEntry<string>>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// The prefab names Set since the last RemoveUnset
		/// </summary>
		private readonly HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Creates an empty list of entries
		/// </summary>
		/// <param name="mirror">The mirror the entries are bound through</param>
		/// <param name="help">What the values do, shown after the prefab name when hovering over an entry</param>
		public StateEntries(ConfigMirror mirror, string help)
		{
			this.mirror = mirror;
			this.help = help;
		}

		/// <summary>
		/// Gives a line's entry its value and name, binding it the first time and binding it again when its section changes
		/// </summary>
		/// <param name="section">The Configuration Manager category</param>
		/// <param name="key">The prefab name</param>
		/// <param name="displayName">The name in game, or blank when it isn't known</param>
		/// <param name="state">The line's value, null for inherit</param>
		public void Set(string section, string key, string displayName, bool? state)
		{
			string entryName = GetEntryName(key, displayName);
			if (entries.TryGetValue(key, out ConfigEntry<string> entry) && entry.Definition.Section != section)
			{
				mirror.Remove(entry);
				entries.Remove(key);
				entry = null;
			}
			if (entry == null)
			{
				try
				{
					entry = mirror.Bind(section, key, Inherit, "Prefab " + key + " - " + help, values, null, entryName);
				}
				catch (ArgumentException)
				{
					// A modded prefab's name might hold a character BepInEx doesn't allow in a key
					return;
				}
				entries[key] = entry;
			}
			else
			{
				ConfigMirror.SetDisplayName(entry, entryName);
			}
			entry.Value = Format(state);
			set.Add(key);
		}

		/// <summary>
		/// Removes the entries of the lines not Set since the last call
		/// </summary>
		public void RemoveUnset()
		{
			List<string> unset = new List<string>();
			foreach (KeyValuePair<string, ConfigEntry<string>> entry in entries)
			{
				if (!set.Contains(entry.Key))
				{
					mirror.Remove(entry.Value);
					unset.Add(entry.Key);
				}
			}
			foreach (string key in unset)
			{
				entries.Remove(key);
			}
			set.Clear();
		}

		/// <summary>
		/// The value of a line's entry
		/// </summary>
		/// <param name="key">The prefab name</param>
		/// <param name="state">Set to the value, null for inherit</param>
		/// <returns>Whether the line has an entry</returns>
		public bool TryGetState(string key, out bool? state)
		{
			state = null;
			if (!entries.TryGetValue(key, out ConfigEntry<string> entry))
			{
				return(false);
			}
			if (bool.TryParse(entry.Value, out bool value))
			{
				state = value;
			}
			return(true);
		}

		/// <summary>
		/// The text written for a true/false/inherit value
		/// </summary>
		/// <param name="state">The value, null for inherit</param>
		/// <returns>"true", "false" or "inherit"</returns>
		public static string Format(bool? state)
		{
			if (!state.HasValue)
			{
				return(Inherit);
			}
			return((state.Value ? "true" : "false"));
		}

		/// <summary>
		/// The name an entry shows in Configuration Manager
		/// </summary>
		/// <param name="key">The prefab name</param>
		/// <param name="displayName">The name in game, or blank when it isn't known</param>
		/// <returns>The name in game followed by the prefab name when they differ, or the prefab name alone</returns>
		private static string GetEntryName(string key, string displayName)
		{
			string name = displayName.Trim();
			if (name.Length == 0 || name.Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return(key);
			}
			return(name + " (" + key + ")");
		}
	}
}
