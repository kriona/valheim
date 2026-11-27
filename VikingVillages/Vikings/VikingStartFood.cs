using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The food a viking starts out with as it's rolled up - a PC or NPC viking having eaten EatenCount of its biome's
	/// Foods and carrying CarriedMin-CarriedMax more of them, or a traveler Traveler.FoodMin-FoodMax, and a dvergr
	/// viking carrying DvergrMin-DvergrMax of the Mistlands', which it never eats
	/// </summary>
	internal static class VikingStartFood
	{
		/// <summary>
		/// The foods a viking starts out with, by the index of its biome (see VikingGear) - its village's starter foods
		/// (see VillageFurnishing.StarterFoods) and a third of the same biome, so it can have eaten three different
		/// foods at once
		/// </summary>
		internal static readonly string[][] Foods =
		{
			// Meadows
			new[] { "CookedMeat", "Raspberry", "NeckTailGrilled" },
			// Black Forest
			new[] { "CookedDeerMeat", "Blueberries", "CarrotSoup" },
			// Swamp
			new[] { "DeerStew", "FishCooked", "TurnipStew" },
			// Mountains
			new[] { "Sausages", "OnionSoup", "WolfJerky" },
			// Plains
			new[] { "CookedLoxMeat", "Bread", "LoxPie" },
			// Mistlands
			new[] { "CookedHareMeat", "CookedBugMeat", "MeatPlatter" },
			// Ashlands
			new[] { "CookedAsksvinMeat", "CookedVoltureMeat", "FierySvinstew" },
			// Deep North
			new[] { "CookedMooseMeat", "CookedSealBlubber", "SmokedMooseMeat" }
		};

		/// <summary>
		/// How many foods a PC or NPC viking starts out having eaten
		/// </summary>
		internal const int EatenCount = 3;

		/// <summary>
		/// How many foods a PC or NPC viking starts out carrying, and a dvergr viking
		/// </summary>
		internal const int CarriedMin = 5;
		internal const int CarriedMax = 6;
		internal const int DvergrMin = 0;
		internal const int DvergrMax = 2;

		/// <summary>
		/// The least and most of its burn time each food a viking starts out having eaten has left, so its foods don't
		/// all wear off at once
		/// </summary>
		internal const float LeftMin = 0.6f;
		internal const float LeftMax = 1f;

		/// <summary>
		/// Gets the foods a viking of a biome starts out with
		/// </summary>
		/// <param name="biome">The biome's index in VikingGear's tables, kept within Foods</param>
		/// <returns>The foods' prefab names</returns>
		internal static string[] FoodsFor(int biome)
		{
			return(Foods[Mathf.Clamp(biome, 0, Foods.Length - 1)]);
		}

		/// <summary>
		/// Works out how many of something there are, from a range and a roll
		/// </summary>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <param name="min">The fewest</param>
		/// <param name="max">The most</param>
		/// <returns>The count, from min to max, each as likely</returns>
		internal static int CountFor(float roll, int min, int max)
		{
			return(Mathf.Clamp(min + Mathf.FloorToInt(roll * (max - min + 1)), min, max));
		}

		/// <summary>
		/// Picks the foods a viking carries, each one of its biome's foods at random
		/// </summary>
		/// <param name="biome">The biome's index in VikingGear's tables</param>
		/// <param name="rolls">A random number from 0 to 1 for each food it carries</param>
		/// <returns>How many of each food it carries, by prefab name, in the order of its biome's foods</returns>
		internal static List<KeyValuePair<string, int>> Carried(int biome, IList<float> rolls)
		{
			string[] foods = FoodsFor(biome);
			int[] counts = new int[foods.Length];
			foreach (float roll in rolls)
			{
				counts[CountFor(roll, 0, foods.Length - 1)]++;
			}
			List<KeyValuePair<string, int>> carried = new List<KeyValuePair<string, int>>();
			for (int i = 0; i < foods.Length; i++)
			{
				if (counts[i] > 0)
				{
					carried.Add(new KeyValuePair<string, int>(foods[i], counts[i]));
				}
			}
			return(carried);
		}

		/// <summary>
		/// Puts the foods a viking starts out carrying in its inventory - between the fewest and most given, each one of
		/// its biome's foods at random
		/// </summary>
		/// <param name="gear">The viking's inventory, being rolled</param>
		/// <param name="biome">The biome's index in VikingGear's tables</param>
		/// <param name="min">The fewest it carries</param>
		/// <param name="max">The most it carries</param>
		internal static void Pack(Inventory gear, int biome, int min, int max)
		{
			int count = CountFor(Random.value, min, max);
			List<float> rolls = new List<float>();
			for (int i = 0; i < count; i++)
			{
				rolls.Add(Random.value);
			}
			foreach (KeyValuePair<string, int> food in Carried(biome, rolls))
			{
				gear.AddItem(food.Key, food.Value, 1, 0, 0L, "", false);
			}
		}

		/// <summary>
		/// Starts a PC or NPC viking being rolled up out fed - having eaten each of its biome's foods, each with
		/// LeftMin-LeftMax of its burn time left (see VikingFood.StartFed), and carrying more - CarriedMin-CarriedMax, or
		/// for a traveler, Traveler.FoodMin-FoodMax
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		/// <param name="gear">Its inventory, being rolled</param>
		/// <param name="biome">The biome's index in VikingGear's tables</param>
		/// <param name="carriedMin">The fewest foods it carries</param>
		/// <param name="carriedMax">The most foods it carries</param>
		internal static void Give(ZDO zdo, Inventory gear, int biome, int carriedMin, int carriedMax)
		{
			string[] foods = FoodsFor(biome);
			List<KeyValuePair<string, float>> eaten = new List<KeyValuePair<string, float>>();
			for (int i = 0; i < foods.Length && i < EatenCount; i++)
			{
				eaten.Add(new KeyValuePair<string, float>(foods[i], Random.Range(LeftMin, LeftMax)));
			}
			VikingFood.StartFed(zdo, eaten);
			Pack(gear, biome, carriedMin, carriedMax);
		}
	}
}
