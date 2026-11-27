using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What PC and dvergr vikings say to a player they know about the runestones of the biome they stand in - each
	/// line about what one of the game's own runestones there says, from the boar stone of the Meadows to the stones
	/// of the Deep North - and about the runestones carved by someone of their own name, like Ulf's
	/// </summary>
	internal static class VikingLoreSpeech
	{
		/// <summary>
		/// How likely a viking talks about a runestone when a player it knows talks to it, and its biome has any
		/// </summary>
		private const float ChatChance = 0.2f;

		/// <summary>
		/// How likely a viking named like someone on the runestones talks about them, when it talks about a runestone
		/// </summary>
		private const float NamesakeChance = 0.5f;

		/// <summary>
		/// What a viking named Bjorn or Bjørn says about the runestones of the Bjorns before it - above NamesakeLines,
		/// since static fields are set in the order they're written
		/// </summary>
		private static readonly string[] BjornLines =
		{
			"I heard another Bjorn lost a beard duel to Otho, and had to carve it in stone himself. I'd have demanded a rematch, {0}.",
			"A lovesick fool on a stone begged Freya for a beard like Bjorn's. Not this Bjorn's, I'd guess, {0}.",
			"That other Bjorn swore Thor could tear his beard off if he lied about Otho's. An honest man, {0}. Or a frightened one.",
			"A Bjorn who was once a farmer carved a stone in the Ashlands, watching warriors lose their will one death at a time. Grim, {0}.",
			"Otho's beard is bright as a corn-field in summer, the other Bjorn carved. Sore loser, that one, {0}."
		};

		/// <summary>
		/// What a viking says about the runestones of someone with its name, by the name, with {0} for the player's name
		/// </summary>
		internal static readonly Dictionary<string, string[]> NamesakeLines = new Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase)
		{
			{
				"Ulf", new[]
				{
					"There was an Ulf here before me, {0}. He carved stones all over the land, and every one says he's moving on.",
					"Some other Ulf hid in a bush from the drake and called himself Ulf the Brave on a stone. No relation, {0}. Probably.",
					"That other Ulf carved a stone to teach us a roof, then walls, then a door. Folk still ask me if I need help with my door.",
					"An Ulf carved a stone in the forest and couldn't think of a thing to say on it. I know how he felt, {0}.",
					"The other Ulf found the swamp too wet, the mountains too deadly, the plains too hot and the Ashlands on fire. I'm staying put, {0}.",
					"An Ulf set his own beard alight with a staff in the Mistlands, his stone says. I keep well away from staffs, {0}."
				}
			},
			{
				"Astrid", new[]
				{
					"I heard someone with my name has been to this realm before, {0}. A shieldmaiden, guided by the raven. I've a lot to live up to.",
					"Some lovesick fool carved a stone because Astrid wouldn't look at him twice. Not this Astrid, {0}. I'd have looked once, at least.",
					"An Astrid carved that the Allfather's words froze to pebbles around her as she slept. Nobody ever leaves me messages, {0}.",
					"Astrid of the Long Arm will die as many times as it takes to see her son again. Same name as me, {0}. It makes my heart ache.",
					"There's an Astrid who raised a stone in the Deep North for her son. I hope she found him, {0}."
				}
			},
			{
				"Bjorn", BjornLines
			},
			{
				"Bjørn", BjornLines
			},
			{
				"Harald", new[]
				{
					"There's a Harald on the stones who lost an eye to Eikthyr, a hand to the Elder and a leg to Moder. I'm keeping all of mine, {0}.",
					"A Harald carved a stone so that at least his name would remain. It has, {0}. I've got it.",
					"The other Harald sees the faces of those he killed in his dreams, his stone says. Same name, {0}. I hope not the same dreams.",
					"That Harald walked on a staff after the drake took his leg, and still he went looking for his last battle. Stubborn lot, Haralds.",
					"I heard someone with my name has been to this realm before, {0}. He woke up here like I did, and never stopped fighting."
				}
			},
			{
				"Otho", new[]
				{
					"There's a stone in the mountains that says Otho has the finest beard in all Valheim. Bright as a corn-field in summer, {0}.",
					"Someone with my name won a beard duel against a Bjorn, {0}, and the loser had to carve it in stone. I like this other Otho.",
					"I heard an Otho came through this realm before me, and the whole land knows about his beard. No pressure, {0}.",
					"Every Bjorn I meet wants a rematch for that old duel, {0}. I keep telling them I wasn't there."
				}
			},
			{
				"Frida", new[]
				{
					"A stone in the forest says Frida was taken by a serpent and her bed lies empty still. Not this Frida, {0}. I stay off the sea.",
					"I heard someone with my name has been to this realm before, {0}. A serpent took her. I don't go fishing much.",
					"Her bed lies empty still, the stone says of that other Frida. Mine's warm, {0}, and I mean to keep it that way."
				}
			},
			{
				"Bjarni", new[]
				{
					"A stone in the forest says Bjarni was bitten in half by a troll, and neither half has been seen since. I run from trolls, {0}.",
					"Neither half of that other Bjarni has been seen since, the stone says. I'd like to keep both of mine together, {0}.",
					"I heard someone with my name has been to this realm before, {0}. He met a troll. It went badly for both halves of him."
				}
			},
			{
				"Gudrun", new[]
				{
					"There's a Gudrun on a stone in the swamp, {0}. No man's wife, no father's daughter. Only her name was left to her.",
					"That other Gudrun wakes feeling the weight of a babe at her breast, her stone says. I think of her when the nights are long, {0}.",
					"She carved that only her name remained to her. Now it's mine too, {0}. I try to wear it well.",
					"Gudrun asked great Freya to take pity on her. If you pass her stone in the swamp, {0}, say something kind for her."
				}
			},
			{
				"Agda", new[]
				{
					"There's a stone in the mountains where an Agda was given to the wind, {0}. A draugr arrow found her heart.",
					"Twenty climbed those mountains and two were left, a stone says. One of them was an Agda. I don't climb much, {0}.",
					"Whoever carved Agda's stone went after the Winged One for death or glory. I hope it was glory, {0}."
				}
			},
			{
				"Kata", new[]
				{
					"Heed the words of Kata, says a stone in the Deep North. Good advice, {0}. Always heed the words of Kata.",
					"A Kata carved that the trolls of the north are tall as towers and harder than stone. I believe every word, {0}.",
					"I heard someone with my name has been to this realm before, {0}. She thought forest trolls were mighty, then she went north.",
					"That other Kata killed trolls the size of towers for a glimmering reward. I'll start with a small one, {0}."
				}
			},
			{
				"Signe", new[]
				{
					"A Signe raised a stone in the Deep North for Eigil, who fell in battle. I wonder who he was to her, {0}.",
					"Not everything there is as frozen as it seems, the other Signe carved. I think she meant her own heart, {0}.",
					"I heard someone with my name has been to this realm before, {0}. She lost her Eigil in the snow."
				}
			},
			{
				"Eigil", new[]
				{
					"There's a stone in the Deep North for an Eigil who fell in battle, {0}. A Signe raised it. Tell her I'm still standing.",
					"The stone says Eigil had yet to learn that not everything there is as frozen as it seems. I'll learn it for him, {0}.",
					"I heard someone with my name has been to this realm before, {0}. He fell in the north, and a Signe remembered him in stone."
				}
			}
		};

		/// <summary>
		/// What a viking says about the runestones of each biome, with {0} for the player's name
		/// </summary>
		internal static readonly Dictionary<Heightmap.Biome, string[]> Lines = new Dictionary<Heightmap.Biome, string[]>
		{
			{
				Heightmap.Biome.Meadows, new[]
				{
					"Read the boar stone yet, {0}? Go quietly and let them eat from your stock. That's the whole secret.",
					"The old stone says boars fear fire and the hand of man, but they can be taught. Roots are their pleasure, it says.",
					"Ulf carved a stone telling us to build a roof, then walls, then a door. Ulf learned that the hard way, {0}.",
					"Somebody carved a stone because a tree fell on his head and he cursed the gods. I've had days like that.",
					"One of the old stones says the necks' tails are delicious. Whoever carved it wasn't wrong, {0}.",
					"A stone out there calls this the last and most lawless of the Ten Worlds. Lawless suits me fine.",
					"The stones say to keep to the meadows and fear the Horned One. Good advice, {0}.",
					"'One day in the sun is a life well-lived.' One of the stones says that. I'd carve it over my door."
				}
			},
			{
				Heightmap.Biome.BlackForest, new[]
				{
					"The stone in the woods says chop one tree and the whole forest knows. I try not to think about that, {0}.",
					"Ulf carved a stone out there and couldn't think of anything to say on it. That's Ulf for you.",
					"Some lovesick fool carved a stone begging Freya for a beard like Bjorn's. Astrid still won't look at him, I bet.",
					"One of the stones says the greydwarfs were raised from seeds of sin. I believe it, {0}.",
					"A stone in the forest says the trolls have flourished here. Then it says run. Best advice I ever read.",
					"The stones say we're not the first folk here, {0}. Someone built those towers, and they're long gone.",
					"'Beware the true dark,' the stone says. 'Keep close to your fire.' I don't need a stone to tell me that."
				}
			},
			{
				Heightmap.Biome.Swamp, new[]
				{
					"Ulf left a stone warning us not to build by the murky water. Then he moved to higher ground. Wise Ulf.",
					"A stone in the swamp calls iron 'bread of the forge', {0}. That carver had a way with words.",
					"A stone out there says killing draugr is a mercy. I try to remember that while they're shooting at me.",
					"Some poor soul carved that he killed seven draugr there, and asked who would end his misery. Grim stone, {0}.",
					"Gudrun carved a stone out in the muck. No man's wife, no father's daughter. I think of her sometimes.",
					"'Walker among the dead, you tread a grave with every step.' One of the stones says that. Thanks, stone."
				}
			},
			{
				Heightmap.Biome.Mountain, new[]
				{
					"Ulf the Brave carved a stone up here, {0}. It says he hid in a bush while the drake flew over. Brave Ulf.",
					"There's a stone about Otho and Bjorn fighting over who had the finest beard. Otho won, it says.",
					"One of the stones says to wear thick furs and keep a bow at hand. Whoever carved it was freezing, {0}.",
					"Astrid carved a stone saying Odin's words froze to pebbles in the cold. I believe it, up here.",
					"'Slow to stalk, quick to bite,' says the fenring stone. I sleep with my axe, {0}.",
					"A stone up here says the wolves can be friends again, if you bring them meat. Moon-singers, it calls them.",
					"Ulf left a stone saying he was off to the sunny plains. He'll regret it, {0}."
				}
			},
			{
				Heightmap.Biome.Plains, new[]
				{
					"Ulf carved a stone saying it's too hot here for a man used to brushing snow from his beard. Then he left. Again.",
					"One of the stones says the flies turned a man's beard white, so he went to build a boat. I understand him, {0}.",
					"Harald carved his whole life on a stone out there. An eye, a hand and a leg, and still he kept going.",
					"The stones say the lox can be taught to love the hand of man again. I'd like to see you try, {0}.",
					"A stone tells of a star falling into the sand. Sword-flesh, it calls it. Find me some, {0}?",
					"The fulings had cities once, a stone says, until Odin trampled them. Now they're just angry."
				}
			},
			{
				Heightmap.Biome.Mistlands, new[]
				{
					"There's a stone in the mist where seven folk counted eight. Every time. I count twice now, {0}.",
					"Ulf made a staff that set his beard on fire, his stone says. Then he went looking for somewhere less troublesome.",
					"A stone out here says don't take the Dvergr's hats off their heads. Their hats don't fit common heads anyway.",
					"The stones say the Jotunn lie buried here, {0}. We're walking on giants' bones.",
					"One of the stones says the Dvergr only fight when they're riled. So let's not rile them, {0}.",
					"'The mist breeds wonders,' says the stone. It breeds ticks too."
				}
			},
			{
				Heightmap.Biome.AshLands, new[]
				{
					"Ulf's stone out here says most things are on fire and the fishing is not good. Ulf, ever the poet.",
					"The stones tell of the King of the Emerald Flame, {0}. A wise king once, they say. Hard to believe now.",
					"There's a stone about a Winter Child found asleep in the snow, taken for a good omen. Fools, the stone says.",
					"Astrid of the Long Arm carved a stone here. She'll die as many times as it takes to see her son again.",
					"Bjorn the farmer carved that warriors lose a little will each time they die. Don't lose yours, {0}.",
					"'War is a fire and men are fuel.' One of the stones here. Cheerful lot, the folk who lived here."
				}
			},
			{
				Heightmap.Biome.DeepNorth, new[]
				{
					"Ulf liked the cold, his stone says, until little creatures tried to drag him under. Then he reconsidered.",
					"Kata's stone says the trolls here are as tall as towers and harder than stone. A glimmering reward, though, {0}.",
					"'Beware of false trees,' says one of the stones. I don't trust a single tree up here now.",
					"Signe raised a stone for Eigil, who fell. Not everything here is as frozen as it seems, {0}.",
					"Someone left stones all over these lands, hunting whoever rules them. I hope they found them, {0}.",
					"Astrid raised a stone up here for her son. I hope she sees him again, {0}."
				}
			}
		};

		/// <summary>
		/// Picks what a viking says about a runestone when a player it knows talks to it - now and then, about the
		/// runestones of someone with its name half the time when it has one of their names, and otherwise about one of
		/// the biome it stands in, when its biome has any
		/// </summary>
		/// <param name="position">Where the viking stands</param>
		/// <param name="player">The player's name</param>
		/// <param name="viking">The viking's name, or ""</param>
		/// <returns>The line, or null when it says something else</returns>
		public static string Chat(Vector3 position, string player, string viking)
		{
			if (Random.value >= ChatChance || WorldGenerator.instance == null)
			{
				return(null);
			}
			System.Func<int, int> pick = (int count) => Random.Range(0, count);
			string namesake = Namesake(viking, player, pick);
			if (namesake != null && Random.value < NamesakeChance)
			{
				return(namesake);
			}
			return(For(WorldGenerator.instance.GetBiome(position), player, viking, pick));
		}

		/// <summary>
		/// Picks a line about a runestone of a biome, filling in the player's name - leaving out the lines that name the
		/// viking saying it, which it would say as someone with its name (see Namesake), and what only PC vikings say for
		/// an NPC viking (see VikingSpeech.Says)
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="player">The player's name</param>
		/// <param name="viking">The viking's name, or ""</param>
		/// <param name="pick">Picks an index below the count it's given</param>
		/// <returns>The line, or null when the biome has none</returns>
		internal static string For(Heightmap.Biome biome, string player, string viking, System.Func<int, int> pick)
		{
			if (!Lines.TryGetValue(biome, out string[] lines))
			{
				return(null);
			}
			string first = FirstName(viking);
			List<string> fitting = new List<string>();
			foreach (string line in lines)
			{
				if ((first.Length == 0 || !Names(line, first)) && VikingSpeech.Says(line))
				{
					fitting.Add(line);
				}
			}
			return((fitting.Count > 0) ? string.Format(fitting[pick(fitting.Count)], player) : null);
		}

		/// <summary>
		/// Picks a line about the runestones of someone with a viking's name, filling in the player's name - leaving out
		/// what only PC vikings say for an NPC viking (see VikingSpeech.Says)
		/// </summary>
		/// <param name="viking">The viking's name, with any byname after it, like "Ulf the Red"</param>
		/// <param name="player">The player's name</param>
		/// <param name="pick">Picks an index below the count it's given</param>
		/// <returns>The line, or null when no one on the runestones has its name or it says none of their lines</returns>
		internal static string Namesake(string viking, string player, System.Func<int, int> pick)
		{
			if (!NamesakeLines.TryGetValue(FirstName(viking), out string[] lines))
			{
				return(null);
			}
			string[] spoken = System.Array.FindAll(lines, (string line) => VikingSpeech.Says(line));
			if (spoken.Length == 0)
			{
				return(null);
			}
			return(string.Format(spoken[pick(spoken.Length)], player));
		}

		/// <summary>
		/// Gets a viking's name without the byname after it - its first word
		/// </summary>
		/// <param name="viking">The viking's name, like "Ulf the Red", or null</param>
		/// <returns>The first word, like "Ulf", or ""</returns>
		internal static string FirstName(string viking)
		{
			string trimmed = (viking ?? "").Trim();
			int space = trimmed.IndexOf(' ');
			return((space < 0) ? trimmed : trimmed.Substring(0, space));
		}

		/// <summary>
		/// Checks whether a line names someone, whatever its case, as a word of its own - "Bjorn's" names Bjorn, while
		/// "Ulfric" doesn't name Ulf - taking an "ø" in the name as the "o" the lines spell it with
		/// </summary>
		/// <param name="line">The line</param>
		/// <param name="name">The name</param>
		/// <returns>True when it does</returns>
		internal static bool Names(string line, string name)
		{
			string plain = name.Replace('ø', 'o').Replace('Ø', 'O');
			return(Regex.IsMatch(line, "\\b" + Regex.Escape(plain) + "\\b", RegexOptions.IgnoreCase));
		}
	}
}
