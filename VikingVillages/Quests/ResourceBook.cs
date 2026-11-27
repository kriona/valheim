using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// The quests a village's vikings go out on, off out of every player's sight, for what they can't get at home
	/// </summary>
	internal enum QuestKind
	{
		None,
		Hunt,
		Forage,
		ChopTrees,
		MineCopper,
		MineTin,
		MineSilver,
		RaidCrypts,
		RaidBurialChambers,
		HuntTrolls,
		HuntGoblins,
		SellTreasure,
		BuyCirclet,
		KillEikthyr,
		FindBees,
		HuntGhosts,
		RaidFrostCaves,
		BuyBelt,
		BuyWiderPockets,
		BuyDeeperPockets
	}

	/// <summary>
	/// How a village gets an item - gathered around the village by the jobs it already has, crafted from a recipe,
	/// smelted from another item, or brought back from a quest
	/// </summary>
	internal enum SourceKind
	{
		Gather,
		Craft,
		Smelt,
		Quest
	}

	/// <summary>
	/// The kind of tool a quest takes for an item - none, a pickaxe for ore and a muddy scrap pile, or an axe for trees
	/// </summary>
	internal enum ToolKind
	{
		None,
		Pickaxe,
		Axe
	}

	/// <summary>
	/// One way a village gets an item
	/// </summary>
	internal class ResourceSource
	{
		public SourceKind Kind;

		/// <summary>
		/// For a craft or a smelt, the station's prefab name, or null for a recipe crafted anywhere, and the level the
		/// station has to be upgraded to
		/// </summary>
		public string Station;
		public int StationLevel;

		/// <summary>
		/// For a craft, what it takes - each item's prefab name and how many - and how many one craft makes
		/// </summary>
		public List<KeyValuePair<string, int>> Ingredients = new List<KeyValuePair<string, int>>();
		public int Amount = 1;

		/// <summary>
		/// For a smelt, the item it's smelted from, the fuel the station burns, or null for one that burns none, and how
		/// much fuel each item smelted takes
		/// </summary>
		public string From;
		public string Fuel;
		public int FuelPer;

		/// <summary>
		/// For a quest, which one, the easiest biome a village going for the item can be, by VikingGear's order, the tool
		/// it takes and the tool tier that tool needs (see HitData.CheckToolTier)
		/// </summary>
		public QuestKind Quest;
		public int MinBiome;
		public ToolKind Tool;
		public int ToolTier;

		/// <summary>
		/// For a quest, the prefab of the rock or tree whose minimum tool tier ToolTier is read from (see
		/// ResourceBookGame), or null
		/// </summary>
		public string ToolTarget;
	}

	/// <summary>
	/// What a quest takes besides the tool for the item it's going for - the easiest biome a village going on it can be,
	/// by VikingGear's order, whether it goes underground and takes a torch or a Dverger circlet, the stations the
	/// village has to have, the coins it takes, counting the treasure the village could sell, how many vikings go on it
	/// and how many can go with more tools, and the beehives the village has to have fewer of
	/// </summary>
	internal class QuestRule
	{
		public QuestKind Kind;
		public int MinBiome;
		public bool NeedsLight;
		public string[] Stations = new string[0];
		public int Coins;
		public int Vikings = 1;
		public int PlainsVikings = 1;
		public int MaxVikings = 1;
		public int HivesBelow;

		/// <summary>
		/// The most vikings that go from the Plains on, or 0 for the same as MaxVikings, and whether the extra vikings
		/// past those the quest needs go whenever the village can spare them, rather than only with more tools
		/// </summary>
		public int PlainsMaxVikings;
		public bool ExtrasJoin;

		/// <summary>
		/// What the quest uses up where it goes, by prefab name, or "" for nothing, and how many - like the deer trophies
		/// Eikthyr's altar takes to summon him, which the vikings take along from the village's chests and leave there
		/// </summary>
		public string Offering = "";
		public int OfferingCount;

		/// <summary>
		/// Gets how many vikings of a village go on the quest - more from the Plains on, where the monsters are worse
		/// </summary>
		/// <param name="biome">The village's biome, by VikingGear's order</param>
		/// <returns>How many</returns>
		public int VikingsFor(int biome)
		{
			return((biome >= ResourceBook.Plains) ? System.Math.Max(Vikings, PlainsVikings) : Vikings);
		}

		/// <summary>
		/// Gets the most vikings of a village that go on the quest - more from the Plains on, when the quest says so
		/// </summary>
		/// <param name="biome">The village's biome, by VikingGear's order</param>
		/// <returns>How many</returns>
		public int MostFor(int biome)
		{
			int most = ((biome >= ResourceBook.Plains && PlainsMaxVikings > 0) ? PlainsMaxVikings : MaxVikings);
			return(System.Math.Max(most, VikingsFor(biome)));
		}
	}

	/// <summary>
	/// A tool a village can make or have for a quest - its prefab name, its kind and its tier
	/// </summary>
	internal class ToolEntry
	{
		public string Prefab;
		public ToolKind Kind;
		public int Tier;
	}

	/// <summary>
	/// Every way a village gets each item - from the game's recipes and smelters, the items its jobs gather, and the
	/// quests that bring items back - with the rules of each quest and the tools quests take
	/// </summary>
	/// <remarks>
	/// The quest rules and the items each quest brings back are written here (see AddQuests). The recipes, smelters,
	/// tool tiers and the prices of what's bought from Haldor come from the game once it has loaded them (see ResourceBookGame),
	/// so a book made in a test has only what the test adds
	/// </remarks>
	internal class ResourceBook
	{
		/// <summary>
		/// The biomes, by VikingGear's order
		/// </summary>
		public const int Meadows = 0;
		public const int BlackForest = 1;
		public const int Swamp = 2;
		public const int Mountain = 3;
		public const int Plains = 4;
		public const int Mistlands = 5;
		public const int AshLands = 6;
		public const int DeepNorth = 7;

		/// <summary>
		/// The prefab names of the stations quests need, the light vikings carry underground, and the coins and treasure
		/// Haldor trades in
		/// </summary>
		public const string Smelter = "smelter";
		public const string Kiln = "charcoal_kiln";
		public const string BlastFurnace = "blastfurnace";
		public const string Torch = "Torch";
		public const string Circlet = "HelmetDverger";
		public const string Coins = "Coins";
		public static readonly string[] Treasure = { "Amber", "AmberPearl", "Ruby", "SilverNecklace" };

		/// <summary>
		/// What the quests to Haldor buy besides the circlet - the strength belt by its prefab name, and the wider and
		/// deeper pockets, which aren't items, by the keys Haldor sells them under (Trader.TradeItem.m_buyKey)
		/// </summary>
		public const string Belt = VikingUpgrades.BeltPrefab;
		public const string WiderPockets = "invslot1";
		public const string DeeperPockets = "invslot2";

		/// <summary>
		/// The quests that buy something from Haldor, in the order a village picks them
		/// </summary>
		public static readonly QuestKind[] Purchases = { QuestKind.BuyCirclet, QuestKind.BuyBelt, QuestKind.BuyWiderPockets, QuestKind.BuyDeeperPockets };

		/// <summary>
		/// Gets what a quest to Haldor buys - an item's prefab name or the key of the pockets (see WiderPockets)
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>What it buys, or null for a quest that buys nothing</returns>
		public static string Bought(QuestKind kind)
		{
			switch (kind)
			{
				case QuestKind.BuyCirclet:
					return(Circlet);
				case QuestKind.BuyBelt:
					return(Belt);
				case QuestKind.BuyWiderPockets:
					return(WiderPockets);
				case QuestKind.BuyDeeperPockets:
					return(DeeperPockets);
				default:
					return(null);
			}
		}

		/// <summary>
		/// Gets the quest that buys something from Haldor (see Bought)
		/// </summary>
		/// <param name="bought">The item's prefab name, or the key of the pockets</param>
		/// <returns>The quest, or None for something no quest buys</returns>
		public static QuestKind PurchaseOf(string bought)
		{
			foreach (QuestKind kind in Purchases)
			{
				if (Bought(kind) == bought)
				{
					return(kind);
				}
			}
			return(QuestKind.None);
		}

		/// <summary>
		/// Checks whether a quest buys pockets, which are sewn into the buyer's clothes, so each viking who needs them
		/// goes along itself
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>True when it does</returns>
		public static bool BuysPockets(QuestKind kind)
		{
			return(kind == QuestKind.BuyWiderPockets || kind == QuestKind.BuyDeeperPockets);
		}

		/// <summary>
		/// Checks whether a quest buys something from Haldor
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>True when it does</returns>
		public static bool IsPurchase(QuestKind kind)
		{
			return(Bought(kind) != null);
		}

		/// <summary>
		/// Checks whether a quest trades with Haldor - selling treasure, or buying something
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>True when it does</returns>
		public static bool IsTrade(QuestKind kind)
		{
			return(kind == QuestKind.SellTreasure || IsPurchase(kind));
		}

		/// <summary>
		/// Gets what a quest to Haldor brings home as items - the item it buys, and nothing for pockets, which are sewn
		/// into the buyer's clothes there
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>The item's prefab name, or null</returns>
		public static string BoughtItem(QuestKind kind)
		{
			return((kind == QuestKind.BuyWiderPockets || kind == QuestKind.BuyDeeperPockets) ? null : Bought(kind));
		}

		/// <summary>
		/// The fewest beehives a village has before it no longer goes looking for bees - as many as it builds
		/// </summary>
		public const int HivesWanted = VillageHives.HivesMax;

		/// <summary>
		/// The items the village's own jobs gather in and around it - wood, stone and flint from the ground, resin from
		/// what it picks up, and the food foraging picks. Honey comes from the village's beehives, but only once looking
		/// for bees is no longer an option (see AddQuests)
		/// </summary>
		public static readonly string[] Gathered = { "Wood", "Stone", "Flint", "Resin", "Raspberry", "Blueberries", "Mushroom", "MushroomYellow", "Dandelion", "Thistle", "Cloudberry" };

		/// <summary>
		/// The ways to get each item, by its prefab name, in the order they're tried
		/// </summary>
		public readonly Dictionary<string, List<ResourceSource>> Sources = new Dictionary<string, List<ResourceSource>>();

		/// <summary>
		/// The rules of each quest
		/// </summary>
		public readonly Dictionary<QuestKind, QuestRule> Quests = new Dictionary<QuestKind, QuestRule>();

		/// <summary>
		/// The pickaxes and axes the game has, easiest to make first
		/// </summary>
		public readonly List<ToolEntry> Tools = new List<ToolEntry>();

		/// <summary>
		/// Adds a way to get an item
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <param name="source">The way</param>
		public void Add(string item, ResourceSource source)
		{
			if (!Sources.TryGetValue(item, out List<ResourceSource> list))
			{
				list = new List<ResourceSource>();
				Sources[item] = list;
			}
			list.Add(source);
		}

		/// <summary>
		/// Gets the ways to get an item
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <returns>The ways, empty when there are none</returns>
		public List<ResourceSource> SourcesOf(string item)
		{
			return(Sources.TryGetValue(item, out List<ResourceSource> list) ? list : new List<ResourceSource>());
		}

		/// <summary>
		/// Adds a tool, keeping the list in order of tier
		/// </summary>
		/// <param name="prefab">The tool's prefab name</param>
		/// <param name="kind">Its kind</param>
		/// <param name="tier">Its tier</param>
		public void AddTool(string prefab, ToolKind kind, int tier)
		{
			Tools.RemoveAll((ToolEntry tool) => tool.Prefab == prefab);
			ToolEntry entry = new ToolEntry { Prefab = prefab, Kind = kind, Tier = tier };
			int at = Tools.FindIndex((ToolEntry tool) => tool.Tier > tier);
			Tools.Insert(((at < 0) ? Tools.Count : at), entry);
		}

		/// <summary>
		/// Adds the items the village's jobs gather (see Gathered)
		/// </summary>
		public void AddGathered()
		{
			foreach (string item in Gathered)
			{
				Add(item, new ResourceSource { Kind = SourceKind.Gather });
			}
		}

		/// <summary>
		/// Adds the quests' rules and the items each brings back - the tool tier of each item's tool left at 0 for the
		/// game's to be read in from its ToolTarget (see ResourceBookGame), and the price of each purchase at 0 for Haldor's
		/// </summary>
		public void AddQuests()
		{
			string[] mining = { Smelter, Kiln };
			// 1-3 hunters, 3-5 from the Plains on, the extras going whenever the village can spare them
			QuestRule hunt = Rule(QuestKind.Hunt, Meadows, 1, 3, 3, false, new string[0]);
			hunt.PlainsMaxVikings = 5;
			hunt.ExtrasJoin = true;
			Rule(QuestKind.Forage, Meadows, 1, 2, 2, false, new string[0]);
			Rule(QuestKind.ChopTrees, Meadows, 1, 2, 3, false, new string[0]);
			Rule(QuestKind.MineCopper, BlackForest, 2, 2, 4, false, mining);
			Rule(QuestKind.MineTin, BlackForest, 2, 2, 4, false, mining);
			Rule(QuestKind.MineSilver, Mountain, 3, 3, 5, false, mining);
			Rule(QuestKind.RaidCrypts, Swamp, 3, 3, 5, true, mining);
			Rule(QuestKind.RaidBurialChambers, BlackForest, 2, 2, 2, true, new string[0]);
			Rule(QuestKind.HuntTrolls, BlackForest, 3, 3, 3, true, new string[0]);
			Rule(QuestKind.HuntGoblins, Plains, 3, 3, 5, false, new[] { BlastFurnace, Kiln });
			Rule(QuestKind.SellTreasure, Meadows, 2, 2, 2, false, new string[0]);
			Rule(QuestKind.BuyCirclet, BlackForest, 2, 2, 2, false, new string[0]);
			Rule(QuestKind.BuyBelt, BlackForest, 2, 2, 2, false, new string[0]);
			Rule(QuestKind.BuyWiderPockets, Mountain, 2, 2, 2, false, new string[0]);
			Rule(QuestKind.BuyDeeperPockets, Mistlands, 2, 2, 2, false, new string[0]);
			// Eikthyr's altar takes 2 deer trophies to summon him, as the game's offeraltar_deer has it
			QuestRule eikthyr = Rule(QuestKind.KillEikthyr, Meadows, 2, 2, 2, false, new string[0]);
			eikthyr.Offering = "TrophyDeer";
			eikthyr.OfferingCount = 2;
			Rule(QuestKind.FindBees, Meadows, 1, 1, 1, false, new string[0]).HivesBelow = HivesWanted;
			Rule(QuestKind.HuntGhosts, BlackForest, 2, 2, 2, true, new string[0]);
			Rule(QuestKind.RaidFrostCaves, Mountain, 3, 3, 3, true, new string[0]);
			Brings("RawMeat", QuestKind.Hunt, Meadows);
			Brings("LeatherScraps", QuestKind.Hunt, Meadows);
			Brings("DeerHide", QuestKind.Hunt, Meadows);
			Brings("DeerMeat", QuestKind.Hunt, Meadows);
			Brings("Feathers", QuestKind.Hunt, Meadows);
			Brings("TrophyDeer", QuestKind.Hunt, Meadows);
			Brings("WolfPelt", QuestKind.Hunt, Mountain);
			Brings("LoxPelt", QuestKind.Hunt, Plains);
			Brings("Raspberry", QuestKind.Forage, Meadows);
			Brings("Mushroom", QuestKind.Forage, Meadows);
			Brings("Dandelion", QuestKind.Forage, Meadows);
			Brings("Blueberries", QuestKind.Forage, BlackForest);
			Brings("Thistle", QuestKind.Forage, BlackForest);
			Brings("MushroomYellow", QuestKind.Forage, BlackForest);
			Brings("Cloudberry", QuestKind.Forage, Plains);
			Chops("Wood", Meadows, "Beech1");
			Chops("FineWood", Meadows, "Birch1");
			Chops("RoundLog", BlackForest, "Pinetree_01");
			Chops("ElderBark", Swamp, "SwampTree1");
			Chops("YggdrasilWood", Mistlands, "YggaShoot1");
			Mines("CopperOre", QuestKind.MineCopper, BlackForest, "rock4_copper");
			Mines("TinOre", QuestKind.MineTin, BlackForest, "MineRock_Tin");
			Mines("SilverOre", QuestKind.MineSilver, Mountain, "silvervein");
			Mines("IronScrap", QuestKind.RaidCrypts, Swamp, "mudpile2");
			Brings("WitheredBone", QuestKind.RaidCrypts, Swamp);
			Brings("MushroomYellow", QuestKind.RaidCrypts, Swamp);
			Brings("SurtlingCore", QuestKind.RaidBurialChambers, BlackForest);
			Brings("BoneFragments", QuestKind.RaidBurialChambers, BlackForest);
			Brings("Amber", QuestKind.RaidBurialChambers, BlackForest);
			Brings("AmberPearl", QuestKind.RaidBurialChambers, BlackForest);
			Brings("Ruby", QuestKind.RaidBurialChambers, BlackForest);
			Brings(Coins, QuestKind.RaidBurialChambers, BlackForest);
			Brings("MushroomYellow", QuestKind.RaidBurialChambers, BlackForest);
			Brings("TrollHide", QuestKind.HuntTrolls, BlackForest);
			Brings("BlackMetalScrap", QuestKind.HuntGoblins, Plains);
			Brings(Circlet, QuestKind.BuyCirclet, BlackForest);
			Brings(Belt, QuestKind.BuyBelt, BlackForest);
			Brings("HardAntler", QuestKind.KillEikthyr, Meadows);
			Brings("QueenBee", QuestKind.FindBees, Meadows);
			Brings("Honey", QuestKind.FindBees, Meadows);
			Brings("Ectoplasm", QuestKind.HuntGhosts, BlackForest);
			Brings("JuteRed", QuestKind.RaidFrostCaves, Mountain);
			Brings("Crystal", QuestKind.RaidFrostCaves, Mountain);
			Brings("Obsidian", QuestKind.RaidFrostCaves, Mountain);
			Brings("WolfFang", QuestKind.RaidFrostCaves, Mountain);
			Brings("TrophyUlv", QuestKind.RaidFrostCaves, Mountain);
			Brings("TrophyCultist", QuestKind.RaidFrostCaves, Mountain);
			Add("Honey", new ResourceSource { Kind = SourceKind.Gather });
		}

		/// <summary>
		/// Adds a quest's rule
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="biome">The easiest biome a village going on it can be</param>
		/// <param name="vikings">How many vikings go</param>
		/// <param name="plainsVikings">How many go from the Plains on</param>
		/// <param name="max">How many can go with more tools</param>
		/// <param name="light">Whether it goes underground and takes a torch or circlet</param>
		/// <param name="stations">The stations the village has to have</param>
		/// <returns>The rule</returns>
		private QuestRule Rule(QuestKind kind, int biome, int vikings, int plainsVikings, int max, bool light, string[] stations)
		{
			QuestRule rule = new QuestRule { Kind = kind, MinBiome = biome, Vikings = vikings, PlainsVikings = plainsVikings, MaxVikings = max, NeedsLight = light, Stations = stations };
			Quests[kind] = rule;
			return(rule);
		}

		/// <summary>
		/// Adds an item a quest brings back that takes no tool
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <param name="quest">The quest</param>
		/// <param name="biome">The easiest biome a village going for it can be</param>
		private void Brings(string item, QuestKind quest, int biome)
		{
			Add(item, new ResourceSource { Kind = SourceKind.Quest, Quest = quest, MinBiome = biome });
		}

		/// <summary>
		/// Adds an item cutting trees brings back, which takes an axe that can cut the tree it comes from
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <param name="biome">The easiest biome a village going for it can be</param>
		/// <param name="tree">The tree's prefab name</param>
		private void Chops(string item, int biome, string tree)
		{
			Add(item, new ResourceSource { Kind = SourceKind.Quest, Quest = QuestKind.ChopTrees, MinBiome = biome, Tool = ToolKind.Axe, ToolTarget = tree });
		}

		/// <summary>
		/// Adds an ore or scrap a quest mines, which takes a pickaxe that can break the rock it comes from
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <param name="quest">The quest</param>
		/// <param name="biome">The easiest biome a village going for it can be</param>
		/// <param name="rock">The rock's prefab name</param>
		private void Mines(string item, QuestKind quest, int biome, string rock)
		{
			Add(item, new ResourceSource { Kind = SourceKind.Quest, Quest = quest, MinBiome = biome, Tool = ToolKind.Pickaxe, ToolTarget = rock });
		}

		/// <summary>
		/// Gets a quest's rule
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>The rule, or null when the book has none</returns>
		public QuestRule RuleOf(QuestKind kind)
		{
			return(Quests.TryGetValue(kind, out QuestRule rule) ? rule : null);
		}
	}
}
