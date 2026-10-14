using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
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
	/// </remarks>
	internal static class CraftConfig
	{
		private const string FileName = Plugin.PluginGuid + ".cfg";
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
		}

		/// <summary>
		/// Fills Keys from every "Key = value" line, ignoring blank lines and anything after ## and logging lines that
		/// can't be read
		/// </summary>
		/// <param name="lines">The file's lines</param>
		private static void Parse(string[] lines)
		{
			Keys.Clear();
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
				// Enum.TryParse also takes a number, which would turn "10 = Stack" into a KeyCode
				if (keyText.Length == 0 || !char.IsLetter(keyText[0]) || !Enum.TryParse(keyText, true, out KeyCode key) || key == KeyCode.None)
				{
					Warn(line);
					continue;
				}
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
