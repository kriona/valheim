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
			{ QuestKind.KillEikthyr, 0.6f },
			{ QuestKind.FindBees, 0.15f },
			{ QuestKind.HuntGhosts, 0.5f }
		};

		/// <summary>
		/// The share of a viking's health it comes home at, at least - vikings never die out on a quest
		/// </summary>
		public const float HealthFloor = 0.1f;

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
		/// each biome the quest is harder than the one its gear comes from and half for each easier, and less with more of
		/// them than the quest takes, more with fewer
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="questBiome">The quest's biome, by VikingGear's order</param>
		/// <param name="gearBiome">The biome the viking's gear comes from</param>
		/// <param name="group">How many went</param>
		/// <param name="wanted">How many the quest takes</param>
		/// <param name="activeSeconds">How long they were at it, in seconds</param>
		/// <param name="maxHealth">The viking's most health</param>
		/// <returns>The harm, in health</returns>
		public static float Harm(QuestKind kind, int questBiome, int gearBiome, int group, int wanted, float activeSeconds, float maxHealth)
		{
			int gap = System.Math.Max(-3, System.Math.Min(3, questBiome - gearBiome));
			float biome = (float)System.Math.Pow(2.0, gap);
			float company = (float)System.Math.Sqrt(System.Math.Max(1, wanted) / (double)System.Math.Max(1, group));
			return(maxHealth * Danger(kind) * biome * company * System.Math.Max(0f, activeSeconds) / 1800f);
		}

		/// <summary>
		/// Works out how many foods a viking eats on a quest - a full set of FoodsAtOnce, again each time they wear off -
		/// out of what it carries
		/// </summary>
		/// <param name="seconds">How long the quest is gone, in seconds</param>
		/// <param name="burnSeconds">How long a food lasts, in seconds</param>
		/// <param name="carried">How many foods it carries</param>
		/// <returns>How many it eats</returns>
		public static int MealsEaten(float seconds, float burnSeconds, int carried)
		{
			return(System.Math.Min(System.Math.Max(0, carried), MealsWanted(seconds, burnSeconds)));
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
		/// <param name="fed">Whether it ate at least half the foods it wanted</param>
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
