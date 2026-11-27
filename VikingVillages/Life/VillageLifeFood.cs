using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Food - the cooking stations a viking cooks and tends at, the raw and cooked food it takes, the skill it raises,
	/// and the food in the village's chests it eats from
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds a cooking station for the viking to cook at and claims it
		/// </summary>
		/// <returns>The job, or null when there's nothing to cook or nowhere to cook it</returns>
		private Job FindCookWork()
		{
			CookingStation station = FindCookStation(false);
			if (station != null && ClaimObject(station.GetComponent<ZNetView>()))
			{
				return(new CookJob(this, station));
			}
			return(null);
		}

		/// <summary>
		/// Finds the nearest cooking station the viking could cook at, with no other viking cooking there and not one it
		/// couldn't get to lately (see Avoided) - unless only what it carries counts, one with food on it to tend first,
		/// done food before food still cooking, so it's taken off before it burns, whoever put it on; then one lit, with
		/// an empty slot, and raw food it cooks carried by the viking or, unless only what it carries counts, in one of
		/// the village's chests within reach (see FetchesRawFrom)
		/// </summary>
		/// <param name="carriedOnly">True to only count the raw food the viking carries</param>
		/// <returns>The station, or null when there's none</returns>
		private CookingStation FindCookStation(bool carriedOnly)
		{
			List<CookingStation> stations = VillageCooking.Stations(village, transform.position);
			if (!carriedOnly)
			{
				CookingStation tend = FindTendStation(stations);
				if (tend != null)
				{
					return(tend);
				}
			}
			List<Container> chests = null;
			foreach (CookingStation station in stations)
			{
				ZNetView view = station.GetComponent<ZNetView>();
				if (!VillageCooking.IsLit(station) || VillageCooking.FreeSlots(station) == 0 || !IsFree(view) || Avoided(view.GetZDO().m_uid))
				{
					continue;
				}
				if (CarriedRaw(station) == null)
				{
					if (carriedOnly)
					{
						continue;
					}
					if (chests == null)
					{
						chests = UsableChests();
					}
					if (!chests.Exists((Container chest) => FetchesRawFrom(chest, station, chests)))
					{
						continue;
					}
				}
				return(station);
			}
			return(null);
		}

		/// <summary>
		/// Finds the nearest cooking station with food on it to tend, no other viking at it and not one the viking
		/// couldn't get to lately (see Avoided) - done food first, then food still cooking over a lit fire
		/// </summary>
		/// <param name="stations">The village's stations, nearest first</param>
		/// <param name="doneOnly">True to only count done food, which burns next</param>
		/// <returns>The station, or null when none needs tending</returns>
		private CookingStation FindTendStation(List<CookingStation> stations, bool doneOnly = false)
		{
			CookingStation cooking = null;
			foreach (CookingStation station in stations)
			{
				ZNetView view = station.GetComponent<ZNetView>();
				if (!IsFree(view) || Avoided(view.GetZDO().m_uid))
				{
					continue;
				}
				if (station.HaveDoneItem())
				{
					return(station);
				}
				if (!doneOnly && cooking == null && VillageCooking.IsLit(station) && station.HaveUncookedItem())
				{
					cooking = station;
				}
			}
			return(cooking);
		}

		/// <summary>
		/// Finds work tending food already on a cooking station, so it's taken off before it burns
		/// </summary>
		/// <param name="doneOnly">True to only tend done food, which burns next</param>
		/// <param name="takeOffOnly">True to only take off the done food, rather than cook on after (see
		/// CookJob)</param>
		/// <returns>The job, or null when no station needs tending</returns>
		private Job FindTendWork(bool doneOnly = false, bool takeOffOnly = false)
		{
			CookingStation station = FindTendStation(VillageCooking.Stations(village, transform.position), doneOnly);
			if (station != null && ClaimObject(station.GetComponent<ZNetView>()))
			{
				return(new CookJob(this, station, takeOffOnly));
			}
			return(null);
		}

		/// <summary>
		/// Checks whether the viking leaves its job a moment to take done food off a cooking station before it burns -
		/// resting or gathering by the fire, talking, repairing, building back, stocking the chests with wood, or keeping
		/// guard through a raid, all of which it picks up again after
		/// </summary>
		/// <returns>True when it does</returns>
		private bool TakesOffDoneFood()
		{
			return(job is GatherJob || job is TalkJob || job is RepairJob || job is RebuildJob || job is StockJob || job is GuardJob);
		}

		/// <summary>
		/// Finds raw food the viking carries that a cooking station cooks - not what the village's chests are short of
		/// for meals at the cauldron (see MealShort), which goes into a chest instead
		/// </summary>
		/// <param name="station">The station</param>
		/// <returns>The food, or null when it carries none</returns>
		private ItemDrop.ItemData CarriedRaw(CookingStation station)
		{
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => IsCarriedFor(item, CarryLoot) && VillageCooking.Cooks(station, item) && !MealShort(item.m_dropPrefab.name)));
		}

		/// <summary>
		/// Finds raw food in a chest that a cooking station cooks, of which the village's chests hold more than they hold
		/// back for meals at the cauldron (see MealHeld)
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="station">The station</param>
		/// <param name="chests">The village's chests the viking may use</param>
		/// <returns>The food, or null when there's none</returns>
		private ItemDrop.ItemData RawIn(Container chest, CookingStation station, List<Container> chests)
		{
			return(chest.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => VillageCooking.Cooks(station, item) && SpareRaw(item.m_dropPrefab.name, chests) > 0));
		}

		/// <summary>
		/// Checks whether a cook may fetch raw food for a cooking station from a chest - one holding some it may take (see
		/// RawIn) that no viking of the village couldn't get to lately (see ChestOutOfReach), so a cook doesn't head off
		/// for a chest out of reach and leave its stations for nothing
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="station">The station</param>
		/// <param name="chests">The village's chests the viking may use</param>
		/// <returns>True when it may</returns>
		private bool FetchesRawFrom(Container chest, CookingStation station, List<Container> chests)
		{
			return(!ChestOutOfReach(chest) && RawIn(chest, station, chests) != null);
		}

		/// <summary>
		/// Counts how much of a raw food the village's chests hold beyond what they hold back for meals at the cauldron
		/// (see MealHeld)
		/// </summary>
		/// <param name="name">The food's prefab name</param>
		/// <param name="chests">The village's chests the viking may use</param>
		/// <returns>How much, which may be 0 or less</returns>
		private int SpareRaw(string name, List<Container> chests)
		{
			int held = MealHeld(name);
			return((held > 0) ? Stored(name, chests) - held : int.MaxValue);
		}

		/// <summary>
		/// Takes some of the carried loot out of the viking's inventory
		/// </summary>
		/// <param name="item">The carried stack</param>
		/// <param name="amount">How many</param>
		private void UseCarried(ItemDrop.ItemData item, int amount)
		{
			humanoid.GetInventory().RemoveItem(item, amount);
		}

		/// <summary>
		/// Takes raw food a cooking station cooks out of a chest the viking has reached, up to a number of pieces -
		/// leaving the village's chests what they hold back for meals at the cauldron (see MealHeld)
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="station">The station</param>
		/// <param name="wanted">How many pieces at most</param>
		/// <returns>How many it took</returns>
		private int TakeRaw(Container chest, CookingStation station, int wanted)
		{
			if (!OpenChest(chest))
			{
				return(0);
			}
			Inventory store = chest.GetInventory();
			List<Container> chests = UsableChests();
			int taken = 0;
			foreach (ItemDrop.ItemData item in store.m_inventory.FindAll((ItemDrop.ItemData stored) => VillageCooking.Cooks(station, stored)))
			{
				if (taken >= wanted)
				{
					break;
				}
				int spare = SpareRaw(item.m_dropPrefab.name, chests);
				int took = ((spare > 0) ? Carry(item, Mathf.Min(wanted - taken, Mathf.Min(item.m_stack, spare)), CarryLoot) : 0);
				if (took <= 0)
				{
					continue;
				}
				store.RemoveItem(item, took);
				taken += took;
			}
			// What the chests hold changed, so the next look at what they're short of counts it afresh
			nextMealReserve = 0f;
			return(taken);
		}

		/// <summary>
		/// Takes what's done off a cooking station the viking has reached and carries it, to put away - putting down at
		/// its feet whatever it has no room for - raising its Cooking skill for each piece and getting a bonus piece of
		/// cooked food as often as a player of its skill does, the way a player's comes
		/// </summary>
		/// <param name="station">The station</param>
		private void TakeCooked(CookingStation station)
		{
			Skills.SkillType skill = StationSkill(station.m_skill);
			string burnt = ((station.m_overCookedItem != null) ? station.m_overCookedItem.gameObject.name : "");
			foreach (string name in VillageCooking.TakeDone(station))
			{
				ItemDrop.ItemData item = ItemOf(name);
				if (item == null)
				{
					continue;
				}
				RaiseSkill(skill, CookTakeSkill);
				// Food cooked well comes off with a bonus as often as a player's skill gives one, never burnt food
				int count = 1;
				if (station.m_canGiveBonusYield && name != burnt && InventoryGui.instance != null && Random.value < humanoid.GetSkillFactor(skill) * InventoryGui.instance.m_craftBonusChance)
				{
					count += InventoryGui.instance.m_craftBonusAmount;
					VillageSignals.SendCraftBonus(station.m_nview.GetZDO().m_uid, nview.GetZDO().m_uid, InventoryGui.instance.m_craftBonusAmount);
				}
				int carried = Carry(item, count, CarryLoot);
				if (carried < count)
				{
					ItemDrop.DropItem(item, count - carried, transform.position + transform.forward * 0.6f + Vector3.up * 0.5f, Quaternion.identity);
				}
				TestLog.Count("Cooked", TestLog.ItemName(item) + ((name == burnt) ? " (burnt)" : ""), count);
			}
		}

		/// <summary>
		/// Raises the viking's skill for a batch it crafted at a station - the station's crafting skill, or Crafting when
		/// it names none - and works out the bonus the batch comes with, as often as a player of its skill gets one
		/// </summary>
		/// <param name="bench">The station</param>
		/// <returns>How many more it got, shown on every client when it's any</returns>
		private int CraftBonus(CraftingStation bench)
		{
			Skills.SkillType skill = ((bench.m_craftingSkill != Skills.SkillType.None) ? bench.m_craftingSkill : Skills.SkillType.Crafting);
			RaiseSkill(skill, 1f);
			if (InventoryGui.instance == null || Random.value >= humanoid.GetSkillFactor(skill) * InventoryGui.instance.m_craftBonusChance)
			{
				return(0);
			}
			int bonus = InventoryGui.instance.m_craftBonusAmount;
			VillageSignals.SendCraftBonus(bench.GetComponent<ZNetView>().GetZDO().m_uid, nview.GetZDO().m_uid, bonus);
			return(bonus);
		}

		/// <summary>
		/// Gets the skill a cooking station raises, or Cooking when it names none
		/// </summary>
		/// <param name="skill">The station's skill</param>
		/// <returns>The skill</returns>
		private static Skills.SkillType StationSkill(Skills.SkillType skill)
		{
			return((skill != Skills.SkillType.None) ? skill : Skills.SkillType.Cooking);
		}

		/// <summary>
		/// Raises one of the viking's skills by an amount, the way a player's rises (see VikingCore.RaiseSkill)
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="factor">How much the action raises it</param>
		private void RaiseSkill(Skills.SkillType skill, float factor)
		{
			if (viking != null)
			{
				viking.RaiseSkill(skill, factor);
			}
		}

		/// <summary>
		/// Finds the food in a chest the viking eats now - wounded, the one that heals it fastest, and keeping its foods
		/// up, the weakest that raises it above its floor, or with none, the strongest of the rest (see
		/// VikingFood.TopUpOrder), so the strongest food is left for the wounded
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="topUp">True when it's keeping its foods up rather than eating for its wounds</param>
		/// <returns>The food, or null when there's none</returns>
		private ItemDrop.ItemData FoodIn(Container chest, bool topUp)
		{
			ItemDrop.ItemData best = null;
			float floor = body.Food.Floor();
			foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
			{
				if (!(topUp ? body.Food.CanTopUp(item) : body.Food.CanEat(item)))
				{
					continue;
				}
				if (best == null || (topUp ? EatsBefore(item, best, floor) : (item.m_shared.m_foodRegen > best.m_shared.m_foodRegen || (item.m_shared.m_foodRegen == best.m_shared.m_foodRegen && item.m_shared.m_food > best.m_shared.m_food))))
				{
					best = item;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether a viking keeping its foods up eats one food before another (see VikingFood.TopUpOrder)
		/// </summary>
		/// <param name="item">The food</param>
		/// <param name="other">The other food</param>
		/// <param name="floor">The viking's floor</param>
		/// <returns>True when it eats the first first</returns>
		private static bool EatsBefore(ItemDrop.ItemData item, ItemDrop.ItemData other, float floor)
		{
			return(VikingFood.TopUpOrder(item.m_shared.m_food, item.m_shared.m_foodRegen, other.m_shared.m_food, other.m_shared.m_foodRegen, floor) < 0);
		}

		/// <summary>
		/// Checks whether the chests the viking may use hold food enough to go around the village (see FoodEnough)
		/// </summary>
		/// <returns>True when they do</returns>
		private bool FoodToSpare()
		{
			int food = 0;
			foreach (Container chest in UsableChests())
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					if (VikingFood.IsFood(item))
					{
						food += item.m_stack;
					}
				}
			}
			return(FoodEnough(food, village.Vikings().Count));
		}

		/// <summary>
		/// Checks whether there's food enough to go around a village - SpareMeals of it for each of its vikings, enough
		/// for every one to eat a full set of foods
		/// </summary>
		/// <param name="food">How much food the chests hold</param>
		/// <param name="vikings">How many vikings the village has loaded</param>
		/// <returns>True when there is</returns>
		internal static bool FoodEnough(int food, int vikings)
		{
			return(food >= Mathf.Max(1, vikings) * SpareMeals);
		}

		/// <summary>
		/// Finds the chest the viking eats from - wounded, the nearest of the village's chests it may use with food in it
		/// it can eat now, and keeping its foods up, the one holding the food it eats first of all of them (see FoodIn),
		/// the nearest of those alike
		/// </summary>
		/// <param name="topUp">True when it's keeping its foods up rather than eating for its wounds</param>
		/// <returns>The chest, or null when there's none</returns>
		private Container FindFoodChest(bool topUp)
		{
			List<Container> pantries = UsableChests().FindAll((Container chest) => FoodIn(chest, topUp) != null);
			if (!topUp || pantries.Count == 0)
			{
				return(Nearest(pantries));
			}
			float floor = body.Food.Floor();
			ItemDrop.ItemData first = null;
			foreach (Container chest in pantries)
			{
				ItemDrop.ItemData food = FoodIn(chest, true);
				if (first == null || EatsBefore(food, first, floor))
				{
					first = food;
				}
			}
			return(Nearest(pantries.FindAll((Container chest) => !EatsBefore(first, FoodIn(chest, true), floor))));
		}

		/// <summary>
		/// Eats the food in a chest the viking has reached that it eats now (see FoodIn), the way a player eats
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="topUp">True when it's keeping its foods up rather than eating for its wounds</param>
		/// <returns>False when there was no food in it it could eat, or it couldn't be used</returns>
		private bool EatFrom(Container chest, bool topUp)
		{
			if (!OpenChest(chest))
			{
				return(false);
			}
			ItemDrop.ItemData food = FoodIn(chest, topUp);
			if (food == null || !body.Food.Eat(food))
			{
				return(false);
			}
			TestLog.Count("Eaten", TestLog.ItemName(food));
			chest.GetInventory().RemoveItem(food, 1);
			chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			humanoid.m_consumeItemEffects.Create(transform.position, Quaternion.identity);
			humanoid.m_zanim.SetTrigger("eat");
			return(true);
		}
	}
}
