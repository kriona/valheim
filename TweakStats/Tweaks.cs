using System;
using System.Collections.Generic;
using System.IO;

namespace TweakStats
{
	/// <summary>
	/// Reads the config file and applies its tweaks, starting from the game's own values each time
	/// </summary>
	public static class Tweaks
	{
		/// <summary>
		/// Names shown in a problem message before the rest are counted
		/// </summary>
		private const int NamesShown = 3;

		/// <summary>
		/// The world being played, set when it is known - as the host or server when ZNet starts, and as a client once
		/// the server has sent it - or null at the main menu
		/// </summary>
		public static World CurrentWorld;

		private static TweakFile file = new TweakFile();

		/// <summary>
		/// Problems already logged since the file was last read, so applying the file again doesn't repeat them
		/// </summary>
		private static readonly HashSet<string> reported = new HashSet<string>();

		/// <summary>
		/// Reads the config file and logs the lines that couldn't be read
		/// </summary>
		public static void Load()
		{
			reported.Clear();
			string text;
			try
			{
				text = File.Exists(Plugin.ConfigPath) ? File.ReadAllText(Plugin.ConfigPath) : "";
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("Couldn't read " + Plugin.ConfigPath + ": " + e.Message);
				return;
			}
			file = TweakFile.Parse(text);
			foreach (string problem in file.Problems)
			{
				Report(problem);
			}
		}

		/// <summary>
		/// Reads the config file again after it changed and applies it, with a message saying so
		/// </summary>
		public static void Reload()
		{
			Load();
			Apply();
			Plugin.Log.LogInfo("Reloaded " + Path.GetFileName(Plugin.ConfigPath));
			string message = Plugin.PluginName + ": config reloaded";
			if (reported.Count > 0)
			{
				message += " with " + reported.Count + ((reported.Count == 1) ? " problem" : " problems") + " - see the BepInEx log";
			}
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, message);
		}

		/// <summary>
		/// Puts back the game's own values and applies every section that applies in the current world
		/// </summary>
		public static void Apply()
		{
			Originals.Restore();
			if (ObjectDB.instance == null)
			{
				return;
			}
			foreach (TweakSection section in file.Sections)
			{
				if (section.Worlds == null || InWorld(section.Worlds))
				{
					ApplySection(section);
				}
			}
			Player player = Player.m_localPlayer;
			if (player != null)
			{
				player.GetInventory().Changed();
			}
		}

		/// <summary>
		/// Puts back the game's own values while an action runs, then applies the tweaks again
		/// </summary>
		/// <param name="action">The action, e.g. listing the game's values</param>
		public static void WithoutTweaks(Action action)
		{
			Originals.Restore();
			try
			{
				action();
			}
			finally
			{
				Apply();
			}
		}

		/// <summary>
		/// Applies each line of a section to each object it names, logging each problem once
		/// </summary>
		/// <param name="section">The section</param>
		private static void ApplySection(TweakSection section)
		{
			string location = "line " + section.LineNumber + " [" + section.Header + "]: ";
			List<string> problems = new List<string>();
			List<Target> targets = Targets.Resolve(section.Selectors, problems);
			foreach (string problem in problems)
			{
				Report(location + problem);
			}
			foreach (TweakLine line in section.Lines)
			{
				Dictionary<string, List<string>> errors = new Dictionary<string, List<string>>();
				foreach (Target target in targets)
				{
					if (StatPath.TryApply(target.Root, line.Path, line.Value, out string error) || error == null)
					{
						continue;
					}
					if (!errors.TryGetValue(error, out List<string> names))
					{
						names = new List<string>();
						errors.Add(error, names);
					}
					names.Add(target.Header);
				}
				foreach (KeyValuePair<string, List<string>> error in errors)
				{
					string where = "line " + line.LineNumber + " [" + section.Header + "]";
					if (error.Value.Count < targets.Count)
					{
						where += " " + DescribeNames(error.Value);
					}
					Report(where + ": " + line.Path + ": " + error.Key);
				}
			}
		}

		/// <summary>
		/// Whether the current world is one of a group's worlds
		/// </summary>
		/// <param name="worlds">World names or IDs, server addresses, Steam IDs or PlayFab IDs, or SinglePlayer for a
		/// world loaded without other players allowed</param>
		/// <returns>True when the current world is in the list</returns>
		private static bool InWorld(List<string> worlds)
		{
			if (CurrentWorld == null)
			{
				return(false);
			}
			foreach (string world in worlds)
			{
				if (world.Equals("SinglePlayer", StringComparison.OrdinalIgnoreCase) && ZNet.IsSinglePlayer)
				{
					return(true);
				}
				if (world.Equals(CurrentWorld.m_name, StringComparison.OrdinalIgnoreCase) || world == CurrentWorld.m_uid.ToString())
				{
					return(true);
				}
				if (IsCurrentServer(world))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Whether this game joined the world from a server matching a group entry - its address, with or without the
		/// port, or its Steam or PlayFab ID
		/// </summary>
		/// <param name="entry">The entry, e.g. 203.0.113.5, 203.0.113.5:2456 or valheim.example.com</param>
		/// <returns>True when the entry names the server this game is connected to</returns>
		private static bool IsCurrentServer(string entry)
		{
			if (ZNet.instance == null || ZNet.instance.IsServer())
			{
				return(false);
			}
			string host = ZNet.m_serverHost;
			if (!string.IsNullOrEmpty(host) && (entry.Equals(host, StringComparison.OrdinalIgnoreCase) || entry.Equals(host + ":" + ZNet.m_serverHostPort, StringComparison.OrdinalIgnoreCase)))
			{
				return(true);
			}
			if (ZNet.m_serverSteamID != 0 && entry == ZNet.m_serverSteamID.ToString())
			{
				return(true);
			}
			return(!string.IsNullOrEmpty(ZNet.m_serverPlayFabPlayerId) && entry.Equals(ZNet.m_serverPlayFabPlayerId, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Lists the objects a problem applies to, for when it doesn't apply to every object in the section
		/// </summary>
		/// <param name="names">The objects' section headers</param>
		/// <returns>The names in brackets, with a count of any more</returns>
		private static string DescribeNames(List<string> names)
		{
			if (names.Count <= NamesShown)
			{
				return("(" + string.Join(", ", names) + ")");
			}
			return("(" + string.Join(", ", names.GetRange(0, NamesShown)) + " and " + (names.Count - NamesShown) + " more)");
		}

		/// <summary>
		/// Logs a problem with the config file, unless it was already logged since the file was last read
		/// </summary>
		/// <param name="problem">The problem, starting with its line number</param>
		private static void Report(string problem)
		{
			if (reported.Add(problem))
			{
				Plugin.Log.LogWarning(Path.GetFileName(Plugin.ConfigPath) + " " + problem);
			}
		}
	}
}
