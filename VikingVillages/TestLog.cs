using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Extra logging for a test run spent watching a village get by on its own, started and stopped with the
	/// kri_vv_testlog command (see TestLogCommand) - the time and the game's day on every line, why each viking died and
	/// the hits it took before, a snapshot of every village and viking every SnapshotInterval seconds, raids starting and
	/// ending, vikings leaving the village, ending up under the ground, in the water, badly hurt, hungry with no food or
	/// with broken gear, creatures killed around the village, loot that despawned before a viking put it away, each kind
	/// of exception once, and frames that hitch, the groups of vikings the server keeps on the road with each snapshot -
	/// and totals of what the vikings did and what happened to them, written with each snapshot and as the run ends
	/// </summary>
	/// <remarks>
	/// It all runs on the client that owns the vikings, which is the only one that knows what they're doing. The totals
	/// count from when the run started. Every line the plugin writes while a run is going goes to BepInEx's log and to
	/// the run's own file too, which is named after the biome of its village, numbered after the first in each biome so
	/// no earlier run's file is written over. The file is written to disk with each snapshot and as the run ends - by the
	/// command, on leaving the world or as the game closes
	/// </remarks>
	internal static class TestLog
	{
		/// <summary>
		/// The folder in BepInEx's the runs' files are saved in
		/// </summary>
		private const string FolderName = "VikingVillagesTestLog";

		/// <summary>
		/// Seconds between checks on the vikings and raids, and between snapshots
		/// </summary>
		private const float TickInterval = 1f;
		private const float SnapshotInterval = 900f;

		/// <summary>
		/// Whether a run is going
		/// </summary>
		private static bool running;

		/// <summary>
		/// Whether the run has no village yet, and waits to take up the first one near the player, as it's set up
		/// </summary>
		private static bool runAdopting;

		/// <summary>
		/// The run's village as it's named in the file, the biome it's in, and the file the run is saved to
		/// </summary>
		private static string runVillage;
		private static string runBiome;
		private static string runFile;

		/// <summary>
		/// The plugin's BepInEx log, whose lines are copied to the run's file, and the file being written while a run is
		/// going
		/// </summary>
		private static BepInEx.Logging.ManualLogSource source;
		private static StreamWriter writer;
		private static readonly object writerLock = new object();

		/// <summary>
		/// When the run started, and the game's day and time it started at
		/// </summary>
		private static DateTime runStarted;
		private static string runStartedGame;

		/// <summary>
		/// How long a frame takes to count as a hitch, and the fewest seconds between hitch lines, with the hitches in
		/// between counted on the next one
		/// </summary>
		private const float HitchTime = 0.25f;
		private const float HitchQuiet = 10f;

		/// <summary>
		/// How far past a village's edge a creature's death still counts as near the village, in meters
		/// </summary>
		private const float KillReach = 20f;

		/// <summary>
		/// When the next check and the next snapshot are due, by Time.time
		/// </summary>
		private static float nextTick;
		private static float nextSnapshot;

		/// <summary>
		/// The frames since the last snapshot - how many, how long they took together, the longest, and how many hitched
		/// </summary>
		private static int frames;
		private static float frameTime;
		private static float worstFrame;
		private static int hitches;

		/// <summary>
		/// When the last hitch line was written, by Time.unscaledTime, and the hitches since that went unwritten
		/// </summary>
		private static float hitchLoggedAt = float.MinValue;
		private static int hitchesUnlogged;

		/// <summary>
		/// Each kind of exception seen (see ExceptionKey) and how many times it came up again since the last snapshot
		/// </summary>
		private static readonly Dictionary<string, int> exceptionRepeats = new Dictionary<string, int>();

		/// <summary>
		/// Whether an exception is being written, so a line the log itself sends back to Unity isn't taken up again
		/// </summary>
		private static bool writingException;

		/// <summary>
		/// What the vikings did and what happened to them, counted since the run started
		/// </summary>
		private static TestTotals totals = new TestTotals();

		/// <summary>
		/// The raids on, by the farm of the village they're on
		/// </summary>
		private static readonly Dictionary<Vector3, RaidWatch> raids = new Dictionary<Vector3, RaidWatch>();

		/// <summary>
		/// What's kept of a raid on a village while it's on, for the line when it ends
		/// </summary>
		private class RaidWatch
		{
			public float StartedAt;
			public int Vikings;
			public int VikingDeaths;
			public int Kills;
		}

		/// <summary>
		/// Whether a run is going, which is when the test log writes and counts
		/// </summary>
		public static bool On
		{
			get
			{
				return(running);
			}
		}

		/// <summary>
		/// Starts taking up Unity's exceptions, which are only written while a run is going
		/// </summary>
		/// <param name="log">The plugin's BepInEx log, whose lines are copied to a run's file</param>
		public static void Start(BepInEx.Logging.ManualLogSource log)
		{
			source = log;
			Application.logMessageReceived += OnUnityLog;
		}

		/// <summary>
		/// Ends the run as the game closes, and stops taking up Unity's exceptions
		/// </summary>
		public static void Stop()
		{
			EndRun("as the game closed");
			Application.logMessageReceived -= OnUnityLog;
		}

		/// <summary>
		/// Starts a run, with the totals from nothing, creating its file with a header and copying the plugin's lines to
		/// it from now on, then writing a first snapshot to start from. A run started with no village near takes up the
		/// first village set up near the player, so the village's generation and setup are in its file
		/// </summary>
		/// <param name="village">The village the player is in or near, or null when there's none yet</param>
		/// <param name="position">Where the player stands, for the biome when there's no village</param>
		/// <returns>The file the run is saved to</returns>
		public static string BeginRun(Village village, Vector3 position)
		{
			Vector3 at = ((village != null) ? village.Farm : position);
			Heightmap.Biome biome = ((WorldGenerator.instance != null) ? WorldGenerator.instance.GetBiome(at) : Heightmap.Biome.None);
			runAdopting = (village == null);
			runVillage = ((village != null) ? VillageLabel(village) : "no village yet, started at " + position.ToString("F0"));
			runBiome = ((biome != Heightmap.Biome.None) ? biome.ToString() : "Unknown");
			runStarted = DateTime.Now;
			runStartedGame = GameTime();
			string folder = Path.Combine(BepInEx.Paths.BepInExRootPath, FolderName);
			Directory.CreateDirectory(folder);
			runFile = FreeFile(folder, runBiome, File.Exists);
			lock (writerLock)
			{
				writer = new StreamWriter(runFile, false, new UTF8Encoding(false));
				writer.WriteLine("Viking Villages test run of " + runVillage + ", in the " + runBiome);
				writer.WriteLine("Started: " + runStarted.ToString("yyyy-MM-dd HH:mm:ss") + ", game " + runStartedGame);
				writer.WriteLine();
			}
			if (source != null)
			{
				source.LogEvent += OnLogLine;
			}
			running = true;
			totals = new TestTotals();
			frames = 0;
			frameTime = 0f;
			worstFrame = 0f;
			hitches = 0;
			hitchesUnlogged = 0;
			exceptionRepeats.Clear();
			raids.Clear();
			nextTick = 0f;
			nextSnapshot = Time.time + SnapshotInterval;
			foreach (VillageLife life in VillageLife.All)
			{
				if (life != null)
				{
					life.LogReset();
				}
			}
			Plugin.Log.LogInfo("Test run started in " + runVillage + ", in the " + runBiome + ", saving to " + runFile);
			Snapshot();
			return(runFile);
		}

		/// <summary>
		/// Ends the run, when one is going, writing its totals and closing its file
		/// </summary>
		/// <param name="how">How it ended, for the log</param>
		public static void EndRun(string how)
		{
			if (running)
			{
				WriteTotals("as the run ended " + how);
				Plugin.Log.LogInfo("Test run ended " + how + ", saved to " + runFile);
			}
			running = false;
			if (source != null)
			{
				source.LogEvent -= OnLogLine;
			}
			lock (writerLock)
			{
				if (writer == null)
				{
					return;
				}
				try
				{
					writer.WriteLine();
					writer.WriteLine("Ended: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ", game " + GameTime() + ", after " + (DateTime.Now - runStarted).TotalMinutes.ToString("F0") + " minutes");
					writer.Dispose();
				}
				catch (Exception exception)
				{
					source?.LogWarning("Couldn't finish the test run's file " + runFile + ": " + exception.Message);
				}
				writer = null;
			}
		}

		/// <summary>
		/// How far the game runs things from the middle of the player's zone, in zones, the way ZNetScene's
		/// PointInsideActiveArea judges it - out to this far in a square, and with a near simulation distance of 2 that
		/// isn't the classic one, no farther than RoundReach in a circle too
		/// </summary>
		private const float RoundReach = 1.75f;

		/// <summary>
		/// How far across the square the game runs things in reaches from the middle of the player's zone, in zones, the
		/// way ZNetScene's PointInsideActiveArea judges it
		/// </summary>
		/// <param name="nearDistance">The near simulation distance, in zones</param>
		/// <returns>The reach</returns>
		internal static float SquareReach(int nearDistance)
		{
			return((nearDistance == 1) ? 1f : 1.5f);
		}

		/// <summary>
		/// Measures how far a point is from another across a square, seen from above - the farther of the two ways along
		/// the ground, the way the game measures its simulated area
		/// </summary>
		/// <param name="middle">The square's middle</param>
		/// <param name="point">The point</param>
		/// <returns>The distance, in meters</returns>
		internal static float SquareDistance(Vector3 middle, Vector3 point)
		{
			return(Mathf.Max(Mathf.Abs(point.x - middle.x), Mathf.Abs(point.z - middle.z)));
		}

		/// <summary>
		/// Describes where a point stands against the area the game runs things in around this client's player - how far
		/// it is from the middle of the player's zone, against how far the area reaches, and where the player is
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The description</returns>
		public static string DescribeSimulatedArea(Vector3 point)
		{
			if (ZNet.instance == null || ZoneSystem.instance == null)
			{
				return("the simulated area isn't known");
			}
			Vector3 middle = ZoneSystem.GetZonePos(ZoneSystem.GetZone(ZNet.instance.GetReferencePosition()));
			SimulationDistance distance = ZNet.instance.GetSyncedSimulationDistance();
			float zoneSize = ZoneSystem.instance.m_zoneSize;
			string text = SquareDistance(middle, point).ToString("F0") + "m out on a square reaching " + (SquareReach(distance.NearSimulationDistance) * zoneSize).ToString("F0") + "m";
			if (distance.NearSimulationDistance == 2 && !distance.IsClassic)
			{
				text += " and " + Utils.DistanceXZ(middle, point).ToString("F0") + "m out on a circle reaching " + (RoundReach * zoneSize).ToString("F0") + "m";
			}
			Player player = Player.m_localPlayer;
			return(text + ", from " + middle.ToString("F0") + ", the middle of the zone " + ((player != null) ? "of the player at " + player.transform.position.ToString("F0") : "this client runs around"));
		}

		/// <summary>
		/// Names a village for the run's file and status
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>Its name and where its farm stands</returns>
		private static string VillageLabel(Village village)
		{
			string name = VillageNames.Get(village);
			return(((name.Length > 0) ? name + " at " : "the village at ") + village.Farm.ToString("F0"));
		}

		/// <summary>
		/// Takes up the first village near the player for a run started with none - or, on a dedicated server, which has
		/// no player, near where the server runs the world (see ServerVillageRun)
		/// </summary>
		private static void Adopt()
		{
			Player player = Player.m_localPlayer;
			Vector3 at = ((player != null) ? player.transform.position : ZNet.instance.GetReferencePosition());
			Village village = Village.Find(at);
			if (village == null)
			{
				return;
			}
			runAdopting = false;
			runVillage = VillageLabel(village);
			Plugin.Log.LogInfo("Test run took up " + runVillage + ", as it was set up near " + ((player != null) ? "the player" : "where the server runs the world"));
		}

		/// <summary>
		/// Copies a line the plugin wrote to the run's file, with its level, and the time in front of it when the line
		/// doesn't start with it already (see StampedLog)
		/// </summary>
		/// <param name="sender">The log</param>
		/// <param name="args">The line</param>
		private static void OnLogLine(object sender, BepInEx.Logging.LogEventArgs args)
		{
			string text = ((args.Data != null) ? args.Data.ToString() : "");
			lock (writerLock)
			{
				if (writer == null)
				{
					return;
				}
				writer.WriteLine("[" + args.Level + "] " + (IsStamped(text) ? text : Stamp() + text));
			}
		}

		/// <summary>
		/// Checks whether a line starts with the time, the way Stamp writes it, like "[14:02:37 "
		/// </summary>
		/// <param name="text">The line</param>
		/// <returns>True when it does</returns>
		internal static bool IsStamped(string text)
		{
			if (text == null || text.Length < 10 || text[0] != '[' || text[3] != ':' || text[6] != ':' || text[9] != ' ')
			{
				return(false);
			}
			foreach (int index in new int[] { 1, 2, 4, 5, 7, 8 })
			{
				if (!char.IsDigit(text[index]))
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Writes what's been copied to the run's file out to disk
		/// </summary>
		private static void Flush()
		{
			lock (writerLock)
			{
				try
				{
					writer?.Flush();
				}
				catch (Exception exception)
				{
					source?.LogWarning("Couldn't write the test run's file " + runFile + ": " + exception.Message);
				}
			}
		}

		/// <summary>
		/// Describes the run going, for the command
		/// </summary>
		/// <returns>The description</returns>
		public static string Status()
		{
			if (!running)
			{
				return("No test run is going - kri_vv_testlog start begins one in the village you're in, or for the next one set up near you");
			}
			return("Testing " + runVillage + ", in the " + runBiome + ", for " + (DateTime.Now - runStarted).TotalMinutes.ToString("F0") + " minutes, saving to " + runFile);
		}

		/// <summary>
		/// The file the run is saved to
		/// </summary>
		public static string RunFile
		{
			get
			{
				return(runFile);
			}
		}

		/// <summary>
		/// Finds the first name for a run's file in a biome that's free - the biome's name, then numbered from 2
		/// </summary>
		/// <param name="folder">The folder the files are in</param>
		/// <param name="biome">The biome's name</param>
		/// <param name="exists">Checks whether a file is there</param>
		/// <returns>The path, like "Meadows.log" or "Meadows-2.log" in the folder</returns>
		internal static string FreeFile(string folder, string biome, Func<string, bool> exists)
		{
			for (int number = 1; ; number++)
			{
				string path = Path.Combine(folder, biome + ((number > 1) ? "-" + number : "") + ".log");
				if (!exists(path))
				{
					return(path);
				}
			}
		}

		/// <summary>
		/// Writes the game's day and time of day
		/// </summary>
		/// <returns>Like "day 41 22:10", or "unknown" outside a world</returns>
		private static string GameTime()
		{
			if (EnvMan.instance == null || ZNet.instance == null)
			{
				return("unknown");
			}
			return("day " + EnvMan.instance.GetDay() + " " + Clock(EnvMan.instance.GetDayFraction()));
		}

		/// <summary>
		/// Times the frame, ends the run as the world is left, and every TickInterval takes up a village for a run that
		/// has none yet and checks on the raids and the vikings this client runs, and every SnapshotInterval writes a
		/// snapshot
		/// </summary>
		public static void Update()
		{
			if (!On)
			{
				return;
			}
			if (ZNet.instance == null || ZNetScene.instance == null || ZDOMan.instance == null)
			{
				EndRun("on leaving the world");
				return;
			}
			TimeFrame();
			if (Time.time < nextTick)
			{
				return;
			}
			nextTick = Time.time + TickInterval;
			if (runAdopting)
			{
				Adopt();
			}
			CheckRaids();
			foreach (VillageLife life in new List<VillageLife>(VillageLife.All))
			{
				if (life != null)
				{
					life.LogRunner();
					life.LogCheck();
				}
			}
			if (Time.time >= nextSnapshot)
			{
				nextSnapshot = Time.time + SnapshotInterval;
				Snapshot();
			}
		}

		/// <summary>
		/// Adds to a total, while the test log is on
		/// </summary>
		/// <param name="category">The category, like "Jobs started"</param>
		/// <param name="key">What's counted, like the kind of job</param>
		/// <param name="amount">How much to add</param>
		public static void Count(string category, string key, double amount = 1.0)
		{
			if (!On || amount == 0.0)
			{
				return;
			}
			totals.Add(category, key, amount);
		}

		/// <summary>
		/// Names an item for the log and the totals, as players see it
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>Its name</returns>
		public static string ItemName(ItemDrop.ItemData item)
		{
			if (item == null || item.m_shared == null)
			{
				return("nothing");
			}
			return((Localization.instance != null) ? Localization.instance.Localize(item.m_shared.m_name) : item.m_shared.m_name);
		}

		/// <summary>
		/// Writes the totals, a line for each category
		/// </summary>
		/// <param name="when">When they're written, for the first line</param>
		private static void WriteTotals(string when)
		{
			double minutes = (DateTime.Now - runStarted).TotalMinutes;
			Plugin.Log.LogInfo("Totals " + when + ", over the " + minutes.ToString("F0") + " minutes since " + runStarted.ToString("HH:mm:ss") + ":");
			foreach (string line in totals.Lines())
			{
				Plugin.Log.LogInfo("Totals - " + line);
			}
		}

		/// <summary>
		/// Gets what goes in front of each line - the time of day and the game's day and time, while a run is going
		/// </summary>
		/// <returns>The stamp, ending in a space, or "" while no run is going</returns>
		public static string Stamp()
		{
			if (!On)
			{
				return("");
			}
			return("[" + DateTime.Now.ToString("HH:mm:ss") + " " + GameTime() + "] ");
		}

		/// <summary>
		/// Writes the time of day the game's day has reached as hours and minutes
		/// </summary>
		/// <param name="fraction">How far through the day it is, 0 at midnight and 0.5 at noon</param>
		/// <returns>The time, like "06:30"</returns>
		internal static string Clock(float fraction)
		{
			int minutes = Mathf.FloorToInt(Mathf.Repeat(fraction, 1f) * 1440f) % 1440;
			return((minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00"));
		}

		/// <summary>
		/// Names a creature and where it is, for the log - a viking by its name
		/// </summary>
		/// <param name="character">The creature</param>
		/// <param name="format">How to write its position</param>
		/// <returns>The name and position</returns>
		public static string Name(Character character, string format = "F0")
		{
			if (character == null)
			{
				return("nothing");
			}
			return(Label(character) + " at " + character.transform.position.ToString(format));
		}

		/// <summary>
		/// Names a viking from its ZDO, for the log
		/// </summary>
		/// <param name="zdo">The viking's ZDO, or null</param>
		/// <returns>Its name, or "a viking" when it has none</returns>
		public static string Name(ZDO zdo)
		{
			string name = VikingNames.Get(zdo);
			return((name.Length > 0) ? name : "a viking");
		}

		/// <summary>
		/// Names a creature, for the log - a player by their name, a viking by its name, and anything else by its prefab
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>The name</returns>
		public static string Label(Character character)
		{
			if (character is Player player)
			{
				return("player " + player.GetPlayerName());
			}
			if (VikingCore.IsLivingViking(character))
			{
				string name = VikingNames.Get(character);
				return((name.Length > 0) ? name : "a nameless viking");
			}
			string prefab = Utils.GetPrefabName(character.gameObject);
			if (VikingCore.IsZombieViking(character))
			{
				string name = VikingNames.Get(character);
				return(prefab + ((name.Length > 0) ? " '" + name + "'" : ""));
			}
			return(prefab + (character.IsTamed() ? " (tamed)" : ""));
		}

		/// <summary>
		/// Describes where a hit came from and how much it dealt - who dealt it, or what kind of hit it was when no one did
		/// </summary>
		/// <param name="hit">The hit</param>
		/// <returns>The description</returns>
		public static string Source(HitData hit)
		{
			return(Dealer(hit) + " (" + hit.m_hitType + ", " + hit.GetTotalDamage().ToString("F1") + " damage)");
		}

		/// <summary>
		/// Names who dealt a hit - a player or viking by name, and a creature by its prefab
		/// </summary>
		/// <param name="hit">The hit</param>
		/// <returns>The name, or "no one" for a hit from no creature</returns>
		private static string Dealer(HitData hit)
		{
			Character attacker = hit.GetAttacker();
			if (attacker != null)
			{
				return(Label(attacker));
			}
			if (!hit.m_attacker.IsNone())
			{
				ZDO zdo = ZDOMan.instance.GetZDO(hit.m_attacker);
				GameObject prefab = ((zdo != null) ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null);
				return((prefab != null) ? prefab.name : "something no longer there");
			}
			return("no one");
		}

		/// <summary>
		/// Names where a hit came from for the totals - the kind of creature that dealt it, or the kind of hit when no
		/// creature did, like a fall or drowning
		/// </summary>
		/// <param name="hit">The hit</param>
		/// <returns>The name</returns>
		private static string SourceKey(HitData hit)
		{
			Character attacker = hit.GetAttacker();
			if (attacker != null)
			{
				if (attacker.IsPlayer())
				{
					return("players");
				}
				if (VikingCore.IsLivingViking(attacker))
				{
					return("vikings");
				}
				return(Utils.GetPrefabName(attacker.gameObject));
			}
			if (!hit.m_attacker.IsNone())
			{
				return(Dealer(hit));
			}
			return(hit.m_hitType.ToString());
		}

		/// <summary>
		/// Notes a hit a living viking took, for the hits listed when it dies
		/// </summary>
		/// <param name="character">The creature hit</param>
		/// <param name="hit">The hit, after its modifiers</param>
		/// <param name="before">Its health before the hit</param>
		public static void OnHit(Character character, HitData hit, float before)
		{
			if (!On || !VikingCore.IsLivingViking(character))
			{
				return;
			}
			VillageLife life = character.GetComponent<VillageLife>();
			if (life == null)
			{
				return;
			}
			float after = Mathf.Max(0f, character.GetHealth());
			Count("Damage taken by vikings", SourceKey(hit), before - after);
			life.NoteHit(Source(hit) + " at " + character.transform.position.ToString("F1") + ", " + before.ToString("F0") + " -> " + after.ToString("F0") + "/" + character.GetMaxHealth().ToString("F0"));
		}

		/// <summary>
		/// Writes a creature's death - a living viking's with why it died and how it was doing, and any other creature's
		/// in or near a village with what killed it - and counts it toward a raid on the village
		/// </summary>
		/// <param name="character">The creature dying</param>
		public static void OnDeath(Character character)
		{
			if (!On || character == null || character.m_nview == null || !character.m_nview.IsValid() || character.IsPlayer())
			{
				return;
			}
			HitData hit = character.m_lastHit;
			string cause = ((hit != null) ? "last hit by " + Source(hit) : "no hit noted");
			if (VikingCore.IsLivingViking(character))
			{
				VillageLife life = character.GetComponent<VillageLife>();
				string report = ((life != null) ? life.DeathReport() : "");
				Plugin.Log.LogWarning("Death: " + Name(character) + " died - " + cause + report);
				Count("Viking deaths by cause", (hit != null) ? SourceKey(hit) : "unknown");
				Count("Viking deaths by job", (life != null) ? life.LogJobName() : "no village life");
				if (VillageRespawn.FarmOf(character, out Vector3 farm) && raids.TryGetValue(farm, out RaidWatch raid))
				{
					raid.VikingDeaths++;
					Count("Raids", "viking deaths in them");
				}
				return;
			}
			Vector3 position = character.transform.position;
			Village village = Village.Find(position);
			if (village == null || village.DistanceOutside(position) > KillReach)
			{
				return;
			}
			string where = (village.Contains(position) ? " in" : " near");
			Plugin.Log.LogInfo("Kill: " + Name(character) + where + " the village at " + village.Farm.ToString("F0") + " - " + cause);
			Character killer = hit?.GetAttacker();
			bool byViking = (killer != null && VikingCore.IsLivingViking(killer));
			Count(byViking ? "Killed by vikings" : "Killed near villages by others", Utils.GetPrefabName(character.gameObject));
			if (raids.TryGetValue(village.Farm, out RaidWatch watch))
			{
				watch.Kills++;
				Count("Raids", "creatures killed in them");
			}
		}

		/// <summary>
		/// Writes loot that despawned before a viking put it away, with why they left it, and counts it
		/// </summary>
		/// <param name="left">The loot and why it was left (see VillageLife.LootLeftReport)</param>
		public static void OnDespawn(VillageLife.LootLeft left)
		{
			Plugin.Log.LogWarning("Despawned: " + left.Report);
			Count("Loot despawned, by why it was left", left.Reason, left.Stack);
			Count("Loot despawned, by item", left.Item, left.Stack);
		}

		/// <summary>
		/// Times the frame for the snapshot, and writes a hitch, at most once every HitchQuiet seconds
		/// </summary>
		private static void TimeFrame()
		{
			float frame = Time.unscaledDeltaTime;
			frames++;
			frameTime += frame;
			worstFrame = Mathf.Max(worstFrame, frame);
			// Loading screens hitch on their own
			if (frame < HitchTime || Player.m_localPlayer == null)
			{
				return;
			}
			hitches++;
			Count("Game", "hitches");
			if (Time.unscaledTime - hitchLoggedAt < HitchQuiet)
			{
				hitchesUnlogged++;
				return;
			}
			string more = ((hitchesUnlogged > 0) ? ", with " + hitchesUnlogged + " more since the last line" : "");
			Plugin.Log.LogWarning("Hitch: a frame took " + (frame * 1000f).ToString("F0") + "ms" + more + " - " + VillageLife.All.Count + " vikings and " + ((ZNetScene.instance != null) ? ZNetScene.instance.m_instances.Count : 0) + " objects loaded");
			hitchLoggedAt = Time.unscaledTime;
			hitchesUnlogged = 0;
		}

		/// <summary>
		/// Writes raids starting and ending on the loaded villages, and ends those on villages that unloaded
		/// </summary>
		private static void CheckRaids()
		{
			HashSet<Vector3> loaded = new HashSet<Vector3>();
			foreach (Village village in Village.Loaded())
			{
				Vector3 farm = village.Farm;
				loaded.Add(farm);
				bool on = VillageRaids.On(village);
				raids.TryGetValue(farm, out RaidWatch raid);
				if (on && raid == null)
				{
					int vikings = village.Vikings().Count;
					raids[farm] = new RaidWatch { StartedAt = Time.time, Vikings = vikings };
					Count("Raids", "started");
					Plugin.Log.LogWarning("Raid: started on the village at " + farm.ToString("F0") + ", with " + vikings + " vikings loaded - " + Flatten(VillageRaids.Describe(village)));
				}
				else if (!on && raid != null)
				{
					EndRaid(farm, raid, "ended", village.Vikings().Count.ToString());
				}
			}
			foreach (Vector3 farm in new List<Vector3>(raids.Keys))
			{
				if (!loaded.Contains(farm))
				{
					EndRaid(farm, raids[farm], "stopped being watched as the village unloaded", "unknown");
				}
			}
		}

		/// <summary>
		/// Writes a raid's end and stops watching it
		/// </summary>
		/// <param name="farm">The farm of the village it was on</param>
		/// <param name="raid">The raid</param>
		/// <param name="how">How it ended</param>
		/// <param name="vikings">How many vikings are loaded now</param>
		private static void EndRaid(Vector3 farm, RaidWatch raid, string how, string vikings)
		{
			raids.Remove(farm);
			Count("Minutes", "in raids", (Time.time - raid.StartedAt) / 60f);
			Plugin.Log.LogWarning("Raid: " + how + " on the village at " + farm.ToString("F0") + " after " + (Time.time - raid.StartedAt).ToString("F0") + "s - " + raid.VikingDeaths + " vikings died, " + raid.Kills + " creatures killed in or near it, " + vikings + " vikings loaded now, " + raid.Vikings + " at the start");
		}

		/// <summary>
		/// Writes a line on how the game runs - frames, hitches, objects and exceptions since the last one - then one for
		/// each loaded village and each viking this client has loaded, and then the totals, and writes the run's file out
		/// to disk
		/// </summary>
		private static void Snapshot()
		{
			float average = ((frames > 0) ? frameTime / frames : 0f);
			string fps = ((average > 0f) ? (1f / average).ToString("F0") : "?");
			Plugin.Log.LogInfo("Snapshot: " + fps + " fps on average, slowest frame " + (worstFrame * 1000f).ToString("F0") + "ms, " + hitches + " hitches - " + ZDOMan.instance.NrOfObjects() + " ZDOs, " + ZNetScene.instance.m_instances.Count + " objects loaded, " + ItemDrop.s_instances.Count + " items lying loaded, " + VillageLife.All.Count + " vikings loaded - " + PlayerPlace());
			frames = 0;
			frameTime = 0f;
			worstFrame = 0f;
			hitches = 0;
			foreach (KeyValuePair<string, int> entry in exceptionRepeats)
			{
				if (entry.Value > 0)
				{
					Plugin.Log.LogWarning("Snapshot: this exception came up " + entry.Value + " more times - " + entry.Key);
				}
			}
			foreach (string key in new List<string>(exceptionRepeats.Keys))
			{
				exceptionRepeats[key] = 0;
			}
			foreach (Village village in Village.Loaded())
			{
				Plugin.Log.LogInfo("Snapshot of the village at " + village.Farm.ToString("F0") + ": " + DescribeVillage(village));
			}
			foreach (VillageLife life in VillageLife.All)
			{
				if (life != null)
				{
					Plugin.Log.LogInfo("Snapshot of " + life.LogName() + ": " + life.LogSnapshot());
				}
			}
			foreach (string line in VillageTravel.DescribeQueue())
			{
				Plugin.Log.LogInfo("Snapshot of travel: " + line);
			}
			string neighbors = NeighborNews.Describe();
			if (neighbors.Length > 0)
			{
				Plugin.Log.LogInfo("Snapshot of neighbor news: " + neighbors);
			}
			WriteTotals("so far");
			Flush();
		}

		/// <summary>
		/// Describes where the player is for the snapshot, and how far that is from the middle of the village they're in
		/// or near
		/// </summary>
		/// <returns>The description</returns>
		private static string PlayerPlace()
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				return("no player");
			}
			Vector3 position = player.transform.position;
			Village village = Village.Find(position);
			string near = ((village != null) ? ", " + Utils.DistanceXZ(position, village.Farm).ToString("F0") + "m from the middle of " + VillageLabel(village) : "");
			return("the player at " + position.ToString("F0") + near);
		}

		/// <summary>
		/// Describes a village for the snapshot - its debug summary, the raid, the items lying around it and its pieces
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The description, on one line</returns>
		private static string DescribeVillage(Village village)
		{
			return(Flatten(VillageDebug.Describe(village) + VillageRaids.Describe(village) + VillageLife.DescribeLyingLoot(village) + PiecesIn(village)));
		}

		/// <summary>
		/// Counts the building pieces loaded in a village, to set beside the pieces it has noted (see VillageRecords)
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The line, ending in a line break</returns>
		private static string PiecesIn(Village village)
		{
			int count = 0;
			foreach (Piece piece in Piece.s_allPieces)
			{
				if (piece != null && village.Contains(piece.transform.position))
				{
					count++;
				}
			}
			return("Pieces loaded in it: " + count + "\n");
		}

		/// <summary>
		/// Turns a multi-line description into one line, without its rich text
		/// </summary>
		/// <param name="text">The description</param>
		/// <returns>Its lines, joined by " | "</returns>
		public static string Flatten(string text)
		{
			List<string> lines = new List<string>();
			foreach (string line in text.RemoveRichTextTags().Split('\n'))
			{
				string trimmed = line.Trim();
				if (trimmed.Length > 0)
				{
					lines.Add(trimmed);
				}
			}
			return(string.Join(" | ", lines));
		}

		/// <summary>
		/// Writes each kind of exception Unity logs the first time it comes up, and counts it after that for the snapshot
		/// </summary>
		/// <param name="condition">The exception's message</param>
		/// <param name="stackTrace">Where it was thrown</param>
		/// <param name="type">What kind of log message it is</param>
		private static void OnUnityLog(string condition, string stackTrace, LogType type)
		{
			if (type != LogType.Exception || writingException || !On)
			{
				return;
			}
			Count("Game", "exceptions");
			string key = ExceptionKey(condition, stackTrace);
			if (exceptionRepeats.TryGetValue(key, out int repeats))
			{
				exceptionRepeats[key] = repeats + 1;
				return;
			}
			exceptionRepeats[key] = 0;
			Count("Game", "kinds of exception");
			writingException = true;
			try
			{
				Plugin.Log.LogWarning("Exception, written once and counted in the snapshots after: " + condition + "\n" + (stackTrace ?? "").TrimEnd());
			}
			finally
			{
				writingException = false;
			}
		}

		/// <summary>
		/// Works out what tells one kind of exception from another - its message and the first place in its stack trace
		/// </summary>
		/// <param name="condition">The exception's message</param>
		/// <param name="stackTrace">Where it was thrown</param>
		/// <returns>The key</returns>
		internal static string ExceptionKey(string condition, string stackTrace)
		{
			string trace = (stackTrace ?? "").Trim();
			int end = trace.IndexOf('\n');
			string first = ((end < 0) ? trace : trace.Substring(0, end)).Trim();
			return((condition ?? "").Trim() + ((first.Length > 0) ? " @ " + first : ""));
		}
	}
}
