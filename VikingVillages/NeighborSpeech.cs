using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// What a viking says about the news from the villages near its own (see NeighborNews) - how word came, by rune
	/// stick, raven or from travelers, and what happened, told to a player who talks to it or in talks with the others.
	/// The lines are in DIALOG.md too
	/// </summary>
	/// <remarks>
	/// A line is the way word came, with "{village}" the village it came from - or "the next village over" when it has
	/// no name - followed by what happened, with "{who}" the viking it happened to and "{quest}" and "{item}" the quest
	/// come home and what it was for
	/// </remarks>
	internal static class NeighborSpeech
	{
		/// <summary>
		/// How likely a viking answers a player with news from the villages near it, and how likely a talk is about it
		/// </summary>
		public const float AnswerChance = 0.2f;
		public const float TalkChance = 0.15f;

		/// <summary>
		/// How likely word came by rune stick rather than raven, when the viking didn't bring it itself
		/// </summary>
		private const float RuneStickChance = 0.6f;

		/// <summary>
		/// What a village is called in a line when it has no name
		/// </summary>
		private const string Unnamed = "the next village over";

		/// <summary>
		/// How word came - carved on a rune stick, by raven, or brought by the viking itself, from the village
		/// </summary>
		internal static readonly string[] RuneStickLines = { "A rune stick came from {village}: ", "Someone carved word from {village} on a stick: ", "Got a rune stick from {village} today - " };
		internal static readonly string[] RavenLines = { "A raven brought word from {village}: ", "The ravens are full of news from {village}: ", "A raven landed on the roof with word from {village} - " };
		internal static readonly string[] TravelerLines = { "Before I left {village}, ", "Back in {village}, ", "When I came through {village}, " };

		/// <summary>
		/// What happened, by the kind of thing
		/// </summary>
		internal static readonly Dictionary<string, string[]> Bodies = new Dictionary<string, string[]>
		{
			{ NeighborEvent.Disbanded, new[] { "they've left their homes, and the fire's gone cold.", "everyone packed up and walked off.", "the village is no more - they've all gone their own ways." } },
			{ NeighborEvent.Started, new[] { "someone lit a new fire and started a village.", "a new village has taken root.", "there are folk living around a new bonfire." } },
			{ NeighborEvent.QuestBack, new[] { "their folk came home from {quest} with {item}.", "they're back from {quest}, arms full of {item}.", "{quest} went well for them - plenty of {item}." } },
			{ NeighborEvent.Death, new[] { "they lost {who}. Odin keep them.", "{who} fell. They're mourning.", "they buried {who}." } },
			{ NeighborEvent.Newcomer, new[] { "{who} has moved in.", "they've taken in {who}.", "a newcomer called {who} turned up." } }
		};

		/// <summary>
		/// How another viking answers news, by the kind of thing
		/// </summary>
		internal static readonly Dictionary<string, string[]> Replies = new Dictionary<string, string[]>
		{
			{ NeighborEvent.Disbanded, new[] { "Gone? Just like that?", "Where will they all go?", "Sad news. I liked that village." } },
			{ NeighborEvent.Started, new[] { "Good. More fires, fewer wolves.", "New neighbors! We should visit." } },
			{ NeighborEvent.QuestBack, new[] { "Lucky them.", "We should try that ourselves." } },
			{ NeighborEvent.Death, new[] { "Hard news.", "May they feast in Valhalla." } },
			{ NeighborEvent.Newcomer, new[] { "Hope they pull their weight.", "More hands for the work." } }
		};

		/// <summary>
		/// What a quest is called in a line, by the quest
		/// </summary>
		private static readonly Dictionary<QuestKind, string> QuestNames = new Dictionary<QuestKind, string>
		{
			{ QuestKind.MineCopper, "the copper mines" },
			{ QuestKind.MineTin, "digging for tin" },
			{ QuestKind.MineSilver, "the silver veins" },
			{ QuestKind.RaidCrypts, "the sunken crypts" },
			{ QuestKind.RaidBurialChambers, "the burial chambers" },
			{ QuestKind.HuntTrolls, "hunting trolls" },
			{ QuestKind.HuntGoblins, "raiding a goblin camp" },
			{ QuestKind.SellTreasure, "trading with Haldor" },
			{ QuestKind.BuyCirclet, "trading with Haldor" },
			{ QuestKind.KillEikthyr, "facing Eikthyr" },
			{ QuestKind.FindBees, "looking for bees" },
			{ QuestKind.ChopTrees, "a timber run" },
			{ QuestKind.Forage, "foraging" },
			{ QuestKind.Hunt, "a hunt" }
		};

		/// <summary>
		/// Picks a line telling a player news from a village near the viking's own
		/// </summary>
		/// <param name="happened">What happened</param>
		/// <param name="brought">Whether the viking brought the news itself, coming from there</param>
		/// <param name="itemName">Gets an item's name as a viking says it, from its prefab name</param>
		/// <returns>The line</returns>
		public static string Answer(NeighborEvent happened, bool brought, System.Func<string, string> itemName)
		{
			return(Line(happened, brought, UnityEngine.Random.value < RuneStickChance, itemName, Pick));
		}

		/// <summary>
		/// Picks one of a set of lines
		/// </summary>
		/// <param name="lines">The lines</param>
		/// <returns>The line</returns>
		private static string Pick(string[] lines)
		{
			return(VikingSpeech.Pick(lines, ""));
		}

		/// <summary>
		/// Picks a talk about news from a village near the viking's own - the news, and another viking's answer
		/// </summary>
		/// <param name="happened">What happened</param>
		/// <param name="brought">Whether the viking starting the talk brought the news itself</param>
		/// <param name="itemName">Gets an item's name as a viking says it, from its prefab name</param>
		/// <returns>The talk's lines</returns>
		public static string[] Talk(NeighborEvent happened, bool brought, System.Func<string, string> itemName)
		{
			string[] replies = (Replies.TryGetValue(happened.Kind, out string[] found) ? found : new[] { "Is that so?" });
			return(new[] { Line(happened, brought, UnityEngine.Random.value < RuneStickChance, itemName, Pick), Pick(replies) });
		}

		/// <summary>
		/// Puts a line of news together - how word came, then what happened
		/// </summary>
		/// <param name="happened">What happened</param>
		/// <param name="brought">Whether the viking brought the news itself</param>
		/// <param name="runeStick">Whether word came by rune stick rather than raven, when it didn't</param>
		/// <param name="itemName">Gets an item's name as a viking says it, from its prefab name</param>
		/// <param name="pick">Picks one of a set of lines</param>
		/// <returns>The line</returns>
		internal static string Line(NeighborEvent happened, bool brought, bool runeStick, System.Func<string, string> itemName, System.Func<string[], string> pick)
		{
			string[] ways = (brought ? TravelerLines : (runeStick ? RuneStickLines : RavenLines));
			string[] bodies = (Bodies.TryGetValue(happened.Kind, out string[] found) ? found : new[] { "something happened." });
			string village = ((happened.Village.Length > 0) ? happened.Village : Unnamed);
			string quest = "their quest";
			string item = "loot";
			if (happened.Kind == NeighborEvent.QuestBack)
			{
				string[] parts = happened.Detail.Split('|');
				if (parts.Length > 0 && System.Enum.TryParse(parts[0], out QuestKind kind) && QuestNames.TryGetValue(kind, out string named))
				{
					quest = named;
				}
				if (parts.Length > 1 && parts[1].Length > 0)
				{
					item = itemName(parts[1]);
				}
			}
			string body = pick(bodies).Replace("{who}", happened.Detail).Replace("{quest}", quest).Replace("{item}", item);
			return(pick(ways).Replace("{village}", village) + body);
		}
	}
}
