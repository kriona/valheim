using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Silver against the dead, with Zombie Apocalypse - a viking lining its weapons with silver at the forge, and an
	/// archer restocking its silver arrows, each when nothing more pressing needs doing
	/// </summary>
	/// <remarks>
	/// A weapon is lined the way a player lines one, with Zombie Apocalypse's recipe for it - silver and ectoplasm at a
	/// forge of its level - so nothing is lined without the mod. The village works toward the lining when it has
	/// nothing better to work toward (see Candidates), which sends its vikings hunting ghosts for the ectoplasm. Silver
	/// arrows are kept apart from the quiver (see IsAmmoOf), and readied only against the dead (see
	/// VikingCore.BestAmmo)
	/// </remarks>
	internal partial class VillageLife
	{
		/// <summary>
		/// An archer restocks its silver arrows, up to VikingSilver.ArrowStack, once it has fewer than this
		/// </summary>
		private const int SilverArrowsLow = 20;

		/// <summary>
		/// Seconds between a viking's looks at whether it wants silver arrows, since it goes through every item
		/// </summary>
		private const float SilverArrowCheckInterval = 15f;

		/// <summary>
		/// When the viking next looks at whether it wants silver arrows, in Time.time seconds
		/// </summary>
		private float nextSilverArrowCheck;

		/// <summary>
		/// Finds a weapon of the viking's own to line with silver (see PlanLining) when the village has everything it
		/// takes - one viking of the village at a time, along with its upgrades
		/// </summary>
		/// <returns>The job, or null when there's nothing it can line</returns>
		private Job FindLiningWork()
		{
			if (OtherDoing(UpgradingKey) || IsNight() || IsDusk())
			{
				return(null);
			}
			foreach (ItemDrop.ItemData item in LineableWeapons(viking, humanoid))
			{
				ArrowPlan plan = PlanLining(item);
				if (plan != null && ClaimObject(plan.Station.GetComponent<ZNetView>()))
				{
					return(new GearUpgradeJob(this, plan));
				}
			}
			return(null);
		}

		/// <summary>
		/// Lists the weapons of a viking's own that Zombie Apocalypse could line with silver - those it keeps to fight
		/// with, the one in its hands first
		/// </summary>
		/// <param name="core">The viking</param>
		/// <param name="humanoid">Its humanoid</param>
		/// <returns>The weapons</returns>
		private static List<ItemDrop.ItemData> LineableWeapons(VikingCore core, Humanoid humanoid)
		{
			List<ItemDrop.ItemData> weapons = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => item.IsWeapon() && item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Torch && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && core.Keeps(item) && ZombieLink.LiningRecipe(item) != null);
			weapons.Sort((ItemDrop.ItemData a, ItemDrop.ItemData b) => b.m_equipped.CompareTo(a.m_equipped));
			return(weapons);
		}

		/// <summary>
		/// Works out what lining a weapon of the viking's with silver takes, from Zombie Apocalypse's recipe for it - its
		/// forge, at the recipe's level, and its silver and ectoplasm, in the village's chests or carried for crafting
		/// </summary>
		/// <param name="item">The weapon</param>
		/// <returns>The lining, or null when the weapon can't be lined, the village has no forge of the level, or it's
		/// short of anything it takes</returns>
		private ArrowPlan PlanLining(ItemDrop.ItemData item)
		{
			Recipe recipe = ZombieLink.LiningRecipe(item);
			if (recipe == null || recipe.m_craftingStation == null || item.m_dropPrefab == null)
			{
				return(null);
			}
			CraftingStation station = FindStation(recipe.m_craftingStation.gameObject.name.GetStableHashCode());
			if (station == null || station.GetLevel() < recipe.m_minStationLevel)
			{
				return(null);
			}
			ArrowPlan plan = new ArrowPlan { Prefab = item.m_dropPrefab.name, Batch = 1, Station = station, Upgrade = item, Quality = item.m_quality, Lines = true };
			List<Container> chests = UsableChests();
			foreach (Piece.Requirement requirement in recipe.m_resources)
			{
				int amount = CraftAmount(requirement, 1);
				if (amount <= 0)
				{
					continue;
				}
				string name = requirement.m_resItem.gameObject.name;
				if (CraftStock(name, chests) < amount)
				{
					return(null);
				}
				plan.Others.Add(new KeyValuePair<string, int>(name, amount));
			}
			return(plan);
		}

		/// <summary>
		/// Finds silver arrows for an archer to restock, every SilverArrowCheckInterval seconds, once it has fewer than
		/// SilverArrowsLow of the ones it wants (see WantedSilverArrows) - from a chest holding some, or else crafted
		/// when the village has what they take and there's wood it can get
		/// </summary>
		/// <returns>The job, or null when it wants none, has enough or can't get any</returns>
		private Job FindSilverArrowWork()
		{
			if (Time.time < nextSilverArrowCheck)
			{
				return(null);
			}
			nextSilverArrowCheck = Time.time + SilverArrowCheckInterval;
			string prefab = WantedSilverArrows(out string ammoType);
			if (prefab == null || OwnSilverArrows(ammoType) >= SilverArrowsLow)
			{
				return(null);
			}
			Container chest = Nearest(UsableChests().FindAll((Container store) => !Avoided(store.m_nview.GetZDO().m_uid) && store.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => IsSilverArrowsOf(item, ammoType))));
			if (chest != null)
			{
				return(new AmmoJob(this, chest, true));
			}
			ArrowPlan plan = PlanSilverArrows();
			if (plan == null)
			{
				return(null);
			}
			bool woodLeft = (Time.time >= noWoodUntil || CarriedWood() >= plan.Wood || FindWoodChest() != null);
			if (!woodLeft || (plan.Station != null && !ClaimObject(plan.Station.GetComponent<ZNetView>())))
			{
				return(null);
			}
			return(new ArrowJob(this, plan));
		}

		/// <summary>
		/// Finds the silver arrows the viking wants - while Zombie Apocalypse is running, the ones its bow hurts the
		/// undead most with, when that's more than its best other arrows do (see VikingSilver.SilverArrowsFor)
		/// </summary>
		/// <param name="ammoType">Set to the ammo type of its bow, or null</param>
		/// <returns>The arrows' prefab name, or null when it wants none</returns>
		private string WantedSilverArrows(out string ammoType)
		{
			ammoType = BowAmmo();
			ItemDrop.ItemData bow = ((viking != null) ? viking.Bow() : null);
			if (ammoType == null || bow == null || bow.m_shared.m_ammoType != ammoType || !ZombieLink.Active())
			{
				return(null);
			}
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (IsAmmoOf(item, ammoType) && (best == null || AmmoDamage(item) > AmmoDamage(best)))
				{
					best = item;
				}
			}
			return(VikingSilver.SilverArrowsFor(bow, best));
		}

		/// <summary>
		/// Works out the silver arrows the viking can craft for its bow (see WantedSilverArrows and PlanCraft)
		/// </summary>
		/// <returns>The arrows, or null when it wants none, or the village has no station for them or is short of what
		/// they take besides wood</returns>
		private ArrowPlan PlanSilverArrows()
		{
			string prefab = WantedSilverArrows(out string _);
			ArrowPlan plan = ((prefab != null) ? PlanCraft(prefab, out string _) : null);
			if (plan != null)
			{
				plan.Silver = true;
			}
			return(plan);
		}

		/// <summary>
		/// Checks whether the viking wants a batch of silver arrows crafted - it's missing at least a batch from
		/// VikingSilver.ArrowStack
		/// </summary>
		/// <param name="batch">How many arrows a craft makes</param>
		/// <returns>True when it wants a batch</returns>
		private bool MissingSilverArrows(int batch)
		{
			string ammoType = BowAmmo();
			return(ammoType != null && OwnSilverArrows(ammoType) <= VikingSilver.ArrowStack - batch);
		}

		/// <summary>
		/// Counts the viking's own silver arrows of an ammo type
		/// </summary>
		/// <param name="ammoType">The ammo type</param>
		/// <returns>How many it has</returns>
		private int OwnSilverArrows(string ammoType)
		{
			int count = 0;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (IsSilverArrowsOf(item, ammoType))
				{
					count += item.m_stack;
				}
			}
			return(count);
		}

		/// <summary>
		/// Checks whether an item is silver arrows of an ammo type, of the viking's own or in a chest
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="ammoType">The ammo type</param>
		/// <returns>True when it is</returns>
		private static bool IsSilverArrowsOf(ItemDrop.ItemData item, string ammoType)
		{
			return(VikingSilver.IsSilverArrows(item) && item.m_shared.m_ammoType == ammoType && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item));
		}

		/// <summary>
		/// Restocks an archer's silver arrows from a chest it has reached, up to VikingSilver.ArrowStack
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>False when it took nothing, or the chest couldn't be used</returns>
		private bool TakeSilverArrows(Container chest)
		{
			string ammoType = BowAmmo();
			if (ammoType == null || !OpenChest(chest))
			{
				return(false);
			}
			Inventory own = humanoid.GetInventory();
			Inventory store = chest.GetInventory();
			int room = VikingSilver.ArrowStack - OwnSilverArrows(ammoType);
			bool changed = false;
			foreach (ItemDrop.ItemData item in store.m_inventory.FindAll((ItemDrop.ItemData stored) => IsSilverArrowsOf(stored, ammoType)))
			{
				if (room <= 0)
				{
					break;
				}
				int count = Mathf.Min(room, item.m_stack);
				ItemDrop.ItemData taken = item.Clone();
				taken.m_stack = count;
				taken.m_equipped = false;
				if (own.CanAddItem(taken) && own.AddItem(taken))
				{
					store.RemoveItem(item, count);
					room -= count;
					changed = true;
				}
			}
			if (changed)
			{
				own.Changed();
				Plugin.Log.LogInfo(LogName() + " took silver arrows from a chest, " + OwnSilverArrows(ammoType) + " now");
			}
			return(changed);
		}
	}
}
