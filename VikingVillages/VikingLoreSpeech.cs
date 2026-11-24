using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What living and dvergr vikings say to a player they know about the runestones of the biome they stand in - each
	/// line about what one of the game's own runestones there says, from the boar stone of the Meadows to the stones
	/// of the Deep North
	/// </summary>
	internal static class VikingLoreSpeech
	{
		/// <summary>
		/// How likely a viking talks about a runestone when a player it knows talks to it, and its biome has any
		/// </summary>
		private const float ChatChance = 0.2f;

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
		/// Picks what a viking says about a runestone of the biome it stands in when a player it knows talks to it - now
		/// and then, when its biome has any
		/// </summary>
		/// <param name="position">Where the viking stands</param>
		/// <param name="player">The player's name</param>
		/// <returns>The line, or null when it says something else</returns>
		public static string Chat(Vector3 position, string player)
		{
			if (Random.value >= ChatChance || WorldGenerator.instance == null)
			{
				return(null);
			}
			return(For(WorldGenerator.instance.GetBiome(position), player, (int count) => Random.Range(0, count)));
		}

		/// <summary>
		/// Picks a line about a runestone of a biome, filling in the player's name
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="player">The player's name</param>
		/// <param name="pick">Picks an index below the count it's given</param>
		/// <returns>The line, or null when the biome has none</returns>
		internal static string For(Heightmap.Biome biome, string player, System.Func<int, int> pick)
		{
			if (!Lines.TryGetValue(biome, out string[] lines))
			{
				return(null);
			}
			return(string.Format(lines[pick(lines.Length)], player));
		}
	}
}
