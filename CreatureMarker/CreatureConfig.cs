using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// The mod's config file - display settings, the All switches and one "Prefab = inherit/true/false ## Name" line
	/// per creature, deciding which creatures get a marker and how it looks
	/// </summary>
	/// <remarks>
	/// BepInEx's config format spends several lines on every entry, so the mod keeps its own file. The file is read
	/// and rewritten when a world's ZNetScene starts, which adds every creature the game knows about, including ones
	/// added by other mods, while keeping the values already set. Lines for creatures that are no longer loaded are
	/// kept so their values survive a mod being removed for a while
	/// </remarks>
	internal static class CreatureConfig
	{
		private const string FileName = Plugin.PluginGuid + ".cfg";
		private const string Comment = "##";
		private const string Inherit = "inherit";

		private const string ShowOnScreenKey = "Show On Screen";
		private const string ShowOnMapKey = "Show On Map";
		private const string MaxDistanceKey = "Max Distance";
		private const string ShowNameKey = "Show Name";
		private const string ShowDistanceKey = "Show Distance";
		private const string PassiveColorKey = "Passive Color";
		private const string HostileColorKey = "Hostile Color";
		private const string BossColorKey = "Boss Color";
		private const string AllCreaturesKey = "All Creatures";
		private const string AllHostileKey = "All Hostile Creatures";

		private const float DefaultMaxDistance = 50f;
		private const float MinMaxDistance = 5f;
		private const float MaxMaxDistance = 500f;
		private const string DefaultPassiveColor = "#40D940";
		private const string DefaultHostileColor = "#FFD91A";
		private const string DefaultBossColor = "#FF3333";

		private static readonly string[] header =
		{
			"## " + Plugin.PluginName,
			"## Changes are read when a world loads",
			"## Colors are #RRGGBB or a color name like red, yellow, green, cyan or magenta",
			"",
		};

		private static readonly string[] creatureHeader =
		{
			"",
			"## All Creatures marks every creature, and All Hostile Creatures marks every creature that will attack you",
			"## Each creature line is: Prefab = inherit/true/false ## Name in game",
			"## inherit follows the All switches, while true or false always shows or hides that creature's marker",
			"",
		};

		/// <summary>
		/// Whether marked creatures get an arrow on screen
		/// </summary>
		public static bool ShowOnScreen { get; private set; } = true;

		/// <summary>
		/// Whether marked creatures get a dot on the minimap and the large map
		/// </summary>
		public static bool ShowOnMap { get; private set; } = true;

		/// <summary>
		/// Furthest a creature can be from the player, in meters, and still get a marker
		/// </summary>
		public static float MaxDistance { get; private set; } = DefaultMaxDistance;

		/// <summary>
		/// Whether the creature's name is shown with its marker
		/// </summary>
		public static bool ShowName { get; private set; }

		/// <summary>
		/// Whether the creature's distance from the player is shown with its marker
		/// </summary>
		public static bool ShowDistance { get; private set; }

		private static string passiveColorText = DefaultPassiveColor;
		private static string hostileColorText = DefaultHostileColor;
		private static string bossColorText = DefaultBossColor;
		private static Color passiveColor = ParseColor(DefaultPassiveColor, Color.green);
		private static Color hostileColor = ParseColor(DefaultHostileColor, Color.yellow);
		private static Color bossColor = ParseColor(DefaultBossColor, Color.red);
		private static bool allCreatures;
		private static bool allHostile = true;

		/// <summary>
		/// The true or false set on creature lines, by prefab hash, leaving out the ones set to inherit
		/// </summary>
		private static readonly Dictionary<int, bool> overrides = new Dictionary<int, bool>();

		/// <summary>
		/// Reads the config file, adds any prefabs with a Character component other than the player and writes it back
		/// </summary>
		/// <param name="scene">The scene holding the prefab list</param>
		public static void Load(ZNetScene scene)
		{
			string path = Path.Combine(Paths.ConfigPath, FileName);
			Dictionary<string, string> values;
			try
			{
				values = Read(path);
			}
			catch (IOException e)
			{
				Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't read " + path + ": " + e.Message);
				return;
			}
			LoadSettings(values);
			SortedDictionary<string, string> names = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (GameObject prefab in scene.m_prefabs)
			{
				if (prefab == null)
				{
					continue;
				}
				Character character = prefab.GetComponent<Character>();
				if (character == null || character is Player)
				{
					continue;
				}
				names[prefab.name] = GetDisplayName(character);
			}
			foreach (string key in values.Keys)
			{
				if (!names.ContainsKey(key))
				{
					names[key] = "";
				}
			}
			// The keys in names keep the prefabs' own casing, which the hashes need to match
			Dictionary<string, bool?> states = new Dictionary<string, bool?>();
			overrides.Clear();
			foreach (string key in names.Keys)
			{
				bool? state = null;
				if (values.TryGetValue(key, out string text))
				{
					state = ParseState(text);
				}
				states[key] = state;
				if (state.HasValue)
				{
					overrides[key.GetStableHashCode()] = state.Value;
				}
			}
			Write(path, names, states);
		}

		/// <summary>
		/// Fills the list with the living marked creatures within Max Distance of the player
		/// </summary>
		/// <param name="player">The local player</param>
		/// <param name="marked">The list to fill, cleared first</param>
		public static void GetMarked(Player player, List<Character> marked)
		{
			marked.Clear();
			Vector3 playerPosition = player.transform.position;
			float maxDistanceSquared = MaxDistance * MaxDistance;
			foreach (Character character in Character.GetAllCharacters())
			{
				if (Vector3.SqrMagnitude(character.transform.position - playerPosition) > maxDistanceSquared)
				{
					continue;
				}
				if (character.IsDead() || !IsMarked(character, player))
				{
					continue;
				}
				marked.Add(character);
			}
		}

		/// <summary>
		/// Whether the creature should get a marker, from its own line when that is true or false, or else from the All switches
		/// </summary>
		/// <param name="character">The creature to check</param>
		/// <param name="player">The local player, for deciding whether the creature is hostile</param>
		/// <returns>True when it should get a marker</returns>
		public static bool IsMarked(Character character, Player player)
		{
			if (character is Player)
			{
				return(false);
			}
			ZNetView nview = character.m_nview;
			if (nview != null && nview.IsValid() && overrides.TryGetValue(nview.GetZDO().GetPrefab(), out bool value))
			{
				return(value);
			}
			return(allCreatures || (allHostile && IsHostile(character, player)));
		}

		/// <summary>
		/// The marker color for the creature - the boss color for bosses, the hostile color for hostile creatures and
		/// the passive color for the rest
		/// </summary>
		/// <param name="character">The marked creature</param>
		/// <param name="player">The local player, for deciding whether the creature is hostile</param>
		/// <returns>The arrow color</returns>
		public static Color GetColor(Character character, Player player)
		{
			if (character.IsBoss())
			{
				return(bossColor);
			}
			if (IsHostile(character, player))
			{
				return(hostileColor);
			}
			return(passiveColor);
		}

		/// <summary>
		/// Whether the creature has monster AI and currently counts as the player's enemy
		/// </summary>
		/// <remarks>
		/// Animals that only flee, like deer, have AnimalAI and are left out. Tamed creatures and Dvergr that haven't
		/// been provoked aren't enemies of the player, so they are left out too
		/// </remarks>
		/// <param name="character">The creature to check</param>
		/// <param name="player">The local player</param>
		/// <returns>True when the creature is hostile</returns>
		private static bool IsHostile(Character character, Player player)
		{
			return(character.GetBaseAI() is MonsterAI && BaseAI.IsEnemy(character, player));
		}

		/// <summary>
		/// Takes the settings and All switches out of the file values, using the defaults for any that are missing or don't parse
		/// </summary>
		/// <param name="values">Value text by key, as read from the file, left holding only the creature lines</param>
		private static void LoadSettings(Dictionary<string, string> values)
		{
			MaxDistance = DefaultMaxDistance;
			string text = TakeValue(values, MaxDistanceKey);
			if (text != null)
			{
				if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float distance))
				{
					MaxDistance = Mathf.Clamp(distance, MinMaxDistance, MaxMaxDistance);
				}
				else
				{
					Warn(MaxDistanceKey, text);
				}
			}
			passiveColorText = TakeColor(values, PassiveColorKey, DefaultPassiveColor, out passiveColor);
			hostileColorText = TakeColor(values, HostileColorKey, DefaultHostileColor, out hostileColor);
			bossColorText = TakeColor(values, BossColorKey, DefaultBossColor, out bossColor);
			allCreatures = (ParseState(TakeValue(values, AllCreaturesKey)) ?? false);
			allHostile = (ParseState(TakeValue(values, AllHostileKey)) ?? true);
			ShowName = (ParseState(TakeValue(values, ShowNameKey)) ?? false);
			ShowDistance = (ParseState(TakeValue(values, ShowDistanceKey)) ?? false);
			ShowOnScreen = (ParseState(TakeValue(values, ShowOnScreenKey)) ?? true);
			ShowOnMap = (ParseState(TakeValue(values, ShowOnMapKey)) ?? true);
		}

		/// <summary>
		/// Takes a color setting out of the file values
		/// </summary>
		/// <param name="values">Value text by key, as read from the file</param>
		/// <param name="key">The setting's key</param>
		/// <param name="defaultText">Color text to use when the setting is missing or doesn't parse</param>
		/// <param name="color">The parsed color</param>
		/// <returns>The color text to write back to the file</returns>
		private static string TakeColor(Dictionary<string, string> values, string key, string defaultText, out Color color)
		{
			string text = TakeValue(values, key);
			if (text != null && ColorUtility.TryParseHtmlString(text, out color))
			{
				return(text);
			}
			if (text != null)
			{
				Warn(key, text);
			}
			color = ParseColor(defaultText, Color.white);
			return(defaultText);
		}

		/// <summary>
		/// Removes a setting's value from the file values
		/// </summary>
		/// <param name="values">Value text by key, as read from the file</param>
		/// <param name="key">The setting's key</param>
		/// <returns>The value text, or null when the file doesn't have the setting</returns>
		private static string TakeValue(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out string text))
			{
				return(null);
			}
			values.Remove(key);
			return(text);
		}

		/// <summary>
		/// Parses a true/false/inherit value
		/// </summary>
		/// <param name="text">The value text, or null</param>
		/// <returns>The value, or null for inherit, a missing value or one that doesn't parse</returns>
		private static bool? ParseState(string text)
		{
			if (text != null && bool.TryParse(text, out bool value))
			{
				return(value);
			}
			return(null);
		}

		/// <summary>
		/// Parses an HTML color string
		/// </summary>
		/// <param name="text">#RRGGBB or a color name</param>
		/// <param name="fallback">Color to use when the text doesn't parse</param>
		/// <returns>The color</returns>
		private static Color ParseColor(string text, Color fallback)
		{
			return((ColorUtility.TryParseHtmlString(text, out Color color) ? color : fallback));
		}

		/// <summary>
		/// Logs a setting that couldn't be parsed and is being reset to its default
		/// </summary>
		/// <param name="key">The setting's key</param>
		/// <param name="text">The value text that didn't parse</param>
		private static void Warn(string key, string text)
		{
			Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't read " + key + " = " + text + ", using the default");
		}

		/// <summary>
		/// Reads the key and value text of every "Key = value" line, ignoring blank lines and anything after ##
		/// </summary>
		/// <param name="path">Path of the config file</param>
		/// <returns>Value text by key, empty when the file doesn't exist</returns>
		private static Dictionary<string, string> Read(string path)
		{
			Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (!File.Exists(path))
			{
				return(values);
			}
			foreach (string line in File.ReadAllLines(path))
			{
				string text = line;
				int comment = text.IndexOf(Comment, StringComparison.Ordinal);
				if (comment >= 0)
				{
					text = text.Substring(0, comment);
				}
				int equals = text.IndexOf('=');
				if (equals <= 0)
				{
					continue;
				}
				string key = text.Substring(0, equals).Trim();
				if (key.Length > 0)
				{
					values[key] = text.Substring(equals + 1).Trim();
				}
			}
			return(values);
		}

		/// <summary>
		/// Writes the settings, the All switches and one line per creature, leaving the file untouched when nothing has changed
		/// </summary>
		/// <param name="path">Path of the config file</param>
		/// <param name="names">Display name by prefab name, sorted, blank for prefabs that aren't loaded</param>
		/// <param name="states">Value by prefab name, null for inherit</param>
		private static void Write(string path, SortedDictionary<string, string> names, Dictionary<string, bool?> states)
		{
			StringBuilder builder = new StringBuilder();
			AppendLines(builder, header);
			AppendLine(builder, ShowOnScreenKey, FormatState(ShowOnScreen), "Show an arrow above each marked creature, pinned to the screen edge when it is off-screen, default true");
			AppendLine(builder, ShowOnMapKey, FormatState(ShowOnMap), "Show a dot on the minimap and the large map for each marked creature, default true");
			builder.Append("\r\n");
			AppendLine(builder, MaxDistanceKey, MaxDistance.ToString(CultureInfo.InvariantCulture), "Meters, from " + MinMaxDistance + " to " + MaxMaxDistance + ", default " + DefaultMaxDistance);
			AppendLine(builder, ShowNameKey, FormatState(ShowName), "Show the creature's name with its marker, default false");
			AppendLine(builder, ShowDistanceKey, FormatState(ShowDistance), "Show the creature's distance in meters with its marker, after the name when Show Name is on, default false");
			AppendLine(builder, PassiveColorKey, passiveColorText, "Creatures that won't attack you, default " + DefaultPassiveColor);
			AppendLine(builder, HostileColorKey, hostileColorText, "Creatures that will attack you, default " + DefaultHostileColor);
			AppendLine(builder, BossColorKey, bossColorText, "Bosses, default " + DefaultBossColor);
			AppendLines(builder, creatureHeader);
			AppendLine(builder, AllCreaturesKey, FormatState(allCreatures), "");
			AppendLine(builder, AllHostileKey, FormatState(allHostile), "");
			builder.Append("\r\n");
			foreach (KeyValuePair<string, string> name in names)
			{
				AppendLine(builder, name.Key, FormatState(states[name.Key]), name.Value);
			}
			string contents = builder.ToString();
			try
			{
				if (File.Exists(path) && File.ReadAllText(path) == contents)
				{
					return;
				}
				File.WriteAllText(path, contents);
			}
			catch (IOException e)
			{
				Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't write " + path + ": " + e.Message);
			}
		}

		/// <summary>
		/// Appends each line followed by CRLF
		/// </summary>
		/// <param name="builder">The file contents being built</param>
		/// <param name="lines">The lines to append</param>
		private static void AppendLines(StringBuilder builder, string[] lines)
		{
			foreach (string line in lines)
			{
				builder.Append(line).Append("\r\n");
			}
		}

		/// <summary>
		/// Appends a "Key = value ## note" line, leaving off the note when it is blank
		/// </summary>
		/// <param name="builder">The file contents being built</param>
		/// <param name="key">The key</param>
		/// <param name="value">The value text</param>
		/// <param name="note">Comment after the value, or blank for none</param>
		private static void AppendLine(StringBuilder builder, string key, string value, string note)
		{
			builder.Append(key).Append(" = ").Append(value);
			if (note.Length > 0)
			{
				builder.Append(" ").Append(Comment).Append(" ").Append(note);
			}
			builder.Append("\r\n");
		}

		/// <summary>
		/// The text written for a true/false/inherit value
		/// </summary>
		/// <param name="value">The value, null for inherit</param>
		/// <returns>"true", "false" or "inherit"</returns>
		private static string FormatState(bool? value)
		{
			if (!value.HasValue)
			{
				return(Inherit);
			}
			return((value.Value ? "true" : "false"));
		}

		/// <summary>
		/// The creature's name in the current language, falling back to its name token
		/// </summary>
		/// <param name="character">The creature prefab's Character</param>
		/// <returns>The name to show after the value</returns>
		private static string GetDisplayName(Character character)
		{
			string name = character.m_name;
			if (Localization.instance != null)
			{
				name = Localization.instance.Localize(name);
			}
			// A line break or # would split the name across lines or into the comment parser
			return(name.Replace("\r", "").Replace("\n", " ").Replace("#", ""));
		}
	}
}
