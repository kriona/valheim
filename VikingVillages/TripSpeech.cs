using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// What a viking says about its last trip for TripStory.TalkHours game hours after it gets there - the thing most
	/// worth telling about it (see TripStory.Notable), to a player who talks to it and in talks with the others. The lines
	/// are in DIALOG.md too
	/// </summary>
	/// <remarks>
	/// Lines are filled in at "{player}" with the player's name, "{to}" and "{from}" with the villages it went to and
	/// left, "{boat}" with the boat it sailed in and "{biome}" with the hardest biome it walked through. A line naming a
	/// village that has no name is left out
	/// </remarks>
	internal static class TripSpeech
	{
		/// <summary>
		/// How likely a viking answers a player about its last trip, and how likely a talk is about it
		/// </summary>
		public const float AnswerChance = 0.5f;
		public const float TalkChance = 0.4f;

		/// <summary>
		/// The tokens lines are filled in at
		/// </summary>
		private const string PlayerToken = "{player}";
		private const string ToToken = "{to}";
		private const string FromToken = "{from}";
		private const string BoatToken = "{boat}";
		private const string BiomeToken = "{biome}";

		/// <summary>
		/// What a viking tells a player about its last trip, by the thing most worth telling
		/// </summary>
		internal static readonly Dictionary<TripStory.Fact, string[]> Answers = new Dictionary<TripStory.Fact, string[]>
		{
			{ TripStory.Fact.Wrecked, new[] { "Our {boat} went down on the way, {player}. I swam until my arms burned.", "Lost the boat at sea! I'm still drying out.", "We were wrecked crossing the water. Lucky to be standing here, {player}." } },
			{ TripStory.Fact.TurnedHome, new[] { "We got all the way to {to} before we heard home was alive again. So we turned right around!", "Halfway across the world and back, {player}. But home is home.", "I've seen {to}, {player}. Nice enough. Not home, though." } },
			{ TripStory.Fact.VillageGone, new[] { "We reached {to} and found nobody. Just cold ashes.", "The village we set out for was gone, {player}. We had to try our luck elsewhere.", "Ever walk for days to find an empty village? I don't recommend it." } },
			{ TripStory.Fact.Rerouted, new[] { "The portal we meant to take was smashed. We had to go the long way round.", "Someone broke the portal on our road, {player}! Cost us half a day.", "Never trust a portal you didn't build yourself. Ours was gone when we got there." } },
			{ TripStory.Fact.Portal, new[] { "Stepped through a portal on the way. My stomach's still somewhere back there.", "Portals, {player}! Halfway across the world in a blink.", "I came through a portal to get here. Still seeing purple." } },
			{ TripStory.Fact.Sailed, new[] { "We built a {boat} and sailed the rest of the way.", "My sea legs are still wobbly from the {boat}, {player}.", "Nothing like a {boat} with the wind behind it. Got here in no time." } },
			{ TripStory.Fact.Biome, new[] { "We came through the {biome} to get here. Long road, {player}.", "Walked through the {biome} the whole way. My boots are done for.", "The {biome}, {player}! I didn't think we'd make it out." } },
			{ TripStory.Fact.Foraged, new[] { "Picked a few berries along the road. Want one, {player}?", "Found mushrooms on the way here - not bad ones either.", "Long road, but there was plenty to pick along it." } }
		};

		/// <summary>
		/// The talks of two vikings about the last trip of the one starting it, by the thing most worth telling
		/// </summary>
		internal static readonly Dictionary<TripStory.Fact, string[][]> Talks = new Dictionary<TripStory.Fact, string[][]>
		{
			{ TripStory.Fact.Wrecked, new[] { new[] { "Our {boat} sank under us on the way.", "And you swam the rest? Tough as an old boot." }, new[] { "I swallowed half the sea getting here.", "That explains the smell." } } },
			{ TripStory.Fact.TurnedHome, new[] { new[] { "We went all the way to {to} and back again.", "Glad you did. It wasn't the same here." }, new[] { "Couldn't stay in {to} once we heard.", "Home has a pull, doesn't it?" } } },
			{ TripStory.Fact.VillageGone, new[] { new[] { "The village we went to was empty.", "Empty? What happened to them?", "Nobody knows." }, new[] { "{to} was nothing but cold ashes.", "Grim. Glad you found us instead." } } },
			{ TripStory.Fact.Rerouted, new[] { new[] { "Someone broke the portal on our road.", "Trolls, probably. Or careless folk." }, new[] { "We had to walk around a broken portal.", "Walking never broke anyone." } } },
			{ TripStory.Fact.Portal, new[] { new[] { "I came here through a portal.", "Did it tickle?", "Like a thousand ants." }, new[] { "One step and I was half a world away.", "Magic. I'll stick to my feet." } } },
			{ TripStory.Fact.Sailed, new[] { new[] { "We built a {boat} to get here, you know.", "And you're still green from it." }, new[] { "Sailed in on a {boat}.", "Did you keep it?", "Broke it up on the beach." } } },
			{ TripStory.Fact.Biome, new[] { new[] { "We crossed the {biome} on the way.", "Rather you than me." }, new[] { "The {biome} nearly had us.", "But here you are." } } },
			{ TripStory.Fact.Foraged, new[] { new[] { "Picked berries all the way here.", "Save some for the rest of us." } } }
		};

		/// <summary>
		/// Picks what a viking tells a player about its last trip
		/// </summary>
		/// <param name="story">The trip</param>
		/// <param name="own">The viking's own biome, by VikingGear's order</param>
		/// <param name="player">The player's name</param>
		/// <param name="biomeName">Gets a biome's name as a viking says it, by VikingGear's order</param>
		/// <returns>The line, or null when there's nothing worth telling</returns>
		public static string Answer(TripStory story, int own, string player, System.Func<int, string> biomeName)
		{
			TripStory.Fact fact = story.Notable(own);
			if (fact == TripStory.Fact.None || !Answers.TryGetValue(fact, out string[] lines))
			{
				return(null);
			}
			List<string> fitting = Fitting(lines, story);
			return((fitting.Count > 0) ? Fill(VikingSpeech.Pick(fitting.ToArray(), ""), story, own, player, biomeName) : null);
		}

		/// <summary>
		/// Picks a talk about the last trip of the viking starting it
		/// </summary>
		/// <param name="story">The trip</param>
		/// <param name="own">The viking's own biome, by VikingGear's order</param>
		/// <param name="biomeName">Gets a biome's name as a viking says it, by VikingGear's order</param>
		/// <returns>The talk's lines, or null when there's nothing worth telling</returns>
		public static string[] Talk(TripStory story, int own, System.Func<int, string> biomeName)
		{
			TripStory.Fact fact = story.Notable(own);
			if (fact == TripStory.Fact.None || !Talks.TryGetValue(fact, out string[][] talks))
			{
				return(null);
			}
			List<string[]> fitting = new List<string[]>();
			foreach (string[] talk in talks)
			{
				if (Fitting(talk, story).Count == talk.Length)
				{
					fitting.Add(talk);
				}
			}
			if (fitting.Count == 0)
			{
				return(null);
			}
			string[] picked = fitting[UnityEngine.Random.Range(0, fitting.Count)];
			return(System.Array.ConvertAll(picked, (string line) => Fill(line, story, own, "", biomeName)));
		}

		/// <summary>
		/// Leaves out the lines naming a village the trip has no name for
		/// </summary>
		/// <param name="lines">The lines</param>
		/// <param name="story">The trip</param>
		/// <returns>The lines that fit</returns>
		internal static List<string> Fitting(string[] lines, TripStory story)
		{
			List<string> fitting = new List<string>();
			foreach (string line in lines)
			{
				if ((line.Contains(ToToken) && story.To.Length == 0) || (line.Contains(FromToken) && story.From.Length == 0))
				{
					continue;
				}
				fitting.Add(line);
			}
			return(fitting);
		}

		/// <summary>
		/// Fills a line in from a trip
		/// </summary>
		/// <param name="line">The line</param>
		/// <param name="story">The trip</param>
		/// <param name="own">The viking's own biome, by VikingGear's order</param>
		/// <param name="player">The player's name, or ""</param>
		/// <param name="biomeName">Gets a biome's name as a viking says it, by VikingGear's order</param>
		/// <returns>The line, filled in</returns>
		internal static string Fill(string line, TripStory story, int own, string player, System.Func<int, string> biomeName)
		{
			int hardest = story.HardestBiome(own);
			string biome = ((hardest >= 0) ? biomeName(hardest) : "wilds");
			return(line.Replace(PlayerToken, player).Replace(ToToken, story.To).Replace(FromToken, story.From).Replace(BoatToken, ((story.Boat.Length > 0) ? story.Boat : "boat")).Replace(BiomeToken, biome));
		}
	}
}
