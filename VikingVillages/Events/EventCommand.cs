using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_event console command, for testing villages' events - saying what the village the player is in has
	/// today, or starting one of its events at once, a traveler from a biome given or picked at random, always bringing
	/// its biome's animal when asked, or setting it to have none today
	/// </summary>
	/// <remarks>
	/// Only the client that runs a village keeps its roll, so only that client can see or set it
	/// </remarks>
	internal static class EventCommand
	{
		/// <summary>
		/// The command's name, the word that sets a village to have no event today, and the word that has a traveler
		/// always bring its biome's animal
		/// </summary>
		private const string Command = "kri_vv_event";
		private const string NoneWord = "none";
		private const string PetWord = "pet";

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			List<string> options = VillageEvents.All.ConvertAll((VillageEvent happening) => happening.Id);
			options.Add(NoneWord);
			new Terminal.ConsoleCommand(Command, "[" + string.Join("|", options) + "] [biome] [pet] - says what the village you're in has for its event today, or starts an event there at once, or sets it to have none today - a traveler from the biome given, or one picked at random, always bringing their biome's animal with pet", Run, isCheat: true, optionsFetcher: () => options);
		}

		/// <summary>
		/// Finds a biome by the name typed for it - the game's own, like Mountain, or as a viking says it, like Mountains
		/// or Deep North, in any case and with or without spaces
		/// </summary>
		/// <param name="typed">The name typed</param>
		/// <returns>The biome's index in VikingGear's tables, or -1 when no biome goes by it</returns>
		private static int BiomeNamed(string typed)
		{
			string wanted = typed.Replace(" ", "").ToLowerInvariant();
			for (int i = 0; i < Kriona.Vikings.VikingGear.Biomes.Length; i++)
			{
				string own = Kriona.Vikings.VikingGear.Biomes[i].Biome.ToString().ToLowerInvariant();
				string said = Traveler.HomeName(i).Replace("the ", "").Replace(" ", "").ToLowerInvariant();
				if (wanted == own || wanted == said)
				{
					return(i);
				}
			}
			return(-1);
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
			System.Func<Village, bool> starter = null;
			string from = "";
			if (happening == TravelerEvent.Event && args.Length >= 3)
			{
				// A last word of "pet" has the traveler always bring its biome's animal
				int words = args.Args.Length - 2;
				bool pet = string.Equals(args.Args[args.Args.Length - 1], PetWord, System.StringComparison.OrdinalIgnoreCase);
				if (pet)
				{
					words--;
				}
				int origin = -1;
				if (words > 0)
				{
					string typed = string.Join(" ", args.Args, 2, words);
					origin = BiomeNamed(typed);
					if (origin < 0)
					{
						args.Context.AddString("No biome called " + typed + " - try Mountains, Plains, Mistlands, Ashlands or DeepNorth");
						return;
					}
					if (pet && TravelerPets.KindFor(origin) == null)
					{
						args.Context.AddString("Travelers from " + Traveler.HomeName(origin) + " bring no animal - the Mountains, Plains, Mistlands, Ashlands and Deep North have one");
						return;
					}
					from = " from " + Traveler.HomeName(origin);
				}
				starter = (Village at) => TravelerEvent.Send(at, origin, pet);
				if (pet)
				{
					from += ((origin >= 0) ? " with their animal" : " with their biome's animal, if it has one");
				}
			}
			VillageEvents.Force(village, happening, starter);
			args.Context.AddString(happening.Name + from + " starts at " + VillageNames.Get(village) + " as soon as it can");
		}
	}
}
