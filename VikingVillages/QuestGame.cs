using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Works out what happens on a village's quest from the game - the vikings' tools, skills and room to carry, the
	/// rocks and trees they work, and the world's resource and durability rates - with QuestSim doing the sums
	/// </summary>
	/// <remarks>
	/// The quest as a whole is worked out as it sets out (see Prepare): how long the work takes, what each viking brings
	/// home, how worn its tool comes back, and how a fight with what it kills goes. What happens to each viking - its harm,
	/// what it eats and drinks, and how its skills rise - is worked out as it leaves (see OutcomeFor), from what it
	/// took along
	/// </remarks>
	internal static class QuestGame
	{
		/// <summary>
		/// The weight and slots a viking keeps free for the food and potions it takes along, when working out what it can
		/// carry home
		/// </summary>
		private const float SupplyWeight = 12f;
		private const int SupplySlots = 3;

		/// <summary>
		/// How much damage breaks off one of an item when its rock or tree can't be read
		/// </summary>
		private const float DefaultHealthPerUnit = 30f;

		/// <summary>
		/// The most foods and healing potions a viking takes along on a quest
		/// </summary>
		public const int FoodMax = 9;
		public const int PotionMax = 2;

		/// <summary>
		/// Finds the way a quest gets the item it's for
		/// </summary>
		/// <param name="book">Every way to get each item</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">The item, or ""</param>
		/// <returns>The way, or a bare one for the quest when the book has none</returns>
		public static ResourceSource SourceFor(ResourceBook book, QuestKind kind, string item)
		{
			ResourceSource source = ((book != null && item.Length > 0) ? book.SourcesOf(item).Find((ResourceSource candidate) => candidate.Kind == SourceKind.Quest && candidate.Quest == kind) : null);
			return(source ?? new ResourceSource { Kind = SourceKind.Quest, Quest = kind });
		}

		/// <summary>
		/// Finds the loaded vikings of a quest, in the order of its members
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="record">The quest</param>
		/// <returns>The vikings, or null when one of them isn't loaded here</returns>
		public static List<Humanoid> Members(Village village, QuestRecord record)
		{
			List<Humanoid> found = new List<Humanoid>();
			List<Character> vikings = village.Vikings();
			foreach (string member in record.Members)
			{
				Humanoid humanoid = vikings.Find((Character viking) => viking != null && !viking.IsDead() && viking.m_nview != null && viking.m_nview.IsValid() && VikingIdentity.Get(viking.m_nview.GetZDO()) == member) as Humanoid;
				if (humanoid == null)
				{
					return(null);
				}
				found.Add(humanoid);
			}
			return(found);
		}

		/// <summary>
		/// Gets the biome a quest is dangerous by - where it works: the biome a hunt or foraging works in, the trees'
		/// biome for cutting trees, and the camp's biome of the trader treasure is sold to, or for the rest, which go to a
		/// place of their own biome, the hardest of the quest's and its item's easiest biomes
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="villageBiome">The village's biome, by VikingGear's order</param>
		/// <param name="roadBiome">The biome its road goes to, by VikingGear's order, or -1 when it isn't known</param>
		/// <param name="ruleBiome">The hardest of the quest's and its item's easiest biomes</param>
		/// <returns>The biome, by VikingGear's order</returns>
		internal static int QuestBiomeOf(QuestKind kind, string item, int villageBiome, int roadBiome, int ruleBiome)
		{
			switch (kind)
			{
				case QuestKind.Hunt:
				case QuestKind.Forage:
					return(QuestLoot.WorksIn(kind, item, villageBiome, roadBiome));
				case QuestKind.ChopTrees:
					return(QuestTargets.BiomeOf(kind, item, villageBiome));
				case QuestKind.SellTreasure:
					return((roadBiome >= 0) ? roadBiome : QuestTargets.BiomeOf(kind, item, villageBiome));
				default:
					return(ruleBiome);
			}
		}

		/// <summary>
		/// Works out a quest as it sets out - for one that takes a tool, how much of its item the vikings can carry home
		/// between them and how long their tools take to get it, wearing each tool; and what else it brings back (see
		/// QuestLoot) - then shares what it finds out among the vikings by their room, works out a fight with the creatures
		/// the quest kills, if any, and the harm it does each viking (see QuestFightGame), and when it's due back
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="record">The quest, its plans and due time filled in</param>
		/// <param name="source">The way the quest gets its item</param>
		/// <param name="there">How long the trip there takes, in seconds</param>
		/// <param name="back">How long the trip back takes, in seconds</param>
		/// <param name="roadBiome">The biome the road goes to, by VikingGear's order, or -1 when it isn't known</param>
		/// <returns>False when one of its vikings isn't loaded here</returns>
		public static bool Prepare(Village village, QuestRecord record, ResourceSource source, float there, float back, int roadBiome)
		{
			List<Humanoid> members = Members(village, record);
			ResourceBook book = ResourceBookGame.Get();
			QuestRule rule = ((book != null) ? book.RuleOf(record.Kind) : null);
			if (members == null || rule == null)
			{
				return(false);
			}
			int biome = VillageCooking.VillageBiome(village);
			record.QuestBiome = QuestBiomeOf(record.Kind, record.Item, biome, roadBiome, Mathf.Max(rule.MinBiome, source.MinBiome));
			record.Wanted = rule.VikingsFor(biome);
			record.Plans.Clear();
			List<CarrySpace> spaces = new List<CarrySpace>();
			for (int i = 0; i < members.Count; i++)
			{
				record.Plans.Add(new MemberPlan());
				spaces.Add(SpaceOf(members[i], ToolItem(record.Tools[i])));
			}
			Dictionary<string, int> found = new Dictionary<string, int>();
			float toNight = ((EnvMan.instance != null) ? QuestRules.SecondsToNight(EnvMan.instance.GetDayFraction(), EnvMan.instance.m_dayLengthSec) : float.MaxValue);
			bool byNight = (QuestRules.HomeByNight(record.Kind) && toNight < float.MaxValue);
			float work = (byNight ? QuestRules.WorkBeforeNight(toNight, there, back) : float.MaxValue);
			float active = QuestRules.ActiveSeconds(record.Kind);
			if (source.Tool != ToolKind.None && record.Item.Length > 0)
			{
				// Cutting trees stops in time to be home before night
				active += Gather(village, record, source, members, spaces, found, (byNight ? Mathf.Max(QuestRules.TreesLeastSeconds, work - active) : float.MaxValue));
			}
			int most = 0;
			if (record.Kind == QuestKind.Hunt)
			{
				most = Mathf.Max(QuestRules.HuntKillsLeast, (byNight ? QuestRules.HuntKillsBeforeNight(toNight, there, back) : QuestRules.HuntKillsMost));
			}
			else if (record.Kind == QuestKind.Forage)
			{
				most = Mathf.Max(QuestRules.ForagePicksLeast, (byNight ? QuestRules.ForagePicksBeforeNight(toNight, there, back) : QuestRules.ForagePicksMost));
			}
			int taken = QuestLoot.Roll(village, record, members, spaces, most, found, roadBiome);
			if (rule.Offering.Length > 0 && rule.OfferingCount > 0 && record.Plans.Count > 0)
			{
				// The first of them takes along what the quest uses up there, from the village's chests
				record.Plans[0].Spend.Add(new KeyValuePair<string, int>(rule.Offering, rule.OfferingCount));
			}
			if (record.Kind == QuestKind.Hunt)
			{
				// A hunt takes as long as its busiest hunter's prey take
				active = taken * QuestRules.HuntKillSeconds;
			}
			else if (record.Kind == QuestKind.Forage)
			{
				active = most * QuestRules.ForagePickSeconds;
			}
			List<KeyValuePair<string, int>> ordered = Ordered(found, record.Item);
			List<Dictionary<string, int>> shares = QuestSim.Share(ordered, spaces, WeightOf, StackOf);
			for (int i = 0; i < members.Count; i++)
			{
				record.Plans[i].Loot = Ordered(shares[i], record.Item);
				if ((record.Kind == QuestKind.SellTreasure || record.Kind == QuestKind.BuyCirclet) && record.Plans[i].Spend.Count > 0)
				{
					// What a trade brings home is worked out again as the viking leaves, from what it really took along
					record.Plans[i].Loot = PlannedTrade(record.Kind, record.Plans[i].Spend);
				}
			}
			FightResult fight = QuestFightGame.Fight(record.Kind, members);
			if (fight != null)
			{
				// The work there takes as long as the fight, when that's longer than the quest's usual work
				active = Mathf.Max(active, fight.Seconds);
				for (int i = 0; i < members.Count; i++)
				{
					record.Plans[i].FightHarm = fight.HarmShares[i];
				}
				LogFight(record, members, fight, active);
			}
			record.ActiveSeconds = active;
			float seconds = QuestRules.Duration(there, back, active, Random.Range(-1f, 1f));
			if (byNight && seconds > toNight)
			{
				seconds = Mathf.Max(there + back + active, toNight);
			}
			record.Due = record.SetOutAt + NetTime.Ticks(seconds);
			return(true);
		}

		/// <summary>
		/// Logs how a quest's fight is worked out to go - how long it takes, and each viking's swing, armor,
		/// block power and health, the harm the fight does it and the harm the quest's danger would have done it instead
		/// (see QuestSim.Harm), to weigh the two up against each other
		/// </summary>
		/// <param name="record">The quest, its biome and how many it takes filled in</param>
		/// <param name="members">Its vikings</param>
		/// <param name="fight">How the fight goes</param>
		/// <param name="active">How long the work there takes, in seconds</param>
		private static void LogFight(QuestRecord record, List<Humanoid> members, FightResult fight, float active)
		{
			List<string> sides = new List<string>();
			for (int i = 0; i < members.Count; i++)
			{
				Humanoid humanoid = members[i];
				VikingCore core = humanoid.GetComponent<VikingCore>();
				int gap = record.QuestBiome - ((core != null) ? core.BiomeIndex() : 0);
				float danger = QuestSim.HarmShare(QuestSim.Danger(record.Kind), gap, members.Count, record.Wanted, QuestRules.ActiveSeconds(record.Kind));
				float swing = ((core != null) ? core.StrikeDamage() : 0f);
				sides.Add(VikingNames.Get(humanoid) + " (hits for " + swing.ToString("F0") + ", armor " + humanoid.GetBodyArmor().ToString("F0") + ", block " + ((core != null) ? core.BlockPower() : 0f).ToString("F0") + ", health " + humanoid.GetMaxHealth().ToString("F0") + ") harmed by " + (fight.HarmShares[i] * 100f).ToString("F0") + "% - " + (danger * 100f).ToString("F0") + "% by the quest's danger");
			}
			Plugin.Log.LogInfo("Quest fight: " + record.Kind + " takes " + fight.Seconds.ToString("F0") + "s of fighting, " + active.ToString("F0") + "s there in all - " + string.Join("; ", sides));
		}

		/// <summary>
		/// Works out the vikings mining or cutting for a quest's item - as much as they can carry home between them, or as
		/// much as they get done in the time they have, each tool swinging until that's done or it wears out - and notes
		/// each one's tool, its wear and its hits
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="record">The quest, its plans filled in</param>
		/// <param name="source">The way the quest gets its item, with its tool and the rock or tree</param>
		/// <param name="members">Its vikings</param>
		/// <param name="spaces">Each viking's room</param>
		/// <param name="found">What they bring back, added to</param>
		/// <param name="most">The most time they have for it, in seconds</param>
		/// <returns>How long the work takes, in seconds</returns>
		private static float Gather(Village village, QuestRecord record, ResourceSource source, List<Humanoid> members, List<CarrySpace> spaces, Dictionary<string, int> found, float most)
		{
			List<QuestTool> tools = new List<QuestTool>();
			List<int> owners = new List<int>();
			Skills.SkillType skill = ((source.Tool == ToolKind.Pickaxe) ? Skills.SkillType.Pickaxes : Skills.SkillType.WoodCutting);
			for (int i = 0; i < members.Count; i++)
			{
				ItemDrop.ItemData tool = ToolFor(members[i], record.Tools[i], source);
				if (tool == null)
				{
					continue;
				}
				float damage = ((source.Tool == ToolKind.Pickaxe) ? tool.GetDamage().m_pickaxe : tool.GetDamage().m_chop);
				tools.Add(new QuestTool { Damage = damage * QuestSim.SkillFactor(members[i].GetSkillFactor(skill) * 100f), Durability = tool.m_durability, Drain = tool.m_shared.m_useDurabilityDrain });
				owners.Add(i);
				record.Plans[i].Tool = tool.m_dropPrefab.name;
			}
			int units = QuestSim.CarryUnits(spaces, WeightOf(record.Item), StackOf(record.Item));
			float unitHealth = HealthPerUnit(source.ToolTarget, record.Item);
			GatherResult result = QuestSim.Gather(units, unitHealth, tools, QuestSim.SwingSeconds, Game.m_durabilityRate);
			if (result.Seconds > most && result.Units > 1)
			{
				// Too long for the time they have, so they break off only as much as it gives time for
				units = Mathf.Max(1, Mathf.FloorToInt(result.Units * most / result.Seconds));
				result = QuestSim.Gather(units, unitHealth, tools, QuestSim.SwingSeconds, Game.m_durabilityRate);
			}
			for (int t = 0; t < owners.Count; t++)
			{
				MemberPlan plan = record.Plans[owners[t]];
				plan.ToolLeft = result.DurabilityLeft[t];
				plan.Hits = result.Hits[t];
			}
			QuestSim.Add(found, record.Item, Scale(record.Item, result.Units));
			return(result.Seconds);
		}

		/// <summary>
		/// Puts what was found in the order it's carried home - what the quest was for first, then the rest by name
		/// </summary>
		/// <param name="found">What was found</param>
		/// <param name="first">The item that goes first, or ""</param>
		/// <returns>Each item and how many</returns>
		private static List<KeyValuePair<string, int>> Ordered(Dictionary<string, int> found, string first)
		{
			List<KeyValuePair<string, int>> ordered = new List<KeyValuePair<string, int>>(found);
			ordered.Sort((KeyValuePair<string, int> a, KeyValuePair<string, int> b) =>
			{
				if ((a.Key == first) != (b.Key == first))
				{
					return((a.Key == first) ? -1 : 1);
				}
				return(string.CompareOrdinal(a.Key, b.Key));
			});
			return(ordered);
		}

		/// <summary>
		/// Scales how many of an item a quest finds by the world's resource rate, the way the game scales drops (see
		/// Game.ScaleDrops)
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <param name="count">How many</param>
		/// <returns>How many, scaled</returns>
		public static int Scale(string item, int count)
		{
			ItemDrop.ItemData data = ItemData(item);
			if (count <= 0 || data == null || Game.instance == null)
			{
				return(Mathf.Max(0, count));
			}
			return(Game.instance.ScaleDrops(data, count));
		}

		/// <summary>
		/// Works out how much room a viking has to carry things home - under the weight it can carry, less what it already
		/// carries, the tool it takes from a chest and its food and potions, and its empty slots less theirs
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <param name="tool">The tool it takes from a chest, or null</param>
		/// <returns>Its room</returns>
		private static CarrySpace SpaceOf(Humanoid humanoid, ItemDrop.ItemData tool)
		{
			LivingVikingBody body = humanoid.GetComponent<LivingVikingBody>();
			Inventory inventory = humanoid.GetInventory();
			float limit = ((body != null) ? body.GetMaxCarryWeight() : LivingVikingBody.MaxCarryWeight);
			float weight = inventory.GetTotalWeight() + SupplyWeight + ((tool != null) ? tool.m_shared.m_weight : 0f);
			int slots = inventory.m_width * inventory.m_height - inventory.m_inventory.Count - SupplySlots - ((tool != null) ? 1 : 0);
			return(new CarrySpace { WeightRoom = Mathf.Max(0f, limit - weight), FreeSlots = Mathf.Max(0, slots) });
		}

		/// <summary>
		/// Finds the tool a viking works with on a quest - one good enough it carries, or the one it takes from a chest
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <param name="fromChest">The tool it takes from a chest, by prefab name, or ""</param>
		/// <param name="source">The way the quest gets its item, with the tool it takes</param>
		/// <returns>The tool, or null when it has none</returns>
		private static ItemDrop.ItemData ToolFor(Humanoid humanoid, string fromChest, ResourceSource source)
		{
			ItemDrop.ItemData carried = humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => VillageQuests.GoodTool(item, source));
			return(carried ?? ToolItem(fromChest));
		}

		/// <summary>
		/// Gets a tool by its prefab name, new
		/// </summary>
		/// <param name="prefab">The tool's prefab name, or ""</param>
		/// <returns>The tool, or null</returns>
		private static ItemDrop.ItemData ToolItem(string prefab)
		{
			return((prefab.Length > 0) ? ItemData(prefab) : null);
		}

		/// <summary>
		/// Gets a copy of an item's data by its prefab name, with its drop prefab set
		/// </summary>
		/// <param name="prefab">The prefab name</param>
		/// <returns>The data, or null when the game has no such item</returns>
		public static ItemDrop.ItemData ItemData(string prefab)
		{
			GameObject found = ((ObjectDB.instance != null && !string.IsNullOrEmpty(prefab)) ? ObjectDB.instance.GetItemPrefab(prefab) : null);
			ItemDrop drop = ((found != null) ? found.GetComponent<ItemDrop>() : null);
			if (drop == null)
			{
				return(null);
			}
			ItemDrop.ItemData data = drop.m_itemData.Clone();
			data.m_dropPrefab = found;
			return(data);
		}

		/// <summary>
		/// Gets an item's weight by its prefab name
		/// </summary>
		/// <param name="prefab">The prefab name</param>
		/// <returns>The weight, or 1 when it isn't known</returns>
		public static float WeightOf(string prefab)
		{
			ItemDrop.ItemData data = ItemData(prefab);
			return((data != null) ? data.m_shared.m_weight : 1f);
		}

		/// <summary>
		/// Gets how many of an item make a stack, by its prefab name
		/// </summary>
		/// <param name="prefab">The prefab name</param>
		/// <returns>How many, or 1 when it isn't known</returns>
		public static int StackOf(string prefab)
		{
			ItemDrop.ItemData data = ItemData(prefab);
			return((data != null) ? Mathf.Max(1, data.m_shared.m_maxStackSize) : 1);
		}

		/// <summary>
		/// Works out how much damage breaks off one of an item from a rock or tree, on average - its health over what it
		/// drops of the item
		/// </summary>
		/// <param name="target">The rock's or tree's prefab name, or null</param>
		/// <param name="item">The item's prefab name</param>
		/// <returns>The damage, or DefaultHealthPerUnit when it can't be read</returns>
		private static float HealthPerUnit(string target, string item)
		{
			GameObject prefab = ((target != null && ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(target) : null);
			if (prefab == null)
			{
				return(DefaultHealthPerUnit);
			}
			float health = 0f;
			float drops = 0f;
			MineRock5 rock5 = prefab.GetComponent<MineRock5>();
			MineRock rock = prefab.GetComponent<MineRock>();
			TreeBase tree = prefab.GetComponent<TreeBase>();
			Destructible destructible = prefab.GetComponent<Destructible>();
			if (rock5 != null)
			{
				health = rock5.m_health;
				drops = AverageDrops(rock5.m_dropItems, item);
			}
			else if (rock != null)
			{
				health = rock.m_health;
				drops = AverageDrops(rock.m_dropItems, item);
			}
			else if (tree != null)
			{
				health = tree.m_health;
				drops = AverageDrops(tree.m_dropWhenDestroyed, item);
			}
			else if (destructible != null)
			{
				DropOnDestroyed dropper = prefab.GetComponent<DropOnDestroyed>();
				health = destructible.m_health;
				drops = ((dropper != null) ? AverageDrops(dropper.m_dropWhenDestroyed, item) : 0f);
			}
			return((health > 0f && drops > 0f) ? health / drops : DefaultHealthPerUnit);
		}

		/// <summary>
		/// Works out how many of an item a drop table drops on average - how many times it picks, how likely it picks the
		/// item, and the size of the stack
		/// </summary>
		/// <param name="table">The table, or null</param>
		/// <param name="item">The item's prefab name</param>
		/// <returns>How many, or 0 when it never drops it</returns>
		internal static float AverageDrops(DropTable table, string item)
		{
			if (table == null || table.m_drops == null || table.m_drops.Count == 0)
			{
				return(0f);
			}
			float total = 0f;
			float stack = 0f;
			float weight = 0f;
			foreach (DropTable.DropData drop in table.m_drops)
			{
				total += drop.m_weight;
				if (drop.m_item != null && drop.m_item.name == item)
				{
					weight += drop.m_weight;
					stack = (drop.m_stackMin + drop.m_stackMax) / 2f;
				}
			}
			if (total <= 0f || weight <= 0f)
			{
				return(0f);
			}
			float picks = (table.m_dropMin + table.m_dropMax) / 2f * table.m_dropChance;
			return(picks * weight / total * stack);
		}

		/// <summary>
		/// Converts a drop table of the game's - a chest's, rock's or tree's - into a loot table to roll
		/// </summary>
		/// <param name="table">The table, or null</param>
		/// <returns>The loot table, empty when there's none</returns>
		public static LootTable FromDropTable(DropTable table)
		{
			LootTable loot = new LootTable { Picks = true };
			if (table == null || table.m_drops == null)
			{
				return(loot);
			}
			loot.PicksMin = table.m_dropMin;
			loot.PicksMax = table.m_dropMax;
			loot.Chance = table.m_dropChance;
			loot.OneOfEach = table.m_oneOfEach;
			foreach (DropTable.DropData drop in table.m_drops)
			{
				if (drop.m_item != null)
				{
					loot.Drops.Add(new LootDrop { Item = drop.m_item.name, Min = drop.m_stackMin, Max = drop.m_stackMax, Weight = drop.m_weight });
				}
			}
			return(loot);
		}

		/// <summary>
		/// Converts a creature's drops into a loot table to roll, each drop rolled on its own
		/// </summary>
		/// <param name="drops">The creature's drops, or null</param>
		/// <returns>The loot table, empty when there's none</returns>
		public static LootTable FromCharacterDrop(CharacterDrop drops)
		{
			LootTable loot = new LootTable { Picks = false };
			if (drops == null)
			{
				return(loot);
			}
			foreach (CharacterDrop.Drop drop in drops.m_drops)
			{
				if (drop.m_prefab != null)
				{
					loot.Drops.Add(new LootDrop { Item = drop.m_prefab.name, Min = drop.m_amountMin, Max = drop.m_amountMax, Chance = drop.m_chance });
				}
			}
			return(loot);
		}

		/// <summary>
		/// Checks whether an item is a healing potion - something drunk, not eaten, that heals
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		public static bool IsPotion(ItemDrop.ItemData item)
		{
			return(PotionHeal(item) > 0f);
		}

		/// <summary>
		/// Gets how much a healing potion heals - at once and over time
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The health, or 0 for anything else</returns>
		public static float PotionHeal(ItemDrop.ItemData item)
		{
			if (item == null || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable || item.m_shared.m_food > 0f)
			{
				return(0f);
			}
			SE_Stats stats = item.m_shared.m_consumeStatusEffect as SE_Stats;
			return((stats != null) ? stats.m_healthUpFront + stats.m_healthOverTime : 0f);
		}

		/// <summary>
		/// Works out how a quest goes for one of its vikings as it leaves - what it brings home and its tool's wear from
		/// the quest's plan, then the harm it takes - the fight's on a quest that kills creatures (see QuestFight), and
		/// otherwise the quest's danger for its gear - whether it dies of it (see
		/// QuestSim.DeathChance), the foods it eats and potions it drinks of what it took along, how its skills rise, and
		/// what it spends
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <param name="core">Its core</param>
		/// <param name="record">The quest</param>
		/// <param name="member">Its GUID</param>
		/// <param name="carried">What it takes along for the quest - food, potions and what it spends</param>
		/// <param name="village">The name of the village it sets out from</param>
		/// <returns>How it goes</returns>
		public static QuestOutcome OutcomeFor(Humanoid humanoid, VikingCore core, QuestRecord record, string member, List<ItemDrop.ItemData> carried, string village)
		{
			MemberPlan plan = record.PlanOf(member);
			QuestOutcome outcome = new QuestOutcome();
			outcome.Kind = record.Kind;
			outcome.Item = record.Item;
			outcome.Companions = record.Others(VikingNames.Get(humanoid));
			outcome.Loot = plan.Loot;
			outcome.Tool = plan.Tool;
			outcome.ToolLeft = plan.ToolLeft;
			outcome.Kills = plan.Kills;
			outcome.SetOutAt = record.SetOutAt;
			outcome.Village = village;
			float seconds = Mathf.Max(0f, (record.Due - record.SetOutAt) / (float)NetTime.Ticks(1f));
			List<ItemDrop.ItemData> foods = carried.FindAll((ItemDrop.ItemData item) => VikingFood.IsFood(item));
			List<ItemDrop.ItemData> potions = carried.FindAll((ItemDrop.ItemData item) => IsPotion(item));
			int foodCount = 0;
			float burn = 0f;
			foreach (ItemDrop.ItemData food in foods)
			{
				foodCount += food.m_stack;
				burn += food.m_shared.m_foodBurnTime * food.m_stack;
			}
			burn = ((foodCount > 0) ? burn / foodCount / Mathf.Max(0.01f, Game.m_foodRate) : QuestSim.DefaultBurn);
			int meals = QuestSim.MealsEaten(seconds, burn, foodCount);
			bool fed = (meals * 2 >= QuestSim.MealsWanted(seconds, burn));
			UseUp(foods, meals, outcome.Used);
			int potionCount = 0;
			float heal = 0f;
			foreach (ItemDrop.ItemData potion in potions)
			{
				potionCount += potion.m_stack;
				heal = Mathf.Max(heal, PotionHeal(potion));
			}
			float max = humanoid.GetMaxHealth();
			float harm = ((plan.FightHarm >= 0f) ? plan.FightHarm * max : QuestSim.Harm(record.Kind, record.QuestBiome, ((core != null) ? core.BiomeIndex() : 0), record.Members.Count, record.Wanted, record.ActiveSeconds, max));
			float health = QuestSim.EndHealth(max, harm, fed, potionCount, heal, out int drunk);
			UseUp(potions, drunk, outcome.Used);
			outcome.HealthShare = ((max > 0f) ? health / max : 1f);
			// The worse a quest harms it, the likelier it dies there, which leaves it out when the others come home
			if (max > 0f && Random.value < QuestSim.DeathChance(harm / max))
			{
				outcome.HealthShare = 0f;
				TestLog.Count("Quests", record.Kind + ": a viking will die on it");
			}
			if (record.Kind == QuestKind.SellTreasure || record.Kind == QuestKind.BuyCirclet)
			{
				Trade(record.Kind, carried, outcome);
			}
			else
			{
				LeaveOfferings(plan, carried, outcome);
			}
			if (plan.Hits > 0)
			{
				ItemDrop.ItemData tool = ItemData(plan.Tool);
				Skills.SkillType skill = ((tool != null && tool.m_shared.m_skillType == Skills.SkillType.Pickaxes) ? Skills.SkillType.Pickaxes : Skills.SkillType.WoodCutting);
				outcome.Skills.Add(new KeyValuePair<int, float>((int)skill, plan.Hits));
			}
			if (plan.Kills > 0 && core != null)
			{
				outcome.Skills.Add(new KeyValuePair<int, float>((int)WeaponSkill(humanoid, record.Kind == QuestKind.Hunt), plan.Kills * QuestLoot.HitsPerKill));
			}
			if (record.Kind == QuestKind.Forage)
			{
				// Each plant picked raises Farming by one, the way picking does for a player (see Pickable.Interact)
				outcome.Skills.Add(new KeyValuePair<int, float>((int)Skills.SkillType.Farming, QuestRules.ForagePicks(record.ActiveSeconds)));
			}
			return(outcome);
		}

		/// <summary>
		/// Uses up what a viking took along to leave where the quest went (see QuestRule.Offering), as much of it as the
		/// quest takes and it carries
		/// </summary>
		/// <param name="plan">The viking's plan, with what it takes along to leave</param>
		/// <param name="carried">What it took along</param>
		/// <param name="outcome">How the quest goes for it, what it used added to</param>
		private static void LeaveOfferings(MemberPlan plan, List<ItemDrop.ItemData> carried, QuestOutcome outcome)
		{
			foreach (KeyValuePair<string, int> offering in plan.Spend)
			{
				int left = offering.Value;
				foreach (ItemDrop.ItemData item in carried)
				{
					if (left <= 0)
					{
						break;
					}
					if (item.m_dropPrefab != null && item.m_dropPrefab.name == offering.Key)
					{
						int used = Mathf.Min(left, item.m_stack);
						outcome.Used.Add(new KeyValuePair<string, int>(offering.Key, used));
						left -= used;
					}
				}
			}
		}

		/// <summary>
		/// Works out what a viking trading with Haldor brings home for the coins and treasure it took along (see
		/// QuestLoot.Trade), spending all it took when it trades
		/// </summary>
		/// <param name="kind">The quest - selling treasure or buying a circlet</param>
		/// <param name="carried">What it took along</param>
		/// <param name="outcome">How the quest goes for it, its loot and what it used filled in</param>
		private static void Trade(QuestKind kind, List<ItemDrop.ItemData> carried, QuestOutcome outcome)
		{
			int coins = 0;
			int value = 0;
			List<ItemDrop.ItemData> purse = new List<ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData item in carried)
			{
				string name = item.m_dropPrefab.name;
				if (name == ResourceBook.Coins)
				{
					coins += item.m_stack;
					purse.Add(item);
				}
				else if (System.Array.IndexOf(ResourceBook.Treasure, name) >= 0)
				{
					value += item.m_stack * item.m_shared.m_value;
					purse.Add(item);
				}
			}
			outcome.Loot = QuestLoot.Trade(kind, coins, value, CircletPrice(), out bool spends);
			if (!spends)
			{
				return;
			}
			foreach (ItemDrop.ItemData item in purse)
			{
				outcome.Used.Add(new KeyValuePair<string, int>(item.m_dropPrefab.name, item.m_stack));
			}
		}

		/// <summary>
		/// Works out what a viking trading with Haldor is planned to bring home for what it's to take along from the
		/// village's chests (see QuestLoot.Trade), the way Trade works it out as it leaves
		/// </summary>
		/// <param name="kind">The quest - selling treasure or buying a circlet</param>
		/// <param name="spend">What it's to take along, each item's prefab name and how many</param>
		/// <returns>What it brings home, each item and how many</returns>
		private static List<KeyValuePair<string, int>> PlannedTrade(QuestKind kind, List<KeyValuePair<string, int>> spend)
		{
			int coins = 0;
			int value = 0;
			foreach (KeyValuePair<string, int> entry in spend)
			{
				if (entry.Key == ResourceBook.Coins)
				{
					coins += entry.Value;
				}
				else if (System.Array.IndexOf(ResourceBook.Treasure, entry.Key) >= 0)
				{
					ItemDrop.ItemData data = ItemData(entry.Key);
					value += ((data != null) ? entry.Value * data.m_shared.m_value : 0);
				}
			}
			return(QuestLoot.Trade(kind, coins, value, CircletPrice(), out bool _));
		}

		/// <summary>
		/// Gets what a Dverger circlet costs at Haldor's
		/// </summary>
		/// <returns>The price in coins, or int.MaxValue when it isn't known</returns>
		private static int CircletPrice()
		{
			ResourceBook book = ResourceBookGame.Get();
			QuestRule rule = ((book != null) ? book.RuleOf(QuestKind.BuyCirclet) : null);
			return((rule != null) ? rule.Coins : int.MaxValue);
		}

		/// <summary>
		/// Gets the skill of the weapon a viking fights with - its melee weapon's, or its bow's when it has no other - or
		/// hunting, its bow's whenever it has one
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <param name="hunting">Whether it's hunting</param>
		/// <returns>The skill, or Unarmed when it has no weapon</returns>
		private static Skills.SkillType WeaponSkill(Humanoid humanoid, bool hunting)
		{
			Skills.SkillType melee = Skills.SkillType.None;
			Skills.SkillType ranged = Skills.SkillType.None;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (VikingItems.IsCarried(item) || VikingItems.IsProp(item) || !item.IsWeapon() || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch || item.m_shared.m_damages.m_pickaxe > 0f || item.m_shared.m_tamedOnly)
				{
					continue;
				}
				if (!VikingCore.IsRanged(item))
				{
					melee = ((melee != Skills.SkillType.None) ? melee : item.m_shared.m_skillType);
				}
				else if (ranged == Skills.SkillType.None)
				{
					ranged = item.m_shared.m_skillType;
				}
			}
			Skills.SkillType first = (hunting ? ranged : melee);
			Skills.SkillType second = (hunting ? melee : ranged);
			if (first != Skills.SkillType.None)
			{
				return(first);
			}
			return((second != Skills.SkillType.None) ? second : Skills.SkillType.Unarmed);
		}

		/// <summary>
		/// Notes a number of items used up out of what was carried, a stack at a time
		/// </summary>
		/// <param name="items">The stacks carried</param>
		/// <param name="count">How many are used up</param>
		/// <param name="used">What's used, by prefab name, added to</param>
		private static void UseUp(List<ItemDrop.ItemData> items, int count, List<KeyValuePair<string, int>> used)
		{
			foreach (ItemDrop.ItemData item in items)
			{
				if (count <= 0)
				{
					break;
				}
				int take = Mathf.Min(count, item.m_stack);
				used.Add(new KeyValuePair<string, int>(item.m_dropPrefab.name, take));
				count -= take;
			}
		}
	}
}
