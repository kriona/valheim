using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The wood a viking gathers and stores - work that takes wood, the branches and wood on the ground near the village,
	/// and the chests it puts wood in and takes it from
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds work that takes wood - the lowest missing piece, then the village's fire or one of its gathering fires
		/// to stack
		/// </summary>
		/// <returns>The job, or null when nothing needs it</returns>
		private Job FindWoodWork()
		{
			// A ward keeps the vikings from building in it, the same as it keeps out players it doesn't permit. The lowest
			// pieces go back first, so walls and roofs have what holds them up
			List<PieceRecord> missing = VillageRecords.Missing(village);
			missing.Sort((PieceRecord a, PieceRecord b) => a.Position.y.CompareTo(b.Position.y));
			// With no more wood to get for now, it only builds back what the wood it carries is enough for, rather than
			// starting a piece it gives up on as soon as it looks for the rest of the wood
			bool moreWood = (Time.time >= noWoodUntil || FindWoodChest() != null);
			int carried = CarriedWood();
			foreach (PieceRecord record in missing)
			{
				if ((moreWood || carried >= Mathf.Min(record.Wood, CarryMax)) && !Warded(record.Position) && ClaimWork(RecordKey(record)))
				{
					return(new RebuildJob(this, record, false));
				}
			}
			// A house razed to the ground goes back up once the rest of the village is whole, lowest pieces first - once
			// one of its pieces stands it's no longer in ruins, and the rest of it is built back with the others
			List<PieceRecord> ruins = VillageRecords.MissingInRuins(village);
			ruins.Sort((PieceRecord a, PieceRecord b) => a.Position.y.CompareTo(b.Position.y));
			foreach (PieceRecord record in ruins)
			{
				if ((moreWood || carried >= Mathf.Min(record.Wood, CarryMax)) && !Warded(record.Position) && ClaimWork(RecordKey(record)))
				{
					return(new RebuildJob(this, record, false));
				}
			}
			foreach (Fireplace fire in village.GatherFires())
			{
				if (fire != null && !IsFull(fire) && ClaimObject(fire.GetComponent<ZNetView>()))
				{
					return(new FuelJob(this, fire));
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds the chest to stock with wood among the village's chests the viking may use with room for more - one with
		/// an unfinished stack of wood to top up first, so the wood fills a stack before it's spread over new ones (see
		/// NearestStacking)
		/// </summary>
		/// <returns>The chest, or null when none has room</returns>
		private Container FindStockChest()
		{
			ItemDrop.ItemData plain = ItemOf("Wood");
			if (plain == null)
			{
				return(null);
			}
			return(NearestStacking(UsableChests().FindAll((Container chest) => ChestRoom(chest, plain, null) > 0), plain, CarryMax));
		}

		/// <summary>
		/// Puts the wood the viking carries in a chest it has reached, as much as the chest may hold
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>How much it put in</returns>
		private int PutWood(Container chest)
		{
			if (!OpenChest(chest))
			{
				return(0);
			}
			Inventory store = chest.GetInventory();
			Inventory inventory = humanoid.GetInventory();
			int put = 0;
			foreach (ItemDrop.ItemData carried in inventory.m_inventory.FindAll((ItemDrop.ItemData held) => IsCarriedFor(held, CarryWood)))
			{
				int amount = Mathf.Min(carried.m_stack, ChestRoom(chest, carried, null));
				if (amount <= 0)
				{
					continue;
				}
				ItemDrop.ItemData item = carried.Clone();
				item.m_customData.Remove(VikingItems.CarryKey);
				item.m_equipped = false;
				item.m_stack = amount;
				if (!store.AddItem(item))
				{
					break;
				}
				inventory.RemoveItem(carried, amount);
				put += amount;
			}
			if (put > 0)
			{
				chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			}
			return(put);
		}

		/// <summary>
		/// Finds the nearest of the village's chests the viking may use that has wood in it - one with an unfinished stack
		/// of wood first, so the wood left stays in full stacks
		/// </summary>
		/// <returns>The chest, or null when none has</returns>
		private Container FindWoodChest()
		{
			List<Container> stocked = UsableChests().FindAll((Container chest) => ChestWood(chest) > 0);
			Container unfinished = Nearest(stocked.FindAll((Container chest) => chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData stored) => IsWoodItem(stored) && stored.m_stack < stored.m_shared.m_maxStackSize)));
			return((unfinished != null) ? unfinished : Nearest(stocked));
		}

		/// <summary>
		/// Finds the chest nearest the viking
		/// </summary>
		/// <param name="chests">The chests</param>
		/// <returns>The nearest, or null when there are none</returns>
		private Container Nearest(List<Container> chests)
		{
			Container nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Container chest in chests)
			{
				float distance = Vector3.Distance(chest.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = chest;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether an item in a chest is wood
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		private static bool IsWoodItem(ItemDrop.ItemData item)
		{
			return(item != null && item.m_dropPrefab != null && WoodItems.Contains(item.m_dropPrefab.name));
		}

		/// <summary>
		/// Counts the wood in a chest
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>The wood, of every kind</returns>
		private static int ChestWood(Container chest)
		{
			int wood = 0;
			foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
			{
				if (IsWoodItem(item))
				{
					wood += item.m_stack;
				}
			}
			return(wood);
		}

		/// <summary>
		/// Takes wood from a chest the viking has reached, as much as it needs and can carry, from its smallest stack first
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="wanted">How much it needs</param>
		/// <returns>How much it took</returns>
		private int TakeWood(Container chest, int wanted)
		{
			if (wanted <= 0 || !OpenChest(chest))
			{
				return(0);
			}
			Inventory store = chest.GetInventory();
			int taken = 0;
			// The smallest stack is used up first, so the wood left stays in full stacks
			List<ItemDrop.ItemData> stacks = store.m_inventory.FindAll((ItemDrop.ItemData stored) => IsWoodItem(stored));
			stacks.Sort((ItemDrop.ItemData a, ItemDrop.ItemData b) => a.m_stack.CompareTo(b.m_stack));
			foreach (ItemDrop.ItemData item in stacks)
			{
				if (taken >= wanted)
				{
					break;
				}
				int took = AddWood(item, Mathf.Min(wanted - taken, item.m_stack));
				if (took <= 0)
				{
					break;
				}
				store.RemoveItem(item, took);
				taken += took;
			}
			if (taken > 0)
			{
				chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			}
			return(taken);
		}

		/// <summary>
		/// Finds the nearest wood near the village that no other viking is after - a branch, or wood lying on the
		/// ground - only in the village while the viking may not leave it (see MayLeave)
		/// </summary>
		/// <returns>The branch or wood, or null when there's none</returns>
		private ZNetView FindWood()
		{
			float reach = (MayLeave() ? WoodReach : 0f);
			ZNetView nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (ZNetView view in LoadedWood())
			{
				if (!IsWood(view) || village.DistanceOutside(view.transform.position) > reach)
				{
					continue;
				}
				ZDOID id = view.GetZDO().m_uid;
				if (Avoided(id))
				{
					continue;
				}
				if (workClaims.TryGetValue(id, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				float distance = Vector3.Distance(view.transform.position, transform.position);
				if (distance < nearestDistance && (village.Contains(view.transform.position) || !DangerNear(view.transform.position)))
				{
					nearest = view;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Goes through the loaded objects that can be wood the viking gathers (see IsWood) - the items on the ground
		/// and the branches
		/// </summary>
		/// <returns>Their objects, leaving out those destroyed since they were listed (see ListLoaded)</returns>
		private static IEnumerable<ZNetView> LoadedWood()
		{
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view != null && view.IsValid() && view.GetZDO().GetPrefab() != BranchHash)
				{
					yield return(view);
				}
			}
			foreach (Pickable pickable in LoadedPickables())
			{
				ZNetView view = ((pickable != null) ? pickable.m_nview : null);
				if (view != null && view.IsValid() && view.GetZDO().GetPrefab() == BranchHash)
				{
					yield return(view);
				}
			}
		}

		/// <summary>
		/// Checks whether an object is wood the viking can gather - a branch that hasn't been picked, or wood lying on
		/// the ground
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>True when it is</returns>
		private static bool IsWood(ZNetView view)
		{
			if (!view.IsValid())
			{
				return(false);
			}
			if (view.GetZDO().GetPrefab() == BranchHash)
			{
				Pickable branch = view.GetComponent<Pickable>();
				return(branch != null && !branch.GetPicked());
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			return(drop != null && drop.m_itemData != null && drop.m_itemData.m_dropPrefab != null && WoodItems.Contains(drop.m_itemData.m_dropPrefab.name));
		}

		/// <summary>
		/// Gathers wood the viking has reached into its inventory - picking a branch, or picking up as much of the wood
		/// on the ground as it can carry, leaving the rest
		/// </summary>
		/// <param name="view">The branch or wood</param>
		/// <returns>How much wood it gathered</returns>
		private int GatherWood(ZNetView view)
		{
			if (view == null || !IsWood(view))
			{
				return(0);
			}
			Pickable branch = view.GetComponent<Pickable>();
			if (branch != null)
			{
				ItemDrop.ItemData picked = ItemOf((branch.m_itemPrefab != null) ? branch.m_itemPrefab.name : "Wood");
				if (picked == null || WoodRoom(picked) <= 0 || !PickBranch(branch))
				{
					return(0);
				}
				return(AddWood(picked, 1));
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			if (drop == null || !OwnItem(view))
			{
				return(0);
			}
			drop.Load();
			ItemDrop.ItemData wood = ItemOf(drop);
			if (wood == null)
			{
				return(0);
			}
			int stack = wood.m_stack;
			int taken = AddWood(wood, stack);
			if (taken <= 0)
			{
				return(0);
			}
			if (taken >= stack)
			{
				ZNetScene.instance.Destroy(drop.gameObject);
			}
			else
			{
				drop.SetStack(stack - taken);
			}
			return(taken);
		}

		/// <summary>
		/// Picks up a branch the viking has reached, the same as a player picking it, so it grows back as usual - the
		/// viking keeps the wood rather than dropping it
		/// </summary>
		/// <param name="branch">The branch</param>
		/// <returns>False when it was already gone</returns>
		private static bool PickBranch(Pickable branch)
		{
			if (branch == null || branch.GetPicked() || branch.m_nview == null || !branch.m_nview.IsValid())
			{
				return(false);
			}
			branch.m_pickEffector.Create(branch.transform.position, Quaternion.identity);
			branch.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true);
			TestLog.Count("Work done", "branches picked up");
			return(true);
		}
	}
}
