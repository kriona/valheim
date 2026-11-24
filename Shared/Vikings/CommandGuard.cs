using System.Collections.Generic;
using HarmonyLib;

namespace Kriona.Vikings
{
	/// <summary>
	/// Keeps a player from using the mod's console commands, spawning what the mod adds or starting its raids in a world
	/// whose server doesn't have the mod
	/// </summary>
	/// <remarks>
	/// The mod adds its prefabs and raids on every client as the game loads, before the server answers (see
	/// ServerCheck), so without this the game's spawn command would make them in a world where no one else has them.
	/// Each mod names what it adds (see AddOwned). In the main menu, with no world, nothing is held back
	/// </remarks>
	internal static partial class CommandGuard
	{
		/// <summary>
		/// What a console command would do with something the mod adds
		/// </summary>
		internal enum Use
		{
			None,
			Command,
			Spawn,
			Raid
		}

		/// <summary>
		/// The game's console commands that spawn a prefab and that start a raid, each by the name in its first argument
		/// </summary>
		private const string SpawnCommand = "spawn";
		private const string EventCommand = "event";

		/// <summary>
		/// Names what this mod adds - the start of its console commands' names, the prefabs and items it adds to the
		/// world, and its raids
		/// </summary>
		/// <param name="commandPrefixes">The starts of its commands' names, added to</param>
		/// <param name="prefabs">Its prefabs and items, added to</param>
		/// <param name="raids">Its raids, added to</param>
		static partial void AddOwned(List<string> commandPrefixes, List<string> prefabs, List<string> raids);

		/// <summary>
		/// Checks whether a console command is held back, as the server doesn't have the mod - saying why when it is
		/// </summary>
		/// <param name="command">The command's name</param>
		/// <param name="argument">Its first argument, or null</param>
		/// <param name="why">Set to what the player is told, or null</param>
		/// <returns>True when it's held back</returns>
		public static bool Refuses(string command, string argument, out string why)
		{
			why = null;
			if (ZNet.instance == null || ServerCheck.Active)
			{
				return(false);
			}
			List<string> commandPrefixes = new List<string>();
			List<string> prefabs = new List<string>();
			List<string> raids = new List<string>();
			string own = VikingCore.OwnPrefabName();
			if (own.Length > 0)
			{
				prefabs.Add(own);
			}
			AddOwned(commandPrefixes, prefabs, raids);
			Use use = Classify(command, argument, commandPrefixes, prefabs, raids);
			why = Refusal(use, VikingMod.Name);
			return(use != Use.None);
		}

		/// <summary>
		/// Works out whether a console command would use something the mod adds - one of its own commands, the game's
		/// spawn command naming one of its prefabs, or a wildcard (ending in "*") taking one in, or the game's event command
		/// naming one of its raids
		/// </summary>
		/// <param name="command">The command's name</param>
		/// <param name="argument">Its first argument, or null</param>
		/// <param name="commandPrefixes">The starts of the mod's commands' names</param>
		/// <param name="prefabs">The mod's prefabs and items</param>
		/// <param name="raids">The mod's raids</param>
		/// <returns>What it would use, or None</returns>
		internal static Use Classify(string command, string argument, List<string> commandPrefixes, List<string> prefabs, List<string> raids)
		{
			if (string.IsNullOrEmpty(command))
			{
				return(Use.None);
			}
			if (commandPrefixes.Exists((string prefix) => command.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)))
			{
				return(Use.Command);
			}
			if (string.IsNullOrEmpty(argument))
			{
				return(Use.None);
			}
			if (string.Equals(command, SpawnCommand, System.StringComparison.OrdinalIgnoreCase))
			{
				// The game's wildcard spawns every prefab whose name holds what comes before the star
				if (argument.Length >= 2 && argument[argument.Length - 1] == '*')
				{
					string part = argument.Substring(0, argument.Length - 1);
					return(prefabs.Exists((string prefab) => prefab.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0) ? Use.Spawn : Use.None);
				}
				return(prefabs.Exists((string prefab) => string.Equals(prefab, argument, System.StringComparison.OrdinalIgnoreCase)) ? Use.Spawn : Use.None);
			}
			if (string.Equals(command, EventCommand, System.StringComparison.OrdinalIgnoreCase) && raids.Exists((string raid) => string.Equals(raid, argument, System.StringComparison.OrdinalIgnoreCase)))
			{
				return(Use.Raid);
			}
			return(Use.None);
		}

		/// <summary>
		/// Says why a console command is held back
		/// </summary>
		/// <param name="use">What it would have used</param>
		/// <param name="mod">The mod's name</param>
		/// <returns>The message, or null when it isn't held back</returns>
		internal static string Refusal(Use use, string mod)
		{
			switch (use)
			{
				case Use.Command:
					return(mod + " isn't on this server, so its commands don't work here");
				case Use.Spawn:
					return(mod + " isn't on this server, so what it adds can't be spawned here");
				case Use.Raid:
					return(mod + " isn't on this server, so its raid can't be started here");
			}
			return(null);
		}
	}

	/// <summary>
	/// Holds back a console command that would use something the mod adds in a world whose server doesn't have it,
	/// telling the player why (see CommandGuard)
	/// </summary>
	[HarmonyPatch(typeof(Terminal.ConsoleCommand), nameof(Terminal.ConsoleCommand.RunAction))]
	internal static class CommandGuardPatch
	{
		/// <summary>
		/// Skips the command when it's held back
		/// </summary>
		/// <param name="__instance">The command</param>
		/// <param name="args">What was typed</param>
		/// <returns>False to skip the command</returns>
		private static bool Prefix(Terminal.ConsoleCommand __instance, Terminal.ConsoleEventArgs args)
		{
			string argument = ((args != null && args.Length >= 2) ? args[1] : null);
			if (!CommandGuard.Refuses(__instance.Command, argument, out string why))
			{
				return(true);
			}
			if (args != null && args.Context != null)
			{
				args.Context.AddString(why);
			}
			return(false);
		}
	}
}
