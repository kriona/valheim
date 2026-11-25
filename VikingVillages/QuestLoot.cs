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
		/// The forage that's no food, which foraging for food leaves where it grows
		/// </summary>
		private static readonly string[] NotFood = { "Dandelion", "Thistle" };

		/// <summary>
		/// The birds a hunt shoots for what they drop, by the biome it hunts in, by VikingGear's order - never for food,
		/// as they drop none. Seagulls fly over the Meadows, the Black Forest and the Plains, as the game's spawn list has
		/// them, crows over the Meadows and the Black Forest, and ash crows over the Ashlands
		/// </summary>
		private static readonly string[][] Birds =
		{
			new[] { "Crow", "Seagal" },
			new[] { "Crow", "Seagal" },
			new string[0],
			new string[0],
			new[] { "Seagal" },
			new string[0],
			new[] { "AshCrow" }
		};

		/// <summary>
		/// The prey that take a hunter more or less time than most, as how many prey's time each takes - the Meadows' boar,
		/// necks and deer two thirds, so half as many again are taken in a day, a lox, which drops a lot, 8/3, so 3 are
		/// taken in 8 prey's time, and the Deep North's moose and seals 2. Those that take longer are come across that much
		/// less often on the road
		/// </summary>
		private static readonly Dictionary<string, float> PreyEffort = new Dictionary<string, float>
		{
			{ "Boar", 2f / 3f },
			{ "Neck", 2f / 3f },
			{ "Deer", 2f / 3f },
			{ "Lox", 8f / 3f },
			{ "Moose", 2f },
			{ "Seal", 2f }
		};

		/// <summary>
		/// How far short of a whole prey's time a hunter's time can be and still count as it - so three two thirds make 2
		/// whatever the rounding
		/// </summary>
		private const float EffortSlack = 0.01f;

		/// <summary>
		/// How many of its fruit each plant picked foraging gives at most - how many plants each viking picks being the
		/// daylight's (see QuestRules.ForagePicksBeforeNight)
		/// </summary>
		private const int PerPickMax = 2;

		/// <summary>
		/// The bosses' trophies, which the vikings never bring home from a quest - Eikthyr's, the Elder's, Bonemass's,
		/// Moder's, Yagluth's, the Queen's and Fader's
		/// </summary>
		internal static readonly string[] BossTrophies = { "TrophyEikthyr", "TrophyTheElder", "TrophyBonemass", "TrophyDragonQueen", "TrophyGoblinKing", "TrophySeekerQueen", "TrophyFader" };

		/// <summary>
		/// The yellow mushroom growing in burial chambers and sunken crypts, which a raid on either picks
		/// </summary>
		private const string YellowMushroom = "Pickable_Mushroom_yellow";

		/// <summary>
		/// The plants a quest picks besides foraging, each of which raises the Farming skill of the viking that picks it,
		/// the way a pick raises a viking's Farming in its village (see VillageLife.PickForage)
		/// </summary>
		private static readonly string[] PickedPlants = { YellowMushroom };

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
		/// it goes to, every creature they meet there, which they fight (see QuestFightGame), and bring home the drops of
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>The sources, empty for a quest with none</returns>
		internal static List<LootSource> Sources(QuestKind kind)
		{
			switch (kind)
			{
				case QuestKind.RaidCrypts:
					return(new List<LootSource> { new LootSource("TreasureChest_sunkencrypt", 1f, 1, false), new LootSource("Draugr", 3f, 3, true), new LootSource("Draugr_Elite", 0.5f, 1, true), new LootSource("Blob", 1f, 1, true), new LootSource(YellowMushroom, 2f, 2, false) });
				case QuestKind.RaidBurialChambers:
					return(new List<LootSource> { new LootSource("TreasureChest_forestcrypt", 2f, 2, false), new LootSource("Skeleton", 4f, 4, true), new LootSource("Skeleton_Poison", 1f, 1, true), new LootSource("Ghost", 0.5f, 1, true), new LootSource("Pickable_SurtlingCoreStand", 1f, 1, false), new LootSource(YellowMushroom, 2f, 2, false) });
				case QuestKind.RaidFrostCaves:
					return(new List<LootSource> { new LootSource("TreasureChest_mountaincave", 1f, 1, false), new LootSource("Pickable_MountainCaveCrystal", 1f, 1, false), new LootSource("Ulv", 3f, 3, true), new LootSource("Fenring_Cultist", 1f, 1, true), new LootSource("Bat", 2f, 2, true) });
				case QuestKind.HuntTrolls:
					return(new List<LootSource> { new LootSource("Troll", 0.5f, 1, true) });
				case QuestKind.HuntGoblins:
					return(new List<LootSource> { new LootSource("Goblin", 2f, 2, true), new LootSource("GoblinArcher", 1f, 1, true), new LootSource("GoblinBrute", 0.34f, 1, true) });
				case QuestKind.KillEikthyr:
					return(new List<LootSource> { new LootSource("Eikthyr", 0f, 1, true) });
				case QuestKind.HuntGhosts:
					return(new List<LootSource> { new LootSource("Ghost", 2f, 2, true), new LootSource("Skeleton", 3f, 3, true), new LootSource("Skeleton_Poison", 1f, 1, true) });
				default:
					return(new List<LootSource>());
			}
		}

		/// <summary>
		/// Works out how many plants one of a quest's vikings picks besides foraging (see PickedPlants) - each plant's
		/// count shared out among them, the first of them picking one more of what doesn't share out evenly
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="vikings">How many went</param>
		/// <param name="index">The viking's place among them, from 0</param>
		/// <returns>How many it picks</returns>
		internal static int PlantsPicked(QuestKind kind, int vikings, int index)
		{
			if (vikings <= 0 || index < 0 || index >= vikings)
			{
				return(0);
			}
			int picked = 0;
			foreach (LootSource source in Sources(kind))
			{
				if (System.Array.IndexOf(PickedPlants, source.Prefab) < 0)
				{
					continue;
				}
				int count = source.CountFor(vikings);
				picked += count / vikings + ((index < count % vikings) ? 1 : 0);
			}
			return(picked);
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
		/// Gets the biome a hunt or foraging goes to - going for an item, the easiest biome no harder than the village's
		/// whose prey or plants give it, and going for food, the hardest no harder than the village's whose prey or plants
		/// are food - or the village's own when none does
		/// </summary>
		/// <param name="item">What it's for, or "" for food</param>
		/// <param name="villageBiome">The village's biome, by VikingGear's order</param>
		/// <param name="gives">Checks whether a biome's prey or plants give an item, or for "", food</param>
		/// <returns>The biome, by VikingGear's order</returns>
		internal static int TargetBiome(string item, int villageBiome, System.Func<int, string, bool> gives)
		{
			if (item.Length > 0)
			{
				for (int biome = 0; biome <= villageBiome; biome++)
				{
					if (gives(biome, item))
					{
						return(biome);
					}
				}
				return(villageBiome);
			}
			for (int biome = villageBiome; biome >= 0; biome--)
			{
				if (gives(biome, item))
				{
					return(biome);
				}
			}
			return(villageBiome);
		}

		/// <summary>
		/// Gets the biome a quest hunting or foraging goes to (see TargetBiome)
		/// </summary>
		/// <param name="kind">The quest, hunting or foraging</param>
		/// <param name="item">What it's for, or "" for food</param>
		/// <param name="villageBiome">The village's biome, by VikingGear's order</param>
		/// <returns>The biome, by VikingGear's order</returns>
		internal static int BiomeFor(QuestKind kind, string item, int villageBiome)
		{
			if (kind == QuestKind.Hunt)
			{
				return(TargetBiome(item, villageBiome, (int biome, string wanted) => PreyFor(biome, wanted).Count > 0));
			}
			return(TargetBiome(item, villageBiome, (int biome, string wanted) => PlantsFor(biome, wanted).Count > 0));
		}

		/// <summary>
		/// Lists every biome a hunt or foraging could go to - going for an item, each one no harder than the village's
		/// whose prey or plants give it, like any with birds for feathers, for the road search to pick the one it gets to
		/// soonest, and going for food, the one TargetBiome picks
		/// </summary>
		/// <param name="item">What it's for, or "" for food</param>
		/// <param name="villageBiome">The village's biome, by VikingGear's order</param>
		/// <param name="gives">Checks whether a biome's prey or plants give an item, or for "", food</param>
		/// <returns>The biomes, by VikingGear's order, easiest first, never none</returns>
		internal static List<int> TargetBiomes(string item, int villageBiome, System.Func<int, string, bool> gives)
		{
			List<int> biomes = new List<int>();
			for (int biome = 0; biome <= villageBiome && item.Length > 0; biome++)
			{
				if (gives(biome, item))
				{
					biomes.Add(biome);
				}
			}
			if (biomes.Count == 0)
			{
				biomes.Add(TargetBiome(item, villageBiome, gives));
			}
			return(biomes);
		}

		/// <summary>
		/// Lists every biome a quest hunting or foraging could go to (see TargetBiomes)
		/// </summary>
		/// <param name="kind">The quest, hunting or foraging</param>
		/// <param name="item">What it's for, or "" for food</param>
		/// <param name="villageBiome">The village's biome, by VikingGear's order</param>
		/// <returns>The biomes, by VikingGear's order, easiest first</returns>
		internal static List<int> BiomesFor(QuestKind kind, string item, int villageBiome)
		{
			if (kind == QuestKind.Hunt)
			{
				return(TargetBiomes(item, villageBiome, (int biome, string wanted) => PreyFor(biome, wanted).Count > 0));
			}
			return(TargetBiomes(item, villageBiome, (int biome, string wanted) => PlantsFor(biome, wanted).Count > 0));
		}

		/// <summary>
		/// Gets the biome a quest hunting or foraging works in - the one its road went to, when it's one it could go to,
		/// or else the one BiomeFor picks
		/// </summary>
		/// <param name="kind">The quest, hunting or foraging</param>
		/// <param name="item">What it's for, or "" for food</param>
		/// <param name="villageBiome">The village's biome, by VikingGear's order</param>
		/// <param name="roadBiome">The biome its road went to, by VikingGear's order, or -1 when it isn't known</param>
		/// <returns>The biome, by VikingGear's order</returns>
		internal static int WorksIn(QuestKind kind, string item, int villageBiome, int roadBiome)
		{
			bool known = (roadBiome >= 0 && BiomesFor(kind, item, villageBiome).Contains(roadBiome));
			return(known ? roadBiome : BiomeFor(kind, item, villageBiome));
		}

		/// <summary>
		/// Lists the plants foraging in a biome picks - the one it's for, or going for food, those that are food
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <param name="item">What it's for, or "" for food</param>
		/// <returns>The prefab names, none when nothing there gives it</returns>
		internal static List<string> PlantsFor(int biome, string item)
		{
			List<string> plants = new List<string>();
			if (biome < 0 || biome >= Forage.Length)
			{
				return(plants);
			}
			foreach (string plant in Forage[biome])
			{
				bool wanted = ((item.Length > 0) ? (plant == item) : (System.Array.IndexOf(NotFood, plant) < 0));
				if (wanted)
				{
					plants.Add(plant);
				}
			}
			return(plants);
		}

		/// <summary>
		/// Lists the prey a hunt in a biome takes - those that drop what it's for, birds among them, or going for food,
		/// those that drop food, or with null, any prey there
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <param name="item">What it's for, "" for food, or null for anything</param>
		/// <returns>The prefab names, none when no prey there gives it or the drops can't be read</returns>
		internal static List<string> PreyFor(int biome, string item)
		{
			List<string> prey = new List<string>();
			if (ZNetScene.instance == null)
			{
				return(prey);
			}
			foreach (PreyKind kind in HuntPrey.Kinds)
			{
				if (kind.Prefab != HuntPrey.FoughtOnTheWay && VikingGear.IndexOf(kind.Biome) == biome && Drops(kind.Prefab, item))
				{
					prey.Add(kind.Prefab);
				}
			}
			if (!string.IsNullOrEmpty(item) && biome >= 0 && biome < Birds.Length)
			{
				prey.AddRange(System.Array.FindAll(Birds[biome], (string bird) => Drops(bird, item)));
			}
			return(prey);
		}

		/// <summary>
		/// Checks whether a creature drops an item, or something cooked or eaten as food
		/// </summary>
		/// <param name="prefab">The creature's prefab name</param>
		/// <param name="item">The item, "" for food, or null for anything</param>
		/// <returns>True when it does</returns>
		private static bool Drops(string prefab, string item)
		{
			foreach (LootDrop drop in TableOf(prefab).Drops)
			{
				bool wanted = ((item == null) || ((item.Length > 0) ? (drop.Item == item) : IsFood(drop.Item)));
				if (wanted)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether an item is food - cooked first, or eaten as it is
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <returns>True when it is</returns>
		private static bool IsFood(string item)
		{
			return(VillageCooking.IsRaw(item) || TripForage.Edible(item));
		}

		/// <summary>
		/// Works out how much food a kill drops on average - each drop that's food by its chance and its average amount
		/// </summary>
		/// <param name="table">What the kill drops</param>
		/// <param name="isFood">Checks whether an item is food</param>
		/// <returns>How much food, 0 for none</returns>
		internal static float FoodOf(LootTable table, System.Func<string, bool> isFood)
		{
			float food = 0f;
			foreach (LootDrop drop in table.Drops)
			{
				if (isFood(drop.Item))
				{
					food += drop.Chance * (drop.Min + System.Math.Max(drop.Min, drop.Max)) / 2f;
				}
			}
			return(food * table.Chance);
		}

		/// <summary>
		/// Picks one of several at random by their weights, or evenly when they have none
		/// </summary>
		/// <param name="weights">Each one's weight, or null for even</param>
		/// <param name="count">How many there are</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The index of the one picked</returns>
		internal static int PickWeighted(List<float> weights, int count, double roll)
		{
			float total = 0f;
			for (int i = 0; weights != null && i < weights.Count; i++)
			{
				total += System.Math.Max(0f, weights[i]);
			}
			if (total <= 0f)
			{
				return(System.Math.Min(count - 1, (int)(roll * count)));
			}
			double at = roll * total;
			for (int i = 0; i < weights.Count; i++)
			{
				at -= System.Math.Max(0f, weights[i]);
				if (at < 0.0)
				{
					return(i);
				}
			}
			return(weights.Count - 1);
		}

		/// <summary>
		/// Gets how many prey's time a prey takes a hunter (see PreyEffort)
		/// </summary>
		/// <param name="prefab">The prey's prefab name</param>
		/// <returns>How many, 1 for most</returns>
		internal static float EffortOf(string prefab)
		{
			return(PreyEffort.TryGetValue(prefab, out float effort) ? effort : 1f);
		}

		/// <summary>
		/// Checks whether a hunter has time left for another prey - always for its first, however long it takes
		/// </summary>
		/// <param name="spent">How many prey's time it has spent</param>
		/// <param name="effort">How many prey's time the next takes (see EffortOf)</param>
		/// <param name="most">The most prey's time it spends</param>
		/// <param name="taken">How many it has taken</param>
		/// <returns>True when it has</returns>
		internal static bool HasTimeFor(float spent, float effort, int most, int taken)
		{
			return(taken == 0 || spent + effort <= most + EffortSlack);
		}

		/// <summary>
		/// Rolls what a creature drops when it's killed
		/// </summary>
		/// <param name="prefab">The creature's prefab name</param>
		/// <param name="rng">The random numbers</param>
		/// <param name="rolled">What's found, added to</param>
		internal static void RollKill(string prefab, System.Random rng, Dictionary<string, int> rolled)
		{
			QuestSim.Roll(TableOf(prefab), rng, rolled);
		}

		/// <summary>
		/// Works out what a quest brings back besides what its tools get (see QuestGame.Gather) - hunting the prey of the
		/// biome it goes to, foraging what grows there, finding wild bees, or the chests and creatures of the place it goes
		/// to - each viking's hunting, foraging or finding raised by its own skill, all of it scaled by the world's
		/// resource rate, sharing out the kills, and for the quests trading with Haldor, what the first viking takes along
		/// to spend
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="record">The quest, its plans made</param>
		/// <param name="members">Its vikings</param>
		/// <param name="spaces">The room each of them has to carry things home, in the order of the members</param>
		/// <param name="most">For a hunt, the most prey each hunter takes, and for foraging, how many plants each forager
		/// picks - as many as there's daylight for</param>
		/// <param name="found">What it brings back, added to</param>
		/// <param name="roadBiome">The biome its road went to, by VikingGear's order, or -1 when it isn't known</param>
		/// <returns>For a hunt, the most prey's time any hunter spent (see EffortOf), which the hunt takes as long as, or
		/// else 0</returns>
		public static int Roll(Village village, QuestRecord record, List<Humanoid> members, List<CarrySpace> spaces, int most, Dictionary<string, int> found, int roadBiome)
		{
			if (record.Kind == QuestKind.SellTreasure || record.Kind == QuestKind.BuyCirclet)
			{
				PlanSpending(village, record);
				return(0);
			}
			System.Random rng = new System.Random(record.Id.GetHashCode());
			Dictionary<string, int> rolled = new Dictionary<string, int>();
			int kills = 0;
			int mostTaken = 0;
			int[] taken = null;
			Skills.SkillType skill = FindSkill(record.Kind);
			if (record.Kind == QuestKind.Hunt || record.Kind == QuestKind.Forage)
			{
				int biome = WorksIn(record.Kind, record.Item, VillageCooking.VillageBiome(village), roadBiome);
				if (record.Kind == QuestKind.Hunt)
				{
					List<string> prey = Hunted(biome, record.Item);
					// Hunting for food, the prey that drop more meat are taken more often
					List<float> weights = ((record.Item.Length == 0) ? prey.ConvertAll((string kind) => FoodOf(TableOf(kind), IsFood)) : null);
					taken = RollHunts(prey, weights, members, skill, spaces, most, rng, rolled, out int[] spent);
					for (int i = 0; i < taken.Length; i++)
					{
						kills += taken[i];
						mostTaken = Mathf.Max(mostTaken, spent[i]);
					}
				}
				else
				{
					RollForage(PlantsFor(biome, record.Item), members, skill, most, rng, rolled);
				}
			}
			else if (record.Kind == QuestKind.FindBees)
			{
				for (int i = 0; i < members.Count; i++)
				{
					Dictionary<string, int> hive = new Dictionary<string, int>();
					QuestSim.Roll(WildHive, rng, hive);
					QuestSim.ApplyBonus(hive, BonusOf(members, i, skill));
					AddAll(hive, rolled);
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
			// A boss's trophy is the players' to win, not the vikings'
			foreach (string trophy in BossTrophies)
			{
				rolled.Remove(trophy);
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
			// Each hunter counts the prey it took, and the rest share their kills out
			for (int i = 0; i < record.Plans.Count && kills > 0; i++)
			{
				bool own = (taken != null && i < taken.Length);
				record.Plans[i].Kills = (own ? taken[i] : kills / record.Plans.Count + ((i < kills % record.Plans.Count) ? 1 : 0));
			}
			return(mostTaken);
		}

		/// <summary>
		/// Lists the prey a hunt in a biome takes (see PreyFor), or when none there gives what it's for, any prey there
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <param name="item">What it's for, or "" for food</param>
		/// <returns>The prefab names, none when the drops can't be read</returns>
		private static List<string> Hunted(int biome, string item)
		{
			List<string> prey = PreyFor(biome, item);
			return((prey.Count > 0) ? prey : PreyFor(biome, null));
		}

		/// <summary>
		/// Rolls a hunt for each hunter - a prey at a time, each taking as many prey's time as EffortOf says, until it has
		/// spent as long as there's daylight for, or once it has spent QuestRules.HuntKillsLeast, until what the next would
		/// drop is more than it has room left to carry - what each takes raised by its own skill
		/// </summary>
		/// <param name="prey">The prey they hunt, by prefab name</param>
		/// <param name="weights">How often each prey is taken, in the order of the prey, or null for evenly</param>
		/// <param name="members">The hunters</param>
		/// <param name="skill">The skill that raises what they take</param>
		/// <param name="spaces">The room each hunter has to carry things home, in the order of the hunters</param>
		/// <param name="most">The most prey's time each hunter spends</param>
		/// <param name="rng">The random numbers</param>
		/// <param name="rolled">What's found, added to</param>
		/// <param name="spentBy">Set to how many prey's time each hunter spent, in the order of the hunters</param>
		/// <returns>How many prey each hunter took, in the order of the hunters</returns>
		private static int[] RollHunts(List<string> prey, List<float> weights, List<Humanoid> members, Skills.SkillType skill, List<CarrySpace> spaces, int most, System.Random rng, Dictionary<string, int> rolled, out int[] spentBy)
		{
			int[] takenBy = new int[spaces.Count];
			spentBy = new int[spaces.Count];
			if (prey.Count == 0)
			{
				return(takenBy);
			}
			for (int i = 0; i < spaces.Count; i++)
			{
				float bonus = BonusOf(members, i, skill);
				Dictionary<string, int> haul = new Dictionary<string, int>();
				float carried = 0f;
				int taken = 0;
				float spent = 0f;
				while (spent < most - EffortSlack)
				{
					string kind = prey[PickWeighted(weights, prey.Count, rng.NextDouble())];
					float effort = EffortOf(kind);
					if (!HasTimeFor(spent, effort, most, taken))
					{
						break;
					}
					Dictionary<string, int> kill = new Dictionary<string, int>();
					RollKill(kind, rng, kill);
					float weight = 0f;
					foreach (KeyValuePair<string, int> drop in kill)
					{
						weight += QuestGame.WeightOf(drop.Key) * drop.Value * (1f + bonus);
					}
					if (spent >= QuestRules.HuntKillsLeast - EffortSlack && carried + weight > spaces[i].WeightRoom)
					{
						break;
					}
					AddAll(kill, haul);
					carried += weight;
					spent += effort;
					taken++;
				}
				QuestSim.ApplyBonus(haul, bonus);
				AddAll(haul, rolled);
				takenBy[i] = taken;
				spentBy[i] = Mathf.CeilToInt(spent - EffortSlack);
			}
			return(takenBy);
		}

		/// <summary>
		/// Rolls foraging for each forager - each pick one of the plants they're picking, a few of its fruit, what each
		/// picks raised by its own skill
		/// </summary>
		/// <param name="plants">The plants they pick, by prefab name</param>
		/// <param name="members">The foragers</param>
		/// <param name="skill">The skill that raises what they pick</param>
		/// <param name="picks">How many plants each picks</param>
		/// <param name="rng">The random numbers</param>
		/// <param name="rolled">What's found, added to</param>
		private static void RollForage(List<string> plants, List<Humanoid> members, Skills.SkillType skill, int picks, System.Random rng, Dictionary<string, int> rolled)
		{
			if (plants.Count == 0)
			{
				return;
			}
			for (int i = 0; i < members.Count; i++)
			{
				Dictionary<string, int> haul = new Dictionary<string, int>();
				for (int pick = 0; pick < picks; pick++)
				{
					QuestSim.Add(haul, plants[rng.Next(plants.Count)], rng.Next(1, PerPickMax + 1));
				}
				QuestSim.ApplyBonus(haul, BonusOf(members, i, skill));
				AddAll(haul, rolled);
			}
		}

		/// <summary>
		/// Works out how much more one of a quest's vikings finds by its skill (see QuestSim.SkillBonus)
		/// </summary>
		/// <param name="members">The quest's vikings</param>
		/// <param name="index">Which of them</param>
		/// <param name="skill">The skill, or None</param>
		/// <returns>The share more</returns>
		private static float BonusOf(List<Humanoid> members, int index, Skills.SkillType skill)
		{
			bool skilled = (skill != Skills.SkillType.None && index < members.Count && members[index] != null);
			return(skilled ? QuestSim.SkillBonus(members[index].GetSkillFactor(skill) * 100f) : 0f);
		}

		/// <summary>
		/// Adds everything found in one tally to another
		/// </summary>
		/// <param name="from">What's added</param>
		/// <param name="to">What it's added to</param>
		private static void AddAll(Dictionary<string, int> from, Dictionary<string, int> to)
		{
			foreach (KeyValuePair<string, int> entry in from)
			{
				QuestSim.Add(to, entry.Key, entry.Value);
			}
		}

		/// <summary>
		/// Reads what a creature, chest or plant drops from its prefab - a creature's drops, a chest's default items, what
		/// a bird drops when it's shot, or a plant's fruit
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
			DropOnDestroyed dropped = found.GetComponent<DropOnDestroyed>();
			if (dropped != null)
			{
				return(QuestGame.FromDropTable(dropped.m_dropWhenDestroyed));
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
