using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a viking says now and then when its village goes by its name - bewildered by it most of the time, and now
	/// and then bragging that the village is named after it - to a player it knows and in the talks it starts with
	/// the others. The lines are in DIALOG.md too
	/// </summary>
	internal static class VillageNamesakeSpeech
	{
		/// <summary>
		/// How likely a viking whose village goes by its name talks about it when a player it knows talks to it, and in
		/// a talk it starts
		/// </summary>
		private const float ChatChance = 0.2f;
		private const float TalkChance = 0.2f;

		/// <summary>
		/// The tokens the lines to a player are filled in at - the player's name and the village's
		/// </summary>
		internal const string PlayerToken = "{player}";
		internal const string VillageToken = "{village}";

		/// <summary>
		/// What the viking says to a player, bewildered that the village goes by its name
		/// </summary>
		internal static readonly string[] BewilderedLines =
		{
			"Everyone keeps calling this place {village}, {player}. That's my name. Nobody asked me.",
			"Someone shouts \"{village}!\" and I turn around. Every time, {player}. It's never for me.",
			"Why would anyone name a village {village}, {player}? I'm {village}!",
			"A rune stick came for {village}, {player}. I read it. It was about the fence.",
			"Travelers ask me the way to {village}, {player}. I never know whether to point at the gate or at myself.",
			"Was {village} named after me, {player}, or was I named after {village}? I can't keep it straight."
		};

		/// <summary>
		/// What the viking says to a player, bragging that the village is named after it
		/// </summary>
		internal static readonly string[] BraggingLines =
		{
			"They named the village after me, {player}. {village}. Has a ring to it, doesn't it?",
			"Welcome to {village}, {player}. My village. Well, it has my name on it, so it might as well be.",
			"When the skalds sing of {village}, {player}, they'll be singing of me.",
			"A whole village called {village}, {player}. I'd say that makes me its chief. The others don't agree."
		};

		/// <summary>
		/// The talks the viking starts with another of its village about the village going by its name, filled in by
		/// VikingSpeech.Address
		/// </summary>
		internal static readonly string[][] Talks =
		{
			new[] { "Do you think they named {village} after me, {name}?", "I think someone just liked the name.", "My name." },
			new[] { "Every time someone says \"{village}\", I turn around.", "We've noticed." },
			new[] { "You could bow when you pass me, {name}. You do live in {village}.", "I'm not bowing to you.", "Worth asking." },
			new[] { "{village} has a fine ring to it, {name}.", "You only say that because it's your name.", "That's why it has a fine ring to it." },
			new[] { "A traveler asked if I was {village} of {village}.", "And were you?", "I suppose I am." },
			new[] { "Shouldn't the one the village is named after get the biggest house?", "Shouldn't they do the most work, then?", "Let's not get carried away." }
		};

		/// <summary>
		/// Checks whether a village goes by a viking's name - its whole name or its first, whatever its case, taking an
		/// "ø" as an "o"
		/// </summary>
		/// <param name="viking">The viking's name, with any byname after it, like "Ragna the Bold", or ""</param>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>True when it does</returns>
		internal static bool IsNamesake(string viking, string village)
		{
			string name = Plain(viking);
			string place = Plain(village);
			if (name.Length == 0 || place.Length == 0)
			{
				return(false);
			}
			return(string.Equals(name, place, System.StringComparison.OrdinalIgnoreCase) || string.Equals(VikingLoreSpeech.FirstName(name), place, System.StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Picks what a viking says about its village going by its name when a player it knows talks to it, now and then
		/// </summary>
		/// <param name="viking">The viking's name, with any byname after it</param>
		/// <param name="village">The village's name</param>
		/// <param name="player">The player's name</param>
		/// <returns>The line, or null when it says something else</returns>
		public static string Chat(string viking, string village, string player)
		{
			if (!IsNamesake(viking, village) || Random.value >= ChatChance)
			{
				return(null);
			}
			return(For(village, player, (int count) => Random.Range(0, count)));
		}

		/// <summary>
		/// Picks a line to a player about the village going by the viking's name, bewildered or bragging, filling in the
		/// player's name and the village's in one pass, so a name that reads like a token is said as it was given
		/// </summary>
		/// <param name="village">The village's name</param>
		/// <param name="player">The player's name</param>
		/// <param name="pick">Picks an index below the count it's given</param>
		/// <returns>The line</returns>
		internal static string For(string village, string player, System.Func<int, int> pick)
		{
			int index = pick(BewilderedLines.Length + BraggingLines.Length);
			string line = ((index < BewilderedLines.Length) ? BewilderedLines[index] : BraggingLines[index - BewilderedLines.Length]);
			Dictionary<string, string> values = new Dictionary<string, string>
			{
				{ PlayerToken, player },
				{ VillageToken, village }
			};
			return(Regex.Replace(line, "\\{(?:player|village)\\}", (Match token) => values[token.Value]));
		}

		/// <summary>
		/// Picks a talk a viking starts about its village going by its name, now and then
		/// </summary>
		/// <param name="viking">The viking's name, with any byname after it</param>
		/// <param name="village">The village's name</param>
		/// <returns>The talk's lines, to be filled in by VikingSpeech.Address, or null for a talk about anything else</returns>
		public static string[] Talk(string viking, string village)
		{
			if (!IsNamesake(viking, village) || Random.value >= TalkChance)
			{
				return(null);
			}
			return(Talks[Random.Range(0, Talks.Length)]);
		}

		/// <summary>
		/// Gets a name the way it's compared - trimmed, with an "ø" as an "o"
		/// </summary>
		/// <param name="name">The name, or null</param>
		/// <returns>The name to compare</returns>
		private static string Plain(string name)
		{
			return((name ?? "").Trim().Replace('ø', 'o').Replace('Ø', 'O'));
		}
	}
}
