using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_quest console command, which shows everything behind the quests of the village the player is in,
	/// starts one of a kind at once, or brings the one it's on home now (see VillageQuests)
	/// </summary>
	/// <remarks>
	/// A quest whose road the village doesn't know yet waits for the server to plan it, then starts on its own, saying so
	/// in the console and at the top left of the screen
	/// </remarks>
	internal static class QuestCommand
	{
		/// <summary>
		/// The command's name, and the word that brings the village's quest home now
		/// </summary>
		private const string Command = "kri_vv_quest";
		private const string DueWord = "due";

		/// <summary>
		/// How long a quest waits for the server's road before it's given up on, and how often it tries again meanwhile,
		/// in seconds
		/// </summary>
		private const float WaitMax = 30f;
		private const float RetryInterval = 0.5f;

		/// <summary>
		/// A quest waiting for the server's road, to start once it has it
		/// </summary>
		private class Waiting
		{
			/// <summary>
			/// The middle of the village's farm, to find it by
			/// </summary>
			public Vector3 Farm;

			/// <summary>
			/// The quest, and what it's for, or "" for the first it can go for
			/// </summary>
			public QuestKind Kind;
			public string Item = "";

			/// <summary>
			/// When it's given up on, and when it's tried again, by Time.time
			/// </summary>
			public float Until;
			public float NextTry;
		}

		/// <summary>
		/// The quest waiting for its road, or null when there's none
		/// </summary>
		private static Waiting waiting;

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "[kind [item]|due] - shows everything behind the quests of the village you're in, or starts a quest of a kind at once, whatever the village wants or the time of day, for the item given or the first it can go for - due brings the quest it's on home now", Run, isCheat: true, optionsFetcher: Options);
		}

		/// <summary>
		/// Starts the quest waiting for its road once the server has planned it, or gives up on it after WaitMax - call it
		/// from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (waiting == null || Time.time < waiting.NextTry)
			{
				return;
			}
			if (Player.m_localPlayer == null)
			{
				waiting = null;
				return;
			}
			waiting.NextTry = Time.time + RetryInterval;
			Village village = Village.Find(waiting.Farm);
			if (village == null)
			{
				Show("The village " + waiting.Kind + " was waiting on is gone, so it was given up on");
				waiting = null;
				return;
			}
			string result = VillageQuests.Force(village, waiting.Kind, waiting.Item, out bool roadAsked);
			if (roadAsked && Time.time < waiting.Until)
			{
				return;
			}
			Show(roadAsked ? "The server didn't plan the road to " + waiting.Kind + " in " + WaitMax.ToString("F0") + "s, so it was given up on" : result);
			waiting = null;
		}

		/// <summary>
		/// Lists what the command can be given, for the console to complete
		/// </summary>
		/// <returns>The kinds of quest, and the word that brings the quest home</returns>
		private static List<string> Options()
		{
			List<string> options = Kinds();
			options.Add(DueWord);
			return(options);
		}

		/// <summary>
		/// Lists the kinds of quest by name
		/// </summary>
		/// <returns>The names</returns>
		private static List<string> Kinds()
		{
			List<string> kinds = new List<string>();
			foreach (QuestKind kind in System.Enum.GetValues(typeof(QuestKind)))
			{
				if (kind != QuestKind.None)
				{
					kinds.Add(kind.ToString());
				}
			}
			return(kinds);
		}

		/// <summary>
		/// Runs the command for the village the player is in - showing everything behind its quests with nothing after
		/// it, starting a quest of the kind given, for the item given when there's one, or bringing the quest it's on home
		/// now with DueWord
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			Player player = Player.m_localPlayer;
			if (player == null || ZNet.instance == null)
			{
				args.Context.AddString("Load a world first");
				return;
			}
			Village village = Village.Find(player.transform.position);
			if (village == null)
			{
				args.Context.AddString("Stand in a village first");
				return;
			}
			string what = ((args.Length > 1) ? args[1] : "");
			if (what.Length == 0)
			{
				foreach (string line in VillageQuests.Report(village))
				{
					args.Context.AddString(line);
				}
				return;
			}
			if (string.Equals(what, DueWord, System.StringComparison.OrdinalIgnoreCase))
			{
				args.Context.AddString(VillageQuests.DueNow(village));
				return;
			}
			QuestKind kind = Kind(what);
			if (kind == QuestKind.None)
			{
				args.Context.AddString("Quests: " + string.Join(", ", Kinds()));
				return;
			}
			string item = ((args.Length > 2) ? args[2] : "");
			string result = VillageQuests.Force(village, kind, item, out bool roadAsked);
			if (!roadAsked)
			{
				waiting = null;
				args.Context.AddString(result);
				return;
			}
			waiting = new Waiting { Farm = village.Farm, Kind = kind, Item = item, Until = Time.time + WaitMax, NextTry = Time.time + RetryInterval };
			args.Context.AddString("Asked the server for the road to " + kind + " - it starts as soon as the server answers");
		}

		/// <summary>
		/// Finds the kind of quest named, ignoring case - by its name only, not its number
		/// </summary>
		/// <param name="name">What was typed</param>
		/// <returns>The kind, or None when it isn't one</returns>
		private static QuestKind Kind(string name)
		{
			foreach (QuestKind kind in System.Enum.GetValues(typeof(QuestKind)))
			{
				if (kind != QuestKind.None && string.Equals(kind.ToString(), name, System.StringComparison.OrdinalIgnoreCase))
				{
					return(kind);
				}
			}
			return(QuestKind.None);
		}

		/// <summary>
		/// Shows what became of a quest that waited for its road, in the console and at the top left of the screen
		/// </summary>
		/// <param name="message">What happened</param>
		private static void Show(string message)
		{
			Plugin.Log.LogInfo("Quest command: " + message);
			if (Console.instance != null)
			{
				Console.instance.AddString(message);
			}
			if (Player.m_localPlayer != null)
			{
				Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, message);
			}
		}
	}
}
