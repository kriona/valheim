using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// An experiment in keeping a village going with no player near it - a dedicated server runs it, by moving the one
	/// area of the world it loads and runs itself to the village, started and stopped with the kri_vv_serverrun command
	/// </summary>
	/// <remarks>
	/// A dedicated server builds the zones, loads the objects and runs what's around its reference position the way a
	/// player's game does around the player, and nothing moves that position from the middle of the world on its own. A
	/// run moves it to the village whenever no player is within ParkMargin of the village's circle, and back to the
	/// middle of the world when one is, so the player's game takes over the village as they come, rather than the
	/// server running the monsters they fight. Only one village is run at a time, and the run lasts until it's stopped
	/// or the server shuts down. Starting a run starts a test run on the server too (see TestLog), saved in the server's
	/// own BepInEx folder, so how the village got on can be looked through afterward
	/// </remarks>
	internal static class ServerVillageRun
	{
		/// <summary>
		/// The command's name
		/// </summary>
		private const string Command = "kri_vv_serverrun";

		/// <summary>
		/// A client's request to the server - start, stop or how it's going - and the server's answer
		/// </summary>
		private const string RequestRpc = "kriona.VikingVillages.ServerRunRequest";
		private const string AnswerRpc = "kriona.VikingVillages.ServerRunAnswer";

		/// <summary>
		/// How near a village's circle a player has to come for the server to let go of it, in meters - near enough that
		/// the area their own game runs reaches into it
		/// </summary>
		private const float ParkMargin = 100f;

		/// <summary>
		/// How often the server checks for players near the village and reads its circle again, in seconds
		/// </summary>
		private const float CheckInterval = 1f;

		/// <summary>
		/// On the server, whether a village is being run, its circle, whether the server has let go of it for a player
		/// near it, and whether the run started the test run, to end it with the run
		/// </summary>
		private static bool running;
		private static Vector3 middle;
		private static float radius;
		private static bool parked;
		private static bool startedTestLog;

		/// <summary>
		/// When the server next checks for players near the village, by Time.time
		/// </summary>
		private static float nextCheck;

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void RegisterCommand()
		{
			new Terminal.ConsoleCommand(Command, "[start|stop] - experiment: has a dedicated server run the village you're in while no player is near it, with a test run log on the server", Run, isCheat: true, optionsFetcher: () => new List<string> { "start", "stop" });
		}

		/// <summary>
		/// Listens for the requests and answers, in each world
		/// </summary>
		public static void RegisterRpc()
		{
			ZRoutedRpc.instance?.Register<string>(RequestRpc, OnRequest);
			ZRoutedRpc.instance?.Register<string>(AnswerRpc, OnAnswer);
		}

		/// <summary>
		/// Runs the command - sending the request to the server, which answers
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			if (ZRoutedRpc.instance == null || ZNet.instance == null || Player.m_localPlayer == null)
			{
				args.Context.AddString("Load a world first");
				return;
			}
			string action = ((args.Length > 1) ? args[1].ToLowerInvariant() : "");
			if (action != "" && action != "start" && action != "stop")
			{
				args.Context.AddString("Use " + Command + " start, " + Command + " stop, or " + Command + " on its own to see how the run is going");
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(RequestRpc, action);
			args.Context.AddString("Asked the server" + ((action == "") ? " how its run is going" : " to " + action + " running the village"));
		}

		/// <summary>
		/// Shows the server's answer to the player who asked
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="message">The answer</param>
		private static void OnAnswer(long sender, string message)
		{
			Console.instance?.Print(message);
			Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, message);
		}

		/// <summary>
		/// Answers a player's request on the server - starting a run of the village they're in, stopping the run, or
		/// saying how it's going
		/// </summary>
		/// <param name="sender">The player's session</param>
		/// <param name="action">"start", "stop", or "" for how it's going</param>
		private static void OnRequest(long sender, string action)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			string answer;
			if (!ZNet.instance.IsDedicated())
			{
				answer = "Only a dedicated server can run a village on its own - this world is hosted by a player, whose game runs what's around them";
			}
			else if (action == "start")
			{
				answer = Start(sender);
			}
			else if (action == "stop")
			{
				answer = Stop("by the command");
			}
			else
			{
				answer = Status();
			}
			Plugin.Log.LogInfo("Server run: " + answer);
			ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, answer);
		}

		/// <summary>
		/// Starts running the village a player is in, and a test run on the server when none is going
		/// </summary>
		/// <param name="sender">The player's session</param>
		/// <returns>The answer</returns>
		private static string Start(long sender)
		{
			ZNetPeer peer = ZNet.instance.GetPeer(sender);
			if (peer == null)
			{
				return("Couldn't find where you are");
			}
			if (!VillageSimulation.FindCircle(peer.GetRefPos(), out Vector3 found, out float reach))
			{
				return("Stand in a village for the server to run it - it knows a village once a player's game has run it for a few seconds");
			}
			running = true;
			middle = found;
			radius = reach;
			parked = true;
			nextCheck = 0f;
			string log = "";
			if (!TestLog.On)
			{
				startedTestLog = true;
				log = ", with a test run log saved to " + TestLog.BeginRun(null, middle);
			}
			return("The server runs the village around " + middle.ToString("F0") + " whenever no player is within " + ParkMargin.ToString("F0") + "m of it" + log);
		}

		/// <summary>
		/// Stops the run, putting the server's area back in the middle of the world, and ends the test run it started
		/// </summary>
		/// <param name="how">How it ended, for the test log</param>
		/// <returns>The answer</returns>
		public static string Stop(string how)
		{
			if (!running)
			{
				return("The server isn't running a village");
			}
			running = false;
			ZNet.instance?.SetReferencePosition(Vector3.zero);
			string log = "";
			if (startedTestLog)
			{
				startedTestLog = false;
				log = ", and ended its test run log, saved to " + TestLog.RunFile;
				TestLog.EndRun(how);
			}
			return("The server stopped running the village around " + middle.ToString("F0") + log);
		}

		/// <summary>
		/// Describes the run, for the command
		/// </summary>
		/// <returns>The description</returns>
		private static string Status()
		{
			if (!running)
			{
				return("The server isn't running a village - " + Command + " start, in a village, has it run that one");
			}
			return("The server runs the village around " + middle.ToString("F0") + ", " + (parked ? "letting go of it while a player is near" : "running it now") + (TestLog.On ? " - " + TestLog.Status() : ""));
		}

		/// <summary>
		/// On the server, every CheckInterval, lets go of the village while a player is near it and runs it again once
		/// none is, reading its circle again as it grows
		/// </summary>
		public static void Update()
		{
			if (!running || ZNet.instance == null || !ZNet.instance.IsServer() || Time.time < nextCheck)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			if (VillageSimulation.FindCircle(middle, out Vector3 found, out float reach))
			{
				middle = found;
				radius = reach;
			}
			bool near = PlayerNear(ZNet.instance.GetPeers(), middle, radius + ParkMargin);
			if (near != parked)
			{
				parked = near;
				Plugin.Log.LogInfo("Server run: " + (near ? "let go of the village around " + middle.ToString("F0") + ", as a player came within " + ParkMargin.ToString("F0") + "m of it" : "running the village around " + middle.ToString("F0") + " again, with no player near it"));
			}
			ZNet.instance.SetReferencePosition(parked ? Vector3.zero : middle);
		}

		/// <summary>
		/// Checks whether any player is within a distance of a point, seen from above
		/// </summary>
		/// <param name="peers">The players connected</param>
		/// <param name="point">The point</param>
		/// <param name="reach">The distance, in meters</param>
		/// <returns>True when one is</returns>
		private static bool PlayerNear(List<ZNetPeer> peers, Vector3 point, float reach)
		{
			foreach (ZNetPeer peer in peers)
			{
				if (peer != null && peer.IsReady() && VillageSimulation.InReach(point, reach, peer.GetRefPos()))
				{
					return(true);
				}
			}
			return(false);
		}
	}
}
