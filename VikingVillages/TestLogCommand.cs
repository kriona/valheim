using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_testlog console command, which starts a test run of the village the player is in (see TestLog), stops
	/// it and saves its totals, or says how the run is going - and shows, starts or brings home the quests of the
	/// village the player is in (see VillageQuests)
	/// </summary>
	internal static class TestLogCommand
	{
		/// <summary>
		/// The command's name
		/// </summary>
		private const string Command = "kri_vv_testlog";

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "[start|stop|quest [kind [item]|due]] - starts a test run of the village you're in, or of the next one set up near you, or stops it - each run is saved to a file named after its biome. quest shows everything behind the quests of the village you're in, quest followed by a kind of quest starts one at once, and quest due brings the one it's on home now", Run, optionsFetcher: () => new List<string> { "start", "stop", "quest" });
		}

		/// <summary>
		/// Runs the command - starting a run, stopping it, or saying how it's going with nothing after the command
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			string action = ((args.Length > 1) ? args[1].ToLowerInvariant() : "");
			if (action == "")
			{
				args.Context.AddString(TestLog.Status());
			}
			else if (action == "start")
			{
				Start(args.Context);
			}
			else if (action == "stop")
			{
				if (!TestLog.On)
				{
					args.Context.AddString("No test run is going");
					return;
				}
				TestLog.EndRun("by the command");
				args.Context.AddString("Test run stopped, saved to " + TestLog.RunFile);
			}
			else if (action == "quest")
			{
				Quest(args);
			}
			else
			{
				args.Context.AddString("Use " + Command + " start, " + Command + " stop, " + Command + " quest, or " + Command + " on its own to see how the run is going");
			}
		}

		/// <summary>
		/// Runs the quest part of the command for the village the player is in - showing everything behind its quests
		/// with nothing after it, starting a quest of the kind given, for the item given when there's one, or bringing
		/// the quest it's on home now with "due"
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Quest(Terminal.ConsoleEventArgs args)
		{
			Player player = Player.m_localPlayer;
			Village village = ((player != null) ? Village.Find(player.transform.position) : null);
			if (village == null)
			{
				args.Context.AddString("Stand in a village first");
				return;
			}
			string what = ((args.Length > 2) ? args[2] : "");
			if (what.Length == 0)
			{
				foreach (string line in VillageQuests.Report(village))
				{
					args.Context.AddString(line);
				}
				return;
			}
			if (what.ToLowerInvariant() == "due")
			{
				args.Context.AddString(VillageQuests.DueNow(village));
				return;
			}
			if (!System.Enum.TryParse(what, true, out QuestKind kind) || kind == QuestKind.None)
			{
				args.Context.AddString("Quests: " + string.Join(", ", System.Array.FindAll(System.Enum.GetNames(typeof(QuestKind)), (string name) => name != "None")));
				return;
			}
			args.Context.AddString(VillageQuests.Force(village, kind, ((args.Length > 3) ? args[3] : "")));
		}

		/// <summary>
		/// Starts a run of the village the player is in or near, or with none near, of the first one set up near them
		/// </summary>
		/// <param name="context">The console</param>
		private static void Start(Terminal context)
		{
			Player player = Player.m_localPlayer;
			if (player == null || ZNet.instance == null)
			{
				context.AddString("Load a world first");
				return;
			}
			if (TestLog.On)
			{
				context.AddString("A test run is already going - " + Command + " stop ends it");
				return;
			}
			Village village = Village.Find(player.transform.position);
			try
			{
				string file = TestLog.BeginRun(village, player.transform.position);
				context.AddString("Test run started" + ((village == null) ? " with no village near - it takes up the first one set up near you" : "") + ", saving to " + file);
			}
			catch (System.Exception exception)
			{
				TestLog.EndRun("as it couldn't start");
				context.AddString("Couldn't start the test run: " + exception.Message);
			}
		}
	}
}
