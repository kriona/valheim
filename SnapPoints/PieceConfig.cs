using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace SnapPoints
{
	/// <summary>
	/// The mod's config file - one "Prefab = true/false/inherit ## Name" line per build piece, grouped by the tool that
	/// builds it, deciding which pieces get snap points
	/// </summary>
	/// <remarks>
	/// BepInEx's config format spends several lines on every entry, so the mod keeps its own file. The file is read
	/// and rewritten when a world's ZNetScene starts, which adds every build piece the game knows about, including ones
	/// added by other mods, while keeping the values already set, and read again whenever it is saved while a world is
	/// loaded. Lines for pieces that are no longer loaded are kept so their values survive a mod being removed for a
	/// while
	/// </remarks>
	internal static class PieceConfig
	{
		private const string FileName = Plugin.PluginGuid + ".cfg";
		private const string Comment = "##";
		private const string Inherit = "inherit";
		private const string NotLoadedGroup = "Not loaded";

		private static readonly string[] header =
		{
			"## " + Plugin.PluginName,
			"## Changes take effect as soon as this file is saved; you don't need to restart Valheim or rejoin the world",
			"",
			"## Each line is: Prefab = true/false/inherit ## Name in game",
			"## - true adds a snap point at each corner of the piece, keeping any it has from the game",
			"## - false takes away all of the piece's snap points, so it never snaps",
			"## - inherit leaves the piece's snap points as the game has them",
			"",
			"## Chests, resource stacks and piles, tables, benches, banners, armor stands, item stands and portals start as true, and everything else starts as inherit",
		};

		/// <summary>
		/// Containers, resource stacks, tables, benches, banners, armor stands, item stands and portals - pieces that are built in rows and stacks, set to true
		/// until the file says otherwise
		/// </summary>
		private static readonly HashSet<string> defaultOn = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"piece_chest_wood", "piece_chest", "piece_chest_private", "piece_chest_blackmetal", "piece_chest_barrel", "piece_chest_grausten", "piece_chest_warderobe",
			"wood_stack", "wood_fine_stack", "wood_core_stack", "wood_frost_stack", "wood_yggdrasil_stack", "blackwood_stack", "stone_pile", "coal_pile", "flint_pile", "obsidian_pile", "grausten_pile", "blackmarble_pile", "bone_stack", "skull_pile",
			"bar_copper_stack", "bar_tin_stack", "bar_bronze_stack", "bar_iron_stack", "bar_silver_stack", "bar_blackmetal_stack", "bar_flametal_stack", "bar_gold_stack",
			"piece_table", "piece_table_oak", "piece_blackmarble_table", "piece_table_runed", "piece_table_runed_small",
			"piece_bench01", "piece_blackwood_bench", "piece_blackwood_bench01", "piece_blackmarble_bench", "piece_bench_runed", "piece_logbench01",
			"piece_banner01", "piece_banner02", "piece_banner03", "piece_banner04", "piece_banner05", "piece_banner06", "piece_banner07", "piece_banner08", "piece_banner09", "piece_banner10", "piece_banner11",
			"ArmorStand", "ArmorStand_Female", "ArmorStand_Male",
			"itemstand", "itemstandh",
			"portal_wood", "portal_stone", "portal",
		};

		/// <summary>
		/// The true or false set on piece lines, by prefab name, leaving out the ones set to inherit
		/// </summary>
		private static readonly Dictionary<string, bool> states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Whether any piece is set to false, so the snap point search can skip the lookup when none is
		/// </summary>
		public static bool AnyOff { get; private set; }

		/// <summary>
		/// The config file's full path
		/// </summary>
		public static string FilePath => Path.Combine(Paths.ConfigPath, FileName);

		/// <summary>
		/// The value set for a piece
		/// </summary>
		/// <param name="prefabName">The piece's prefab name</param>
		/// <returns>True or false as set, or null for inherit</returns>
		public static bool? GetState(string prefabName)
		{
			if (states.TryGetValue(prefabName, out bool value))
			{
				return(value);
			}
			return(null);
		}

		/// <summary>
		/// Reads the config file again and updates the snap points when a world is loaded - without one, the file is
		/// read when the next world loads
		/// </summary>
		/// <remarks>
		/// Writing the file back changes it again, which reads it once more, finds nothing new and leaves it alone
		/// </remarks>
		public static void Reload()
		{
			if (ZNetScene.instance != null)
			{
				Load(ZNetScene.instance);
				Snapping.ApplyAll();
			}
		}

		/// <summary>
		/// Reads the config file, adds any build pieces it doesn't list yet and writes it back
		/// </summary>
		/// <param name="scene">The scene holding the prefab list</param>
		public static void Load(ZNetScene scene)
		{
			string path = FilePath;
			Dictionary<string, string> values;
			try
			{
				values = Read(path);
			}
			catch (IOException e)
			{
				Plugin.Log.LogWarning("Couldn't read " + path + ": " + e.Message);
				return;
			}
			SortedDictionary<string, SortedDictionary<string, string>> groups = GetBuildPieces(scene);
			HashSet<string> listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (SortedDictionary<string, string> group in groups.Values)
			{
				listed.UnionWith(group.Keys);
			}
			SortedDictionary<string, string> notLoaded = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (string key in values.Keys)
			{
				if (!listed.Contains(key))
				{
					notLoaded[key] = "";
				}
			}
			Dictionary<string, bool?> lineStates = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
			states.Clear();
			foreach (SortedDictionary<string, string> group in groups.Values)
			{
				foreach (string key in group.Keys)
				{
					lineStates[key] = GetLineState(values, key);
				}
			}
			foreach (string key in notLoaded.Keys)
			{
				lineStates[key] = GetLineState(values, key);
			}
			foreach (KeyValuePair<string, bool?> line in lineStates)
			{
				if (line.Value.HasValue)
				{
					states[line.Key] = line.Value.Value;
				}
			}
			AnyOff = states.ContainsValue(false);
			Write(path, groups, notLoaded, lineStates);
		}

		/// <summary>
		/// The build pieces of every tool, leaving out terrain tools like paths and leveling that have no shape to snap
		/// </summary>
		/// <remarks>
		/// A piece that more than one tool builds is listed under the first tool by name
		/// </remarks>
		/// <param name="scene">The scene holding the prefab list</param>
		/// <returns>Display name by prefab name, by tool name, both sorted</returns>
		private static SortedDictionary<string, SortedDictionary<string, string>> GetBuildPieces(ZNetScene scene)
		{
			SortedDictionary<string, PieceTable> tables = new SortedDictionary<string, PieceTable>(StringComparer.OrdinalIgnoreCase);
			foreach (GameObject prefab in scene.m_prefabs)
			{
				ItemDrop item = (prefab == null ? null : prefab.GetComponent<ItemDrop>());
				PieceTable table = item?.m_itemData?.m_shared?.m_buildPieces;
				if (table == null)
				{
					continue;
				}
				string toolName = Clean(Localize(item.m_itemData.m_shared.m_name));
				if (tables.ContainsKey(toolName))
				{
					toolName += " (" + prefab.name + ")";
				}
				tables[toolName] = table;
			}
			SortedDictionary<string, SortedDictionary<string, string>> groups = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<string, PieceTable> table in tables)
			{
				SortedDictionary<string, string> group = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (GameObject prefab in table.Value.m_pieces)
				{
					if (prefab == null || prefab.GetComponent<TerrainModifier>() != null || prefab.GetComponent<TerrainOp>() != null)
					{
						continue;
					}
					Piece piece = prefab.GetComponent<Piece>();
					if (piece == null || !seen.Add(prefab.name))
					{
						continue;
					}
					group[prefab.name] = Clean(Localize(piece.m_name));
				}
				if (group.Count > 0)
				{
					groups[table.Key] = group;
				}
			}
			return(groups);
		}

		/// <summary>
		/// The value of a piece's line, or its default when the file has no line for it
		/// </summary>
		/// <param name="values">Value text by key, as read from the file</param>
		/// <param name="key">The piece's prefab name</param>
		/// <returns>True or false, or null for inherit</returns>
		private static bool? GetLineState(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out string text))
			{
				return((defaultOn.Contains(key) ? true : (bool?)null));
			}
			if (bool.TryParse(text, out bool value))
			{
				return(value);
			}
			if (!text.Equals(Inherit, StringComparison.OrdinalIgnoreCase))
			{
				Plugin.Log.LogWarning("Couldn't read " + key + " = " + text + ", using inherit");
			}
			return(null);
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
		/// Writes the header and one line per piece under a heading for each tool, leaving the file untouched when
		/// nothing has changed
		/// </summary>
		/// <param name="path">Path of the config file</param>
		/// <param name="groups">Display name by prefab name, by tool name</param>
		/// <param name="notLoaded">Prefab names in the file that no tool builds, each with a blank display name</param>
		/// <param name="lineStates">Value by prefab name, null for inherit</param>
		private static void Write(string path, SortedDictionary<string, SortedDictionary<string, string>> groups, SortedDictionary<string, string> notLoaded, Dictionary<string, bool?> lineStates)
		{
			StringBuilder builder = new StringBuilder();
			foreach (string line in header)
			{
				builder.Append(line).Append("\r\n");
			}
			foreach (KeyValuePair<string, SortedDictionary<string, string>> group in groups)
			{
				AppendGroup(builder, group.Key, group.Value, lineStates);
			}
			if (notLoaded.Count > 0)
			{
				AppendGroup(builder, NotLoadedGroup, notLoaded, lineStates);
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
				Plugin.Log.LogWarning("Couldn't write " + path + ": " + e.Message);
			}
		}

		/// <summary>
		/// Appends a blank line, a heading and a "Prefab = value ## Name" line per piece, leaving off the name when it
		/// is blank
		/// </summary>
		/// <param name="builder">The file contents being built</param>
		/// <param name="heading">The tool name or other heading</param>
		/// <param name="pieces">Display name by prefab name</param>
		/// <param name="lineStates">Value by prefab name, null for inherit</param>
		private static void AppendGroup(StringBuilder builder, string heading, SortedDictionary<string, string> pieces, Dictionary<string, bool?> lineStates)
		{
			builder.Append("\r\n").Append(Comment).Append(" ").Append(heading).Append("\r\n");
			foreach (KeyValuePair<string, string> piece in pieces)
			{
				builder.Append(piece.Key).Append(" = ").Append(FormatState(lineStates[piece.Key]));
				if (piece.Value.Length > 0)
				{
					builder.Append(" ").Append(Comment).Append(" ").Append(piece.Value);
				}
				builder.Append("\r\n");
			}
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
		/// A name token in the current language, or the token itself before the language is loaded
		/// </summary>
		/// <param name="token">A name token like $piece_chestwood</param>
		/// <returns>The translated name</returns>
		private static string Localize(string token)
		{
			if (Localization.instance == null)
			{
				return(token);
			}
			return(Localization.instance.Localize(token));
		}

		/// <summary>
		/// Strips what would break a line of the file from a name
		/// </summary>
		/// <param name="name">A display name</param>
		/// <returns>The name on one line without #</returns>
		private static string Clean(string name)
		{
			// A line break or # would split the name across lines or into the comment parser
			return(name.Replace("\r", "").Replace("\n", " ").Replace("#", "").Trim());
		}
	}
}
