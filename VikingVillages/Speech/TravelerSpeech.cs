using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a traveling viking says, apart from what a village's own vikings say - greeting players, answering them,
	/// talking with the village's vikings about its travels, trading its food and saying goodbye - and what the village's
	/// vikings say about it, to each other and to players, while it's there and for a while after (see Traveler)
	/// </summary>
	/// <remarks>
	/// Every line here is listed in the mod's DIALOG.md, which changes along with them. "{from}" is the village the
	/// traveler came from, "{to}" the one it goes to, "{fromfolk}" and "{tofolk}" a viking of each, "{home}" the biome it
	/// comes from, "{traveler}" its name, "{village}" the village it visits and "{player}" the player spoken to - a line
	/// with one of them that has nothing to fill it in is left out. "{name}" in a talk is the viking spoken to (see
	/// VikingSpeech.Address)
	/// </remarks>
	internal static class TravelerSpeech
	{
		/// <summary>
		/// The tokens a line is filled in at
		/// </summary>
		internal const string FromToken = "{from}";
		internal const string ToToken = "{to}";
		internal const string FromFolkToken = "{fromfolk}";
		internal const string ToFolkToken = "{tofolk}";
		internal const string HomeToken = "{home}";
		internal const string TravelerToken = "{traveler}";
		internal const string VillageToken = "{village}";
		internal const string PlayerToken = "{player}";

		/// <summary>
		/// How likely a talk two of the village's vikings have is about the traveler, and what a viking of the village says
		/// to a player talking to it
		/// </summary>
		internal const float VillageTalkChance = 0.4f;
		internal const float VillageAnswerChance = 0.3f;

		/// <summary>
		/// What a traveler says greeting a player it doesn't know yet, by day and at night
		/// </summary>
		internal static readonly string[] DayFirstGreetings =
		{
			"Hail, stranger! I'm only passing through.",
			"Well met! Don't mind me - I'm a traveler myself.",
			"Hail! A fellow wanderer, by the look of you.",
			"Hail, friend. I've come a long way to sit by {village}'s fire."
		};
		internal static readonly string[] NightFirstGreetings =
		{
			"Hail, stranger. Come sit by the fire - it's a long night.",
			"Who's there? Ah, another traveler. Sit, sit.",
			"Late to be wandering. I should know."
		};

		/// <summary>
		/// What a traveler says greeting a player it knows, by day and at night
		/// </summary>
		internal static readonly string[] DayGreetings =
		{
			"{player}! The road brings us together again.",
			"Hail, {player}! Still here, for now.",
			"Good to see you, {player}. Fair winds today."
		};
		internal static readonly string[] NightGreetings =
		{
			"Evening, {player}. Pull up a log.",
			"{player}! Come and warm yourself.",
			"A long night ahead, {player}. Sit with me a while."
		};

		/// <summary>
		/// What a traveler says when a player it knows talks to it, by day and at night
		/// </summary>
		internal static readonly string[] DayAnswers =
		{
			"I came from {from}, {player}. A long way, and wet most of it.",
			"I'm bound for {to} next, if the winds are kind.",
			"Ever been to {from}, {player}? Fine mead, finer folk.",
			"{fromfolk} in {from} sends greetings to anyone who'll listen.",
			"I promised {tofolk} I'd reach {to} before the next full moon.",
			"I'm from {home}, {player}. This land is gentle by comparison.",
			"The sea's been my road for weeks now, {player}.",
			"Nothing like a fair wind and a full sail, {player}.",
			"The sea between here and {from} nearly had me twice.",
			"I've seen serpents off the coast as long as a longship.",
			"The folk of {village} keep a good fire. I'll tell them so in {to}.",
			"Every harbor has its story, {player}. {from}'s was a good one."
		};
		internal static readonly string[] NightAnswers =
		{
			"No bed for a traveler, {player}. I'll sit by this fire till morning.",
			"I'll be on my way to {to} at first light.",
			"Do you know the stars, {player}? They brought me here from {from}.",
			"Quiet nights like this are rare at sea, {player}.",
			"A warm fire and a long night. I've had far worse."
		};

		/// <summary>
		/// The talks a traveler starts with one of the village's vikings, a line each in turn, the traveler first
		/// </summary>
		internal static readonly string[][] TravelerTalks =
		{
			new[] { "I came here from {from}, {name}.", "{from}? That's a long way.", "Long, and most of it by sea." },
			new[] { "Ever sailed out past the last island, {name}?", "Can't say I have.", "Don't. The sea goes on forever out there." },
			new[] { "I met {fromfolk} back in {from}.", "Is that so? I've never been that far." },
			new[] { "Where I come from, in {home}, this would be a warm day.", "Then I'm glad I don't live there." },
			new[] { "I'm bound for {to} next.", "Safe winds, then.", "And a dry boat, I hope." },
			new[] { "Your fire's a good one, {name}.", "We keep it well. Stay as long as you like.", "Just the night. The road calls." },
			new[] { "They say {tofolk} in {to} brews the best mead around.", "Bring some back this way, would you?" },
			new[] { "Have you seen the serpents at sea, {name}?", "Only from the shore.", "Keep it that way." },
			new[] { "Three days without sight of land, {name}.", "What did you eat?", "Fish. Then more fish." }
		};

		/// <summary>
		/// The talks one of the village's vikings starts with a traveler, a line each in turn, the village's viking first
		/// </summary>
		internal static readonly string[][] VillagerTalks =
		{
			new[] { "Where are you headed, {name}?", "To {to}, if the winds allow." },
			new[] { "What's it like in {home}, {name}?", "Harsh. You'd hate it.", "I believe you." },
			new[] { "How was the sailing from {from}, {name}?", "Rough. I was bailing water half the way." },
			new[] { "Any news from {from}, {name}?", "{fromfolk} sends greetings to all who'll hear them." },
			new[] { "You're welcome by our fire, {name}.", "Thank you. A warm fire is worth a lot on the road." },
			new[] { "Do you ever stay anywhere long, {name}?", "Not since I first saw the open sea." }
		};

		/// <summary>
		/// The talks two of the village's vikings have about the traveler while it's there, and once it has left
		/// </summary>
		internal static readonly string[][] VisitingTalks =
		{
			new[] { "Have you met {traveler}?", "The traveler? Came all the way from {from}." },
			new[] { "That armor of {traveler}'s is from {home}.", "Then they've seen worse than anything around here." },
			new[] { "{traveler} says they're bound for {to}.", "A long way. Rather them than me." },
			new[] { "A traveler by our fire tonight.", "Good. They'll bring news.", "And eat our food." },
			new[] { "Did you hear {traveler}'s tales of the sea?", "Serpents and storms. I'll stay ashore." }
		};
		internal static readonly string[][] VisitedTalks =
		{
			new[] { "I wonder if {traveler} made it to {to}.", "With that boat? Surely." },
			new[] { "It's been quiet since {traveler} left.", "I miss the stories." },
			new[] { "{traveler} left us some good food.", "Food from {home}! Fancy." }
		};

		/// <summary>
		/// What one of the village's vikings says to a player talking to it about the traveler while it's there, and once
		/// it has left
		/// </summary>
		internal static readonly string[] VisitingAnswers =
		{
			"We have a guest, {player} - {traveler}, from {from}.",
			"{traveler} is passing through on the way to {to}.",
			"Have you met {traveler}, {player}? They've come all the way from {home}."
		};
		internal static readonly string[] VisitedAnswers =
		{
			"{traveler} left for {to}, {player}. Good company while it lasted.",
			"We still talk of {traveler}'s tales of the sea, {player}."
		};

		/// <summary>
		/// What a traveler says as it trades its food at a village chest, and as it sets off
		/// </summary>
		internal static readonly string[] TradeLines =
		{
			"A little of yours for a little of mine. Fair trade.",
			"I'll leave you some food from {home}. Fair's fair.",
			"A bite for the road - and something better in its place."
		};
		internal static readonly string[] FarewellLines =
		{
			"Thank you for the fire. On to {to}!",
			"Farewell, {village}! Fair winds to you all.",
			"Time I was on my way. {to} won't wait.",
			"Safe travels to me, and safe homes to you."
		};

		/// <summary>
		/// The tokens a line can have, for finding those it holds
		/// </summary>
		private static readonly Regex Tokens = new Regex("\\{(?:from|to|fromfolk|tofolk|home|traveler|village|player)\\}");

		/// <summary>
		/// Gathers what a traveler's lines are filled in with
		/// </summary>
		/// <param name="player">The player's name, or ""</param>
		/// <param name="village">The village's name, or ""</param>
		/// <param name="from">The village the traveler came from, or ""</param>
		/// <param name="to">The village it goes to, or ""</param>
		/// <param name="fromFolk">A viking of the village it came from, or ""</param>
		/// <param name="toFolk">A viking of the village it goes to, or ""</param>
		/// <param name="home">The biome it comes from, as a viking says it, or ""</param>
		/// <param name="traveler">Its name, or ""</param>
		/// <returns>The values, by token</returns>
		internal static Dictionary<string, string> Values(string player, string village, string from, string to, string fromFolk, string toFolk, string home, string traveler)
		{
			return(new Dictionary<string, string>
			{
				{ PlayerToken, player ?? "" },
				{ VillageToken, village ?? "" },
				{ FromToken, from ?? "" },
				{ ToToken, to ?? "" },
				{ FromFolkToken, fromFolk ?? "" },
				{ ToFolkToken, toFolk ?? "" },
				{ HomeToken, home ?? "" },
				{ TravelerToken, traveler ?? "" }
			});
		}

		/// <summary>
		/// Checks whether every token a line holds has something to fill it in
		/// </summary>
		/// <param name="line">The line</param>
		/// <param name="values">The values, by token</param>
		/// <returns>True when it does</returns>
		internal static bool Fits(string line, Dictionary<string, string> values)
		{
			foreach (Match token in Tokens.Matches(line))
			{
				if (!values.TryGetValue(token.Value, out string value) || value.Length == 0)
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Fills in a line's tokens in one pass, so a name that reads like a token is said as it was given
		/// </summary>
		/// <param name="line">The line</param>
		/// <param name="values">The values, by token</param>
		/// <returns>The line</returns>
		internal static string Fill(string line, Dictionary<string, string> values)
		{
			return(Tokens.Replace(line, (Match token) => (values.TryGetValue(token.Value, out string value) ? value : token.Value)));
		}

		/// <summary>
		/// Picks one of the lines that fit at random and fills it in
		/// </summary>
		/// <param name="lines">The lines</param>
		/// <param name="values">The values, by token</param>
		/// <returns>The line, or null when none fits</returns>
		internal static string Pick(string[] lines, Dictionary<string, string> values)
		{
			List<string> fitting = FittingLines(lines, values);
			return((fitting.Count > 0) ? fitting[Random.Range(0, fitting.Count)] : null);
		}

		/// <summary>
		/// Lists the lines that fit, filled in
		/// </summary>
		/// <param name="lines">The lines</param>
		/// <param name="values">The values, by token</param>
		/// <returns>The lines that fit</returns>
		internal static List<string> FittingLines(string[] lines, Dictionary<string, string> values)
		{
			List<string> fitting = new List<string>(lines).FindAll((string line) => Fits(line, values));
			return(fitting.ConvertAll((string line) => Fill(line, values)));
		}

		/// <summary>
		/// Lists the talks every line of which fits, filled in
		/// </summary>
		/// <param name="talks">The talks</param>
		/// <param name="values">The values, by token</param>
		/// <returns>The talks that fit</returns>
		internal static List<string[]> Fitting(string[][] talks, Dictionary<string, string> values)
		{
			List<string[]> fitting = new List<string[]>();
			foreach (string[] talk in talks)
			{
				if (System.Array.TrueForAll(talk, (string line) => Fits(line, values)))
				{
					fitting.Add(System.Array.ConvertAll(talk, (string line) => Fill(line, values)));
				}
			}
			return(fitting);
		}

		/// <summary>
		/// Picks one of the talks that fit at random, filled in
		/// </summary>
		/// <param name="talks">The talks</param>
		/// <param name="values">The values, by token</param>
		/// <returns>The talk, or null when none fits</returns>
		internal static string[] PickTalk(string[][] talks, Dictionary<string, string> values)
		{
			List<string[]> fitting = Fitting(talks, values);
			return((fitting.Count > 0) ? fitting[Random.Range(0, fitting.Count)] : null);
		}

		/// <summary>
		/// Picks a traveler's greeting for a player - by name for one it knows, and as a stranger otherwise, giving its own
		/// name (see VikingSpeech.Introduce)
		/// </summary>
		/// <param name="night">Whether it's night</param>
		/// <param name="known">Whether it knows the player</param>
		/// <param name="self">Its own name, or ""</param>
		/// <param name="values">The values, by token</param>
		/// <returns>The line</returns>
		internal static string Greeting(bool night, bool known, string self, Dictionary<string, string> values)
		{
			if (known)
			{
				return(Pick(night ? NightGreetings : DayGreetings, values) ?? "");
			}
			return(VikingSpeech.Introduce(Pick(night ? NightFirstGreetings : DayFirstGreetings, values) ?? "", self));
		}
	}
}
