using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// Which quests a village's NPC vikings go on, and who of its vikings goes first - NPC vikings forage and find bees
	/// before PC vikings, and a trip to a trader takes one PC viking and NPC vikings for the rest
	/// </summary>
	internal static class NPCQuests
	{
		/// <summary>
		/// Checks whether NPC vikings go on a quest - foraging, finding bees, selling treasure and buying from Haldor, none
		/// of which fights or takes a weapon to the work
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>True when they do</returns>
		internal static bool Allows(QuestKind kind)
		{
			return(kind == QuestKind.Forage || kind == QuestKind.FindBees || TradesWith(kind));
		}

		/// <summary>
		/// Checks whether a quest is a trip to a trader - selling treasure or buying from Haldor
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>True when it is</returns>
		internal static bool TradesWith(QuestKind kind)
		{
			return(kind == QuestKind.SellTreasure || Buys(kind));
		}

		/// <summary>
		/// Checks whether a quest buys something from Haldor for the viking who lacks it
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>True when it does</returns>
		internal static bool Buys(QuestKind kind)
		{
			return(kind == QuestKind.BuyCirclet || kind == QuestKind.BuyBelt || kind == QuestKind.BuyWiderPockets || kind == QuestKind.BuyDeeperPockets);
		}

		/// <summary>
		/// Picks the vikings who go on a quest, from those fit to go in the order they'd go (see VillageQuests.Plan) - on a
		/// trip to Haldor to buy something, the one who lacks it first, a PC viking before an NPC viking, and on any trip
		/// to a trader one PC viking, with NPC vikings in every other place while there are any - and a trip to a trader
		/// never goes without a PC viking; NPC vikings first on a forage or bee quest; and the order as it is on any other
		/// </summary>
		/// <typeparam name="T">The vikings</typeparam>
		/// <param name="kind">The quest</param>
		/// <param name="fit">Those fit to go, in the order they'd go</param>
		/// <param name="npc">Whether a viking is an NPC viking</param>
		/// <param name="lacks">Whether a viking lacks what the quest buys</param>
		/// <param name="size">How many go</param>
		/// <returns>Those who go, the first of them the one the quest is for when it buys something, or none for a trip
		/// to a trader when no PC viking is fit to go</returns>
		internal static List<T> Pick<T>(QuestKind kind, List<T> fit, System.Predicate<T> npc, System.Predicate<T> lacks, int size) where T : class
		{
			List<T> left = new List<T>(fit);
			List<T> going = new List<T>();
			if (TradesWith(kind) && !left.Exists((T viking) => !npc(viking)))
			{
				return(going);
			}
			if (TradesWith(kind))
			{
				if (Buys(kind))
				{
					T buyer = (left.Find((T viking) => lacks(viking) && !npc(viking)) ?? left.Find((T viking) => lacks(viking)));
					if (buyer != null)
					{
						Take(buyer, left, going);
					}
				}
				if (going.Count < size && !going.Exists((T viking) => !npc(viking)))
				{
					T pc = left.Find((T viking) => !npc(viking));
					if (pc != null)
					{
						Take(pc, left, going);
					}
				}
			}
			bool npcFirst = (TradesWith(kind) || kind == QuestKind.Forage || kind == QuestKind.FindBees);
			if (npcFirst)
			{
				foreach (T viking in left.FindAll(npc))
				{
					if (going.Count < size)
					{
						Take(viking, left, going);
					}
				}
			}
			while (going.Count < size && left.Count > 0)
			{
				Take(left[0], left, going);
			}
			return(going);
		}

		/// <summary>
		/// Moves a viking from those still to pick to those going
		/// </summary>
		/// <typeparam name="T">The vikings</typeparam>
		/// <param name="viking">The viking</param>
		/// <param name="left">Those still to pick, taken from</param>
		/// <param name="going">Those going, added to</param>
		private static void Take<T>(T viking, List<T> left, List<T> going)
		{
			left.Remove(viking);
			going.Add(viking);
		}
	}
}
