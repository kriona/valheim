using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What the vikings say about their village's quests (see VillageQuests) - as one sets out, while it's out, about
	/// it for a day once they're back, and coming home to a village whose vikings all died while they were gone. Hunting,
	/// foraging and cutting trees are too ordinary to talk about (see QuestRules.WorthTalkingAbout). The lines are in
	/// DIALOG.md too
	/// </summary>
	internal static class QuestSpeech
	{
		/// <summary>
		/// The tokens lines are filled in at - roughly when a quest is due back, like "tomorrow" (see TravelTime.Phrase),
		/// the names of the vikings out on it, the names of the others who went with the one speaking, the player's name,
		/// and the name of one of the village's dead
		/// </summary>
		public const string WhenToken = "{when}";
		public const string AwayToken = "{away}";
		public const string WithToken = "{with}";
		public const string PlayerToken = "{player}";
		public const string FallenToken = "{fallen}";

		/// <summary>
		/// How likely a talk between two vikings is about a quest out, by one at home, and about a quest just back, by one
		/// who went on it, and how likely a viking answers a player about them
		/// </summary>
		public const float AwayTalkChance = 0.3f;
		public const float BackTalkChance = 0.5f;
		public const float AwayAnswerChance = 0.4f;
		public const float BackAnswerChance = 0.6f;

		/// <summary>
		/// How long the vikings who went on a quest talk about it once they're back, in game days
		/// </summary>
		public const float BackDays = 1f;

		/// <summary>
		/// What one of the vikings setting out on a quest says as they go - with roughly when they'll be back
		/// </summary>
		internal static readonly string[] DepartureLines =
		{
			"We're off! Expect us back {when}.",
			"Keep the fire going - we'll be back {when}.",
			"Don't wait up. We should be home {when}.",
			"Right, we're away. Back {when}, if the gods allow.",
			"Off we go. Look for us {when}."
		};

		/// <summary>
		/// The talks of two vikings at home about a quest that's out, by the quest - with the names of those out on it -
		/// and the talks about any quest
		/// </summary>
		internal static readonly Dictionary<QuestKind, string[][]> AwayTalks = new Dictionary<QuestKind, string[][]>
		{
			{ QuestKind.MineCopper, new[] { new[] { "Think {away} found the copper yet?", "If anyone can find it, it's them." }, new[] { "Quiet without {away} banging on rocks.", "Enjoy it. There'll be ore to smelt soon enough." } } },
			{ QuestKind.MineTin, new[] { new[] { "{away} went down to the shore after tin.", "Tin's easy to find. It's the trolls by the water that worry me." }, new[] { "Bronze needs tin, {name}.", "And {away} went to fetch it." } } },
			{ QuestKind.MineSilver, new[] { new[] { "{away} went up the mountain for silver.", "Wolves and drakes up there. I hope they took a cloak." }, new[] { "Silver's buried deep, {name}.", "Then let's hope {away} dug deep." } } },
			{ QuestKind.RaidCrypts, new[] { new[] { "{away} went into the sunken crypts.", "The swamp swallows the careless.", "Then let's hope they were careful." }, new[] { "I hate thinking of {away} down in those crypts.", "Iron doesn't fetch itself, {name}." } } },
			{ QuestKind.RaidBurialChambers, new[] { new[] { "{away} went into a burial chamber.", "Skeletons and ghosts. Better them than me." }, new[] { "Think {away} will bring back amber?", "If the dead left any." } } },
			{ QuestKind.HuntTrolls, new[] { new[] { "{away} went after a troll.", "A troll? On purpose?", "Its hide makes good armor, they said." }, new[] { "Do you hear that crashing in the forest, {name}?", "That'll be {away} and the troll. I hope." } } },
			{ QuestKind.HuntGoblins, new[] { new[] { "{away} went to raid the fulings.", "Brave. Or mad.", "Out on the Plains it's the same thing." }, new[] { "Black metal, they said. Worth the fulings' spears, I hope.", "We'll know soon enough." } } },
			{ QuestKind.SellTreasure, new[] { new[] { "{away} took our amber to Haldor.", "I hope they come back with coins and not some trinket of his." }, new[] { "Haldor drives a hard bargain.", "{away} can haggle with the best of them." } } },
			{ QuestKind.BuyCirclet, new[] { new[] { "{away} went to Haldor for a Dverger circlet.", "No more torches in the dark, then." }, new[] { "A circlet costs a fortune, {name}.", "A fortune well spent, if it keeps the dark away." } } },
			{ QuestKind.KillEikthyr, new[] { new[] { "{away} went to face Eikthyr.", "The stag? For his antlers?", "For a pickaxe, they said." }, new[] { "Thunder over the meadow, {name}. Hear it?", "That'll be {away} and the stag." } } },
			{ QuestKind.FindBees, new[] { new[] { "{away} went looking for wild bees.", "I hope they bring back more honey than stings." }, new[] { "Wild hives are hard to find, {name}.", "Follow the buzzing, they said." } } },
			{ QuestKind.HuntGhosts, new[] { new[] { "{away} went into a burial chamber after ghosts.", "On purpose?", "Ectoplasm, they said. For silvering blades." }, new[] { "How do you kill something that's already dead, {name}?", "Ask {away} when they get back." } } },
			{ QuestKind.None, new[] { new[] { "Any word from {away}?", "Not yet. They'll be back {when}." }, new[] { "It's quiet without {away}, {name}.", "Too quiet. I'll be glad when they're back." } } }
		};

		/// <summary>
		/// What a viking at home says to a player about a quest that's out, by the quest - with the names of those out on
		/// it - and about any quest
		/// </summary>
		internal static readonly Dictionary<QuestKind, string[]> AwayAnswers = new Dictionary<QuestKind, string[]>
		{
			{ QuestKind.MineCopper, new[] { "{away} went mining copper, {player}. We'll have bronze before long." } },
			{ QuestKind.MineTin, new[] { "{away} went down to the shore after tin, {player}." } },
			{ QuestKind.MineSilver, new[] { "{away} went up the mountain for silver, {player}." } },
			{ QuestKind.RaidCrypts, new[] { "{away} went into the sunken crypts for iron. Pray for them, {player}." } },
			{ QuestKind.RaidBurialChambers, new[] { "{away} went raiding a burial chamber, {player}." } },
			{ QuestKind.HuntTrolls, new[] { "{away} went troll hunting. Troll hunting, {player}!" } },
			{ QuestKind.HuntGoblins, new[] { "{away} went to raid a fuling village, {player}." } },
			{ QuestKind.SellTreasure, new[] { "{away} took our treasure to Haldor, {player}." } },
			{ QuestKind.BuyCirclet, new[] { "{away} went to buy a circlet from Haldor, {player}." } },
			{ QuestKind.KillEikthyr, new[] { "{away} went to face Eikthyr, {player}. For his antlers." } },
			{ QuestKind.FindBees, new[] { "{away} went looking for wild bees, {player}." } },
			{ QuestKind.HuntGhosts, new[] { "{away} went hunting ghosts in the burial chambers, {player}. We need their ectoplasm." } },
			{ QuestKind.None, new[] { "{away} went out on a quest, {player}. Back {when}, they said.", "We're short-handed until {away} gets back, {player}." } }
		};

		/// <summary>
		/// The talks a viking who went on a quest starts about it once it's back, by the quest - with the names of the
		/// others who went, for the talks that name them - and about any quest
		/// </summary>
		internal static readonly Dictionary<QuestKind, string[][]> BackTalks = new Dictionary<QuestKind, string[][]>
		{
			{ QuestKind.MineCopper, new[] { new[] { "{with} and I dug enough copper for a dozen axes.", "Then the smelter's busy tonight." }, new[] { "My arms ache from all that digging, {name}.", "Copper doesn't come out of the rock on its own." } } },
			{ QuestKind.MineTin, new[] { new[] { "Tin from the shore, {name}. {with} carried more than I did.", "Then the bronze can start." }, new[] { "The tin was right by the water.", "And the trolls?", "We didn't stay to ask." } } },
			{ QuestKind.MineSilver, new[] { new[] { "The mountain was cold, {name}. Ask {with} - we nearly froze.", "But you brought silver?", "We brought silver." }, new[] { "I never want to see another drake.", "Then I hope you dug enough silver." } } },
			{ QuestKind.RaidCrypts, new[] { new[] { "The crypts stank, {name}. {with} and I won't forget it.", "Did you get the iron?", "Every scrap we could carry." }, new[] { "The draugr down there don't stay down.", "Then why go back?", "Iron, {name}. Always iron." } } },
			{ QuestKind.RaidBurialChambers, new[] { new[] { "Skeletons everywhere, {name}. {with} smashed most of them.", "Any treasure?", "Amber, bones and dust." }, new[] { "It was so dark in there, {name}.", "Good thing you took a torch." } } },
			{ QuestKind.HuntTrolls, new[] { new[] { "We brought down a troll! {with} took the first swing.", "And lived to tell it. Impressive." }, new[] { "Have you ever smelled a troll up close, {name}?", "I'll take your word for it." } } },
			{ QuestKind.HuntGoblins, new[] { new[] { "The fulings didn't see us coming. {with} saw to that.", "Black metal?", "Enough to make the forge sing." }, new[] { "The Plains are no place for the careless, {name}.", "And yet here you are." } } },
			{ QuestKind.SellTreasure, new[] { new[] { "Haldor tried to cheat us, {name}. {with} wasn't having it.", "So you got the coins?", "Every last one." }, new[] { "Haldor's cart is full of strange things, {name}.", "Did you buy any of them?", "Only with my eyes." } } },
			{ QuestKind.BuyCirclet, new[] { new[] { "Look, {name} - a Dverger circlet!", "No more torches, then." }, new[] { "Haldor wanted every coin we had, {name}.", "Was it worth it?", "Ask me in the dark." } } },
			{ QuestKind.KillEikthyr, new[] { new[] { "Eikthyr is down, {name}! {with} and I saw to it.", "The stag himself?", "Antlers and all." }, new[] { "My hair's still standing up from the stag's lightning.", "It suits you, {name}." } } },
			{ QuestKind.FindBees, new[] { new[] { "We found wild bees, {name}.", "And the stings to prove it, I see." }, new[] { "Honey, {name}! Straight from a wild hive.", "Save some for the rest of us." } } },
			{ QuestKind.HuntGhosts, new[] { new[] { "Ghosts are cold, {name}. Colder than the grave. {with} felt it too.", "But you got the ectoplasm?", "Enough to line a blade or two." }, new[] { "I keep hearing wailing, {name}.", "That's just the wind.", "I hope so." } } },
			{ QuestKind.None, new[] { new[] { "We made it back, {name}.", "In one piece?", "Mostly." }, new[] { "{with} and I are home.", "And not empty-handed, I see." } } }
		};

		/// <summary>
		/// What a viking who went on a quest says about it to a player once it's back, by the quest - with the names of
		/// the others who went, for the lines that name them - and about any quest
		/// </summary>
		internal static readonly Dictionary<QuestKind, string[]> BackAnswers = new Dictionary<QuestKind, string[]>
		{
			{ QuestKind.MineCopper, new[] { "Copper, {player}! {with} and I carried home all we could lift." } },
			{ QuestKind.MineTin, new[] { "Tin for the bronze, {player}. {with} found a good seam." } },
			{ QuestKind.MineSilver, new[] { "We brought silver down from the mountain, {player}. {with} dug most of it." } },
			{ QuestKind.RaidCrypts, new[] { "We went into the sunken crypts, {player}. {with} and I came out with iron." } },
			{ QuestKind.RaidBurialChambers, new[] { "We raided a burial chamber, {player}! {with} nearly got lost in the dark." } },
			{ QuestKind.HuntTrolls, new[] { "We killed a troll, {player}! Ask {with} if you don't believe me." } },
			{ QuestKind.HuntGoblins, new[] { "We raided the fulings, {player}. {with} fought like a bear." } },
			{ QuestKind.SellTreasure, new[] { "Haldor paid well for our treasure, {player}. {with} drove the bargain." } },
			{ QuestKind.BuyCirclet, new[] { "Haldor's circlet, {player}! No more torches for us." } },
			{ QuestKind.KillEikthyr, new[] { "We faced Eikthyr, {player}! {with} and I brought back his antlers." } },
			{ QuestKind.FindBees, new[] { "I found wild bees, {player}. They found me too." } },
			{ QuestKind.HuntGhosts, new[] { "We hunted ghosts in a burial chamber, {player}. {with} and I brought back their ectoplasm." } },
			{ QuestKind.None, new[] { "We're back, {player}! {with} and I didn't come home empty-handed.", "Home again, {player}. It was a long trip." } }
		};

		/// <summary>
		/// What a viking says coming home from a quest to a village whose vikings all died while it was gone, and as it
		/// mourns each of them by name
		/// </summary>
		internal static readonly string[] AloneLines =
		{
			"What happened while we were gone?!",
			"Where is everyone?!",
			"No... not all of them.",
			"We were only gone a little while...",
			"Hello? Is anyone here?"
		};
		internal static readonly string[] MournLines =
		{
			"{fallen}... I should have been here.",
			"I'm sorry I wasn't here, {fallen}.",
			"Rest now, {fallen}. We'll remember you.",
			"Not you too, {fallen}...",
			"Save me a seat in Valhalla, {fallen}."
		};

		/// <summary>
		/// Picks what one of the vikings setting out on a quest says as they go
		/// </summary>
		/// <param name="when">Roughly when they're due back, like "tomorrow", or "now"</param>
		/// <returns>The line</returns>
		public static string Departure(string when)
		{
			return(Fill(Pick(DepartureLines), ((when == "now") ? "soon" : when), "", "", ""));
		}

		/// <summary>
		/// Picks a talk at home about a quest that's out - one about the quest or about any quest
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="away">The names of the vikings out on it</param>
		/// <param name="when">Roughly when they're due back</param>
		/// <returns>The talk's lines, filled in but for the name of the one spoken to</returns>
		public static string[] AwayTalk(QuestKind kind, List<string> away, string when)
		{
			return(AwayTalk(kind, away, when, Roll()));
		}

		/// <summary>
		/// Picks a talk at home about a quest that's out, by a roll
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="away">The names of the vikings out on it</param>
		/// <param name="when">Roughly when they're due back</param>
		/// <param name="roll">A roll from 0 up, picking the talk</param>
		/// <returns>The talk's lines, filled in but for the name of the one spoken to, or null when there's none</returns>
		internal static string[] AwayTalk(QuestKind kind, List<string> away, string when, int roll)
		{
			string names = JoinNames(away);
			string[][] talks = Candidates(AwayTalks, kind, names.Length > 0, AwayToken);
			return((talks.Length > 0) ? FillAll(talks[roll % talks.Length], when, names, "", "") : null);
		}

		/// <summary>
		/// Picks what a viking at home says to a player about a quest that's out
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="away">The names of the vikings out on it</param>
		/// <param name="when">Roughly when they're due back</param>
		/// <param name="player">The player's name</param>
		/// <returns>The line, or null when there's none for a quest of no named vikings</returns>
		public static string AwayAnswer(QuestKind kind, List<string> away, string when, string player)
		{
			string names = JoinNames(away);
			List<string> lines = Lines(AwayAnswers, kind, names.Length > 0, AwayToken);
			return((lines.Count > 0) ? Fill(lines[Random.Range(0, lines.Count)], when, names, "", player) : null);
		}

		/// <summary>
		/// Picks a talk a viking who went on a quest starts about it once it's back - one about the quest or about any
		/// quest, leaving out those naming the others when it went alone
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="companions">The names of the others who went</param>
		/// <returns>The talk's lines, filled in but for the name of the one spoken to</returns>
		public static string[] BackTalk(QuestKind kind, List<string> companions)
		{
			return(BackTalk(kind, companions, Roll()));
		}

		/// <summary>
		/// Picks a talk a viking who went on a quest starts about it once it's back, by a roll
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="companions">The names of the others who went</param>
		/// <param name="roll">A roll from 0 up, picking the talk</param>
		/// <returns>The talk's lines, filled in but for the name of the one spoken to, or null when there's none</returns>
		internal static string[] BackTalk(QuestKind kind, List<string> companions, int roll)
		{
			string names = JoinNames(companions);
			string[][] talks = Candidates(BackTalks, kind, names.Length > 0, WithToken);
			return((talks.Length > 0) ? FillAll(talks[roll % talks.Length], "", "", names, "") : null);
		}

		/// <summary>
		/// Picks what a viking who went on a quest says to a player about it once it's back
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="companions">The names of the others who went</param>
		/// <param name="player">The player's name</param>
		/// <returns>The line</returns>
		public static string BackAnswer(QuestKind kind, List<string> companions, string player)
		{
			string names = JoinNames(companions);
			List<string> lines = Lines(BackAnswers, kind, names.Length > 0, WithToken);
			return((lines.Count > 0) ? Fill(lines[Random.Range(0, lines.Count)], "", "", names, player) : null);
		}

		/// <summary>
		/// Picks what a viking cries out coming home from a quest to a village whose vikings all died
		/// </summary>
		/// <returns>The line</returns>
		public static string Alone()
		{
			return(Pick(AloneLines));
		}

		/// <summary>
		/// Picks what a viking says mourning one of its village who died while it was out on a quest
		/// </summary>
		/// <param name="fallen">The dead viking's name</param>
		/// <returns>The line</returns>
		public static string Mourn(string fallen)
		{
			return(Mourn(fallen, Roll()));
		}

		/// <summary>
		/// Picks what a viking says mourning one of its village who died while it was out on a quest, by a roll
		/// </summary>
		/// <param name="fallen">The dead viking's name</param>
		/// <param name="roll">A roll from 0 up, picking the line</param>
		/// <returns>The line</returns>
		internal static string Mourn(string fallen, int roll)
		{
			return(MournLines[roll % MournLines.Length].Replace(FallenToken, fallen));
		}

		/// <summary>
		/// Rolls a number to pick a line or talk by
		/// </summary>
		/// <returns>The roll, from 0 up</returns>
		private static int Roll()
		{
			return(Random.Range(0, int.MaxValue));
		}

		/// <summary>
		/// Joins names the way they're said - "Ragna", "Ragna and Ulf", "Ragna, Ulf and Sigrid"
		/// </summary>
		/// <param name="names">The names</param>
		/// <returns>The names joined, or "" for none</returns>
		internal static string JoinNames(List<string> names)
		{
			List<string> named = names.FindAll((string name) => !string.IsNullOrEmpty(name));
			if (named.Count <= 1)
			{
				return((named.Count == 1) ? named[0] : "");
			}
			return(string.Join(", ", named.GetRange(0, named.Count - 1)) + " and " + named[named.Count - 1]);
		}

		/// <summary>
		/// Lists the talks about a quest and about any quest that fit - leaving out those with a token for names when
		/// there are none to fill it with
		/// </summary>
		/// <param name="talks">The talks, by quest</param>
		/// <param name="kind">The quest</param>
		/// <param name="named">Whether there are names to fill the token with</param>
		/// <param name="token">The token for names</param>
		/// <returns>The talks</returns>
		internal static string[][] Candidates(Dictionary<QuestKind, string[][]> talks, QuestKind kind, bool named, string token)
		{
			List<string[]> found = new List<string[]>();
			foreach (QuestKind key in new[] { kind, QuestKind.None })
			{
				if (!talks.TryGetValue(key, out string[][] lines))
				{
					continue;
				}
				foreach (string[] talk in lines)
				{
					if (named || !System.Array.Exists(talk, (string line) => line.Contains(token)))
					{
						found.Add(talk);
					}
				}
			}
			return(found.ToArray());
		}

		/// <summary>
		/// Lists the lines about a quest and about any quest that fit - leaving out those with a token for names when
		/// there are none to fill it with
		/// </summary>
		/// <param name="lines">The lines, by quest</param>
		/// <param name="kind">The quest</param>
		/// <param name="named">Whether there are names to fill the token with</param>
		/// <param name="token">The token for names</param>
		/// <returns>The lines</returns>
		internal static List<string> Lines(Dictionary<QuestKind, string[]> lines, QuestKind kind, bool named, string token)
		{
			List<string> found = new List<string>();
			foreach (QuestKind key in new[] { kind, QuestKind.None })
			{
				if (lines.TryGetValue(key, out string[] some))
				{
					found.AddRange(System.Array.FindAll(some, (string line) => named || !line.Contains(token)));
				}
			}
			return(found);
		}

		/// <summary>
		/// Fills in a line's tokens in one pass, so a name that reads like a token is said as it was given
		/// </summary>
		/// <param name="line">The line</param>
		/// <param name="when">Roughly when a quest is due back</param>
		/// <param name="away">The names of the vikings out on it</param>
		/// <param name="with">The names of the others who went with the one speaking</param>
		/// <param name="player">The player's name</param>
		/// <returns>The line filled in</returns>
		internal static string Fill(string line, string when, string away, string with, string player)
		{
			Dictionary<string, string> values = new Dictionary<string, string>
			{
				{ WhenToken, when },
				{ AwayToken, away },
				{ WithToken, with },
				{ PlayerToken, player }
			};
			string filled = System.Text.RegularExpressions.Regex.Replace(line, "\\{(?:when|away|with|player)\\}", (System.Text.RegularExpressions.Match token) => values[token.Value]);
			return((filled.Length > 0) ? char.ToUpperInvariant(filled[0]) + filled.Substring(1) : filled);
		}

		/// <summary>
		/// Fills in each line of a talk
		/// </summary>
		/// <param name="talk">The talk's lines</param>
		/// <param name="when">Roughly when a quest is due back</param>
		/// <param name="away">The names of the vikings out on it</param>
		/// <param name="with">The names of the others who went with the one speaking</param>
		/// <param name="player">The player's name</param>
		/// <returns>The lines filled in</returns>
		private static string[] FillAll(string[] talk, string when, string away, string with, string player)
		{
			return(System.Array.ConvertAll(talk, (string line) => Fill(line, when, away, with, player)));
		}

		/// <summary>
		/// Picks one of a set of lines at random
		/// </summary>
		/// <param name="lines">The lines</param>
		/// <returns>One of them</returns>
		private static string Pick(string[] lines)
		{
			return(lines[Random.Range(0, lines.Length)]);
		}
	}
}
