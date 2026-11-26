using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a viking named Elsa, Anna, Kristoff, Olaf or Hans says now and then to a player it knows - a nod to the
	/// film Frozen and the one of its folk who shares the viking's name
	/// </summary>
	internal static class VikingFrozenSpeech
	{
		/// <summary>
		/// How likely a viking with one of the names talks about it when a player it knows talks to it
		/// </summary>
		private const float ChatChance = 0.25f;

		/// <summary>
		/// What a viking says by its name, with {0} for the player's name
		/// </summary>
		internal static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase)
		{
			{
				"Elsa", new[]
				{
					"Let it go, {0}. Whatever's weighing on you, just let it go. It worked for me.",
					"My hands go cold when I'm upset, {0}. I keep my gloves on near the fire, just in case.",
					"I once raised a palace of ice on a mountain, all by myself. Don't tell the builders here, {0}. They'd want one.",
					"If you ever hear a voice calling from the north, {0}, follow it. Into the unknown, as they say.",
					"My sister is the brave one, {0}. I'm the one who kept her door shut for years.",
					"Everyone else shivers in the Mountains, {0}. I find it rather pleasant.",
					"Snow in the Meadows in midsummer? Don't look at me, {0}."
				}
			},
			{
				"Anna", new[]
				{
					"Do you wanna build a snowman, {0}? No? Nobody ever does.",
					"I once got engaged to a man I'd known for a day, {0}. Get to know them first. Trust me.",
					"My sister kept her door shut for years, {0}. I knocked anyway. Every single day.",
					"The gates are open and there's a feast tonight, {0}! Well, stew. It's usually stew.",
					"The rock trolls told me love was the only cure for a frozen heart, {0}. They were right, for once.",
					"Kristoff smells a little of reindeer, {0}. I've stopped minding.",
					"If a man called Hans offers you his hand, {0}, keep yours in your pocket."
				}
			},
			{
				"Kristoff", new[]
				{
					"I was raised by rock trolls, {0}. Little round fellows covered in moss. They say they're experts on love.",
					"Those trolls in the Black Forest aren't my family, {0}. Mine never threw a tree at anyone.",
					"If a boulder rolls up and starts talking to you, {0}, be polite. It's probably one of my aunts.",
					"My family called me a fixer-upper, {0}. To my face. Then they sang about it.",
					"I used to cut ice for a living, {0}. Nobody here buys ice. They just walk up a mountain and take it.",
					"My best friend is a reindeer, {0}. I've met Vikings called Sven since. It's not the same.",
					"Ask the rock trolls anything, {0}. They'll answer, then try to marry you off to someone."
				}
			},
			{
				"Olaf", new[]
				{
					"I've always wondered what summer is like, {0}. Is it nice? It sounds nice.",
					"I hear the Ashlands are lovely this time of year, {0}. Do you think I'd like it there?",
					"Is it me, or is it warm today, {0}? I think I'm melting a little.",
					"Keep an eye on the carrots, {0}. Somehow one always ends up on my face.",
					"Have you ever been hugged by someone who's always cold, {0}? You'd remember it.",
					"I'd melt for my friends, {0}. Happily. Well, maybe not happily. But I would."
				}
			},
			{
				"Hans", new[]
				{
					"I'm the youngest of thirteen brothers, {0}. Thirteenth in line for anything. Do you know what that's like?",
					"I'd make an excellent leader, {0}. If only someone would step aside. Or fall over.",
					"Never trust anyone you've only just met, {0}. Including me. Especially me.",
					"I come from the Southern Isles, {0}. Warm, sunny, and not an ice queen in sight.",
					"Love is an open door, they say. I like to see who's behind it first, {0}.",
					"If a red-haired woman called Anna tells you about me, {0}, don't believe a word of it."
				}
			}
		};

		/// <summary>
		/// Picks what a viking with one of the names says when a player it knows talks to it - now and then, when it has
		/// one of them
		/// </summary>
		/// <param name="player">The player's name</param>
		/// <param name="viking">The viking's name, or ""</param>
		/// <returns>The line, or null when it says something else</returns>
		public static string Chat(string player, string viking)
		{
			if (Random.value >= ChatChance)
			{
				return(null);
			}
			return(For(viking, player, (int count) => Random.Range(0, count)));
		}

		/// <summary>
		/// Picks a line for a viking's name, by its first name, filling in the player's name
		/// </summary>
		/// <param name="viking">The viking's name, with any byname after it, like "Elsa the Wise"</param>
		/// <param name="player">The player's name</param>
		/// <param name="pick">Picks an index below the count it's given</param>
		/// <returns>The line, or null when its name has none</returns>
		internal static string For(string viking, string player, System.Func<int, int> pick)
		{
			if (!Lines.TryGetValue(VikingLoreSpeech.FirstName(viking), out string[] lines))
			{
				return(null);
			}
			return(string.Format(lines[pick(lines.Length)], player));
		}
	}
}
