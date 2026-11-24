using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What dvergr vikings say - a set of their own, about working for the dvergr and being hired by them, apart from
	/// the living vikings' (see VikingSpeech) but for what both say about the Mistlands in the news (see
	/// DvergrNewsLines) - shown over their heads the way the living vikings' words are
	/// </summary>
	/// <remarks>
	/// Every line here is listed in the mod's DIALOG.md, which changes along with them
	/// </remarks>
	internal static class DvergrSpeech
	{
		/// <summary>
		/// What a dvergr viking calls a player it doesn't know yet as it thanks them for a power
		/// </summary>
		public const string Stranger = "traveler";

		/// <summary>
		/// What a dvergr viking says to a player it has never met, by day and at night
		/// </summary>
		private static readonly string[] DayFirstGreetings =
		{
			"Hail, stranger. You're a long way into the mist.",
			"Easy there. This outpost is under Dvergr protection.",
			"A traveler? Mind your manners - the Dvergr are watching.",
			"Hail! Don't mind me. The Dvergr pay me to stand here and look fierce."
		};

		private static readonly string[] NightFirstGreetings =
		{
			"Who's there? ...Oh, a traveler. Come in out of the dark.",
			"Late to be wandering the mist, stranger. Stay near the lanterns.",
			"Hail, stranger. Quietly - the Dvergr don't like noise after dark."
		};

		/// <summary>
		/// What a dvergr viking adds after greeting a player it has never met, with its name
		/// </summary>
		private static readonly string[] Introductions =
		{
			"I'm {0}, sword for hire.",
			"Name's {0}. I guard this lot for the Dvergr.",
			"They call me {0}. The Dvergr call me something I can't pronounce."
		};

		/// <summary>
		/// What a dvergr viking says to a player it knows as they arrive, by day and at night, with the player's name
		/// </summary>
		private static readonly string[] DayGreetings =
		{
			"{0}! Good to see a Viking face out here.",
			"Hail, {0}. Still alive in all this fog?",
			"{0}! Come to keep me company on watch?",
			"Well met, {0}. The Dvergr say you're welcome here. Mostly."
		};

		private static readonly string[] NightGreetings =
		{
			"{0}. Stay close to the lanterns tonight.",
			"Late to be about, {0}. The seekers love the dark.",
			"{0}! Quiet now - the Dvergr are resting."
		};

		/// <summary>
		/// What a dvergr viking says when a player it knows talks to it, by day and at night, with the player's name
		/// </summary>
		private static readonly string[] DayChat =
		{
			"The Dvergr pay well, {0}. In coin, mostly. Once in black metal.",
			"I took this work at the harbor. A Dvergr pointed at me, then at a pile of coins. That was the whole contract.",
			"Don't touch their chests, {0}. I've seen what happens to folk who do.",
			"The Dvergr don't say much. I've learned to read their beards.",
			"They hired me for my axe arm, {0}. I stay for the stew.",
			"Every few days a Dvergr hands me a bag of coins and walks off. Best master I've ever had.",
			"I tried to haggle my wages once. The Dvergr just stared until I gave up.",
			"You get used to the mist, {0}. Eventually. I think.",
			"Keep a wisp light handy out here, {0}. The mist hides more than trees."
		};

		private static readonly string[] NightChat =
		{
			"Can't sleep with the seekers clicking out there, {0}.",
			"The Dvergr keep their lanterns lit all night. Can't say I mind.",
			"Quiet nights are rare in the mist, {0}. Enjoy this one."
		};

		/// <summary>
		/// What a dvergr viking says when a player talks to it in a fight
		/// </summary>
		private static readonly string[] BusyChat =
		{
			"Not now - I'm earning my pay!",
			"Later! Guard the Dvergr!",
			"Busy! Talk after!"
		};

		/// <summary>
		/// What a dvergr viking calls out as it spots an enemy, with the enemy's name
		/// </summary>
		private static readonly string[] SpotLines =
		{
			"{0}! Protect the Dvergr!",
			"{0} coming - to arms!",
			"Eyes up - {0}!",
			"{0} in the mist!",
			"Guard the outpost - {0}!"
		};

		/// <summary>
		/// What a badly hurt dvergr viking shouts for help
		/// </summary>
		private static readonly string[] HelpLines =
		{
			"Help me!",
			"To me - I'm hurt!",
			"Dvergr, help!",
			"I need a hand here!"
		};

		/// <summary>
		/// What a dvergr viking says as it reaches a new level of a skill, with the skill's name and its new level
		/// </summary>
		internal static readonly string[] ProudLines =
		{
			"My {0} gets better every day out here!",
			"Level {1} in {0}! The Dvergr should pay me more.",
			"Ha! All this guard duty is good for my {0}.",
			"Even a Dvergr would nod at my {0} now."
		};

		/// <summary>
		/// What a dvergr viking says as it uses a forsaken power, with the power's name
		/// </summary>
		private static readonly string[] PowerUseLines =
		{
			"{0}, guard us!",
			"By {0}! For the Dvergr!",
			"{0}, lend us your strength!",
			"Feel {0}'s blessing, friends!"
		};

		/// <summary>
		/// What a dvergr viking says when a player shares their forsaken power with it, with the power's name and the
		/// player's name, or Stranger when it doesn't know them
		/// </summary>
		private static readonly string[] PowerLines =
		{
			"{0}'s gift! The Dvergr thank you too, {1}.",
			"I feel {0}'s power, {1}! Worth more than my wages.",
			"Thank you, {1}. With {0}'s blessing, nothing gets past me.",
			"{0}! Now that's a bonus the Dvergr never paid, {1}."
		};

		/// <summary>
		/// Picks a dvergr viking's greeting for a player - by name for a player it knows, and as a stranger otherwise,
		/// with its name after it when it has one
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="night">Whether it's night</param>
		/// <param name="known">Whether the viking knows the player</param>
		/// <param name="self">The viking's own name, which it gives a stranger, or ""</param>
		/// <returns>The line</returns>
		public static string Greeting(Player player, bool night, bool known, string self)
		{
			if (!known)
			{
				string first = Pick(night ? NightFirstGreetings : DayFirstGreetings);
				return((self.Length > 0) ? first + " " + string.Format(Pick(Introductions), self) : first);
			}
			return(string.Format(Pick(night ? NightGreetings : DayGreetings), player.GetPlayerName()));
		}

		/// <summary>
		/// Picks what a dvergr viking says when a player it knows talks to it
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="night">Whether it's night</param>
		/// <returns>The line</returns>
		public static string Answer(Player player, bool night)
		{
			return(string.Format(Pick(night ? NightChat : DayChat), player.GetPlayerName()));
		}

		/// <summary>
		/// Picks what a dvergr viking says when a player talks to it in a fight
		/// </summary>
		/// <returns>The line</returns>
		public static string Busy()
		{
			return(Pick(BusyChat));
		}

		/// <summary>
		/// Picks what a dvergr viking calls out as it spots an enemy
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>The line</returns>
		public static string Spotted(Character enemy)
		{
			return(string.Format(Pick(SpotLines), enemy.GetHoverName()));
		}

		/// <summary>
		/// Picks a badly hurt dvergr viking's shout for help
		/// </summary>
		/// <returns>The line</returns>
		public static string Help()
		{
			return(Pick(HelpLines));
		}

		/// <summary>
		/// Picks what a dvergr viking says as it uses a forsaken power
		/// </summary>
		/// <param name="power">The power's name</param>
		/// <returns>The line</returns>
		public static string PowerUsed(string power)
		{
			return(string.Format(Pick(PowerUseLines), power));
		}

		/// <summary>
		/// Picks what some dvergr vikings say when a player shares their forsaken power with them, a different line for
		/// each
		/// </summary>
		/// <param name="power">The power's name</param>
		/// <param name="players">The player's name, or how each viking calls them, for each viking</param>
		/// <returns>The lines, one for each viking</returns>
		public static List<string> PowerShared(string power, List<string> players)
		{
			List<int> left = new List<int>();
			for (int i = 0; i < PowerLines.Length; i++)
			{
				left.Add(i);
			}
			List<string> lines = new List<string>();
			foreach (string player in players)
			{
				if (left.Count == 0)
				{
					break;
				}
				int picked = Random.Range(0, left.Count);
				lines.Add(string.Format(PowerLines[left[picked]], power, player));
				left.RemoveAt(picked);
			}
			return(lines);
		}

		/// <summary>
		/// Picks a line at random
		/// </summary>
		/// <param name="lines">The lines</param>
		/// <returns>The line</returns>
		private static string Pick(string[] lines)
		{
			return(lines[Random.Range(0, lines.Length)]);
		}
	}
}
