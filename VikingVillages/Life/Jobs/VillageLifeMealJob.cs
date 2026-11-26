using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Making a meal at the village's cauldron (see MealJob), and the meals the village keeps in reserve
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// How many of a meal the village keeps in its chests before its vikings stop making more of it at the cauldron
		/// </summary>
		private const int MealKeep = 10;

		/// <summary>
		/// How often a viking looks for a meal to make, in seconds, since it goes through every recipe and chest
		/// </summary>
		private const float MealCheckInterval = 15f;

		/// <summary>
		/// When the viking next looks for a meal to make, by Time.time
		/// </summary>
		private float nextMealCheck;

		/// <summary>
		/// Finds a meal for the viking to make at the village's cauldron by day (see VillageCooking.TryPlaceCauldron) -
		/// the one that feeds best, by its health and stamina, among the cauldron's foods whose recipe its level allows,
		/// the chests hold what it takes, and the chests hold fewer than MealKeep of - claiming the cauldron, so one
		/// viking cooks at it at a time
		/// </summary>
		/// <returns>The job, or null when there's no cauldron, nothing it can make, or someone else is at it</returns>
		private Job FindMealWork()
		{
			if (Time.time < nextMealCheck || IsNight() || IsDusk() || ObjectDB.instance == null)
			{
				return(null);
			}
			nextMealCheck = Time.time + MealCheckInterval;
			CraftingStation cauldron = FindStation(VillageCooking.CauldronHash);
			ZNetView view = ((cauldron != null) ? cauldron.GetComponent<ZNetView>() : null);
			if (view == null || !IsFree(view))
			{
				return(null);
			}
			List<Container> chests = UsableChests();
			ArrowPlan best = null;
			float bestFood = 0f;
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!IsMeal(recipe))
				{
					continue;
				}
				ItemDrop.ItemData.SharedData food = recipe.m_item.m_itemData.m_shared;
				float feeds = food.m_food + food.m_foodStamina;
				string prefab = recipe.m_item.gameObject.name;
				if (feeds <= bestFood || Stored(prefab, chests) >= MealKeep)
				{
					continue;
				}
				ArrowPlan plan = PlanCraft(prefab, out string _);
				if (plan != null && plan.Station == cauldron && plan.Wood <= 0)
				{
					best = plan;
					bestFood = feeds;
				}
			}
			if (best == null || !ClaimObject(view))
			{
				return(null);
			}
			return(new MealJob(this, best));
		}

		/// <summary>
		/// Checks whether a recipe makes a meal at the cauldron that the vikings eat - not what's cooked further
		/// elsewhere, like bread dough
		/// </summary>
		/// <param name="recipe">The recipe</param>
		/// <returns>True when it does</returns>
		private static bool IsMeal(Recipe recipe)
		{
			if (!Crafts(recipe) || recipe.m_craftingStation == null || recipe.m_craftingStation.gameObject.name != VillageCooking.CauldronPrefab)
			{
				return(false);
			}
			ItemDrop.ItemData.SharedData food = recipe.m_item.m_itemData.m_shared;
			return(food.m_itemType == ItemDrop.ItemData.ItemType.Consumable && food.m_food > 0f);
		}

		/// <summary>
		/// Checks whether a village's cauldron needs something for its meals - one of the meals its level allows takes
		/// it, and the chests hold fewer than MealKeep of that meal and less of it than the meal takes - like thistle for
		/// sausages, which foraging for food brings home then (see QuestLoot.PlantsFor)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="item">The item's prefab name</param>
		/// <returns>True when it does, false with no cauldron loaded</returns>
		internal static bool CauldronWants(Village village, string item)
		{
			if (ObjectDB.instance == null || ZNetScene.instance == null || !VillageCooking.IsCauldronIngredient(item))
			{
				return(false);
			}
			int level = -1;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				CraftingStation station = ((instance.Key.GetPrefab() == VillageCooking.CauldronHash && instance.Value != null && village.OwnsPiece(instance.Value.transform.position)) ? instance.Value.GetComponent<CraftingStation>() : null);
				if (station != null)
				{
					level = Mathf.Max(level, station.GetLevel());
				}
			}
			if (level < 0)
			{
				return(false);
			}
			List<Container> chests = VillageChests(village);
			int stored = Stored(item, chests);
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!IsMeal(recipe) || recipe.m_minStationLevel > level || Stored(recipe.m_item.gameObject.name, chests) >= MealKeep)
				{
					continue;
				}
				foreach (Piece.Requirement requirement in recipe.m_resources)
				{
					if (requirement.m_resItem != null && requirement.m_resItem.gameObject.name == item && stored < CraftAmount(requirement, 1))
					{
						return(true);
					}
				}
			}
			return(false);
		}

		/// <summary>
		/// How often a viking works out again how much raw food the village's chests hold back for meals, in seconds
		/// </summary>
		private const float MealReserveInterval = 10f;

		/// <summary>
		/// The raw food the village's chests hold back for meals at its cauldron, and how much of each they held, by
		/// prefab name, as last worked out (see UpdateMealReserve), and when to work it out again
		/// </summary>
		private readonly Dictionary<string, int> mealReserve = new Dictionary<string, int>();
		private readonly Dictionary<string, int> mealReserveStored = new Dictionary<string, int>();
		private float nextMealReserve;

		/// <summary>
		/// Works out how much raw food the village's chests hold back from its cooking stations for meals at its
		/// cauldron, every MealReserveInterval seconds - for each meal the cauldron's level allows and the chests hold
		/// fewer than MealKeep of, as much of each raw food its recipe takes, like the raw meat sausages take, so cooking
		/// leaves enough in the chests for one of each
		/// </summary>
		private void UpdateMealReserve()
		{
			if (Time.time < nextMealReserve)
			{
				return;
			}
			nextMealReserve = Time.time + MealReserveInterval;
			mealReserve.Clear();
			mealReserveStored.Clear();
			CraftingStation cauldron = ((ObjectDB.instance != null) ? FindStation(VillageCooking.CauldronHash) : null);
			if (cauldron == null)
			{
				return;
			}
			int level = cauldron.GetLevel();
			List<Container> chests = UsableChests();
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!IsMeal(recipe) || recipe.m_minStationLevel > level || Stored(recipe.m_item.gameObject.name, chests) >= MealKeep)
				{
					continue;
				}
				foreach (Piece.Requirement requirement in recipe.m_resources)
				{
					int amount = CraftAmount(requirement, 1);
					if (amount <= 0 || !VillageCooking.IsRaw(requirement.m_resItem.gameObject.name))
					{
						continue;
					}
					string name = requirement.m_resItem.gameObject.name;
					mealReserve.TryGetValue(name, out int held);
					mealReserve[name] = Mathf.Max(held, amount);
				}
			}
			foreach (string name in mealReserve.Keys)
			{
				mealReserveStored[name] = Stored(name, chests);
			}
		}

		/// <summary>
		/// Gets how much of a raw food the village's chests hold back for meals (see UpdateMealReserve)
		/// </summary>
		/// <param name="name">The food's prefab name</param>
		/// <returns>How much, or 0</returns>
		private int MealHeld(string name)
		{
			UpdateMealReserve();
			return(mealReserve.TryGetValue(name, out int held) ? held : 0);
		}

		/// <summary>
		/// Checks whether the village's chests are short of a raw food they hold back for meals, as last worked out (see
		/// UpdateMealReserve), so what the viking carries of it goes into a chest rather than onto a cooking station
		/// </summary>
		/// <param name="name">The food's prefab name</param>
		/// <returns>True when they are</returns>
		private bool MealShort(string name)
		{
			int held = MealHeld(name);
			return(held > 0 && (!mealReserveStored.TryGetValue(name, out int stored) || stored < held));
		}

		/// <summary>
		/// Counts how many of an item the village's chests hold
		/// </summary>
		/// <param name="prefab">The item's prefab name</param>
		/// <param name="chests">The chests</param>
		/// <returns>How many</returns>
		private static int Stored(string prefab, List<Container> chests)
		{
			int count = 0;
			foreach (Container chest in chests)
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					if (item.m_dropPrefab != null && item.m_dropPrefab.name == prefab)
					{
						count += item.m_stack;
					}
				}
			}
			return(count);
		}

		/// <summary>
		/// Making a meal at the village's cauldron (see FindMealWork) - getting what it takes from the village's chests,
		/// walking to the cauldron and working at it with the player's crafting animation and the cauldron's sounds,
		/// then trading what it carries for the meal, carried as loot to put away in a chest - raising the viking's
		/// Cooking skill. The game's crafting isn't run. What it carries for cooking goes back to a chest when it's done
		/// </summary>
		private class MealJob : Job
		{
			private readonly ArrowPlan plan;
			private readonly CraftingStation cauldron;
			private Container chest;
			private float actAt = -1f;
			private float craftUntil = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the cauldron already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="meal">The meal it makes</param>
			public MealJob(VillageLife owner, ArrowPlan meal) : base(owner)
			{
				plan = meal;
				cauldron = meal.Station;
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name and the meal it makes</returns>
			public override string Describe()
			{
				return("Cook a meal (" + plan.Prefab + ")");
			}

			/// <summary>
			/// Gets what the meal takes, walks to the cauldron, works at it and makes the meal
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the meal's made, what it takes is gone, or it can't get to the cauldron</returns>
			public override bool Tick(float dt)
			{
				if (cauldron == null)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(cauldron.GetComponent<ZNetView>());
				}
				if (craftUntil >= 0f)
				{
					life.Face(cauldron.transform.position);
					if (Time.time < craftUntil)
					{
						return(false);
					}
					craftUntil = -1f;
					life.SetCrafting(0);
					if (life.UseCraft(plan))
					{
						life.CarryMeal(plan.Prefab, plan.Batch);
						life.RaiseSkill(Skills.SkillType.Cooking, CookAddSkill);
						VillageSignals.SendEffect(VillageSignals.CraftDoneEffect, cauldron.GetComponent<ZNetView>().GetZDO().m_uid);
					}
					return(true);
				}
				if (life.MissingCraft(plan, out string ingredient, out int needed))
				{
					return(Fetch(dt, ingredient, needed));
				}
				// A cauldron over a fire is worked from outside the fire's flames, the way a cooking station is
				FireSafety.CookSpot(cauldron.transform.position, life.transform.position, CraftReach, out Vector3 spot, out float within);
				if (Utils.DistanceXZ(spot, life.transform.position) > Mathf.Max(within, ArriveReach))
				{
					life.Go(dt, spot, within, false);
					return(life.moveFailed);
				}
				life.Face(cauldron.transform.position);
				life.SetCrafting(Mathf.Max(1, cauldron.m_useAnimation));
				VillageSignals.SendEffect(VillageSignals.CraftEffect, cauldron.GetComponent<ZNetView>().GetZDO().m_uid);
				craftUntil = Time.time + CraftTime;
				return(false);
			}

			/// <summary>
			/// Walks to the nearest chest holding something the meal takes and takes it - giving up when no chest holds it
			/// any more, or it can't get to the chest
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="ingredient">The ingredient's prefab name</param>
			/// <param name="needed">How many more it needs</param>
			/// <returns>True when it gives up</returns>
			private bool Fetch(float dt, string ingredient, int needed)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.ChestWithCraft(ingredient);
					actAt = -1f;
					if (chest == null)
					{
						return(true);
					}
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				if (life.TakeCraft(chest, ingredient, needed) <= 0)
				{
					life.Avoid(chest.m_nview);
				}
				chest = null;
				actAt = -1f;
				return(false);
			}

			/// <summary>
			/// Stops working at the cauldron and lets go of it, carrying what it carries for cooking back to a chest
			/// </summary>
			public override void End()
			{
				life.SetCrafting(0);
				if (cauldron != null)
				{
					life.ReleaseObject(cauldron.GetComponent<ZNetView>());
				}
				life.ReturnCraft();
			}
		}

		/// <summary>
		/// Carries a meal the viking made, as loot to put away in a chest - dropping what it has no room to carry at its
		/// feet, where it's picked up like any loot
		/// </summary>
		/// <param name="prefab">The meal's prefab name</param>
		/// <param name="amount">How many it made</param>
		private void CarryMeal(string prefab, int amount)
		{
			GameObject item = ObjectDB.instance.GetItemPrefab(prefab);
			ItemDrop drop = ((item != null) ? item.GetComponent<ItemDrop>() : null);
			if (drop == null)
			{
				return;
			}
			ItemDrop.ItemData meal = drop.m_itemData.Clone();
			meal.m_dropPrefab = item;
			int carried = Carry(meal, amount, CarryLoot);
			if (carried < amount)
			{
				ItemDrop.DropItem(meal, amount - carried, transform.position + transform.forward * 0.6f + Vector3.up * 0.5f, Quaternion.identity);
			}
		}
	}
}
