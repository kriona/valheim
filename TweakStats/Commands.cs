using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

namespace TweakStats
{
	/// <summary>
	/// The tweakstats console command, for finding names and listing stats to put in the config file
	/// </summary>
	public static class Commands
	{
		/// <summary>
		/// Most search results printed to the console
		/// </summary>
		private const int MaxFound = 30;

		/// <summary>
		/// Adds the tweakstats command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand("tweakstats", "find <text>, inventory, world, dump <section>, list or reload - Tweak Stats config tools", Run, optionsFetcher: () => new List<string> { "find", "inventory", "world", "dump", "list", "reload" });
		}

		/// <summary>
		/// Runs a tweakstats subcommand
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			string subcommand = (args.Length > 1) ? args[1].ToLowerInvariant() : "";
			string rest = (args.Length > 2) ? args.ArgsAll.Substring(args.ArgsAll.IndexOf(' ') + 1).Trim() : "";
			if (subcommand == "find" && rest != "")
			{
				Find(args.Context, rest);
			}
			else if (subcommand == "inventory" || subcommand == "inv")
			{
				Inventory(args.Context);
			}
			else if (subcommand == "world")
			{
				World(args.Context);
			}
			else if (subcommand == "dump" && rest != "")
			{
				Dump(args.Context, rest);
			}
			else if (subcommand == "list")
			{
				List(args.Context);
			}
			else if (subcommand == "reload")
			{
				Tweaks.Reload();
				args.Context.AddString("Reloaded " + Path.GetFileName(Plugin.ConfigPath));
			}
			else
			{
				args.Context.AddString("tweakstats find <text> - finds the sections for items, creatures, build pieces and status effects whose name contains the text, e.g. tweakstats find troll");
				args.Context.AddString("tweakstats inventory - lists the items you're carrying with their section names and main stats");
				args.Context.AddString("tweakstats world - shows the world's name and ID and the server you're on, for [Worlds: ...] groups");
				args.Context.AddString("tweakstats dump <section> - writes every stat of what a section names to " + DumpFileName() + ", e.g. tweakstats dump SwordIron");
				args.Context.AddString("tweakstats list - writes every item, recipe, creature, build piece and status effect name to " + ListFileName());
				args.Context.AddString("tweakstats reload - reads " + Path.GetFileName(Plugin.ConfigPath) + " again");
			}
		}

		/// <summary>
		/// Prints the sections for the items, creatures, build pieces and status effects whose name in game or prefab
		/// name contains some text
		/// </summary>
		/// <param name="terminal">The console to print to</param>
		/// <param name="text">The text to search for</param>
		private static void Find(Terminal terminal, string text)
		{
			List<string> found = new List<string>();
			foreach (ItemDrop item in Prefabs.Items())
			{
				AddIfFound(found, text, "", item.gameObject.name, item.m_itemData.m_shared.m_name);
			}
			foreach (GameObject creature in Prefabs.Creatures())
			{
				AddIfFound(found, text, "Creature:", creature.name, creature.GetComponent<Character>().m_name);
			}
			foreach (GameObject piece in Prefabs.Pieces())
			{
				AddIfFound(found, text, "Piece:", piece.name, piece.GetComponent<Piece>().m_name);
			}
			foreach (StatusEffect effect in Prefabs.Effects())
			{
				AddIfFound(found, text, "Effect:", effect.name, effect.m_name);
			}
			found.Sort(StringComparer.OrdinalIgnoreCase);
			if (found.Count == 0)
			{
				terminal.AddString("Nothing matches \"" + text + "\"" + ((ZNetScene.instance == null) ? " - join a world to search creatures and build pieces too" : ""));
				return;
			}
			for (int i = 0; i < found.Count && i < MaxFound; i++)
			{
				terminal.AddString(found[i]);
			}
			if (found.Count > MaxFound)
			{
				terminal.AddString("...and " + (found.Count - MaxFound) + " more - search for more of the name to narrow it down");
			}
		}

		/// <summary>
		/// Adds a "[section] - name" line when a prefab's name or its name in game contains the searched text
		/// </summary>
		/// <param name="found">The lines found so far</param>
		/// <param name="text">The searched text</param>
		/// <param name="kind">The section's prefix, e.g. Creature:, or empty for an item</param>
		/// <param name="prefabName">The prefab's name</param>
		/// <param name="gameName">The game's name for it, e.g. $enemy_troll</param>
		private static void AddIfFound(List<string> found, string text, string kind, string prefabName, string gameName)
		{
			string displayName = Prefabs.Localize(gameName);
			if (prefabName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || displayName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				found.Add("[" + kind + prefabName + "]" + ((displayName != "") ? " - " + displayName : ""));
			}
		}

		/// <summary>
		/// Prints each item the local player is carrying with its section name and main stats, as tweaked and upgraded
		/// </summary>
		/// <param name="terminal">The console to print to</param>
		private static void Inventory(Terminal terminal)
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				terminal.AddString("Join a world to list the items you're carrying");
				return;
			}
			List<ItemDrop.ItemData> items = player.GetInventory().GetAllItemsInGridOrder();
			if (items.Count == 0)
			{
				terminal.AddString("You aren't carrying anything");
				return;
			}
			terminal.AddString("You're carrying " + items.Count + ((items.Count == 1) ? " item" : " items") + " - section, name and stats:");
			foreach (ItemDrop.ItemData item in items)
			{
				terminal.AddString(DescribeItem(item));
			}
		}

		/// <summary>
		/// Prints the current world's name and ID, how this game is connected to it, and the [Worlds: ...] groups that
		/// apply to it
		/// </summary>
		/// <param name="terminal">The console to print to</param>
		private static void World(Terminal terminal)
		{
			World world = Tweaks.CurrentWorld;
			if (ZNet.instance == null || world == null)
			{
				terminal.AddString("Join a world to see its name and ID");
				return;
			}
			terminal.AddString("World: " + world.m_name + " (ID " + world.m_uid + ")");
			terminal.AddString("Playing: " + DescribeConnection());
			List<string> groups = new List<string> { world.m_name, world.m_uid.ToString() };
			string server = ServerEntry();
			if (server != null)
			{
				groups.Add(server);
			}
			if (ZNet.IsSinglePlayer)
			{
				groups.Add("SinglePlayer");
			}
			terminal.AddString("Sections in " + string.Join(" or ", groups.ConvertAll(entry => "[Worlds: " + entry + "]")) + " apply here - use the ID or server when other worlds share the name");
		}

		/// <summary>
		/// What a [Worlds: ...] group can use to name the server this game joined
		/// </summary>
		/// <returns>The server's address, Steam ID or PlayFab ID, or null when this game is the server</returns>
		private static string ServerEntry()
		{
			if (ZNet.instance.IsServer())
			{
				return(null);
			}
			if (!string.IsNullOrEmpty(ZNet.m_serverHost))
			{
				return(ZNet.m_serverHost);
			}
			if (ZNet.m_serverSteamID != 0)
			{
				return(ZNet.m_serverSteamID.ToString());
			}
			return(string.IsNullOrEmpty(ZNet.m_serverPlayFabPlayerId) ? null : ZNet.m_serverPlayFabPlayerId);
		}

		/// <summary>
		/// How this game is playing the current world - alone, hosting, or connected to a server and by what address
		/// </summary>
		/// <returns>e.g. on a server at 203.0.113.5:2456</returns>
		private static string DescribeConnection()
		{
			if (ZNet.instance.IsServer())
			{
				if (ZNet.IsSinglePlayer)
				{
					return("single player");
				}
				return("hosting" + ((ZNet.m_ServerName != "") ? " the server \"" + ZNet.m_ServerName + "\"" : " for other players"));
			}
			if (!string.IsNullOrEmpty(ZNet.m_serverHost))
			{
				return("on a server at " + ZNet.m_serverHost + ":" + ZNet.m_serverHostPort);
			}
			if (ZNet.m_serverSteamID != 0)
			{
				return("on a server with Steam ID " + ZNet.m_serverSteamID);
			}
			if (!string.IsNullOrEmpty(ZNet.m_serverPlayFabPlayerId))
			{
				return("on a crossplay server with PlayFab ID " + ZNet.m_serverPlayFabPlayerId);
			}
			return("on a server");
		}

		/// <summary>
		/// One line about a carried item - its section, name, level and the stats that matter for its kind
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>e.g. [SwordIron] Iron sword - level 2, equipped, damages slash 55, durability 180/250, weight 1.5</returns>
		private static string DescribeItem(ItemDrop.ItemData item)
		{
			ItemDrop.ItemData.SharedData shared = item.m_shared;
			string prefabName = (item.m_dropPrefab != null) ? item.m_dropPrefab.name : shared.m_name;
			string line = "[" + prefabName + "] " + Prefabs.Localize(shared.m_name);
			if (item.m_stack > 1)
			{
				line += " x" + item.m_stack;
			}
			List<string> details = new List<string>();
			if (shared.m_maxQuality > 1)
			{
				details.Add("level " + item.m_quality);
			}
			if (item.m_equipped)
			{
				details.Add("equipped");
			}
			string damages = DescribeDamages(item.GetDamage());
			if (damages != "")
			{
				details.Add("damages " + damages);
			}
			float armor = item.GetArmor();
			if (armor > 0f)
			{
				details.Add("armor " + TweakValue.Format(armor));
			}
			if (shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
			{
				details.Add("block " + TweakValue.Format(item.GetBaseBlockPower()));
			}
			List<string> food = new List<string>();
			AddFood(food, shared.m_food, "health");
			AddFood(food, shared.m_foodStamina, "stamina");
			AddFood(food, shared.m_foodEitr, "eitr");
			if (food.Count > 0)
			{
				details.Add("food " + string.Join(" ", food) + " for " + TweakValue.Format(shared.m_foodBurnTime) + "s");
			}
			if (shared.m_useDurability)
			{
				details.Add("durability " + TweakValue.Format(Math.Round(item.m_durability)) + "/" + TweakValue.Format(item.GetMaxDurability()));
			}
			details.Add("weight " + TweakValue.Format(item.GetWeight()));
			return(line + " - " + string.Join(", ", details));
		}

		/// <summary>
		/// Adds one of a food's values to a list when the food gives any of it
		/// </summary>
		/// <param name="food">The list of values</param>
		/// <param name="amount">How much the food gives</param>
		/// <param name="name">What it gives, e.g. health</param>
		private static void AddFood(List<string> food, float amount, string name)
		{
			if (amount > 0f)
			{
				food.Add(TweakValue.Format(amount) + " " + name);
			}
		}

		/// <summary>
		/// Lists the damage types that do any damage, named as in the config file
		/// </summary>
		/// <param name="damages">The damages</param>
		/// <returns>e.g. slash 55 fire 10, or empty when there's no damage</returns>
		private static string DescribeDamages(HitData.DamageTypes damages)
		{
			List<string> parts = new List<string>();
			foreach (FieldInfo field in typeof(HitData.DamageTypes).GetFields())
			{
				if (field.FieldType == typeof(float) && (float)field.GetValue(damages) > 0f)
				{
					parts.Add(field.Name.Substring(2) + " " + TweakValue.Format(field.GetValue(damages)));
				}
			}
			return(string.Join(" ", parts));
		}

		/// <summary>
		/// Writes the game's value of every stat of what a section header names, as commented-out config lines
		/// </summary>
		/// <param name="terminal">The console to print to</param>
		/// <param name="header">The section header, without brackets</param>
		private static void Dump(Terminal terminal, string header)
		{
			header = header.Trim().TrimStart('[').TrimEnd(']');
			List<string> problems = new List<string>();
			StringBuilder text = new StringBuilder();
			int count = 0;
			Tweaks.WithoutTweaks(() =>
			{
				List<string> selectors = new List<string>(header.Split(','));
				foreach (Target target in Targets.Resolve(selectors.ConvertAll(selector => selector.Trim()), problems, null))
				{
					List<string> lines = new List<string>();
					target.List(lines);
					text.Append("\n[" + target.Header + "]\n");
					foreach (string line in lines)
					{
						text.Append("# " + line + "\n");
					}
					count++;
				}
			});
			foreach (string problem in problems)
			{
				terminal.AddString(problem);
			}
			if (count == 0)
			{
				return;
			}
			string intro = "# " + Plugin.PluginName + " dump of [" + header + "] from Valheim " + global::Version.GetVersionString() + " on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n" +
				"# These are the game's values before any tweaks. Copy the lines you want into " + Path.GetFileName(Plugin.ConfigPath) + " and remove the # to use them\n";
			if (WriteFile(terminal, DumpFileName(), intro + text))
			{
				terminal.AddString("Wrote " + count + ((count == 1) ? " section" : " sections") + " to " + Path.Combine(Paths.ConfigPath, DumpFileName()));
			}
		}

		/// <summary>
		/// Writes a Markdown list of every item, recipe, creature, build piece and status effect the config file can name
		/// </summary>
		/// <param name="terminal">The console to print to</param>
		private static void List(Terminal terminal)
		{
			StringBuilder text = new StringBuilder();
			text.Append("# Tweak Stats Names\n\n");
			text.Append("Generated by " + Plugin.PluginName + " " + Plugin.PluginVersion + " from Valheim " + global::Version.GetVersionString() + " on " + DateTime.Now.ToString("yyyy-MM-dd") + ".\n\n");
			text.Append("## Items\n\n");
			text.Append("| Section | Name | Type | Skill |\n");
			text.Append("| --- | --- | --- | --- |\n");
			List<ItemDrop> items = Prefabs.Items();
			items.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name, StringComparison.OrdinalIgnoreCase));
			foreach (ItemDrop item in items)
			{
				ItemDrop.ItemData.SharedData shared = item.m_itemData.m_shared;
				text.Append("| `[" + item.gameObject.name + "]` | " + Prefabs.DisplayName(item) + " | " + shared.m_itemType + " | " + shared.m_skillType + " |\n");
			}
			text.Append("\n## Recipes\n\n");
			text.Append("| Section | Makes | Station |\n");
			text.Append("| --- | --- | --- |\n");
			List<Recipe> recipes = Prefabs.Recipes();
			recipes.Sort((a, b) => string.Compare(a.m_item.gameObject.name, b.m_item.gameObject.name, StringComparison.OrdinalIgnoreCase));
			foreach (Recipe recipe in recipes)
			{
				string station = (recipe.m_craftingStation != null) ? recipe.m_craftingStation.gameObject.name : "none";
				text.Append("| `[Recipe:" + recipe.m_item.gameObject.name + "]` | " + Prefabs.DisplayName(recipe.m_item) + " x" + recipe.m_amount + " | " + station + " |\n");
			}
			List<GameObject> creatures = Prefabs.Creatures();
			AppendNames(text, "Creatures", "Creature:", creatures.ConvertAll(creature => (creature.name, creature.GetComponent<Character>().m_name)));
			List<GameObject> pieces = Prefabs.Pieces();
			AppendNames(text, "Build Pieces", "Piece:", pieces.ConvertAll(piece => (piece.name, piece.GetComponent<Piece>().m_name)));
			List<StatusEffect> effects = Prefabs.Effects();
			AppendNames(text, "Status Effects", "Effect:", effects.ConvertAll(effect => (effect.name, effect.m_name)));
			if (WriteFile(terminal, ListFileName(), text.ToString()))
			{
				terminal.AddString("Wrote " + items.Count + " items, " + recipes.Count + " recipes, " + creatures.Count + " creatures, " + pieces.Count + " build pieces and " + effects.Count + " status effects to " + Path.Combine(Paths.ConfigPath, ListFileName()));
				if (ZNetScene.instance == null)
				{
					terminal.AddString("Join a world to list creatures and build pieces too");
				}
			}
		}

		/// <summary>
		/// Adds a Markdown table of sections and names in game, sorted by prefab name
		/// </summary>
		/// <param name="text">The file being written</param>
		/// <param name="heading">The table's heading</param>
		/// <param name="kind">The section prefix, e.g. Creature:</param>
		/// <param name="entries">Each prefab's name and the game's name for it</param>
		private static void AppendNames(StringBuilder text, string heading, string kind, List<(string prefabName, string gameName)> entries)
		{
			entries.Sort((a, b) => string.Compare(a.prefabName, b.prefabName, StringComparison.OrdinalIgnoreCase));
			text.Append("\n## " + heading + "\n\n");
			text.Append("| Section | Name |\n");
			text.Append("| --- | --- |\n");
			foreach ((string prefabName, string gameName) in entries)
			{
				text.Append("| `[" + kind + prefabName + "]` | " + Prefabs.Localize(gameName) + " |\n");
			}
		}

		/// <summary>
		/// Writes a file to the BepInEx config folder, printing why when it can't
		/// </summary>
		/// <param name="terminal">The console to print to</param>
		/// <param name="fileName">The file's name</param>
		/// <param name="text">The file's contents</param>
		/// <returns>True when the file was written</returns>
		private static bool WriteFile(Terminal terminal, string fileName, string text)
		{
			string path = Path.Combine(Paths.ConfigPath, fileName);
			try
			{
				File.WriteAllText(path, text);
				return(true);
			}
			catch (Exception e)
			{
				terminal.AddString("Couldn't write " + path + ": " + e.Message);
				return(false);
			}
		}

		/// <summary>
		/// The name of the file dump writes
		/// </summary>
		/// <returns>The file name</returns>
		private static string DumpFileName()
		{
			return(Plugin.PluginGuid + ".dump.txt");
		}

		/// <summary>
		/// The name of the file list writes
		/// </summary>
		/// <returns>The file name</returns>
		private static string ListFileName()
		{
			return(Plugin.PluginGuid + ".names.md");
		}
	}
}
