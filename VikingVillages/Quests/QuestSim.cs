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
	/// A food a viking takes along on a quest - the item, the health it adds, how much it heals every 10 seconds, how
	/// long it lasts at the world's food rate, in seconds, and how many of it
	/// </summary>
	internal class QuestFood
	{
		public string Item;
		public float Health;
		public float Regen;
		public float Burn;
		public int Count;
	}

	/// <summary>
	/// A food a viking has eaten - the item, the health it adds, how long it lasts at the world's food rate and how long
	/// it has left, in seconds
	/// </summary>
	internal class QuestMeal
	{
		public string Item;
		public float Health;
		public float Burn;
		public float Left;
	}

	/// <summary>
	/// What a viking eats on a quest (see QuestSim.Eat) - how many of each food, the share of the time its food was up,
	/// its max health on average while it's out and as it comes home, and the foods it has eaten that still last then,
	/// with the time each has left
	/// </summary>
	internal class QuestMeals
	{
		public Dictionary<string, int> Eaten = new Dictionary<string, int>();
		public float FedShare;
		public float MaxHealth;
		public float EndMaxHealth;
		public List<QuestMeal> Lasting = new List<QuestMeal>();

		/// <summary>
		/// Whether its food was up at least QuestSim.FedShare of the time, which keeps its health coming back
		/// </summary>
		public bool Fed
		{
			get
			{
				return(FedShare >= QuestSim.FedShare);
			}
		}
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
		/// The share of the harm a viking takes on a quest still with it as it comes home - less when it had food to
		/// keep its health coming back - and the share it drinks a potion at
		/// </summary>
		public const float LingerFed = 0.3f;
		public const float LingerHungry = 0.7f;
		public const float PotionAt = 0.25f;

		/// <summary>
		/// How many foods a viking keeps up at once, and how long each lasts when it's not known, in seconds
		/// </summary>
		public const int FoodsAtOnce = 3;
		public const float DefaultBurn = 1800f;

		/// <summary>
		/// The share of the time a viking's food has to be up on a quest for it to count as fed (see QuestMeals.Fed)
		/// </summary>
		public const float FedShare = 0.5f;

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
		/// A food a viking eats on a quest, from when it's eaten - before the quest, for one it set out with - until it
		/// wears off, in seconds from when the quest sets out
		/// </summary>
		private class Bite
		{
			public string Item;
			public float Health;
			public float Burn;
			public float Start;
			public float End;
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
		/// Works out which foods a viking takes along on a quest - going down the foods it may take in the order it wants
		/// them, no more than perKind of one so it has different foods to eat side by side, until it has as many as it
		/// wants, then more of those it wants most when there aren't enough different foods
		/// </summary>
		/// <param name="ranked">The foods it may take, one of each item, in the order it wants them, each with how many
		/// it may take</param>
		/// <param name="wanted">How many more foods it wants</param>
		/// <param name="perKind">The most of one food it takes while there are others, counting what it carries</param>
		/// <param name="carried">How many of each food it already carries for the quest, by item</param>
		/// <returns>How many more of each food it takes, by item</returns>
		public static Dictionary<string, int> Pack(List<QuestFood> ranked, int wanted, int perKind, Dictionary<string, int> carried)
		{
			Dictionary<string, int> picks = new Dictionary<string, int>();
			int left = System.Math.Max(0, wanted);
			foreach (bool capped in new bool[] { true, false })
			{
				foreach (QuestFood food in ranked)
				{
					if (left <= 0)
					{
						break;
					}
					int picked = (picks.TryGetValue(food.Item, out int known) ? known : 0);
					int had = (carried.TryGetValue(food.Item, out int held) ? held : 0);
					int room = (capped ? System.Math.Min(food.Count, System.Math.Max(1, perKind) - had) : food.Count) - picked;
					int take = System.Math.Min(left, room);
					if (take > 0)
					{
						picks[food.Item] = picked + take;
						left -= take;
					}
				}
			}
			return(picks);
		}

		/// <summary>
		/// Works out what a viking eats on a quest - going on with the foods it set out with, and eating what it carries,
		/// in the order given, whenever one of its FoodsAtOnce places is free, never two of one food at once, the way a
		/// player's foods work - and its max health from them as they wear off (see VikingFood.MaxHealth), on average
		/// while it's gone and as it comes home
		/// </summary>
		/// <param name="eating">The foods it has eaten as it sets out, with the time each has left</param>
		/// <param name="carried">The foods it carries, in the order it eats them, left as they are</param>
		/// <param name="seconds">How long the quest is gone, in seconds</param>
		/// <param name="floor">The viking's floor (see VikingFood.HealthFloor)</param>
		/// <returns>What it eats</returns>
		public static QuestMeals Eat(List<QuestMeal> eating, List<QuestFood> carried, float seconds, float floor)
		{
			QuestMeals result = new QuestMeals();
			float length = System.Math.Max(0f, seconds);
			List<Bite> bites = new List<Bite>();
			List<Bite> up = new List<Bite>();
			foreach (QuestMeal meal in eating)
			{
				if (meal.Left > 0f && meal.Burn > 0f && up.Count < FoodsAtOnce && !up.Exists((Bite bite) => bite.Item == meal.Item))
				{
					Bite bite = new Bite { Item = meal.Item, Health = meal.Health, Burn = meal.Burn, Start = meal.Left - meal.Burn, End = meal.Left };
					bites.Add(bite);
					up.Add(bite);
				}
			}
			int[] counts = new int[carried.Count];
			for (int i = 0; i < carried.Count; i++)
			{
				counts[i] = System.Math.Max(0, carried[i].Count);
			}
			float now = 0f;
			while (true)
			{
				up.RemoveAll((Bite bite) => bite.End <= now);
				while (up.Count < FoodsAtOnce)
				{
					int next = NextFood(carried, counts, up);
					if (next < 0)
					{
						break;
					}
					QuestFood food = carried[next];
					counts[next]--;
					Bite bite = new Bite { Item = food.Item, Health = food.Health, Burn = food.Burn, Start = now, End = now + food.Burn };
					bites.Add(bite);
					up.Add(bite);
					Add(result.Eaten, food.Item, 1);
				}
				if (up.Count == 0)
				{
					break;
				}
				float wearsOff = float.MaxValue;
				foreach (Bite bite in up)
				{
					wearsOff = System.Math.Min(wearsOff, bite.End);
				}
				if (wearsOff >= length)
				{
					break;
				}
				now = wearsOff;
			}
			result.EndMaxHealth = MaxHealthAt(bites, length, floor);
			if (length <= 0f)
			{
				result.MaxHealth = result.EndMaxHealth;
				result.FedShare = 1f;
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
				float upTime = 0f;
				foreach (Bite bite in bites)
				{
					upTime += System.Math.Max(0f, System.Math.Min(bite.End, length) - System.Math.Max(bite.Start, 0f));
				}
				result.FedShare = System.Math.Min(1f, upTime / (FoodsAtOnce * length));
			}
			foreach (Bite bite in bites)
			{
				if (bite.End > length)
				{
					result.Lasting.Add(new QuestMeal { Item = bite.Item, Health = bite.Health, Burn = bite.Burn, Left = bite.End - length });
				}
			}
			return(result);
		}

		/// <summary>
		/// Finds the next food a viking eats of what it carries - the first, in the order given, it has any of left and
		/// isn't eating already
		/// </summary>
		/// <param name="carried">The foods it carries, in the order it eats them</param>
		/// <param name="counts">How many of each it has left, by the same index</param>
		/// <param name="up">The foods it's eating</param>
		/// <returns>The food's index, or -1 when there's none</returns>
		private static int NextFood(List<QuestFood> carried, int[] counts, List<Bite> up)
		{
			for (int i = 0; i < carried.Count; i++)
			{
				string item = carried[i].Item;
				if (counts[i] > 0 && carried[i].Burn > 0f && !up.Exists((Bite bite) => bite.Item == item))
				{
					return(i);
				}
			}
			return(-1);
		}

		/// <summary>
		/// Works out a viking's max health at a moment on a quest from the foods it has eaten up then, each fading as it
		/// wears off (see VikingFood.HealthShare)
		/// </summary>
		/// <param name="bites">The foods it eats</param>
		/// <param name="at">The moment, in seconds from when the quest sets out</param>
		/// <param name="floor">The viking's floor (see VikingFood.HealthFloor)</param>
		/// <returns>The max health</returns>
		private static float MaxHealthAt(List<Bite> bites, float at, float floor)
		{
			float food = 0f;
			foreach (Bite bite in bites)
			{
				if (bite.Start <= at && at < bite.End)
				{
					food += bite.Health * VikingFood.HealthShare(bite.End - at, bite.Burn);
				}
			}
			return(VikingFood.MaxHealth(floor, food));
		}

		/// <summary>
		/// Works out how many foods a viking wants on a quest - a full set of FoodsAtOnce, again each time they wear off
		/// </summary>
		/// <param name="seconds">How long the quest is gone, in seconds</param>
		/// <param name="burnSeconds">How long a food lasts, in seconds</param>
		/// <returns>How many</returns>
		public static int MealsWanted(float seconds, float burnSeconds)
		{
			float burn = ((burnSeconds > 0f) ? burnSeconds : DefaultBurn);
			return(FoodsAtOnce * (int)System.Math.Ceiling(System.Math.Max(0f, seconds) / burn));
		}

		/// <summary>
		/// Works out how a viking comes home - the share of its harm still with it, by whether it ate enough, less what
		/// the potions it drinks heal, drinking them while it's below PotionAt of its health - never below HealthFloor
		/// </summary>
		/// <param name="maxHealth">Its most health</param>
		/// <param name="harm">The harm it took (see Harm)</param>
		/// <param name="fed">Whether it was fed (see QuestMeals.Fed)</param>
		/// <param name="potions">How many healing potions it carries</param>
		/// <param name="potionHeal">How much one heals</param>
		/// <param name="drunk">Set to how many it drinks</param>
		/// <returns>Its health as it comes home</returns>
		public static float EndHealth(float maxHealth, float harm, bool fed, int potions, float potionHeal, out int drunk)
		{
			float health = maxHealth - System.Math.Max(0f, harm) * (fed ? LingerFed : LingerHungry);
			drunk = 0;
			while (health < maxHealth * PotionAt && drunk < potions && potionHeal > 0f)
			{
				health += potionHeal;
				drunk++;
			}
			return(System.Math.Max(maxHealth * HealthFloor, System.Math.Min(maxHealth, health)));
		}
	}
}
