using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a living viking has eaten, the way a player's foods work - up to MaxMeals different foods at once, each
	/// lasting its burn time at the world's food rate, adding its health to the viking's max health, fading as it
	/// wears off, and healing its food regen every TickTime seconds, boosted by resting and being rested the same as a
	/// player's, and eaten again once half of it has worn off
	/// </summary>
	/// <remarks>
	/// The meals are noted in the viking's ZDO, so they carry over to whichever client owns it next. The food adds to
	/// the viking's own health for its biome and stars, where a player's adds to a base of 25
	/// </remarks>
	internal class VikingFood
	{
		/// <summary>
		/// On a living viking, the foods it has eaten - each food's prefab and until when in ZNet time ticks it lasts
		/// </summary>
		private static readonly int MealsKey = "kriona.VikingVillages.vikingMeals".GetStableHashCode();

		/// <summary>
		/// How many foods a viking can have eaten at once, and how often its food heals it, in seconds - the same as a
		/// player
		/// </summary>
		private const int MaxMeals = 3;
		private const float TickTime = 10f;

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
		/// The viking whose food this is
		/// </summary>
		private readonly LivingVikingBody viking;

		/// <summary>
		/// How long since the viking was last healed by its food on this client, and since its max health last followed
		/// its food, in seconds
		/// </summary>
		private float sinceTick;
		private float sinceMaxHealth;

		/// <summary>
		/// Starts a living viking's food
		/// </summary>
		/// <param name="viking">The viking</param>
		public VikingFood(LivingVikingBody viking)
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
			if (!IsFood(item) || viking.m_nview == null || !viking.m_nview.IsValid())
			{
				return(false);
			}
			List<Meal> meals = Read(viking.m_nview.GetZDO());
			int prefab = item.m_dropPrefab.name.GetStableHashCode();
			return(HasRoom(meals, prefab, NetTime.Now(), NetTime.Ticks(BurnTime(item) / 2f)));
		}

		/// <summary>
		/// Checks whether a food fits beside the meals a viking has eaten - one it has eaten when less than half of it is
		/// left, or one it hasn't when it has eaten fewer than MaxMeals foods
		/// </summary>
		/// <param name="meals">The meals that still last</param>
		/// <param name="prefab">The food's prefab hash</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <param name="halfBurn">Half the food's burn time, in ticks</param>
		/// <returns>True when it fits</returns>
		internal static bool HasRoom(List<Meal> meals, int prefab, long now, long halfBurn)
		{
			foreach (Meal meal in meals)
			{
				if (meal.Prefab == prefab)
				{
					return(meal.Until - now < halfBurn);
				}
			}
			return(meals.Count < MaxMeals);
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
		/// Sets the viking's max health to its own health and what its food adds, the way a player's follows its food -
		/// its health drops with the max as food wears off, while a higher max fills in as it heals - on its owner
		/// </summary>
		private void UpdateMaxHealth()
		{
			VikingCore core = viking.GetComponent<VikingCore>();
			float baseHealth = ((core != null) ? core.BaseHealth() : 0f);
			if (baseHealth <= 0f || viking.m_nview == null || !viking.m_nview.IsOwner())
			{
				return;
			}
			float max = baseHealth + FoodHealth();
			if (Mathf.Abs(max - viking.GetMaxHealth()) > 0.05f)
			{
				viking.SetMaxHealth(max);
			}
		}

		/// <summary>
		/// Heals the viking by its foods' regen every TickTime seconds while they last, boosted by a multiplier, and
		/// keeps its max health following its food every MaxHealthInterval seconds - call it while this client owns it
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <param name="multiplier">How much faster it heals, from resting and being rested</param>
		public void Update(float dt, float multiplier)
		{
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
		/// left, and how much they heal it together every TickTime seconds
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
			return(string.Join(", ", foods) + " - +" + FoodHealth().ToString("0") + " max health, heals " + regen.ToString("0.#") + " every " + TickTime.ToString("0") + "s");
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
