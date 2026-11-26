using System;
using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Notes while a console command runs, so a living viking it spawns out in the wild starts out lost (see
	/// VillageTravel.NoteSpawned) - the game's spawn command, and any other mod's that spawns through a console command
	/// </summary>
	[HarmonyPatch(typeof(Terminal.ConsoleCommand), nameof(Terminal.ConsoleCommand.RunAction))]
	public static class ConsoleCommandSpawnPatch
	{
		/// <summary>
		/// Notes that a command is running
		/// </summary>
		private static void Prefix()
		{
			VillageTravel.CommandsRunning++;
		}

		/// <summary>
		/// Notes that the command is done, whether or not it threw
		/// </summary>
		/// <param name="__exception">What it threw, or null</param>
		/// <returns>The exception, so it's thrown on as usual</returns>
		private static Exception Finalizer(Exception __exception)
		{
			VillageTravel.CommandsRunning = Math.Max(0, VillageTravel.CommandsRunning - 1);
			return(__exception);
		}
	}
}
