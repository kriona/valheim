using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// A tool a viking works with on a quest - how much a hit with it does, the viking's skill counted, its durability
	/// left and how much a hit wears it
	/// </summary>
	internal class QuestTool
	{
		public float Damage;
		public float Durability;
		public float Drain;
	}

	/// <summary>
	/// What the vikings' tools got done on a quest - how long it took, in seconds, how many of the item they got, and
	/// each tool's hits and durability left, in the order the tools were given
	/// </summary>
	internal class GatherResult
	{
		public float Seconds;
		public int Units;
		public List<int> Hits = new List<int>();
		public List<float> DurabilityLeft = new List<float>();
	}

	/// <summary>
	/// One thing a loot table can drop - the item, how many at least and at most, how likely it drops each time the
	/// table rolls, and how likely it's picked among the others, for a table that picks
	/// </summary>
	internal class LootDrop
	{
		public string Item;
		public int Min = 1;
		public int Max = 1;
		public float Chance = 1f;
		public float Weight = 1f;
	}

	/// <summary>
	/// A loot table - a creature's drops, each rolled on its own, or a chest's or rock's, which picks a number of its
	/// drops by their weights - with how likely it drops anything at all
	/// </summary>
	internal class LootTable
	{
		public List<LootDrop> Drops = new List<LootDrop>();
		public bool Picks;
		public int PicksMin = 1;
		public int PicksMax = 1;
		public bool OneOfEach;
		public float Chance = 1f;
	}

	/// <summary>
	/// How much room a viking has to carry things home - weight left under what it can carry, and empty slots
	/// </summary>
	internal class CarrySpace
	{
		public float WeightRoom;
		public int FreeSlots;
	}

	/// <summary>
	/// A food a viking carries on a quest or trip - the item, the health it adds, how much it heals every
	/// QuestSim.TickSeconds, how long it lasts at the world's food rate, in seconds, how many of it, and how far into the
	/// trip it has it, in seconds - 0 for what it set out with, later for what it finds on the way
	/// </summary>
	internal class QuestFood
	{
		public string Item;
		public float Health;
		public float Regen;
		public float Burn;
		public int Count;
		public float From;
	}

	/// <summary>
	/// A food a viking has eaten - the item, the health it adds, how much it heals every QuestSim.TickSeconds, how long
	/// it lasts at the world's food rate and how long it has left, in seconds
	/// </summary>
	internal class QuestMeal
	{
		public string Item;
		public float Health;
		public float Regen;
		public float Burn;
		public float Left;
	}

	/// <summary>
	/// A food a viking eats on a quest or trip, from when it's eaten - before it set out, for one it set out with -
	/// until it wears off, in seconds from when it sets out
	/// </summary>
	internal class QuestBite
	{
		public string Item;
		public float Health;
		public float Regen;
		public float Burn;
		public float Start;
		public float End;
	}

	/// <summary>
	/// What a viking eats on a quest or trip (see QuestSim.Eat) - how many of each food, its max health on average while
	/// it's out and as it gets there, the foods it has eaten that still last then, with the time each has left, and
	/// every food it eats along the way
	/// </summary>
	internal class QuestMeals
	{
		public Dictionary<string, int> Eaten = new Dictionary<string, int>();
		public float MaxHealth;
		public float EndMaxHealth;
		public List<QuestMeal> Lasting = new List<QuestMeal>();
		public List<QuestBite> Bites = new List<QuestBite>();
	}

	/// <summary>
	/// Works out what happens to vikings out on a quest, where no one can see them - what their tools get done, what
	/// they find, what they can carry home, how hurt they come back, and what they eat and drink - apart from the game,
	/// so it can be tested
	/// </summary>
	/// <remarks>
	/// The numbers follow the game's where it has them: a swing of a tool does its damage times the viking's skill
	/// factor the way a player's does (see Skills.GetRandomSkillFactor, whose average this takes), wears it by its
	/// durability drain each hit, and loot is scaled by the world's resource rate as the caller works it out (see
	/// Game.ScaleDrops)
	/// </remarks>
	internal static class QuestSim
	{
		/// <summary>
		/// How long a swing of a pickaxe or axe takes, in seconds
		/// </summary>
		public const float SwingSeconds = 1.6f;

		/// <summary>
		/// The most rounds of swings a gathering is worked out to, so a tool doing almost nothing ends
		/// </summary>
		private const int MaxRounds = 100000;

		/// <summary>
		/// How dangerous each quest is - the share of a viking's health it would lose in half an hour of it, at the
		/// biome its gear comes from and with the group it takes
		/// </summary>
		private static readonly Dictionary<QuestKind, float> Dangers = new Dictionary<QuestKind, float>
		{
			{ QuestKind.Hunt, 0.2f },
			{ QuestKind.Forage, 0.1f },
			{ QuestKind.ChopTrees, 0.1f },
			{ QuestKind.MineCopper, 0.2f },
			{ QuestKind.MineTin, 0.15f },
			{ QuestKind.MineSilver, 0.5f },
			{ QuestKind.RaidCrypts, 0.6f },
			{ QuestKind.RaidBurialChambers, 0.5f },
			{ QuestKind.HuntTrolls, 0.8f },
			{ QuestKind.HuntGoblins, 0.8f },
			{ QuestKind.SellTreasure, 0.05f },
			{ QuestKind.BuyCirclet, 0.05f },
			{ QuestKind.KillEikthyr, 0.8f },
			{ QuestKind.FindBees, 0.15f },
			{ QuestKind.HuntGhosts, 0.5f },
			{ QuestKind.RaidFrostCaves, 0.6f }
		};

		/// <summary>
		/// The share of a viking's health it comes home at, at least, when it comes home
		/// </summary>
		public const float HealthFloor = 0.1f;

		/// <summary>
		/// How likely a viking is to die on a quest that harms it as much as all its health - the chance going by the
		/// square of the share of its health the quest harms it by, so it's rare on all but the most dangerous quests (see
		/// DeathChance)
		/// </summary>
		public const float DeathChanceFull = 0.075f;

		/// <summary>
		/// The least chance a viking dies on a quest at all - one less likely than this never dies there
		/// </summary>
		public const float DeathChanceLeast = 0.01f;

		/// <summary>
		/// The share of its max health a viking drinks a healing potion below
		/// </summary>
		public const float PotionAt = 0.25f;

		/// <summary>
		/// How many foods a viking keeps up at once, and how often its food heals it, in seconds - the same as at home
		/// (see VikingFood)
		/// </summary>
		public const int FoodsAtOnce = 3;
		public const float TickSeconds = 10f;

		/// <summary>
		/// How often a viking's max health is looked at to work out its average on a quest, in seconds, and the most
		/// times it's looked at, for a long quest
		/// </summary>
		private const float HealthSampleSeconds = 10f;
		private const int HealthSamplesMost = 2000;

		/// <summary>
		/// The most a viking's skill raises what it finds, as a share of it
		/// </summary>
		public const float SkillBonusMax = 0.5f;

		/// <summary>
		/// Gets how dangerous a quest is (see Dangers)
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>The share of health lost in half an hour of it</returns>
		public static float Danger(QuestKind kind)
		{
			return(Dangers.TryGetValue(kind, out float danger) ? danger : 0.2f);
		}

		/// <summary>
		/// Gets the average skill factor a hit is done with at a skill level, the way the game rolls one
		/// </summary>
		/// <param name="level">The skill level, from 0 to 100</param>
		/// <returns>The factor, from 0.4 to 1</returns>
		public static float SkillFactor(float level)
		{
			float share = System.Math.Max(0f, System.Math.Min(1f, level / 100f));
			return(0.4f + 0.6f * share);
		}

		/// <summary>
		/// Gets how much more a viking finds for its skill (see SkillBonusMax)
		/// </summary>
		/// <param name="level">The skill level, from 0 to 100</param>
		/// <returns>The share more</returns>
		public static float SkillBonus(float level)
		{
			return(SkillBonusMax * System.Math.Max(0f, System.Math.Min(1f, level / 100f)));
		}

		/// <summary>
		/// Works out what tools working together get done - every tool swinging each round until enough is broken off for
		/// the units wanted, or every tool has worn out - and how worn each comes back
		/// </summary>
		/// <param name="unitsWanted">How many of the item are wanted</param>
		/// <param name="healthPerUnit">How much damage breaks off one, on average</param>
		/// <param name="tools">The tools</param>
		/// <param name="swingSeconds">How long a swing takes</param>
		/// <param name="durabilityRate">How fast tools wear, as the world sets it (see Game.m_durabilityRate)</param>
		/// <returns>What they got done</returns>
		public static GatherResult Gather(int unitsWanted, float healthPerUnit, List<QuestTool> tools, float swingSeconds, float durabilityRate)
		{
			GatherResult result = new GatherResult();
			int[] left = new int[tools.Count];
			for (int i = 0; i < tools.Count; i++)
			{
				QuestTool tool = tools[i];
				float wear = tool.Drain * durabilityRate;
				left[i] = ((wear > 0f) ? (int)System.Math.Floor(tool.Durability / wear) : int.MaxValue);
				result.Hits.Add(0);
			}
			float need = System.Math.Max(0, unitsWanted) * System.Math.Max(0.01f, healthPerUnit);
			float done = 0f;
			int rounds = 0;
			while (done < need && rounds < MaxRounds)
			{
				bool any = false;
				for (int i = 0; i < tools.Count; i++)
				{
					if (left[i] <= 0 || tools[i].Damage <= 0f)
					{
						continue;
					}
					done += tools[i].Damage;
					left[i]--;
					result.Hits[i]++;
					any = true;
				}
				if (!any)
				{
					break;
				}
				rounds++;
			}
			result.Seconds = rounds * swingSeconds;
			result.Units = System.Math.Min(System.Math.Max(0, unitsWanted), (int)System.Math.Floor(done / System.Math.Max(0.01f, healthPerUnit)));
			for (int i = 0; i < tools.Count; i++)
			{
				result.DurabilityLeft.Add(System.Math.Max(0f, tools[i].Durability - result.Hits[i] * tools[i].Drain * durabilityRate));
			}
			return(result);
		}

		/// <summary>
		/// Works out how many of an item vikings can carry home between them - by the weight each can still carry and its
		/// empty slots
		/// </summary>
		/// <param name="spaces">Each viking's room</param>
		/// <param name="weight">The item's weight</param>
		/// <param name="maxStack">How many of it make a stack</param>
		/// <returns>How many</returns>
		public static int CarryUnits(List<CarrySpace> spaces, float weight, int maxStack)
		{
			int total = 0;
			foreach (CarrySpace space in spaces)
			{
				total += Fits(space, weight, maxStack);
			}
			return(total);
		}

		/// <summary>
		/// Works out how many of an item fit in a viking's room
		/// </summary>
		/// <param name="space">The room</param>
		/// <param name="weight">The item's weight</param>
		/// <param name="maxStack">How many of it make a stack</param>
		/// <returns>How many</returns>
		private static int Fits(CarrySpace space, float weight, int maxStack)
		{
			int bySlots = System.Math.Max(0, space.FreeSlots) * System.Math.Max(1, maxStack);
			int byWeight = ((weight > 0f) ? (int)System.Math.Floor(System.Math.Max(0f, space.WeightRoom) / weight) : int.MaxValue);
			return(System.Math.Min(bySlots, byWeight));
		}

		/// <summary>
		/// Rolls a loot table once, adding what it drops to a tally
		/// </summary>
		/// <param name="table">The table</param>
		/// <param name="rng">The random numbers to roll with</param>
		/// <param name="tally">What has dropped, by item, added to</param>
		public static void Roll(LootTable table, System.Random rng, Dictionary<string, int> tally)
		{
			if (table.Drops.Count == 0 || rng.NextDouble() >= table.Chance)
			{
				return;
			}
			if (!table.Picks)
			{
				foreach (LootDrop drop in table.Drops)
				{
					if (rng.NextDouble() < drop.Chance)
					{
						Add(tally, drop.Item, rng.Next(drop.Min, System.Math.Max(drop.Min, drop.Max) + 1));
					}
				}
				return;
			}
			List<LootDrop> pool = new List<LootDrop>(table.Drops);
			int picks = rng.Next(table.PicksMin, System.Math.Max(table.PicksMin, table.PicksMax) + 1);
			for (int i = 0; i < picks && pool.Count > 0; i++)
			{
				float total = 0f;
				foreach (LootDrop drop in pool)
				{
					total += System.Math.Max(0f, drop.Weight);
				}
				double roll = rng.NextDouble() * total;
				LootDrop picked = pool[pool.Count - 1];
				foreach (LootDrop drop in pool)
				{
					roll -= System.Math.Max(0f, drop.Weight);
					if (roll < 0.0)
					{
						picked = drop;
						break;
					}
				}
				Add(tally, picked.Item, rng.Next(picked.Min, System.Math.Max(picked.Min, picked.Max) + 1));
				if (table.OneOfEach)
				{
					pool.Remove(picked);
				}
			}
		}

		/// <summary>
		/// Adds to a tally
		/// </summary>
		/// <param name="tally">The tally</param>
		/// <param name="item">The item</param>
		/// <param name="count">How many more</param>
		public static void Add(Dictionary<string, int> tally, string item, int count)
		{
			if (string.IsNullOrEmpty(item) || count <= 0)
			{
				return;
			}
			tally[item] = (tally.TryGetValue(item, out int known) ? known : 0) + count;
		}

		/// <summary>
		/// Raises what was found by a skill bonus, rounding to the nearest
		/// </summary>
		/// <param name="tally">What was found, changed</param>
		/// <param name="bonus">The share more (see SkillBonus)</param>
		public static void ApplyBonus(Dictionary<string, int> tally, float bonus)
		{
			foreach (string item in new List<string>(tally.Keys))
			{
				tally[item] = (int)System.Math.Round(tally[item] * (1f + System.Math.Max(0f, bonus)));
			}
		}

		/// <summary>
		/// Shares what was found out among vikings by the room each has - the items in the order given, so what the quest
		/// was for goes first, and each item spread across the vikings in turn - leaving behind what none has room for
		/// </summary>
		/// <param name="found">What was found, each item and how many, in the order it's wanted</param>
		/// <param name="spaces">Each viking's room, used up as they're given things</param>
		/// <param name="weightOf">Gets an item's weight</param>
		/// <param name="stackOf">Gets how many of an item make a stack</param>
		/// <returns>What each viking carries home, in the order of spaces</returns>
		public static List<Dictionary<string, int>> Share(List<KeyValuePair<string, int>> found, List<CarrySpace> spaces, System.Func<string, float> weightOf, System.Func<string, int> stackOf)
		{
			List<Dictionary<string, int>> shares = new List<Dictionary<string, int>>();
			foreach (CarrySpace space in spaces)
			{
				shares.Add(new Dictionary<string, int>());
			}
			foreach (KeyValuePair<string, int> entry in found)
			{
				float weight = weightOf(entry.Key);
				int maxStack = System.Math.Max(1, stackOf(entry.Key));
				int left = entry.Value;
				bool placed = true;
				// A unit at a time to each viking in turn, so the load is spread out
				while (left > 0 && placed)
				{
					placed = false;
					for (int i = 0; i < spaces.Count && left > 0; i++)
					{
						if (!TakeOne(spaces[i], shares[i], entry.Key, weight, maxStack))
						{
							continue;
						}
						left--;
						placed = true;
					}
				}
			}
			return(shares);
		}

		/// <summary>
		/// Gives a viking one of an item when it has room - on a stack of it it already has, or in an empty slot
		/// </summary>
		/// <param name="space">The viking's room, used up</param>
		/// <param name="share">What it carries, added to</param>
		/// <param name="item">The item</param>
		/// <param name="weight">Its weight</param>
		/// <param name="maxStack">How many make a stack</param>
		/// <returns>True when it had room</returns>
		private static bool TakeOne(CarrySpace space, Dictionary<string, int> share, string item, float weight, int maxStack)
		{
			if (space.WeightRoom < weight)
			{
				return(false);
			}
			int held = (share.TryGetValue(item, out int known) ? known : 0);
			bool newSlot = (held % maxStack == 0);
			if (newSlot && space.FreeSlots <= 0)
			{
				return(false);
			}
			if (newSlot)
			{
				space.FreeSlots--;
			}
			space.WeightRoom -= weight;
			share[item] = held + 1;
			return(true);
		}

		/// <summary>
		/// Works out the harm a viking takes on a quest - the quest's danger for as long as it's at it, twice as much for
		/// each biome the quest is harder than the one its gear comes from and half for each easier, however many, and
		/// less with more of them than the quest takes, more with fewer - rounded down to a whole share in hundredths, so
		/// a quest far easier than its gear does it no harm, of the health a viking fully fed on its gear's biome's food
		/// has. A viking fed better than that takes a smaller share of its health, and one fed worse a bigger one
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="questBiome">The quest's biome, by VikingGear's order</param>
		/// <param name="gearBiome">The biome the viking's gear comes from</param>
		/// <param name="group">How many went</param>
		/// <param name="wanted">How many the quest takes</param>
		/// <param name="activeSeconds">How long they were at it, in seconds</param>
		/// <param name="fedHealth">The health a viking fully fed on its gear's biome's food has (see
		/// VikingFood.FedHealth)</param>
		/// <returns>The harm, in health</returns>
		public static float Harm(QuestKind kind, int questBiome, int gearBiome, int group, int wanted, float activeSeconds, float fedHealth)
		{
			return(fedHealth * HarmShare(Danger(kind), questBiome - gearBiome, group, wanted, activeSeconds));
		}

		/// <summary>
		/// Checks whether a viking would be expected to lose all its health on a quest, going with as many as the quest
		/// takes for as long as its work usually takes, which keeps it from going
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="gap">How many biomes the quest is harder than the one the viking's gear comes from, less than 0
		/// for easier</param>
		/// <returns>True when it would</returns>
		public static bool ExpectedToDie(QuestKind kind, int gap)
		{
			return(HarmShare(Danger(kind), gap, 1, 1, QuestRules.ActiveSeconds(kind)) >= 1f);
		}

		/// <summary>
		/// Works out how likely a viking is to die on a quest - DeathChanceFull for the square of the share of its health
		/// the quest harms it by, up to all of it, or none at all when that's less than DeathChanceLeast
		/// </summary>
		/// <param name="harmShare">The share of its health the quest harms it by (see HarmShare)</param>
		/// <returns>The chance, 0 or from DeathChanceLeast to DeathChanceFull</returns>
		public static float DeathChance(float harmShare)
		{
			float share = System.Math.Max(0f, System.Math.Min(1f, harmShare));
			float chance = DeathChanceFull * share * share;
			return((chance >= DeathChanceLeast) ? chance : 0f);
		}

		/// <summary>
		/// Works out the share of its health a viking loses on a quest (see Harm)
		/// </summary>
		/// <param name="danger">The quest's danger (see Danger)</param>
		/// <param name="gap">How many biomes the quest is harder than the one the viking's gear comes from, less than 0
		/// for easier</param>
		/// <param name="group">How many went</param>
		/// <param name="wanted">How many the quest takes</param>
		/// <param name="activeSeconds">How long they were at it, in seconds</param>
		/// <returns>The share, rounded down to hundredths</returns>
		internal static float HarmShare(float danger, int gap, int group, int wanted, float activeSeconds)
		{
			double biome = System.Math.Pow(2.0, gap);
			double company = System.Math.Sqrt(System.Math.Max(1, wanted) / (double)System.Math.Max(1, group));
			double share = danger * biome * company * System.Math.Max(0f, activeSeconds) / 1800.0;
			// A sliver more for the rounding, so a share that's exactly whole isn't rounded down for a float's error
			return((float)(System.Math.Floor(share * 100.0 + 1e-6) / 100.0));
		}

		/// <summary>
		/// Puts foods in the order a viking wants them on a quest - for a fight, the strongest first (see
		/// VikingFood.StrongestOrder), and otherwise the way it keeps its foods up at home (see VikingFood.TopUpOrder), so
		/// it travels on the weakest food that still does it good and leaves the strongest for the wounded - with foods
		/// alike in order of their names
		/// </summary>
		/// <param name="foods">The foods, sorted</param>
		/// <param name="fight">Whether the quest fights (see QuestLoot.Fights)</param>
		/// <param name="floor">The viking's floor (see VikingFood.HealthFloor)</param>
		public static void Rank(List<QuestFood> foods, bool fight, float floor)
		{
			foods.Sort((QuestFood a, QuestFood b) =>
			{
				int order = (fight ? VikingFood.StrongestOrder(a.Health, a.Regen, b.Health, b.Regen) : VikingFood.TopUpOrder(a.Health, a.Regen, b.Health, b.Regen, floor));
				return((order != 0) ? order : string.CompareOrdinal(a.Item, b.Item));
			});
		}

		/// <summary>
		/// Works out which foods a viking takes along on a quest - what it would eat of the foods it may take, in the order
		/// it wants them, for as long as the quest is gone (see Eat), going on with the foods it has eaten as it sets out,
		/// up to most foods in all - less what it already carries for it
		/// </summary>
		/// <param name="eating">The foods it has eaten as it sets out, with the time each has left</param>
		/// <param name="ranked">The foods it may take and those it already carries for the quest, one of each item, in the
		/// order it wants them, each with how many it may take or carries</param>
		/// <param name="seconds">How long the quest is gone, in seconds</param>
		/// <param name="most">The most foods it takes along</param>
		/// <param name="carried">How many of each food it already carries for the quest, by item</param>
		/// <returns>How many more of each food it takes, by item</returns>
		public static Dictionary<string, int> Pack(List<QuestMeal> eating, List<QuestFood> ranked, float seconds, int most, Dictionary<string, int> carried)
		{
			Dictionary<string, int> picks = new Dictionary<string, int>();
			foreach (KeyValuePair<string, int> eaten in Eat(eating, ranked, seconds, 0f, most).Eaten)
			{
				int more = eaten.Value - (carried.TryGetValue(eaten.Key, out int held) ? held : 0);
				if (more > 0)
				{
					picks[eaten.Key] = more;
				}
			}
			return(picks);
		}

		/// <summary>
		/// Works out what a viking eats on a quest or trip - going on with the foods it set out with, and eating what it
		/// carries, in the order given, once it has it, whenever one of its FoodsAtOnce places is free, never two of one
		/// food at once, the way a player's foods work - and its max health from them as they wear off (see
		/// VikingFood.MaxHealth), on average while it's gone and as it gets there
		/// </summary>
		/// <param name="eating">The foods it has eaten as it sets out, with the time each has left</param>
		/// <param name="carried">The foods it carries, in the order it eats them, left as they are</param>
		/// <param name="seconds">How long it's gone, in seconds</param>
		/// <param name="floor">The viking's floor (see VikingFood.HealthFloor)</param>
		/// <param name="most">The most foods it eats of what it carries</param>
		/// <returns>What it eats</returns>
		public static QuestMeals Eat(List<QuestMeal> eating, List<QuestFood> carried, float seconds, float floor, int most = int.MaxValue)
		{
			QuestMeals result = new QuestMeals();
			float length = System.Math.Max(0f, seconds);
			List<QuestBite> bites = result.Bites;
			List<QuestBite> up = new List<QuestBite>();
			foreach (QuestMeal meal in eating)
			{
				if (meal.Left > 0f && meal.Burn > 0f && up.Count < FoodsAtOnce && !up.Exists((QuestBite bite) => bite.Item == meal.Item))
				{
					QuestBite bite = new QuestBite { Item = meal.Item, Health = meal.Health, Regen = meal.Regen, Burn = meal.Burn, Start = meal.Left - meal.Burn, End = meal.Left };
					bites.Add(bite);
					up.Add(bite);
				}
			}
			int[] counts = new int[carried.Count];
			for (int i = 0; i < carried.Count; i++)
			{
				counts[i] = System.Math.Max(0, carried[i].Count);
			}
			int eatenCount = 0;
			float now = 0f;
			while (true)
			{
				up.RemoveAll((QuestBite bite) => bite.End <= now);
				while (up.Count < FoodsAtOnce && eatenCount < most)
				{
					int pick = NextFood(carried, counts, up, now);
					if (pick < 0)
					{
						break;
					}
					QuestFood food = carried[pick];
					counts[pick]--;
					eatenCount++;
					QuestBite bite = new QuestBite { Item = food.Item, Health = food.Health, Regen = food.Regen, Burn = food.Burn, Start = now, End = now + food.Burn };
					bites.Add(bite);
					up.Add(bite);
					Add(result.Eaten, food.Item, 1);
				}
				// The next moment anything changes - one of its foods wearing off, or with a place free, a food it finds
				float next = float.MaxValue;
				foreach (QuestBite bite in up)
				{
					next = System.Math.Min(next, bite.End);
				}
				if (up.Count < FoodsAtOnce && eatenCount < most)
				{
					for (int i = 0; i < carried.Count; i++)
					{
						if (counts[i] > 0 && carried[i].Burn > 0f && carried[i].From > now)
						{
							next = System.Math.Min(next, carried[i].From);
						}
					}
				}
				if (next >= length)
				{
					break;
				}
				now = next;
			}
			result.EndMaxHealth = MaxHealthAt(bites, length, floor);
			if (length <= 0f)
			{
				result.MaxHealth = result.EndMaxHealth;
			}
			else
			{
				int samples = (int)System.Math.Min(HealthSamplesMost, System.Math.Max(1.0, System.Math.Ceiling(length / HealthSampleSeconds)));
				float total = 0f;
				for (int i = 0; i < samples; i++)
				{
					total += MaxHealthAt(bites, (i + 0.5f) * length / samples, floor);
				}
				result.MaxHealth = total / samples;
			}
			foreach (QuestBite bite in bites)
			{
				if (bite.End > length)
				{
					result.Lasting.Add(new QuestMeal { Item = bite.Item, Health = bite.Health, Regen = bite.Regen, Burn = bite.Burn, Left = bite.End - length });
				}
			}
			return(result);
		}

		/// <summary>
		/// Finds the next food a viking eats of what it carries - the first, in the order given, it has by now, has any
		/// of left and isn't eating already
		/// </summary>
		/// <param name="carried">The foods it carries, in the order it eats them</param>
		/// <param name="counts">How many of each it has left, by the same index</param>
		/// <param name="up">The foods it's eating</param>
		/// <param name="now">The time, in seconds from when it set out</param>
		/// <returns>The food's index, or -1 when there's none</returns>
		private static int NextFood(List<QuestFood> carried, int[] counts, List<QuestBite> up, float now)
		{
			for (int i = 0; i < carried.Count; i++)
			{
				string item = carried[i].Item;
				if (counts[i] > 0 && carried[i].Burn > 0f && carried[i].From <= now && !up.Exists((QuestBite bite) => bite.Item == item))
				{
					return(i);
				}
			}
			return(-1);
		}

		/// <summary>
		/// Works out a viking's max health at a moment on a quest or trip from the foods it has eaten up then, each fading
		/// as it wears off (see VikingFood.HealthShare)
		/// </summary>
		/// <param name="bites">The foods it eats</param>
		/// <param name="at">The moment, in seconds from when it set out</param>
		/// <param name="floor">The viking's floor (see VikingFood.HealthFloor)</param>
		/// <returns>The max health</returns>
		internal static float MaxHealthAt(List<QuestBite> bites, float at, float floor)
		{
			float food = 0f;
			foreach (QuestBite bite in bites)
			{
				if (bite.Start <= at && at < bite.End)
				{
					food += bite.Health * VikingFood.HealthShare(bite.End - at, bite.Burn);
				}
			}
			return(VikingFood.MaxHealth(floor, food));
		}

		/// <summary>
		/// Works out how much a viking's foods heal it at a moment on a quest or trip - the regen of every food it has
		/// eaten up then, the way its food heals it at home (see VikingFood.Update)
		/// </summary>
		/// <param name="bites">The foods it eats</param>
		/// <param name="at">The moment, in seconds from when it set out</param>
		/// <returns>How much they heal it each TickSeconds</returns>
		private static float RegenAt(List<QuestBite> bites, float at)
		{
			float regen = 0f;
			foreach (QuestBite bite in bites)
			{
				if (bite.Start <= at && at < bite.End)
				{
					regen += bite.Regen;
				}
			}
			return(regen);
		}

		/// <summary>
		/// Works out a viking's health through a quest or trip, a TickSeconds at a time - from its health as it set out,
		/// its foods healing it each TickSeconds while it's below its max health, which follows its food and its health
		/// with it (see VikingFood.Update), its harm spread evenly over the time it's at its work, drinking a healing
		/// potion whenever that leaves it below PotionAt of its max health, and never below HealthFloor of it
		/// </summary>
		/// <param name="meals">What it eats (see Eat)</param>
		/// <param name="floor">The viking's floor (see VikingFood.HealthFloor)</param>
		/// <param name="start">Its health as it set out</param>
		/// <param name="harm">The harm it takes, in health</param>
		/// <param name="harmFrom">When its work starts, in seconds from when it set out</param>
		/// <param name="harmTo">When its work ends, in seconds from when it set out</param>
		/// <param name="seconds">How long it's gone, in seconds</param>
		/// <param name="potions">How many healing potions it carries</param>
		/// <param name="potionHeal">How much one heals</param>
		/// <param name="drunk">Set to how many it drinks</param>
		/// <returns>Its health as it gets there</returns>
		public static float Health(QuestMeals meals, float floor, float start, float harm, float harmFrom, float harmTo, float seconds, int potions, float potionHeal, out int drunk)
		{
			drunk = 0;
			float length = System.Math.Max(0f, seconds);
			float health = System.Math.Min(start, MaxHealthAt(meals.Bites, 0f, floor));
			float from = System.Math.Max(0f, System.Math.Min(harmFrom, length));
			float to = System.Math.Max(from, System.Math.Min(harmTo, length));
			float prev = 0f;
			bool harmed = (harm <= 0f);
			while (true)
			{
				float at = System.Math.Min(prev + TickSeconds, length);
				float max = MaxHealthAt(meals.Bites, at, floor);
				health = System.Math.Min(health, max);
				float dealt = 0f;
				if (!harmed && to > from)
				{
					dealt = harm * (System.Math.Min(at, to) - System.Math.Max(prev, from)) / (to - from);
					dealt = System.Math.Max(0f, dealt);
					harmed = (at >= to);
				}
				else if (!harmed && from <= at)
				{
					dealt = harm;
					harmed = true;
				}
				if (dealt > 0f)
				{
					health -= dealt;
					while (health < max * PotionAt && drunk < potions && potionHeal > 0f)
					{
						health += potionHeal;
						drunk++;
					}
					health = System.Math.Max(health, max * HealthFloor);
				}
				// Its food heals it only at a whole tick, the way it does at home
				if (at - prev >= TickSeconds && health < max)
				{
					health = System.Math.Min(max, health + RegenAt(meals.Bites, at));
				}
				if (at >= length)
				{
					break;
				}
				prev = at;
			}
			float endMax = meals.EndMaxHealth;
			return(System.Math.Max(endMax * HealthFloor, System.Math.Min(endMax, health)));
		}
	}
}
