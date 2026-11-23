using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_testlog console command, which starts a test run of the village the player is in (see TestLog), stops
	/// it and saves its totals, or says how the run is going
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
			new Terminal.ConsoleCommand(Command, "[start|stop] - starts a test run of the village you're in, or of the next one set up near you, or stops it - each run is saved to a file named after its biome", Run, optionsFetcher: () => new List<string> { "start", "stop" });
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
			else
			{
				args.Context.AddString("Use " + Command + " start, " + Command + " stop, or " + Command + " on its own to see how the run is going");
			}
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
