using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using Kriona.Shared;
using UnityEngine;

namespace CraftMore
{
	/// <summary>
	/// What holding a key while pressing Craft crafts
	/// </summary>
	internal enum CraftMode
	{
		Count,
		Stack,
		All,
	}

	/// <summary>
	/// A key and what holding it while pressing Craft crafts
	/// </summary>
	internal class CraftKey
	{
		public readonly KeyCode Key;
		public readonly CraftMode Mode;

		/// <summary>
		/// How many to craft when the mode is Count
		/// </summary>
		public readonly int Count;

		/// <summary>
		/// Creates a key binding
		/// </summary>
		/// <param name="key">The key to hold</param>
		/// <param name="mode">What holding it crafts</param>
		/// <param name="count">How many to craft when the mode is Count</param>
		public CraftKey(KeyCode key, CraftMode mode, int count)
		{
			Key = key;
			Mode = mode;
			Count = count;
		}
	}

	/// <summary>
	/// The mod's config file - one "Key = Stack/All/number" line per key that crafts more than one at a time
	/// </summary>
	/// <remarks>
	/// BepInEx's config format can't hold a list of keys, so the mod keeps its own file. It is written with the
	/// default keys when it doesn't exist, and otherwise only read, so comments and the order of the lines are kept
	///
	/// The settings and key lines are also mirrored as entries in the plugin's BepInEx config so Configuration Manager
	/// can list and change them - a change made there rewrites only the settings' and keys' lines. Each key line gets a
	/// numbered slot, in the file's order, holding its key and what it crafts. Keys are added or removed in the file,
	/// since Configuration Manager can't add or remove entries
	/// </remarks>
	internal static class CraftConfig
	{
		private const string FileName = Plugin.PluginGuid + ".cfg";
		private const string SettingsSection = "1 - Crafting";
		private const string KeysSection = "2 - Keys";
		private const string Comment = "##";
		private const string StackValue = "Stack";
		private const string AllValue = "All";
		private const string ShowHintsKey = "Show Hints";
		private const string FullExperienceKey = "Full Experience";
		private const string OneAtATimeKey = "One At A Time";

		/// <summary>
		/// Most a single craft can make, so a large number or Craft All with a huge pile of materials stays reasonable
		/// </summary>
		public const int MaxCount = 10000;

		private static readonly string[] defaultContents =
		{
			"## " + Plugin.PluginName,
			"## Changes take effect as soon as this file is saved; you don't need to restart Valheim or rejoin the world",
			"",
			ShowHintsKey + " = true ## Lists the keys and how many each crafts under the Craft button, true or false",
			"",
			"## The game raises a crafting skill by at most one level per batch, however many crafts are in it, and throws",
			"## away the rest of the experience - with Full Experience on, a batch gives the experience of crafting each one",
			"## separately, so a large batch can raise the skill several levels",
			"## It isn't needed when One At A Time is true, since each craft then gives its own experience",
			FullExperienceKey + " = true ## true or false",
			"",
			"## With One At A Time on, a batch crafts its items one after another, each going into your inventory as it's done,",
			"## instead of all of them at the end - the batch takes as long either way, drops what you can't carry on the",
			"## ground, and stops early when it's canceled or you run out of materials",
			OneAtATimeKey + " = true ## true or false",
			"",
			"## Hold a key while pressing Craft to craft more than one at a time. Each line is: Key = Stack/All/number",
			"## Key is a Unity KeyCode name, e.g. LeftControl, LeftAlt or LeftShift - either side of Ctrl, Shift, Alt and Command counts",
			"## Stack crafts as many as fit in one inventory slot",
			"## All crafts as many as you have the materials for, dropping what you can't carry on the ground",
			"## A number crafts that many, up to " + MaxCount,
			"## When more than one key is held, the first line wins",
			"",
			"LeftControl = " + StackValue,
			"LeftAlt = " + AllValue,
		};

		/// <summary>
		/// The keys read from the file, in the file's order
		/// </summary>
		public static readonly List<CraftKey> Keys = new List<CraftKey>();

		/// <summary>
		/// Whether the keys are listed under the Craft button
		/// </summary>
		public static bool ShowHints { get; private set; } = true;

		/// <summary>
		/// Whether a batch raises the crafting skill one craft at a time, so it can give more than one level
		/// </summary>
		public static bool FullExperience { get; private set; } = true;

		/// <summary>
		/// Whether a batch crafts its items one after another instead of all at the end
		/// </summary>
		public static bool OneAtATime { get; private set; } = true;

		/// <summary>
		/// The config file's full path
		/// </summary>
		public static string FilePath => Path.Combine(Paths.ConfigPath, FileName);

		/// <summary>
		/// Mirrors the settings as entries for Configuration Manager
		/// </summary>
		private static ConfigMirror mirror;

		private static ConfigEntry<bool> showHintsEntry;
		private static ConfigEntry<bool> fullExperienceEntry;
		private static ConfigEntry<bool> oneAtATimeEntry;

		/// <summary>
		/// The key and value text of the default key lines, the defaults of the first key slots
		/// </summary>
		private static readonly KeyValuePair<KeyCode, string>[] defaultKeys =
		{
			new KeyValuePair<KeyCode, string>(KeyCode.LeftControl, StackValue),
			new KeyValuePair<KeyCode, string>(KeyCode.LeftAlt, AllValue),
		};

		/// <summary>
		/// The key and value text of every line with a valid key, in the file's order, including ones whose value can't
		/// be read
		/// </summary>
		private static readonly List<KeyValuePair<KeyCode, string>> keyLines = new List<KeyValuePair<KeyCode, string>>();

		/// <summary>
		/// The key entry of each key slot
		/// </summary>
		private static readonly List<ConfigEntry<KeyCode>> keyEntries = new List<ConfigEntry<KeyCode>>();

		/// <summary>
		/// The value entry of each key slot
		/// </summary>
		private static readonly List<ConfigEntry<string>> craftsEntries = new List<ConfigEntry<string>>();

		/// <summary>
		/// Binds the settings as entries in the plugin's BepInEx config for Configuration Manager, in the order the
		/// file lists them
		/// </summary>
		/// <param name="file">The plugin's BepInEx config</param>
		public static void Init(ConfigFile file)
		{
			mirror = new ConfigMirror(file, OnEntryChanged);
			showHintsEntry = mirror.Bind(SettingsSection, ShowHintsKey, true, "List the keys and how many each crafts under the Craft button", null, 3, null);
			fullExperienceEntry = mirror.Bind(SettingsSection, FullExperienceKey, true, "A batch gives the experience of crafting each one separately, so it can raise the skill several levels", null, 2, null);
			oneAtATimeEntry = mirror.Bind(SettingsSection, OneAtATimeKey, true, "A batch crafts its items one after another, each going into your inventory as it's done, instead of all of them at the end", null, 1, null);
		}

		/// <summary>
		/// Gives the entries the values just read, binding slots for new key lines and removing those of lines that are
		/// gone, without writing the file
		/// </summary>
		private static void SyncEntries()
		{
			if (mirror == null)
			{
				return;
			}
			mirror.Sync(() =>
			{
				showHintsEntry.Value = ShowHints;
				fullExperienceEntry.Value = FullExperience;
				oneAtATimeEntry.Value = OneAtATime;
				for (int i = 0; i < keyLines.Count; i++)
				{
					if (i == keyEntries.Count)
					{
						BindKeySlot(i);
					}
					keyEntries[i].Value = keyLines[i].Key;
					craftsEntries[i].Value = keyLines[i].Value;
				}
				while (keyEntries.Count > keyLines.Count)
				{
					int last = keyEntries.Count - 1;
					mirror.Remove(keyEntries[last]);
					mirror.Remove(craftsEntries[last]);
					keyEntries.RemoveAt(last);
					craftsEntries.RemoveAt(last);
				}
			});
		}

		/// <summary>
		/// Binds the key and value entries of a key slot
		/// </summary>
		/// <remarks>
		/// The first slots default to the default key lines, and the rest to the line they were bound for
		/// </remarks>
		/// <param name="slot">The slot's index, counting from 0</param>
		private static void BindKeySlot(int slot)
		{
			KeyValuePair<KeyCode, string> defaults = (slot < defaultKeys.Length ? defaultKeys[slot] : keyLines[slot]);
			string name = "Key " + (slot + 1);
			int order = 1000 - (slot * 2);
			keyEntries.Add(mirror.Bind(KeysSection, name, defaults.Key, "The key to hold while pressing Craft - either side of Ctrl, Shift, Alt and Command counts. When more than one key is held, the first slot wins", null, order, null));
			craftsEntries.Add(mirror.Bind(KeysSection, name + " Crafts", defaults.Value, "Stack crafts as many as fit in one inventory slot, All crafts as many as you have the materials for, and a number crafts that many, up to " + MaxCount, null, order - 1, null));
		}

		/// <summary>
		/// Reads the config file, writing one with the default keys first when it doesn't exist - the default keys are
		/// used when the file can't be read
		/// </summary>
		public static void Load()
		{
			string path = FilePath;
			string[] lines = defaultContents;
			try
			{
				if (File.Exists(path))
				{
					lines = File.ReadAllLines(path);
				}
				else
				{
					File.WriteAllText(path, string.Join("\r\n", defaultContents) + "\r\n");
				}
			}
			catch (IOException e)
			{
				Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't read " + path + ", using the default keys: " + e.Message);
			}
			Parse(lines);
			SyncEntries();
		}

		/// <summary>
		/// Takes the entries' values after one is changed in Configuration Manager, writes them to the settings' and
		/// keys' lines in the file, leaving the rest of the file as it is, and reads the lines again
		/// </summary>
		private static void OnEntryChanged()
		{
			string path = FilePath;
			string[] lines = defaultContents;
			try
			{
				if (File.Exists(path))
				{
					lines = File.ReadAllLines(path);
				}
				List<string> newLines = new List<string>(lines);
				SetLine(newLines, ShowHintsKey, (showHintsEntry.Value ? "true" : "false"));
				SetLine(newLines, FullExperienceKey, (fullExperienceEntry.Value ? "true" : "false"));
				SetLine(newLines, OneAtATimeKey, (oneAtATimeEntry.Value ? "true" : "false"));
				SetKeyLines(newLines);
				lines = newLines.ToArray();
				File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n");
			}
			catch (IOException e)
			{
				Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't write " + path + ": " + e.Message);
			}
			Parse(lines);
			SyncEntries();
			Plugin.Watcher?.Remember();
		}

		/// <summary>
		/// Sets the value on each of a setting's lines, keeping the comment after it, or adds a line for it when there
		/// is none
		/// </summary>
		/// <param name="lines">The file's lines</param>
		/// <param name="key">The setting's key</param>
		/// <param name="valueText">The setting's value text</param>
		private static void SetLine(List<string> lines, string key, string valueText)
		{
			bool found = false;
			for (int i = 0; i < lines.Count; i++)
			{
				if (!SplitLine(lines[i], out string keyText, out _, out string comment) || !string.Equals(keyText, key, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				lines[i] = JoinLine(keyText, valueText, comment);
				found = true;
			}
			if (!found)
			{
				lines.Add(key + " = " + valueText);
			}
		}

		/// <summary>
		/// Replaces each line with a valid key with the key and value of its slot, keeping the comment after it, and
		/// keeping the line's own key when the slot's is None
		/// </summary>
		/// <remarks>
		/// A None line would be skipped when the file is read, taking its slot away
		/// </remarks>
		/// <param name="lines">The file's lines</param>
		private static void SetKeyLines(List<string> lines)
		{
			int slot = 0;
			for (int i = 0; i < lines.Count && slot < keyEntries.Count; i++)
			{
				if (!SplitLine(lines[i], out string keyText, out _, out string comment) || !TryParseKey(keyText, out _))
				{
					continue;
				}
				KeyCode key = keyEntries[slot].Value;
				lines[i] = JoinLine((key == KeyCode.None ? keyText : key.ToString()), craftsEntries[slot].Value.Trim(), comment);
				slot++;
			}
		}

		/// <summary>
		/// Splits a "Key = value ## comment" line
		/// </summary>
		/// <param name="line">The line from the file</param>
		/// <param name="keyText">Set to the key, trimmed</param>
		/// <param name="valueText">Set to the value, trimmed</param>
		/// <param name="comment">Set to the comment from ## on, or blank when there is none</param>
		/// <returns>Whether the line has a key and an =</returns>
		private static bool SplitLine(string line, out string keyText, out string valueText, out string comment)
		{
			string text = line;
			comment = "";
			int index = text.IndexOf(Comment, StringComparison.Ordinal);
			if (index >= 0)
			{
				comment = text.Substring(index);
				text = text.Substring(0, index);
			}
			int equals = text.IndexOf('=');
			keyText = (equals > 0 ? text.Substring(0, equals).Trim() : "");
			valueText = (equals > 0 ? text.Substring(equals + 1).Trim() : "");
			return(keyText.Length > 0);
		}

		/// <summary>
		/// Builds a "Key = value ## comment" line, leaving off the comment when it is blank
		/// </summary>
		/// <param name="keyText">The key</param>
		/// <param name="valueText">The value text</param>
		/// <param name="comment">The comment from ## on, or blank</param>
		/// <returns>The line</returns>
		private static string JoinLine(string keyText, string valueText, string comment)
		{
			return(keyText + " = " + valueText + (comment.Length > 0 ? " " + comment : ""));
		}

		/// <summary>
		/// Parses a key line's key
		/// </summary>
		/// <param name="keyText">The key text</param>
		/// <param name="key">Set to the key</param>
		/// <returns>Whether the text names a key other than None</returns>
		private static bool TryParseKey(string keyText, out KeyCode key)
		{
			// Enum.TryParse also takes a number, which would turn "10 = Stack" into a KeyCode
			key = KeyCode.None;
			return(keyText.Length > 0 && char.IsLetter(keyText[0]) && Enum.TryParse(keyText, true, out key) && key != KeyCode.None);
		}

		/// <summary>
		/// Fills Keys and the key lines from every "Key = value" line, ignoring blank lines and anything after ## and
		/// logging lines that can't be read
		/// </summary>
		/// <param name="lines">The file's lines</param>
		private static void Parse(string[] lines)
		{
			Keys.Clear();
			keyLines.Clear();
			ShowHints = true;
			FullExperience = true;
			OneAtATime = true;
			foreach (string line in lines)
			{
				string text = line;
				int comment = text.IndexOf(Comment, StringComparison.Ordinal);
				if (comment >= 0)
				{
					text = text.Substring(0, comment);
				}
				if (text.Trim().Length == 0)
				{
					continue;
				}
				int equals = text.IndexOf('=');
				if (equals <= 0)
				{
					Warn(line);
					continue;
				}
				string keyText = text.Substring(0, equals).Trim();
				string valueText = text.Substring(equals + 1).Trim();
				if (string.Equals(keyText, ShowHintsKey, StringComparison.OrdinalIgnoreCase))
				{
					ShowHints = ParseBool(line, valueText, ShowHints);
					continue;
				}
				if (string.Equals(keyText, FullExperienceKey, StringComparison.OrdinalIgnoreCase))
				{
					FullExperience = ParseBool(line, valueText, FullExperience);
					continue;
				}
				if (string.Equals(keyText, OneAtATimeKey, StringComparison.OrdinalIgnoreCase))
				{
					OneAtATime = ParseBool(line, valueText, OneAtATime);
					continue;
				}
				if (!TryParseKey(keyText, out KeyCode key))
				{
					Warn(line);
					continue;
				}
				keyLines.Add(new KeyValuePair<KeyCode, string>(key, valueText));
				if (string.Equals(valueText, StackValue, StringComparison.OrdinalIgnoreCase))
				{
					Keys.Add(new CraftKey(key, CraftMode.Stack, 0));
				}
				else if (string.Equals(valueText, AllValue, StringComparison.OrdinalIgnoreCase))
				{
					Keys.Add(new CraftKey(key, CraftMode.All, 0));
				}
				else if (int.TryParse(valueText, out int count) && count >= 1)
				{
					Keys.Add(new CraftKey(key, CraftMode.Count, Mathf.Min(count, MaxCount)));
				}
				else
				{
					Warn(line);
				}
			}
		}

		/// <summary>
		/// Parses a true/false setting, logging the line when it can't be read
		/// </summary>
		/// <param name="line">The line from the file</param>
		/// <param name="valueText">The setting's value text</param>
		/// <param name="fallback">The value to keep when the text can't be read</param>
		/// <returns>The parsed value, or the fallback</returns>
		private static bool ParseBool(string line, string valueText, bool fallback)
		{
			if (bool.TryParse(valueText, out bool value))
			{
				return(value);
			}
			Warn(line);
			return(fallback);
		}

		/// <summary>
		/// Logs a line that couldn't be read and is being skipped
		/// </summary>
		/// <param name="line">The line from the file</param>
		private static void Warn(string line)
		{
			Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't read \"" + line.Trim() + "\" in " + FileName + ", skipping it");
		}
	}
}
