using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_testlog console command, which starts a test run of the village the player is in (see TestLog) - or
	/// spawns a village where they stand for the run to take up, with kri_vv_spawnvillage - stops it and saves its
	/// totals, or says how the run is going
	/// </summary>
	internal static class TestLogCommand
	{
		/// <summary>
		/// The command's name
		/// </summary>
		private const string Command = "kri_vv_testlog";

		/// <summary>
		/// What follows start to spawn a village for the run
		/// </summary>
		private const string SpawnOption = "spawn";

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "[start [spawn [farm]]|stop] - starts a test run of the village you're in, or of the next one set up near you, or stops it - start spawn also spawns a village where you stand for the run (devcommands, single player or host) - each run is saved to a file named after its biome", Run, optionsFetcher: () => new List<string> { "start", "stop" });
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
			else if (action == "start" && args.Length > 2 && !string.Equals(args[2], SpawnOption, System.StringComparison.OrdinalIgnoreCase))
			{
				args.Context.AddString("Use " + Command + " start, or " + Command + " start " + SpawnOption + " [farm] to spawn a village for the run");
			}
			else if (action == "start")
			{
				Start(args.Context, (args.Length > 2), ((args.Length > 3) ? args[3] : ""));
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
				args.Context.AddString("Use " + Command + " start, " + Command + " stop, or " + Command + " on its own to see how the run is going - kri_vv_quest covers the village's quests");
			}
		}

		/// <summary>
		/// Starts a run of the village the player is in or near, or with none near, of the first one set up near them -
		/// and when asked, spawns a village where they stand for it to take up, once the run has started so the village's
		/// generation and setup are in its file
		/// </summary>
		/// <param name="context">The console</param>
		/// <param name="spawn">Whether to spawn a village for the run</param>
		/// <param name="farm">The farm location to spawn, or "" for one at random</param>
		private static void Start(Terminal context, bool spawn, string farm)
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
			if (spawn)
			{
				string trouble = SpawnTrouble(context, village, player.transform.position, farm);
				if (trouble != null)
				{
					context.AddString(trouble);
					return;
				}
			}
			try
			{
				string file = TestLog.BeginRun(village, player.transform.position);
				context.AddString("Test run started" + ((village == null && !spawn) ? " with no village near - it takes up the first one set up near you" : "") + ", saving to " + file);
			}
			catch (System.Exception exception)
			{
				TestLog.EndRun("as it couldn't start");
				context.AddString("Couldn't start the test run: " + exception.Message);
				return;
			}
			if (spawn)
			{
				context.TryRunCommand(VillageSpawnCommand.Command + ((farm.Length > 0) ? " " + farm : ""));
			}
		}

		/// <summary>
		/// Checks whether a village can be spawned where the player stands for a run, before the run starts - with
		/// devcommands on, in single player or as the host, with no village near for the run to take up in its place,
		/// away from the world's start, and with a farm location of the name given, when one is
		/// </summary>
		/// <param name="context">The console</param>
		/// <param name="village">The village the player is in or near, or null</param>
		/// <param name="position">Where the player stands</param>
		/// <param name="farm">The farm location to spawn, or "" for one at random</param>
		/// <returns>Why it can't be, or null when it can</returns>
		private static string SpawnTrouble(Terminal context, Village village, UnityEngine.Vector3 position, string farm)
		{
			if (!context.IsCheatsEnabled())
			{
				return("Spawning a village needs devcommands, in single player or as the host");
			}
			if (village != null)
			{
				return("There's a village here already, which the run would take up - use " + Command + " start, or move away from it");
			}
			if (StartClearance.NearStart(position))
			{
				return("Too close to the world's start location - move farther away to spawn a village");
			}
			List<string> farms = VillageSpawnCommand.Farms();
			if (farms.Count == 0 || (farm.Length > 0 && !farms.Exists((string name) => string.Equals(name, farm, System.StringComparison.OrdinalIgnoreCase))))
			{
				return("Found no farm location" + ((farm.Length > 0) ? " called " + farm : "") + " - try one of: " + string.Join(", ", farms));
			}
			return(null);
		}
	}
}
