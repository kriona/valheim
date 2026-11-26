using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// What vikings out of every player's reach eat on their road, and how their food heals them - worked out as they
	/// arrive, on the server, from the values they're kept with on the road (see VillageTravel), the same way as on a
	/// quest (see QuestSim.Eat and QuestSim.Health)
	/// </summary>
	/// <remarks>
	/// They go on with the foods they had eaten as they left every player's reach and eat what they carry, the way they
	/// keep their foods up at home (see VikingFood.TopUpOrder), along with what they find growing on the way (see
	/// TripForage) once they've found it, each found thing spread evenly over the road. What they eat comes out of their
	/// inventory, the foods they still have eaten are noted to last past their arrival, and their health is what their
	/// food healed it to. A group out on a quest has its food worked out as it set out (see QuestGame.OutcomeFor), so
	/// it's left alone here
	/// </remarks>
	internal static class TripFood
	{
		/// <summary>
		/// Feeds each viking of a group arriving at the end of its road - once, as it's due, on the server
		/// </summary>
		/// <param name="entry">The group, its vikings' values changed</param>
		/// <param name="found">What each viking found growing on the way, in the order of the group's vikings (see
		/// TripForage.Give)</param>
		/// <param name="now">The time it arrives, in ZNet time ticks</param>
		public static void Feed(TravelEntry entry, List<string>[] found, long now)
		{
			if (entry.OnQuest || ObjectDB.instance == null)
			{
				return;
			}
			float seconds = System.Math.Max(0f, (System.Math.Min(now, entry.Due) - entry.Unloaded) / (float)NetTime.Ticks(1f));
			int eaten = 0;
			for (int i = 0; i < entry.Vikings.Count; i++)
			{
				byte[] fed = Feed(entry.Vikings[i], ((i < found.Length) ? found[i] : new List<string>()), entry.Unloaded, seconds, now, entry.Biome, ref eaten);
				if (fed != null)
				{
					entry.Vikings[i] = fed;
				}
			}
			if (eaten > 0)
			{
				Plugin.Log.LogInfo("Travel: " + entry.Vikings.Count + " vikings on their way to " + entry.Destination.ToString("F0") + " ate " + eaten + " foods along the road");
			}
		}

		/// <summary>
		/// Feeds a viking at the end of its road - what it eats, the foods it still has eaten as it arrives, and its
		/// health as its food healed it
		/// </summary>
		/// <param name="snapshot">The viking's values (see VillageDeparted.SnapshotOf)</param>
		/// <param name="found">What it found growing on the way, a prefab name for each thing in the order it found them,
		/// already in its inventory</param>
		/// <param name="unloaded">When it left every player's reach, in ZNet time ticks</param>
		/// <param name="seconds">How long it was on the road out of reach, in seconds</param>
		/// <param name="now">The time it arrives, in ticks</param>
		/// <param name="fallbackBiome">Its biome when its values don't say, by VikingGear's order</param>
		/// <param name="eaten">How many foods the group ate, added to</param>
		/// <returns>The viking's values once fed, or null when they couldn't be read</returns>
		private static byte[] Feed(byte[] snapshot, List<string> found, long unloaded, float seconds, long now, int fallbackBiome, ref int eaten)
		{
			VillageDeparted.VikingValues values = VillageDeparted.Decode(snapshot);
			if (values == null)
			{
				return(null);
			}
			int at = values.Bytes.FindIndex((KeyValuePair<int, byte[]> value) => value.Key == VikingCore.DataKey);
			VikingData data = ((at >= 0) ? VikingData.FromBytes(values.Bytes[at].Value) : null);
			if (data == null)
			{
				return(null);
			}
			float floor = VikingFood.HealthFloor((data.Biome >= 0) ? data.Biome : fallbackBiome);
			Inventory inventory = new Inventory("trip", null, data.Width, data.Height);
			byte[] held = Get(values.Bytes, VikingCore.InventoryKey, null);
			if (held != null)
			{
				inventory.Load(new ZPackage(held));
			}
			List<QuestFood> foods = Foods(inventory.GetAllItems(), found, seconds);
			QuestSim.Rank(foods, false, floor);
			List<QuestMeal> eating = VikingFood.Eating(VikingFood.Decode(Get(values.Bytes, VikingFood.MealsKey, null)), unloaded);
			QuestMeals meals = QuestSim.Eat(eating, foods, seconds, floor);
			foreach (KeyValuePair<string, int> meal in meals.Eaten)
			{
				Remove(inventory, meal.Key, meal.Value);
				TestLog.Count("Eaten on the road", meal.Key, meal.Value);
				eaten += meal.Value;
			}
			float start = Get(values.Floats, ZDOVars.s_health, float.MaxValue);
			float health = QuestSim.Health(meals, floor, start, 0f, 0f, 0f, seconds, 0, 0f, out int _);
			ZPackage pkg = new ZPackage();
			inventory.Save(pkg);
			Set(values.Bytes, VikingCore.InventoryKey, pkg.GetArray());
			Set(values.Bytes, VikingFood.MealsKey, VikingFood.Encode(VikingFood.MealsFrom(meals.Lasting, now)));
			Set(values.Longs, VikingFood.SeenKey, now);
			Set(values.Floats, ZDOVars.s_health, health);
			Set(values.Floats, ZDOVars.s_maxHealth, meals.EndMaxHealth);
			return(VillageDeparted.Encode(values, new HashSet<int>()));
		}

		/// <summary>
		/// Lists the foods a viking has on its road - what it carried all along, from the start, and what it found
		/// growing (see TripForage), each found thing from its share of the way, spread evenly over the road
		/// </summary>
		/// <param name="items">What the viking carries as it arrives, what it found among it</param>
		/// <param name="found">What it found growing, a prefab name for each thing in the order it found them</param>
		/// <param name="seconds">How long it was on the road, in seconds</param>
		/// <returns>The foods, not yet in the order it eats them</returns>
		internal static List<QuestFood> Foods(List<ItemDrop.ItemData> items, List<string> found, float seconds)
		{
			List<QuestFood> foods = QuestGame.FoodsOf(items, (ItemDrop.ItemData item) => item.m_stack);
			List<QuestFood> finds = new List<QuestFood>();
			for (int i = 0; i < found.Count; i++)
			{
				QuestFood carried = foods.Find((QuestFood food) => food.Item == found[i]);
				// What it found is in its inventory already, so it comes off what it carried all along
				if (carried == null || carried.Count <= 0)
				{
					continue;
				}
				carried.Count--;
				finds.Add(new QuestFood { Item = carried.Item, Health = carried.Health, Regen = carried.Regen, Burn = carried.Burn, Count = 1, From = FoundAt(i, found.Count, seconds) });
			}
			foods.RemoveAll((QuestFood food) => food.Count <= 0);
			foods.AddRange(finds);
			return(foods);
		}

		/// <summary>
		/// Works out when a viking found one of the things it found on its road - spread evenly over it
		/// </summary>
		/// <param name="index">Which of them it is, from 0, in the order it found them</param>
		/// <param name="count">How many things it found</param>
		/// <param name="seconds">How long it was on the road, in seconds</param>
		/// <returns>When it found it, in seconds from when it set out</returns>
		internal static float FoundAt(int index, int count, float seconds)
		{
			return(System.Math.Max(0f, seconds) * (index + 1) / (count + 1));
		}

		/// <summary>
		/// Takes a number of a food out of an inventory, a stack at a time
		/// </summary>
		/// <param name="inventory">The inventory</param>
		/// <param name="item">The food's prefab name</param>
		/// <param name="count">How many</param>
		private static void Remove(Inventory inventory, string item, int count)
		{
			foreach (ItemDrop.ItemData stack in inventory.GetAllItems().FindAll((ItemDrop.ItemData held) => held.m_dropPrefab != null && held.m_dropPrefab.name == item))
			{
				if (count <= 0)
				{
					return;
				}
				int take = System.Math.Min(count, stack.m_stack);
				inventory.RemoveItem(stack, take);
				count -= take;
			}
		}

		/// <summary>
		/// Gets one of a viking's values
		/// </summary>
		/// <typeparam name="T">The type of value</typeparam>
		/// <param name="values">The viking's values of that type</param>
		/// <param name="key">The value's key</param>
		/// <param name="fallback">What to give when it has none</param>
		/// <returns>The value, or the fallback</returns>
		private static T Get<T>(List<KeyValuePair<int, T>> values, int key, T fallback)
		{
			int at = values.FindIndex((KeyValuePair<int, T> value) => value.Key == key);
			return((at >= 0) ? values[at].Value : fallback);
		}

		/// <summary>
		/// Sets one of a viking's values, adding it when it has none
		/// </summary>
		/// <typeparam name="T">The type of value</typeparam>
		/// <param name="values">The viking's values of that type, changed</param>
		/// <param name="key">The value's key</param>
		/// <param name="value">The value</param>
		private static void Set<T>(List<KeyValuePair<int, T>> values, int key, T value)
		{
			int at = values.FindIndex((KeyValuePair<int, T> known) => known.Key == key);
			KeyValuePair<int, T> entry = new KeyValuePair<int, T>(key, value);
			if (at >= 0)
			{
				values[at] = entry;
			}
			else
			{
				values.Add(entry);
			}
		}
	}
}
