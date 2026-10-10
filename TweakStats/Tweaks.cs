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

		/// <summary>
		/// The text of this game's config file, which a server sends to the players who join it
		/// </summary>
		public static string LocalText = "";

		private static TweakFile localFile = new TweakFile();

		/// <summary>
		/// The config the server sent, used in place of this game's own while connected to it, or null
		/// </summary>
		private static TweakFile serverFile;

		/// <summary>
		/// A message to show once the player is in the world, or null
		/// </summary>
		private static string pendingMessage;

		/// <summary>
		/// The sections the last apply skipped because they need the server to have the mod, logged once the player is in
		/// the world, since the server's config arrives just after a client first applies its own
		/// </summary>
		private static readonly List<string> skippedNotes = new List<string>();

		/// <summary>
		/// Whether the skipped sections were logged for the current connection
		/// </summary>
		private static bool skippedNoticed;

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
			LocalText = text;
			localFile = TweakFile.Parse(text);
			foreach (string problem in localFile.Problems)
			{
				Report(Path.GetFileName(Plugin.ConfigPath) + " " + problem);
			}
		}

		/// <summary>
		/// Reads the config file again after it changed and applies it, sending it to the players on this game's world,
		/// with a message saying so
		/// </summary>
		public static void Reload()
		{
			Load();
			if (RunsWorld)
			{
				ServerSync.SendConfigToAll();
			}
			Apply();
			Plugin.Log.LogInfo("Reloaded " + Path.GetFileName(Plugin.ConfigPath));
			string message = Plugin.PluginName + ": config reloaded";
			if (UsingServerConfig)
			{
				message += ", but this server's config is used while you're on it";
			}
			else if (reported.Count > 0)
			{
				message += " with " + reported.Count + ((reported.Count == 1) ? " problem" : " problems") + " - see the BepInEx log";
			}
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, message);
			if (skippedNoticed)
			{
				NoticeSkipped();
			}
		}

		/// <summary>
		/// Switches to the config a server sent, telling the player when their own config file is ignored because of it
		/// </summary>
		/// <param name="text">The server's config file</param>
		public static void UseServerConfig(string text)
		{
			serverFile = TweakFile.Parse(text);
			foreach (string problem in serverFile.Problems)
			{
				Report("the server's config " + problem);
			}
			Plugin.Log.LogInfo("Using the server's config");
			if (localFile.Sections.Count > 0)
			{
				pendingMessage = Plugin.PluginName + ": this server has its own config, so your " + Path.GetFileName(Plugin.ConfigPath) + " is ignored while you're on it";
				Plugin.Log.LogInfo(pendingMessage);
			}
			Apply();
		}

		/// <summary>
		/// Goes back to this game's own config file after leaving a server
		/// </summary>
		public static void ClearServerConfig()
		{
			serverFile = null;
			pendingMessage = null;
			skippedNoticed = false;
		}

		/// <summary>
		/// Shows the message waiting for the player and logs the skipped sections, once they're in the world
		/// </summary>
		public static void ShowPendingMessage()
		{
			if (MessageHud.instance == null || Player.m_localPlayer == null)
			{
				return;
			}
			if (pendingMessage != null)
			{
				MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, pendingMessage);
				pendingMessage = null;
			}
			if (!skippedNoticed)
			{
				skippedNoticed = true;
				NoticeSkipped();
			}
		}

		/// <summary>
		/// Logs each section the last apply skipped, with a message saying how many
		/// </summary>
		private static void NoticeSkipped()
		{
			if (skippedNotes.Count == 0)
			{
				return;
			}
			foreach (string note in skippedNotes)
			{
				Report(note);
			}
			string message = Plugin.PluginName + ": this server doesn't have " + Plugin.PluginName + ", so " + skippedNotes.Count + ((skippedNotes.Count == 1) ? " section is" : " sections are") + " skipped - see the BepInEx log";
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, message);
		}

		/// <summary>
		/// Whether this game runs the world - single player, hosting or a dedicated server
		/// </summary>
		public static bool RunsWorld
		{
			get
			{
				return(ZNet.instance != null && ZNet.instance.IsServer());
			}
		}

		/// <summary>
		/// Whether this game is using the config a server sent
		/// </summary>
		public static bool UsingServerConfig
		{
			get
			{
				return(serverFile != null);
			}
		}

		/// <summary>
		/// Whether creature, build piece and hit effect tweaks apply - when this game runs the world, or uses the config
		/// of a server that runs it, so every player with the mod uses the same values
		/// </summary>
		public static bool WorldTweaksApply
		{
			get
			{
				return(RunsWorld || UsingServerConfig);
			}
		}

		/// <summary>
		/// Whether this game, as the server, refuses players who don't have the same version of the mod - the
		/// requireMod setting, where auto requires it when the config has Creature, Piece or Effect sections
		/// </summary>
		public static bool RequireMod
		{
			get
			{
				if (!localFile.Settings.TryGetValue("requireMod", out string setting) || setting == "auto")
				{
					return(HasWorldSections(localFile));
				}
				return(setting == "true");
			}
		}

		/// <summary>
		/// Puts back the game's own values and applies every section that applies in the current world, then updates the
		/// creatures and pieces already in the world
		/// </summary>
		public static void Apply()
		{
			Instances.StartApply();
			Originals.Restore();
			skippedNotes.Clear();
			if (ObjectDB.instance != null)
			{
				foreach (TweakSection section in (serverFile ?? localFile).Sections)
				{
					if (section.Worlds == null || InWorld(section.Worlds))
					{
						ApplySection(section);
					}
				}
			}
			Instances.Sync();
			Instances.RefreshEquipmentEffects();
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
			string source = Source() + " ";
			List<string> problems = new List<string>();
			List<string> skipped = new List<string>();
			List<Target> targets = Targets.Resolve(section.Selectors, problems, skipped);
			if (!WorldTweaksApply)
			{
				for (int i = targets.Count - 1; i >= 0; i--)
				{
					if (targets[i].NeedsServer)
					{
						skipped.Add(targets[i].Header);
						targets.RemoveAt(i);
					}
				}
			}
			foreach (string problem in problems)
			{
				Report(source + location + problem);
			}
			if (skipped.Count > 0)
			{
				skippedNotes.Add(source + location + "skipped " + DescribeNames(skipped) + " - creature, build piece and hit effect tweaks only apply in single player, when hosting, or on a server that has " + Plugin.PluginName);
			}
			foreach (TweakLine line in section.Lines)
			{
				Dictionary<string, List<string>> errors = new Dictionary<string, List<string>>();
				foreach (Target target in targets)
				{
					if (target.TryApply(line.Path, line.Value, out string error) || error == null)
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
					string where = source + "line " + line.LineNumber + " [" + section.Header + "]";
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
		/// Lists the objects a message is about, for when it doesn't apply to every object in the section
		/// </summary>
		/// <param name="names">The objects' section headers or selectors</param>
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
				Plugin.Log.LogWarning(problem);
			}
		}

		/// <summary>
		/// What the applied sections come from, for messages
		/// </summary>
		/// <returns>The config file's name, or "the server's config"</returns>
		private static string Source()
		{
			return(UsingServerConfig ? "the server's config" : Path.GetFileName(Plugin.ConfigPath));
		}

		/// <summary>
		/// Whether a config has sections whose tweaks need every player to use the same values
		/// </summary>
		/// <param name="file">The config</param>
		/// <returns>True when a section names creatures, build pieces or status effects</returns>
		private static bool HasWorldSections(TweakFile file)
		{
			foreach (TweakSection section in file.Sections)
			{
				foreach (string selector in section.Selectors)
				{
					string trimmed = selector.TrimStart();
					if (trimmed.StartsWith("Creature:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("Piece:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("Effect:", StringComparison.OrdinalIgnoreCase))
					{
						return(true);
					}
				}
			}
			return(false);
		}
	}
}
