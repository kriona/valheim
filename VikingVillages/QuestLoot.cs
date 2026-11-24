using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What each quest brings back besides what its tools mine or cut - from the creatures it kills, the chests it
	/// opens and the plants it picks, each rolled from the game's own drops for them - and what the quests that trade
	/// with Haldor take along to spend
	/// </summary>
	/// <remarks>
	/// How many of each a quest kills, opens or picks goes by how many vikings went (see Sources). What's found is
	/// raised by the vikings' skill at the work (see QuestSim.SkillBonus) and scaled by the world's resource rate, and
	/// the kills are shared out among them, raising their weapon skills (see QuestGame.OutcomeFor). A quest whose drops
	/// can't be read brings back what it was for, as many as the village wanted
	/// </remarks>
	internal static class QuestLoot
	{
		/// <summary>
		/// How much a viking's weapon skill rises for each enemy it kills on a quest, in hits
		/// </summary>
		public const float HitsPerKill = 4f;

		/// <summary>
		/// How many of each thing a quest's vikings kill, open or pick, each - a creature, a chest or a plant by its
		/// prefab name - and how many it makes at least, whatever the number of vikings
		/// </summary>
		internal struct LootSource
		{
			public string Prefab;
			public float Each;
			public int Least;
			public bool Kill;

			/// <summary>
			/// Names a source
			/// </summary>
			/// <param name="prefab">The creature's, chest's or plant's prefab name</param>
			/// <param name="each">How many for each viking</param>
			/// <param name="least">How many at least</param>
			/// <param name="kill">Whether it's a creature killed</param>
			public LootSource(string prefab, float each, int least, bool kill)
			{
				Prefab = prefab;
				Each = each;
				Least = least;
				Kill = kill;
			}

			/// <summary>
			/// Works out how many of it a group kills, opens or picks
			/// </summary>
			/// <param name="vikings">How many went</param>
			/// <returns>How many</returns>
			public int CountFor(int vikings)
			{
				return(System.Math.Max(Least, (int)System.Math.Round(Each * vikings)));
			}
		}

		/// <summary>
		/// The prefab names of the forage that grows back, by the biome it grows in, by VikingGear's order
		/// </summary>
		private static readonly string[][] Forage =
		{
			new[] { "Raspberry", "Mushroom", "Dandelion" },
			new[] { "Blueberries", "Thistle", "MushroomYellow" },
			new string[0],
			new string[0],
			new[] { "Cloudberry" }
		};

		/// <summary>
		/// How many plants each viking picks foraging, and how many of their fruit each gives at most
		/// </summary>
		private const int PicksEach = 12;
		private const int PerPickMax = 2;

		/// <summary>
		/// How many prey each viking takes hunting
		/// </summary>
		private const float KillsEach = 3f;

		/// <summary>
		/// What a wild beehive gives when it's found - queen bees and honey
		/// </summary>
		private static readonly LootTable WildHive = new LootTable
		{
			Picks = false,
			Drops = new List<LootDrop>
			{
				new LootDrop { Item = "QueenBee", Min = 1, Max = 2 },
				new LootDrop { Item = "Honey", Min = 3, Max = 6 }
			}
		};

		/// <summary>
		/// Lists what a quest kills, opens and picks besides hunting and foraging - the chests and creatures of the place
		/// it goes to
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>The sources, empty for a quest with none</returns>
		internal static List<LootSource> Sources(QuestKind kind)
		{
			switch (kind)
			{
				case QuestKind.RaidCrypts:
					return(new List<LootSource> { new LootSource("TreasureChest_sunkencrypt", 1f, 1, false), new LootSource("Draugr", 2f, 2, true), new LootSource("Blob", 1f, 1, true) });
				case QuestKind.RaidBurialChambers:
					return(new List<LootSource> { new LootSource("TreasureChest_forestcrypt", 2f, 2, false), new LootSource("Skeleton", 2f, 2, true), new LootSource("Pickable_SurtlingCoreStand", 1f, 1, false) });
				case QuestKind.HuntTrolls:
					return(new List<LootSource> { new LootSource("Troll", 0.5f, 1, true) });
				case QuestKind.HuntGoblins:
					return(new List<LootSource> { new LootSource("Goblin", 2f, 2, true), new LootSource("GoblinArcher", 1f, 1, true), new LootSource("GoblinBrute", 0.34f, 1, true) });
				case QuestKind.KillEikthyr:
					return(new List<LootSource> { new LootSource("Eikthyr", 0f, 1, true) });
				case QuestKind.HuntGhosts:
					return(new List<LootSource> { new LootSource("Ghost", 2f, 2, true), new LootSource("Skeleton", 1f, 1, true) });
				default:
					return(new List<LootSource>());
			}
		}

		/// <summary>
		/// Gets the skill a quest's finds go by - hunting by the bow, foraging by farming, and the rest by none, as what
		/// they find is what the place has
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>The skill, or None</returns>
		internal static Skills.SkillType FindSkill(QuestKind kind)
		{
			switch (kind)
			{
				case QuestKind.Hunt:
					return(Skills.SkillType.Bows);
				case QuestKind.Forage:
				case QuestKind.FindBees:
					return(Skills.SkillType.Farming);
				default:
					return(Skills.SkillType.None);
			}
		}

		/// <summary>
		/// Gets the forage that grows in the biomes a village can forage in - its own and every one before it
		/// </summary>
		/// <param name="biome">The village's biome, by VikingGear's order</param>
		/// <returns>The prefab names</returns>
		internal static List<string> ForageFor(int biome)
		{
			List<string> found = new List<string>();
			for (int i = 0; i <= biome && i < Forage.Length; i++)
			{
				found.AddRange(Forage[i]);
			}
			return(found);
		}

		/// <summary>
		/// Works out what a quest brings back besides what its tools get (see QuestGame.Gather) - hunting the prey of the
		/// village's biome and those before it, foraging what grows there, finding wild bees, or the chests and creatures
		/// of the place it goes to - raised by the vikings' skill and scaled by the world's resource rate, sharing out the
		/// kills, and for the quests trading with Haldor, what the first viking takes along to spend
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="record">The quest, its plans made</param>
		/// <param name="members">Its vikings</param>
		/// <param name="found">What it brings back, added to</param>
		public static void Roll(Village village, QuestRecord record, List<Humanoid> members, Dictionary<string, int> found)
		{
			if (record.Kind == QuestKind.SellTreasure || record.Kind == QuestKind.BuyCirclet)
			{
				PlanSpending(village, record);
				return;
			}
			System.Random rng = new System.Random(record.Id.GetHashCode());
			Dictionary<string, int> rolled = new Dictionary<string, int>();
			int kills = 0;
			int biome = VillageCooking.VillageBiome(village);
			if (record.Kind == QuestKind.Hunt)
			{
				kills += RollHunt(biome, Mathf.RoundToInt(KillsEach * members.Count), rng, rolled);
			}
			else if (record.Kind == QuestKind.Forage)
			{
				RollForage(biome, PicksEach * members.Count, rng, rolled);
			}
			else if (record.Kind == QuestKind.FindBees)
			{
				for (int i = 0; i < members.Count; i++)
				{
					QuestSim.Roll(WildHive, rng, rolled);
				}
			}
			foreach (LootSource source in Sources(record.Kind))
			{
				LootTable table = TableOf(source.Prefab);
				int count = source.CountFor(members.Count);
				for (int i = 0; i < count; i++)
				{
					QuestSim.Roll(table, rng, rolled);
				}
				kills += (source.Kill ? count : 0);
			}
			Skills.SkillType skill = FindSkill(record.Kind);
			if (skill != Skills.SkillType.None)
			{
				QuestSim.ApplyBonus(rolled, QuestSim.SkillBonus(AverageSkill(members, skill)));
			}
			// What a quest that takes a tool was for comes from the tools, not twice from the place
			bool tooled = (record.Plans.Exists((MemberPlan plan) => plan.Hits > 0));
			if (tooled)
			{
				rolled.Remove(record.Item);
			}
			foreach (KeyValuePair<string, int> entry in rolled)
			{
				QuestSim.Add(found, entry.Key, QuestGame.Scale(entry.Key, entry.Value));
			}
			if (!tooled && record.Item.Length > 0 && !found.ContainsKey(record.Item) && rolled.Count == 0)
			{
				QuestSim.Add(found, record.Item, QuestGame.Scale(record.Item, Mathf.Max(1, record.Amount)));
			}
			for (int i = 0; i < record.Plans.Count && kills > 0; i++)
			{
				record.Plans[i].Kills = kills / record.Plans.Count + ((i < kills % record.Plans.Count) ? 1 : 0);
			}
		}

		/// <summary>
		/// Rolls a hunt - each kill one of the prey of the village's biome and those before it, picked at random, and what
		/// it drops
		/// </summary>
		/// <param name="biome">The village's biome, by VikingGear's order</param>
		/// <param name="kills">How many prey are taken</param>
		/// <param name="rng">The random numbers</param>
		/// <param name="rolled">What's found, added to</param>
		/// <returns>How many were taken</returns>
		private static int RollHunt(int biome, int kills, System.Random rng, Dictionary<string, int> rolled)
		{
			List<string> prey = new List<string>();
			foreach (PreyKind kind in HuntPrey.Kinds)
			{
				if (kind.Prefab != HuntPrey.FoughtOnTheWay && VikingGear.IndexOf(kind.Biome) <= biome)
				{
					prey.Add(kind.Prefab);
				}
			}
			if (prey.Count == 0)
			{
				return(0);
			}
			for (int i = 0; i < kills; i++)
			{
				QuestSim.Roll(TableOf(prey[rng.Next(prey.Count)]), rng, rolled);
			}
			return(kills);
		}

		/// <summary>
		/// Rolls foraging - each pick one of the forage of the village's biome and those before it, a few of its fruit
		/// </summary>
		/// <param name="biome">The village's biome, by VikingGear's order</param>
		/// <param name="picks">How many plants are picked</param>
		/// <param name="rng">The random numbers</param>
		/// <param name="rolled">What's found, added to</param>
		private static void RollForage(int biome, int picks, System.Random rng, Dictionary<string, int> rolled)
		{
			List<string> forage = ForageFor(biome);
			if (forage.Count == 0)
			{
				return;
			}
			for (int i = 0; i < picks; i++)
			{
				QuestSim.Add(rolled, forage[rng.Next(forage.Count)], rng.Next(1, PerPickMax + 1));
			}
		}

		/// <summary>
		/// Reads what a creature, chest or plant drops from its prefab - a creature's drops, a chest's default items, or a
		/// plant's fruit
		/// </summary>
		/// <param name="prefab">The prefab name</param>
		/// <returns>The table, empty when it can't be read</returns>
		private static LootTable TableOf(string prefab)
		{
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(prefab) : null);
			if (found == null)
			{
				return(new LootTable());
			}
			CharacterDrop drops = found.GetComponent<CharacterDrop>();
			if (drops != null)
			{
				return(QuestGame.FromCharacterDrop(drops));
			}
			Container chest = found.GetComponent<Container>();
			if (chest != null)
			{
				return(QuestGame.FromDropTable(chest.m_defaultItems));
			}
			Pickable pickable = found.GetComponent<Pickable>();
			LootTable table = new LootTable { Picks = false };
			if (pickable != null && pickable.m_itemPrefab != null)
			{
				table.Drops.Add(new LootDrop { Item = pickable.m_itemPrefab.name, Min = pickable.m_amount, Max = pickable.m_amount });
			}
			return(table);
		}

		/// <summary>
		/// Works out the average level of a skill among a quest's vikings
		/// </summary>
		/// <param name="members">The vikings</param>
		/// <param name="skill">The skill</param>
		/// <returns>The level, from 0 to 100</returns>
		private static float AverageSkill(List<Humanoid> members, Skills.SkillType skill)
		{
			if (members.Count == 0)
			{
				return(0f);
			}
			float total = 0f;
			foreach (Humanoid member in members)
			{
				total += member.GetSkillFactor(skill) * 100f;
			}
			return(total / members.Count);
		}

		/// <summary>
		/// Has the first of a quest trading with Haldor take along what it spends - every piece of treasure in the
		/// village's chests to sell, and to buy a circlet, the coins too
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="record">The quest, its plans made</param>
		private static void PlanSpending(Village village, QuestRecord record)
		{
			if (record.Plans.Count == 0)
			{
				return;
			}
			Dictionary<string, int> spend = new Dictionary<string, int>();
			foreach (Container chest in VillageLife.VillageChests(village))
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					string name = ((item.m_dropPrefab != null && !VikingItems.IsCarried(item)) ? item.m_dropPrefab.name : null);
					bool treasure = (name != null && System.Array.IndexOf(ResourceBook.Treasure, name) >= 0);
					bool coins = (name == ResourceBook.Coins && record.Kind == QuestKind.BuyCirclet);
					if (treasure || coins)
					{
						QuestSim.Add(spend, name, item.m_stack);
					}
				}
			}
			record.Plans[0].Spend = new List<KeyValuePair<string, int>>(spend);
		}

		/// <summary>
		/// Works out what a viking trading with Haldor brings home for what it took along - coins for the treasure sold,
		/// and buying a circlet, the circlet and the coins left over once it's paid for, or nothing at all when what it
		/// took won't pay for it
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="coins">The coins it took along</param>
		/// <param name="treasureValue">What the treasure it took along sells for</param>
		/// <param name="price">What a circlet costs</param>
		/// <param name="spends">Set to whether it spends what it took along</param>
		/// <returns>What it brings home, each item and how many</returns>
		internal static List<KeyValuePair<string, int>> Trade(QuestKind kind, int coins, int treasureValue, int price, out bool spends)
		{
			List<KeyValuePair<string, int>> loot = new List<KeyValuePair<string, int>>();
			int purse = coins + treasureValue;
			spends = false;
			if (kind == QuestKind.SellTreasure)
			{
				spends = (treasureValue > 0);
				if (spends)
				{
					loot.Add(new KeyValuePair<string, int>(ResourceBook.Coins, purse));
				}
				return(loot);
			}
			if (kind != QuestKind.BuyCirclet || purse < price)
			{
				return(loot);
			}
			spends = true;
			loot.Add(new KeyValuePair<string, int>(ResourceBook.Circlet, 1));
			if (purse > price)
			{
				loot.Add(new KeyValuePair<string, int>(ResourceBook.Coins, purse - price));
			}
			return(loot);
		}
	}
}
