using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_event console command, for testing villages' events - saying what the village the player is in has
	/// today, or starting one of its events at once, or setting it to have none today
	/// </summary>
	/// <remarks>
	/// Only the client that runs a village keeps its roll, so only that client can see or set it
	/// </remarks>
	internal static class EventCommand
	{
		/// <summary>
		/// The command's name, and the word that sets a village to have no event today
		/// </summary>
		private const string Command = "kri_vv_event";
		private const string NoneWord = "none";

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			List<string> options = VillageEvents.All.ConvertAll((VillageEvent happening) => happening.Id);
			options.Add(NoneWord);
			new Terminal.ConsoleCommand(Command, "[" + string.Join("|", options) + "] - says what the village you're in has for its event today, or starts an event there at once, or sets it to have none today", Run, isCheat: true, optionsFetcher: () => options);
		}

		/// <summary>
		/// Runs the command
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
			if (!village.IsOwner())
			{
				args.Context.AddString("Another client runs " + VillageNames.Get(village) + ", so only it knows the village's event");
				return;
			}
			if (args.Length < 2)
			{
				args.Context.AddString(VillageNames.Get(village) + " - " + VillageEvents.Describe(village).TrimEnd('\n'));
				return;
			}
			string word = args[1];
			if (string.Equals(word, NoneWord, System.StringComparison.OrdinalIgnoreCase))
			{
				VillageEvents.Force(village, null);
				args.Context.AddString(VillageNames.Get(village) + " has no event today");
				return;
			}
			VillageEvent happening = VillageEvents.ById(word);
			if (happening == null)
			{
				args.Context.AddString("No event called " + word + " - the events are " + string.Join(", ", VillageEvents.All.ConvertAll((VillageEvent each) => each.Id)));
				return;
			}
			VillageEvents.Force(village, happening);
			args.Context.AddString(happening.Name + " starts at " + VillageNames.Get(village) + " as soon as it can");
		}
	}
}
