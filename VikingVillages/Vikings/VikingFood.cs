using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a PC viking has eaten, the way a player's foods work - up to MaxMeals different foods at once, each
	/// lasting its burn time at the world's food rate, adding its health to the viking's max health, fading as it
	/// wears off, and healing its food regen every TickTime seconds, boosted by resting and being rested the same as a
	/// player's, and eaten again once half of it has worn off while it's wounded, or once it has nearly run out to keep
	/// its foods up (see TopUpShare)
	/// </summary>
	/// <remarks>
	/// The meals are noted in the viking's ZDO, so they carry over to whichever client owns it next. The food adds to
	/// a base of 25, the same as a player's, but the viking's max health never falls below the floor for its biome
	/// (see HealthFloors), since a village can't make every food its biome's players eat. Only its food heals it
	/// </remarks>
	internal class VikingFood
	{
		/// <summary>
		/// On a PC viking, the foods it has eaten - each food's prefab and until when in ZNet time ticks it lasts
		/// </summary>
		internal static readonly int MealsKey = "kriona.VikingVillages.vikingMeals".GetStableHashCode();

		/// <summary>
		/// On a PC viking, when its owner last noted it was loaded, in ZNet time ticks - so its food doesn't wear off
		/// while it's out of every player's reach, when it does nothing (see CatchUp)
		/// </summary>
		internal static readonly int SeenKey = "kriona.VikingVillages.vikingFoodSeen".GetStableHashCode();

		/// <summary>
		/// How often a viking's owner notes it's loaded, and how long it goes unnoted before its food counts as having
		/// been put away the while, in seconds - longer than the first, so the moment ownership passes to another client
		/// doesn't count
		/// </summary>
		private const float SeenInterval = 10f;
		private const float UnseenGap = 30f;

		/// <summary>
		/// How many foods a viking can have eaten at once, and how often its food heals it, in seconds - the same as a
		/// player
		/// </summary>
		private const int MaxMeals = 3;
		private const float TickTime = 10f;

		/// <summary>
		/// The share of a food's burn time left at which a viking that isn't wounded eats it again to keep its foods up -
		/// once it has nearly run out, so the village's food lasts
		/// </summary>
		internal const float TopUpShare = 0.1f;

		/// <summary>
		/// The least a food has to raise a viking's max health by for it to eat the food to keep its foods up at full
		/// health (see CanTopUp)
		/// </summary>
		internal const float RaiseLeast = 1f;

		/// <summary>
		/// A food a viking has eaten
		/// </summary>
		internal struct Meal
		{
			public int Prefab;
			public long Until;
		}

		/// <summary>
		/// How often a viking's max health follows its food as the food wears off, in seconds - the same as a player's
		/// </summary>
		private const float MaxHealthInterval = 1f;

		/// <summary>
		/// The health a viking's food adds to - the same as a player's base health (Player.m_baseHP)
		/// </summary>
		internal const float BaseHealth = 25f;

		/// <summary>
		/// The least a viking's max health can be, by the index of the biome its gear comes from (see VikingGear) - the
		/// base health and the health of the previous biome's three best foods, so a village eats at least as well as
		/// the land before its own, and its own biome's food brings it to full strength. A Meadows viking's is 25 above
		/// the base, so it doesn't start out at a bare 25 before its first meal
		/// </summary>
		internal static readonly float[] HealthFloors =
		{
			// Meadows
			50f,
			// Black Forest - Cooked Deer Meat 35, Cooked Boar Meat 30, Grilled Neck Tail 25
			115f,
			// Swamp - Deer Stew 45, Cooked Fish 45, Minced Meat Sauce 40
			155f,
			// Mountains - Sausages 55, Black Soup 50, Deer Stew 45
			175f,
			// Plains - Wolf Skewer 65, Sausages 55, Black Soup 50
			195f,
			// Mistlands - Lox Meat Pie 75, Fish Wraps 70, Wolf Skewer 65
			235f,
			// Ashlands - Misthare Supreme 85, Meat Platter 80, Honey Glazed Chicken 80
			270f,
			// Deep North - Piquant Pie 105, Mashed Meat 100, Fiery Svinstew 95
			325f
		};

		/// <summary>
		/// The viking whose food this is
		/// </summary>
		private readonly PCVikingBody viking;

		/// <summary>
		/// How long since the viking was last healed by its food on this client, and since its max health last followed
		/// its food, in seconds
		/// </summary>
		private float sinceTick;
		private float sinceMaxHealth;

		/// <summary>
		/// Whether the viking's food has caught up with the time it was out of reach since this client began owning it
		/// (see CatchUp), and how long since this client last noted it's loaded, in seconds
		/// </summary>
		private bool caughtUp;
		private float sinceSeen;

		/// <summary>
		/// Starts a PC viking's food
		/// </summary>
		/// <param name="viking">The viking</param>
		public VikingFood(PCVikingBody viking)
		{
			this.viking = viking;
		}

		/// <summary>
		/// Checks whether an item is food a viking can eat - something a player eats for health
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		public static bool IsFood(ItemDrop.ItemData item)
		{
			return(item != null && item.m_shared != null && item.m_dropPrefab != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable && item.m_shared.m_food > 0f);
		}

		/// <summary>
		/// Checks whether the viking can eat a food now - a food it hasn't eaten that fits beside the others, or one it
		/// has eaten that's half worn off
		/// </summary>
		/// <param name="item">The food</param>
		/// <returns>True when it can</returns>
		public bool CanEat(ItemDrop.ItemData item)
		{
			return(Fits(item, 0.5f));
		}

		/// <summary>
		/// Checks whether the viking eats a food to keep its foods up when it isn't wounded - a food it hasn't eaten that
		/// fits beside the others, or one it has eaten that has nearly run out (see TopUpShare) - when it does the viking
		/// good: raising its max health, or healing it while it's below its max health. A food that does neither, like
		/// one too weak to lift it above its floor at full health, is left for the wounded
		/// </summary>
		/// <param name="item">The food</param>
		/// <returns>True when it does</returns>
		public bool CanTopUp(ItemDrop.ItemData item)
		{
			if (!Fits(item, TopUpShare))
			{
				return(false);
			}
			return(viking.GetHealth() < viking.GetMaxHealth() || RaisesMaxHealth(item));
		}

		/// <summary>
		/// Checks whether eating a food raises the viking's max health, by what it has eaten - the food's health in place
		/// of what's left of it, for one it has eaten already (see RaisesMaxHealth)
		/// </summary>
		/// <param name="item">The food</param>
		/// <returns>True when it does</returns>
		private bool RaisesMaxHealth(ItemDrop.ItemData item)
		{
			long now = NetTime.Now();
			int prefab = item.m_dropPrefab.name.GetStableHashCode();
			float replaced = 0f;
			foreach (Meal meal in Read(viking.m_nview.GetZDO()))
			{
				ItemDrop.ItemData food = ((meal.Prefab == prefab) ? FoodOf(meal) : null);
				if (food != null && food.m_shared.m_foodBurnTime > 0f)
				{
					replaced = food.m_shared.m_food * HealthShare((float)(meal.Until - now) / System.TimeSpan.TicksPerSecond, BurnTime(food));
				}
			}
			return(RaisesMaxHealth(Floor(), FoodHealth(), replaced, item.m_shared.m_food));
		}

		/// <summary>
		/// Checks whether eating a food raises a viking's max health by at least RaiseLeast - its max health with the
		/// food's health on top of what its foods add, less what's left of the same food eaten already, against its max
		/// health now (see MaxHealth)
		/// </summary>
		/// <param name="floor">The viking's floor (see HealthFloor)</param>
		/// <param name="foodHealth">What its foods add now (see FoodHealth)</param>
		/// <param name="replaced">What's left of the same food eaten already, which the food takes the place of</param>
		/// <param name="added">The food's health</param>
		/// <returns>True when it does</returns>
		internal static bool RaisesMaxHealth(float floor, float foodHealth, float replaced, float added)
		{
			return(MaxHealth(floor, foodHealth - replaced + added) >= MaxHealth(floor, foodHealth) + RaiseLeast);
		}

		/// <summary>
		/// Checks whether a food fits beside the meals the viking has eaten, eating one it has eaten again once a share of
		/// it is left (see HasRoom)
		/// </summary>
		/// <param name="item">The food</param>
		/// <param name="againShare">The share of its burn time left at which a food it has eaten is eaten again</param>
		/// <returns>True when it fits</returns>
		private bool Fits(ItemDrop.ItemData item, float againShare)
		{
			if (!IsFood(item) || viking.m_nview == null || !viking.m_nview.IsValid())
			{
				return(false);
			}
			List<Meal> meals = Read(viking.m_nview.GetZDO());
			int prefab = item.m_dropPrefab.name.GetStableHashCode();
			return(HasRoom(meals, prefab, NetTime.Now(), NetTime.Ticks(BurnTime(item) * againShare)));
		}

		/// <summary>
		/// Checks whether a food fits beside the meals a viking has eaten - one it has eaten when less than a share of it
		/// is left, or one it hasn't when it has eaten fewer than MaxMeals foods
		/// </summary>
		/// <param name="meals">The meals that still last</param>
		/// <param name="prefab">The food's prefab hash</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <param name="eatAgain">How much of a food it has eaten is left when it eats it again, in ticks</param>
		/// <returns>True when it fits</returns>
		internal static bool HasRoom(List<Meal> meals, int prefab, long now, long eatAgain)
		{
			foreach (Meal meal in meals)
			{
				if (meal.Prefab == prefab)
				{
					return(meal.Until - now < eatAgain);
				}
			}
			return(meals.Count < MaxMeals);
		}

		/// <summary>
		/// Checks whether a food raises a viking's max health above its floor - one adding more than a third of what the
		/// floor holds over the base health, so a full set of foods like it would add more than the floor gives
		/// </summary>
		/// <param name="health">The food's health</param>
		/// <param name="floor">The viking's floor (see HealthFloor)</param>
		/// <returns>True when it does</returns>
		internal static bool BeatsFloor(float health, float floor)
		{
			return(health > (floor - BaseHealth) / MaxMeals);
		}

		/// <summary>
		/// Orders foods for a viking keeping its foods up, or packing for a trip - those that raise it above its floor
		/// before those that don't (see BeatsFloor), the weakest of the first first, so the strongest food is left for the
		/// wounded and fights, and the strongest of the rest first - by health, then by how fast it heals
		/// </summary>
		/// <param name="healthA">The first food's health</param>
		/// <param name="regenA">How much the first food heals every TickTime seconds</param>
		/// <param name="healthB">The second food's health</param>
		/// <param name="regenB">How much the second food heals every TickTime seconds</param>
		/// <param name="floor">The viking's floor (see HealthFloor)</param>
		/// <returns>Less than 0 when the first comes first, more than 0 when the second does, and 0 when they're alike</returns>
		internal static int TopUpOrder(float healthA, float regenA, float healthB, float regenB, float floor)
		{
			bool beatsA = BeatsFloor(healthA, floor);
			bool beatsB = BeatsFloor(healthB, floor);
			if (beatsA != beatsB)
			{
				return(beatsA ? -1 : 1);
			}
			return(beatsA ? -StrongestOrder(healthA, regenA, healthB, regenB) : StrongestOrder(healthA, regenA, healthB, regenB));
		}

		/// <summary>
		/// Orders foods strongest first - by health, then by how fast it heals - for a viking packing for a fight
		/// </summary>
		/// <param name="healthA">The first food's health</param>
		/// <param name="regenA">How much the first food heals every TickTime seconds</param>
		/// <param name="healthB">The second food's health</param>
		/// <param name="regenB">How much the second food heals every TickTime seconds</param>
		/// <returns>Less than 0 when the first comes first, more than 0 when the second does, and 0 when they're alike</returns>
		internal static int StrongestOrder(float healthA, float regenA, float healthB, float regenB)
		{
			if (healthA != healthB)
			{
				return((healthA > healthB) ? -1 : 1);
			}
			if (regenA != regenB)
			{
				return((regenA > regenB) ? -1 : 1);
			}
			return(0);
		}

		/// <summary>
		/// Has the viking eat a food, when it can - lasting the food's burn time from now
		/// </summary>
		/// <param name="item">The food</param>
		/// <returns>False when it can't eat it, or this client doesn't own it</returns>
		public bool Eat(ItemDrop.ItemData item)
		{
			if (!CanEat(item) || !viking.m_nview.IsOwner())
			{
				return(false);
			}
			ZDO zdo = viking.m_nview.GetZDO();
			List<Meal> meals = Read(zdo);
			int prefab = item.m_dropPrefab.name.GetStableHashCode();
			meals.RemoveAll((Meal meal) => meal.Prefab == prefab);
			meals.Add(new Meal { Prefab = prefab, Until = NetTime.Now() + NetTime.Ticks(BurnTime(item)) });
			Write(zdo, meals);
			UpdateMaxHealth();
			return(true);
		}

		/// <summary>
		/// Works out how much the viking's foods add to its max health, the way a player's do - each food's health,
		/// fading as it wears off, slowly at first and quickly at the end
		/// </summary>
		/// <returns>The health its food adds</returns>
		public float FoodHealth()
		{
			if (viking.m_nview == null || !viking.m_nview.IsValid() || ObjectDB.instance == null)
			{
				return(0f);
			}
			long now = NetTime.Now();
			float health = 0f;
			foreach (Meal meal in Read(viking.m_nview.GetZDO()))
			{
				ItemDrop.ItemData food = FoodOf(meal);
				if (food == null || food.m_shared.m_foodBurnTime <= 0f)
				{
					continue;
				}
				float left = (float)(meal.Until - now) / System.TimeSpan.TicksPerSecond;
				health += food.m_shared.m_food * HealthShare(left, BurnTime(food));
			}
			return(health);
		}

		/// <summary>
		/// Works out how much the viking's foods add to its stamina, the way a player's do - each food's stamina, fading
		/// as it wears off like its health (see FoodHealth) - for how long it can swim (see BoatRules.SwimSeconds)
		/// </summary>
		/// <returns>The stamina its food adds</returns>
		public float FoodStamina()
		{
			if (viking.m_nview == null || !viking.m_nview.IsValid() || ObjectDB.instance == null)
			{
				return(0f);
			}
			long now = NetTime.Now();
			float stamina = 0f;
			foreach (Meal meal in Read(viking.m_nview.GetZDO()))
			{
				ItemDrop.ItemData food = FoodOf(meal);
				if (food == null || food.m_shared.m_foodBurnTime <= 0f)
				{
					continue;
				}
				float left = (float)(meal.Until - now) / System.TimeSpan.TicksPerSecond;
				stamina += food.m_shared.m_foodStamina * HealthShare(left, BurnTime(food));
			}
			return(stamina);
		}

		/// <summary>
		/// Works out how long a food lasts on a viking, the way it does on a player - its burn time, worn off faster or
		/// slower by the world's food rate
		/// </summary>
		/// <param name="food">The food</param>
		/// <returns>How long it lasts, in seconds</returns>
		private static float BurnTime(ItemDrop.ItemData food)
		{
			return(Lasts(food.m_shared.m_foodBurnTime, Game.m_foodRate));
		}

		/// <summary>
		/// Works out how long a food lasts at a food rate - its burn time divided by the rate, the way a player's food
		/// counts down by the rate each second, or its burn time when the rate isn't above 0
		/// </summary>
		/// <param name="burnTime">The food's burn time, in seconds</param>
		/// <param name="foodRate">The world's food rate, 1 for the usual</param>
		/// <returns>How long it lasts, in seconds</returns>
		internal static float Lasts(float burnTime, float foodRate)
		{
			return((foodRate > 0f) ? burnTime / foodRate : burnTime);
		}

		/// <summary>
		/// Works out how much of a food's health it still adds, the way a player's does - all of it when it's just eaten,
		/// fading slowly at first and quickly at the end, and none once it has worn off
		/// </summary>
		/// <param name="left">How long the food has left, in seconds</param>
		/// <param name="burnTime">How long the food lasts, in seconds, more than 0</param>
		/// <returns>The share of its health, from 0 to 1</returns>
		internal static float HealthShare(float left, float burnTime)
		{
			return(Mathf.Pow(Mathf.Clamp01(left / burnTime), 0.3f));
		}

		/// <summary>
		/// Gets the least a viking's max health can be for the biome its gear comes from
		/// </summary>
		/// <param name="biome">The biome's index in VikingGear's tables, kept within HealthFloors</param>
		/// <returns>The floor</returns>
		internal static float HealthFloor(int biome)
		{
			return(HealthFloors[Mathf.Clamp(biome, 0, HealthFloors.Length - 1)]);
		}

		/// <summary>
		/// Gets the viking's floor, by the biome its gear comes from (see HealthFloor)
		/// </summary>
		/// <returns>The floor, or the first biome's before its data is read</returns>
		public float Floor()
		{
			VikingCore core = viking.GetComponent<VikingCore>();
			return(HealthFloor((core != null && core.GetData() != null) ? core.BiomeIndex() : 0));
		}

		/// <summary>
		/// Lists the foods the viking has eaten that still last, with their health, how long each lasts and how long it
		/// has left, for working out what it eats on a quest (see QuestSim.Eat)
		/// </summary>
		/// <returns>The foods</returns>
		public List<QuestMeal> Eating()
		{
			if (viking.m_nview == null || !viking.m_nview.IsValid())
			{
				return(new List<QuestMeal>());
			}
			return(Eating(Read(viking.m_nview.GetZDO()), NetTime.Now()));
		}

		/// <summary>
		/// Lists meals as the foods a viking has eaten, with their health, healing, how long each lasts and how long it
		/// has left at a moment, for working out what it eats on a quest or trip (see QuestSim.Eat)
		/// </summary>
		/// <param name="meals">The meals</param>
		/// <param name="at">The moment, in ZNet time ticks</param>
		/// <returns>The foods, leaving out those that can't be found or have worn off by then</returns>
		public static List<QuestMeal> Eating(List<Meal> meals, long at)
		{
			List<QuestMeal> eating = new List<QuestMeal>();
			if (ObjectDB.instance == null)
			{
				return(eating);
			}
			foreach (Meal meal in meals)
			{
				ItemDrop.ItemData food = FoodOf(meal);
				float left = (float)(meal.Until - at) / System.TimeSpan.TicksPerSecond;
				if (food != null && food.m_shared.m_foodBurnTime > 0f && left > 0f)
				{
					eating.Add(new QuestMeal { Item = food.m_dropPrefab.name, Health = food.m_shared.m_food, Regen = food.m_shared.m_foodRegen, Burn = BurnTime(food), Left = left });
				}
			}
			return(eating);
		}

		/// <summary>
		/// Notes the foods a viking still has eaten as it comes home from a quest (see QuestSim.Eat), so its max health
		/// follows them once it's back, and that it was last seen as it got there, so the time it's away doesn't put
		/// them off (see CatchUp)
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		/// <param name="lasting">The foods it still has eaten at the quest's end, each with the time it has left</param>
		/// <param name="end">When the quest ends, in ZNet time ticks</param>
		public static void Note(ZDO zdo, List<QuestMeal> lasting, long end)
		{
			Write(zdo, MealsFrom(lasting, end));
			zdo.Set(SeenKey, end);
		}

		/// <summary>
		/// Works out the meals a viking has from foods it has eaten, each lasting the time it has left past a moment
		/// </summary>
		/// <param name="eating">The foods, each with the time it has left</param>
		/// <param name="from">The moment, in ZNet time ticks</param>
		/// <returns>The meals, leaving out those with no time left</returns>
		internal static List<Meal> MealsFrom(List<QuestMeal> eating, long from)
		{
			List<Meal> meals = new List<Meal>();
			foreach (QuestMeal meal in eating)
			{
				if (meal.Left > 0f)
				{
					meals.Add(new Meal { Prefab = meal.Item.GetStableHashCode(), Until = from + (long)(meal.Left * System.TimeSpan.TicksPerSecond) });
				}
			}
			return(meals);
		}

		/// <summary>
		/// Works out a viking's max health - the base health and what its food adds, the way a player's is, or its
		/// biome's floor when that's more
		/// </summary>
		/// <param name="floor">Its biome's floor (see HealthFloor)</param>
		/// <param name="foodHealth">What its food adds (see FoodHealth)</param>
		/// <returns>The max health</returns>
		internal static float MaxHealth(float floor, float foodHealth)
		{
			return(Mathf.Max(floor, BaseHealth + foodHealth));
		}

		/// <summary>
		/// Works out the viking's max health from its biome and what it has eaten (see MaxHealth)
		/// </summary>
		/// <returns>The max health, or 0 before its data is read</returns>
		public float MaxHealth()
		{
			VikingCore core = viking.GetComponent<VikingCore>();
			if (core == null || core.GetData() == null)
			{
				return(0f);
			}
			return(MaxHealth(HealthFloor(core.BiomeIndex()), FoodHealth()));
		}

		/// <summary>
		/// Sets the viking's max health from its biome and food, the way a player's follows its food - its health drops
		/// with the max as food wears off, while a higher max fills in as it heals - on its owner
		/// </summary>
		public void UpdateMaxHealth()
		{
			float max = MaxHealth();
			if (max <= 0f || viking.m_nview == null || !viking.m_nview.IsOwner())
			{
				return;
			}
			if (Mathf.Abs(max - viking.GetMaxHealth()) > 0.05f)
			{
				viking.SetMaxHealth(max);
			}
		}

		/// <summary>
		/// Heals the viking by its foods' regen every TickTime seconds while they last, boosted by a multiplier, and
		/// keeps its max health following its food every MaxHealthInterval seconds, first catching its food up with the
		/// time it was out of every player's reach (see CatchUp) - call it while this client owns it
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <param name="multiplier">How much faster it heals, from resting and being rested</param>
		public void Update(float dt, float multiplier)
		{
			if (viking.m_nview != null && viking.m_nview.IsValid())
			{
				if (!caughtUp)
				{
					caughtUp = true;
					CatchUp(viking.m_nview.GetZDO());
				}
				sinceSeen += dt;
				if (sinceSeen >= SeenInterval)
				{
					sinceSeen = 0f;
					viking.m_nview.GetZDO().Set(SeenKey, NetTime.Now());
				}
			}
			sinceMaxHealth += dt;
			if (sinceMaxHealth >= MaxHealthInterval)
			{
				sinceMaxHealth = 0f;
				UpdateMaxHealth();
			}
			sinceTick += dt;
			if (sinceTick < TickTime)
			{
				return;
			}
			sinceTick = 0f;
			if (viking.m_nview == null || !viking.m_nview.IsValid() || ObjectDB.instance == null)
			{
				return;
			}
			float regen = 0f;
			foreach (Meal meal in Read(viking.m_nview.GetZDO()))
			{
				ItemDrop.ItemData food = FoodOf(meal);
				if (food != null)
				{
					regen += food.m_shared.m_foodRegen;
				}
			}
			if (regen > 0f && viking.GetHealth() < viking.GetMaxHealth())
			{
				viking.Heal(regen * multiplier, false);
			}
		}

		/// <summary>
		/// Describes what the viking has eaten for the debug hover text - each food that still lasts and how long it has
		/// left, the health they add over the base, the viking's floor (see HealthFloor), and how much they heal it
		/// together every TickTime seconds
		/// </summary>
		/// <returns>The description</returns>
		public string Describe()
		{
			if (viking.m_nview == null || !viking.m_nview.IsValid() || ObjectDB.instance == null)
			{
				return("unknown");
			}
			List<Meal> meals = Read(viking.m_nview.GetZDO());
			if (meals.Count == 0)
			{
				return("nothing");
			}
			long now = NetTime.Now();
			List<string> foods = new List<string>();
			float regen = 0f;
			foreach (Meal meal in meals)
			{
				ItemDrop.ItemData food = FoodOf(meal);
				string name = ((food != null) ? Localization.instance.Localize(food.m_shared.m_name) : "unknown food");
				if (food != null)
				{
					regen += food.m_shared.m_foodRegen;
				}
				foods.Add(name + " (" + Duration((float)(meal.Until - now) / System.TimeSpan.TicksPerSecond) + " left)");
			}
			VikingCore core = viking.GetComponent<VikingCore>();
			string floor = ((core != null) ? ", floor " + HealthFloor(core.BiomeIndex()).ToString("0") : "");
			return(string.Join(", ", foods) + " - +" + FoodHealth().ToString("0") + " health over the base " + BaseHealth.ToString("0") + floor + ", heals " + regen.ToString("0.#") + " every " + TickTime.ToString("0") + "s");
		}

		/// <summary>
		/// Writes a time in minutes and seconds
		/// </summary>
		/// <param name="seconds">The time, in seconds</param>
		/// <returns>The time, like "4m 12s"</returns>
		public static string Duration(float seconds)
		{
			int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
			return((total >= 60) ? (total / 60) + "m " + (total % 60) + "s" : total + "s");
		}

		/// <summary>
		/// Finds the food a meal was of
		/// </summary>
		/// <param name="meal">The meal</param>
		/// <returns>The food's item data, or null when its prefab can't be found</returns>
		private static ItemDrop.ItemData FoodOf(Meal meal)
		{
			GameObject prefab = ObjectDB.instance.GetItemPrefab(meal.Prefab);
			ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
			return((drop != null) ? drop.m_itemData : null);
		}

		/// <summary>
		/// Reads the foods a viking has eaten that still last
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <returns>The meals</returns>
		private static List<Meal> Read(ZDO zdo)
		{
			return(Lasting(ReadAll(zdo), NetTime.Now()));
		}

		/// <summary>
		/// Leaves out the meals that have worn off - those that last until now or earlier
		/// </summary>
		/// <param name="meals">The meals, taken from</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <returns>The same list, holding the meals that still last</returns>
		internal static List<Meal> Lasting(List<Meal> meals, long now)
		{
			meals.RemoveAll((Meal meal) => meal.Until <= now);
			return(meals);
		}

		/// <summary>
		/// Reads every food noted in a viking's ZDO, worn off or not
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <returns>The meals</returns>
		private static List<Meal> ReadAll(ZDO zdo)
		{
			return(Decode(zdo.GetByteArray(MealsKey)));
		}

		/// <summary>
		/// Reads meals from the bytes they're noted as - none when there are no bytes or they can't be read, since this
		/// is read every second
		/// </summary>
		/// <param name="bytes">The bytes (see Encode), or null</param>
		/// <returns>The meals</returns>
		internal static List<Meal> Decode(byte[] bytes)
		{
			List<Meal> meals = new List<Meal>();
			if (bytes == null)
			{
				return(meals);
			}
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					meals.Add(new Meal { Prefab = pkg.ReadInt(), Until = pkg.ReadLong() });
				}
			}
			catch (System.Exception)
			{
				meals.Clear();
			}
			return(meals);
		}

		/// <summary>
		/// Writes meals as bytes, each one's prefab and until when it lasts
		/// </summary>
		/// <param name="meals">The meals</param>
		/// <returns>The bytes</returns>
		internal static byte[] Encode(List<Meal> meals)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(meals.Count);
			foreach (Meal meal in meals)
			{
				pkg.Write(meal.Prefab);
				pkg.Write(meal.Until);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Puts off the end of each food a viking has eaten by the time it went unnoted as loaded (see Unseen) - out of
		/// every player's reach, where it does nothing, so its food lasts as long as it would have had no time passed -
		/// and notes it's loaded now
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		private static void CatchUp(ZDO zdo)
		{
			long now = NetTime.Now();
			long unseen = Unseen(zdo.GetLong(SeenKey, 0L), now, NetTime.Ticks(UnseenGap));
			if (unseen > 0L)
			{
				Postpone(zdo, unseen);
			}
			zdo.Set(SeenKey, now);
		}

		/// <summary>
		/// Works out how long a viking went unnoted as loaded - from when it was last noted until now, when that's longer
		/// than a gap, and otherwise none, as for a viking never noted
		/// </summary>
		/// <param name="seen">When it was last noted as loaded, in ZNet time ticks, or 0 when it never was</param>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="gap">The longest it goes unnoted while loaded, in ticks</param>
		/// <returns>How long it went unnoted, in ticks</returns>
		internal static long Unseen(long seen, long now, long gap)
		{
			return((seen > 0L && now - seen > gap) ? now - seen : 0L);
		}

		/// <summary>
		/// Puts off the end of each food a viking has eaten by the time the world skipped ahead while players slept, so
		/// its food lasts as long as it would have, the way a player's does - leaving alone what had already worn off
		/// before the skip
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		/// <param name="skipped">How far the world skipped ahead, in ZNet time ticks</param>
		public static void Postpone(ZDO zdo, long skipped)
		{
			if (skipped <= 0L || zdo.GetByteArray(MealsKey) == null)
			{
				return;
			}
			List<Meal> meals = Postponed(ReadAll(zdo), NetTime.Now(), skipped);
			if (meals.Count > 0)
			{
				Write(zdo, meals);
			}
		}

		/// <summary>
		/// Works out the meals a viking has eaten once their ends are put off by the time the world skipped ahead -
		/// leaving out what had already worn off before the skip
		/// </summary>
		/// <param name="meals">The meals, worn off or not, left as they are</param>
		/// <param name="now">The time now, after the skip, in ZNet time ticks</param>
		/// <param name="skipped">How far the world skipped ahead, in ticks</param>
		/// <returns>The meals that still lasted as the skip began, each lasting skipped longer - none when nothing was
		/// skipped</returns>
		internal static List<Meal> Postponed(List<Meal> meals, long now, long skipped)
		{
			if (skipped <= 0L)
			{
				return(new List<Meal>());
			}
			long before = now - skipped;
			List<Meal> postponed = meals.FindAll((Meal meal) => meal.Until > before);
			for (int i = 0; i < postponed.Count; i++)
			{
				Meal meal = postponed[i];
				meal.Until += skipped;
				postponed[i] = meal;
			}
			return(postponed);
		}

		/// <summary>
		/// Writes the foods a viking has eaten
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		/// <param name="meals">The meals</param>
		private static void Write(ZDO zdo, List<Meal> meals)
		{
			zdo.Set(MealsKey, Encode(meals));
		}
	}
}
