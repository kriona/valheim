using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Loot lying around - what a viking picks up after a fight and puts away, the drops it collects, and the gear it
	/// takes up from the village's chests when it's better than its own
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// How near loot has to be for a viking going by it to grab it, the same as a player's autopickup reach, and
		/// how often it looks, in meters and seconds
		/// </summary>
		internal const float GrabRange = 2f;
		private const float GrabInterval = 0.5f;

		/// <summary>
		/// When the viking next looks for loot to grab as it goes by, in Time.time seconds (see UpdateGrab)
		/// </summary>
		private float nextGrab;

		/// <summary>
		/// Has a viking going about its job grab loot it passes - within GrabRange of it, lying in or near the village,
		/// that no other viking is after and that it wants and has room for (see WantsLoot) - picking it up at once as
		/// loot it carries, which it puts away later (see FindLootWork), every GrabInterval seconds on its owner. Not
		/// while there's danger near, it's seated or swimming, or it's lightening its load
		/// </summary>
		private void UpdateGrab()
		{
			if (Time.time < nextGrab || nview == null || !nview.IsValid() || !nview.IsOwner() || village == null)
			{
				return;
			}
			nextGrab = Time.time + GrabInterval;
			if (viking == null || !viking.NoDangerNear() || attachPoint != null || inBed || humanoid.IsSwimming() || job is UnburdenJob || body.IsEncumbered())
			{
				return;
			}
			List<Container> chests = null;
			bool mayLeave = false;
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view == null || !view.IsValid() || Vector3.Distance(view.transform.position, transform.position) > GrabRange || village.DistanceOutside(view.transform.position) > LootReach || !IsLoot(view))
				{
					continue;
				}
				ZDOID id = view.GetZDO().m_uid;
				if (workClaims.TryGetValue(id, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				ItemDrop.ItemData item = drop.m_itemData;
				if (chests == null)
				{
					chests = UsableChests();
					mayLeave = MayLeave();
				}
				if (CarryRoom(item, CarryLoot) <= 0 || !WantsLoot(item, chests, mayLeave))
				{
					continue;
				}
				// Another game's item is asked for, and grabbed on a later look once it's this client's
				if (!OwnItem(view))
				{
					RequestItem(view);
					continue;
				}
				if (PickUpLoot(view))
				{
					TestLog.Count("Looted", "grabbed going by");
				}
				return;
			}
		}

		/// <summary>
		/// Finds loot work for the viking - putting away the loot it carries, by day cooking the raw food among it first
		/// at a cooking station with room, or else picking up the nearest loot lying around the village that it wants
		/// </summary>
		/// <param name="fight">True for clearing up after a fight, which goes on through dusk and night</param>
		/// <returns>The job, or null when there's no loot work</returns>
		private Job FindLootWork(bool fight)
		{
			bool carrying = (CarriedLoot() != null);
			if (carrying)
			{
				if (!IsNight() && !IsDusk() && FindCookStation(true) != null)
				{
					Job cook = FindCookWork();
					if (cook != null)
					{
						return(cook);
					}
				}
				// The job puts the raw food away too rather than leaving it for a station, since no cooking work could be
				// had for it. A viking holding on to something it can't drop outside yet doesn't pick up more
				return((Time.time >= dumpAgainAt) ? new LootJob(this, null, fight, false, false) : null);
			}
			dumpNowhereSince = -1f;
			ZNetView loot = FindLoot();
			if (loot != null && ClaimWork(loot.GetZDO().m_uid))
			{
				UpdateOuting(!village.Contains(loot.transform.position));
				return(new LootJob(this, loot, fight));
			}
			return(null);
		}

		/// <summary>
		/// Notes the enemy the viking fights now, and where and when the one it fought before died, when it did - its
		/// corpse gone or dead - so it can pick up what that drops (see FindKillLoot)
		/// </summary>
		/// <param name="target">The enemy it fights now, or null</param>
		private void NoteFought(Character target)
		{
			if (!ReferenceEquals(fought, null) && !ReferenceEquals(fought, target) && (fought == null || fought.IsDead()))
			{
				killSpot = foughtAt;
				killAt = Time.time;
			}
			fought = target;
			if (target != null)
			{
				foughtAt = target.transform.position;
			}
		}

		/// <summary>
		/// Checks whether the viking picks up what an enemy it fought dropped - within KillLootTime of the kill, while
		/// it isn't badly hurt or held back by its load, and with nothing to do it wouldn't drop for loot after a fight
		/// (see YieldsToLoot) - the night watch included, which goes back to its watch after
		/// </summary>
		/// <returns>True when it does</returns>
		private bool PicksUpKillLoot()
		{
			if (killAt < 0f || Time.time - killAt >= KillLootTime || Wounded() || body.IsEncumbered())
			{
				return(false);
			}
			return(job == null || job is WatchJob || YieldsToLoot());
		}

		/// <summary>
		/// Finds what an enemy the viking fought dropped - the nearest loot within KillLootRadius of where it died, however
		/// far from the village, once there's no danger there - to pick up and put away with the loot near it
		/// </summary>
		/// <returns>The job, or null when there's none, or danger near</returns>
		private Job FindKillLoot()
		{
			List<Container> chests = UsableChests();
			if (chests.Count == 0 || DangerNear(killSpot))
			{
				return(null);
			}
			ZNetView best = null;
			float bestDistance = float.MaxValue;
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view == null || !IsLoot(view) || Utils.DistanceXZ(view.transform.position, killSpot) > KillLootRadius)
				{
					continue;
				}
				ZDOID id = view.GetZDO().m_uid;
				if (Avoided(id) || (workClaims.TryGetValue(id, out VillageLife holder) && holder != this && holder != null && holder.job != null))
				{
					continue;
				}
				if (CarryRoom(drop.m_itemData, CarryLoot) <= 0 || !WantsLoot(drop.m_itemData, chests, true))
				{
					continue;
				}
				float distance = Vector3.Distance(view.transform.position, transform.position);
				if (distance < bestDistance)
				{
					best = view;
					bestDistance = distance;
				}
			}
			if (best == null || !ClaimWork(best.GetZDO().m_uid))
			{
				return(null);
			}
			UpdateOuting(!village.Contains(best.transform.position));
			return(new LootJob(this, best, true, true));
		}

		/// <summary>
		/// Checks whether the viking fought within LootRushTime, so it clears up the loot before going back to what it
		/// was doing
		/// </summary>
		/// <returns>True when it did</returns>
		private bool AfterFight()
		{
			return(Time.time - lastFightAt < LootRushTime);
		}

		/// <summary>
		/// Checks whether the viking drops its job to clear up loot after a fight - standing about, strolling, petting a
		/// boar, talking, gathering at the fire or resting for the night, and the work loot comes ahead of (repairing,
		/// rebuilding, keeping the fire stacked, stocking the chests with wood, crafting arrows and repairing its gear),
		/// which it picks up again after. Not keeping watch, cooking, eating or resting while wounded, or going out to
		/// hunt or forage
		/// </summary>
		/// <remarks>
		/// A fight interrupts the viking's job, and the loot it leaves is still coming to rest when the viking picks the
		/// next one, so the work it picks over the loot gives way to it once it's there
		/// </remarks>
		/// <returns>True when it does</returns>
		private bool YieldsToLoot()
		{
			if (Wounded() || IsWatch())
			{
				return(false);
			}
			if (job is RepairJob || job is RebuildJob || job is FuelJob || job is StockJob || job is ArrowJob || job is GearJob)
			{
				return(true);
			}
			return(job is IdleJob || job is StrollJob || job is PetJob || job is TalkJob || job is RestJob || (job is GatherJob gather && !gather.Heals() && !gather.Warms()));
		}

		/// <summary>
		/// Finds the nearest loot lying around the village that no other viking is after and that the viking wants -
		/// one of the village's chests has room for it, or it's something the vikings don't use to drop outside the
		/// village (see WantsLoot) - only in the village while the viking may not leave it (see MayLeave)
		/// </summary>
		/// <returns>The loot, or null when there's none</returns>
		private ZNetView FindLoot()
		{
			List<Container> chests = UsableChests();
			if (chests.Count == 0)
			{
				return(null);
			}
			bool mayLeave = MayLeave();
			float reach = (mayLeave ? LootReach : 0f);
			List<KeyValuePair<float, ZNetView>> found = new List<KeyValuePair<float, ZNetView>>();
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view == null || !IsLoot(view) || village.DistanceOutside(view.transform.position) > reach)
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
				ItemDrop.ItemData item = drop.m_itemData;
				if (CarryRoom(item, CarryLoot) > 0 && WantsLoot(item, chests, mayLeave))
				{
					found.Add(new KeyValuePair<float, ZNetView>(Vector3.Distance(view.transform.position, transform.position), view));
				}
			}
			found.Sort((KeyValuePair<float, ZNetView> a, KeyValuePair<float, ZNetView> b) => a.Key.CompareTo(b.Key));
			// Loot it can't get within reach of is left alone for a while, and the next nearest tried - except in a pen,
			// which has no way in but over its fence, which the viking jumps. Loot lying up on a pen's fence is told by the
			// ground it's reached from, which lies in the pen, since its own spot is above the fence, and loot at the far
			// side of a pen too big to see as fenced in by the way there (see PennedByPaths)
			int tries = 0;
			foreach (KeyValuePair<float, ZNetView> entry in found)
			{
				// Loot out of the village with a monster near it is left for now
				if (!village.Contains(entry.Value.transform.position) && DangerNear(entry.Value.transform.position))
				{
					continue;
				}
				if (InPen(entry.Value.GetZDO().m_uid, entry.Value.transform.position) || CanReach(entry.Value.transform.position, out Vector3 stand, out string why, out bool noPath) || (noPath && (Village.FencedIn(stand) || PennedByPaths(stand))))
				{
					return(entry.Value);
				}
				DebugAvoid(entry.Value, why);
				Avoid(entry.Value);
				if (++tries >= ReachTries)
				{
					break;
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether an object is loot the viking puts away - an item at rest that has lain on the ground for
		/// DropSettle, or LootSettle for one a player dropped, so what a player just put down is left a while, and not a
		/// fish, ammo, which
		/// the viking would shoot, food set out as a piece, or one a viking dropped outside the village because the
		/// chests hold enough of it
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>True when it is</returns>
		private static bool IsLoot(ZNetView view)
		{
			if (!view.IsValid() || view.GetZDO().GetBool(DiscardKey))
			{
				return(false);
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null || drop.IsPiece() || view.GetComponent<Fish>() != null)
			{
				return(false);
			}
			ItemDrop.ItemData.ItemType type = drop.m_itemData.m_shared.m_itemType;
			if (type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable)
			{
				return(false);
			}
			// What's still flying or rolling isn't loot until it comes to rest, so where it can be reached from is judged
			// where it ends up
			Rigidbody body = drop.m_body;
			if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > LootRestSpeed * LootRestSpeed)
			{
				return(false);
			}
			// A player's drop no longer picks itself up, which tells it from what drops from a kill
			return(drop.GetTimeSinceSpawned() >= (drop.m_autoPickup ? DropSettle : LootSettle) && ItemOf(drop) != null);
		}

		/// <summary>
		/// Picks up loot the viking has reached, as much of it as its inventory has room for
		/// </summary>
		/// <param name="view">The loot</param>
		/// <returns>False when it was gone or there was no room</returns>
		private bool PickUpLoot(ZNetView view)
		{
			if (view == null || !IsLoot(view))
			{
				return(false);
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			if (!OwnItem(view))
			{
				return(false);
			}
			drop.Load();
			ItemDrop.ItemData item = ItemOf(drop);
			int stack = item.m_stack;
			int taken = Carry(item, stack, CarryLoot);
			if (taken <= 0)
			{
				return(false);
			}
			if (taken >= stack)
			{
				ZNetScene.instance.Destroy(drop.gameObject);
			}
			else
			{
				drop.SetStack(stack - taken);
			}
			TestLog.Count("Looted", TestLog.ItemName(item), taken);
			TakeUpGear();
			return(true);
		}

		/// <summary>
		/// Asks the game that owns an item on the ground to hand it over, the way a player's game does as they pick it
		/// up, so it's this client's by the time the viking reaching for it takes it (see OwnItem)
		/// </summary>
		/// <param name="view">The item, or a branch, which needs no asking</param>
		private static void RequestItem(ZNetView view)
		{
			ItemDrop drop = ((view != null && view.IsValid()) ? view.GetComponent<ItemDrop>() : null);
			if (drop != null)
			{
				drop.RequestOwn();
			}
		}

		/// <summary>
		/// Makes an item on the ground the viking has reached this client's to take, the way only its owner may - left
		/// alone while another game owns it, which hasn't handed it over yet (see RequestItem) or is picking it up
		/// </summary>
		/// <param name="view">The item</param>
		/// <returns>True when this client owns it</returns>
		private static bool OwnItem(ZNetView view)
		{
			if (view.IsOwner())
			{
				return(true);
			}
			if (view.GetZDO().HasOwner())
			{
				return(false);
			}
			view.ClaimOwnership();
			return(true);
		}

		/// <summary>
		/// Has the viking take up gear among the loot it carries that's better than its own - armor, a shield or a
		/// weapon - so it wears or wields it rather than putting it in a chest, putting it on in the piece's equip time
		/// the way a player does (see FoundGearDone)
		/// </summary>
		private void TakeUpGear()
		{
			if (viking == null || GearHeld())
			{
				return;
			}
			Inventory inventory = humanoid.GetInventory();
			bool changed = false;
			foreach (ItemDrop.ItemData item in inventory.m_inventory.FindAll((ItemDrop.ItemData carried) => IsCarriedFor(carried, CarryLoot) && IsGear(carried) && !body.GearQueued(carried)))
			{
				// It's its own while it's weighed up, since a viking never wields what it carries for a chest
				item.m_customData.Remove(VikingItems.CarryKey);
				if (!viking.WouldTakeUp(item))
				{
					item.m_customData[VikingItems.CarryKey] = CarryLoot;
					continue;
				}
				body.QueueGear(item, true, false, true);
				changed = true;
			}
			if (changed)
			{
				inventory.Changed();
			}
		}

		/// <summary>
		/// Finishes taking up a piece of gear the viking found as loot or in a chest - carrying what it replaced to a
		/// chest, or the piece itself when it turned out not to be better by the time it was put on
		/// </summary>
		/// <param name="item">The piece</param>
		/// <param name="tookUp">Whether the viking took it up</param>
		/// <param name="replaced">The armor, shield or weapon it replaced, or null</param>
		private void FoundGearDone(ItemDrop.ItemData item, bool tookUp, ItemDrop.ItemData replaced)
		{
			if (!tookUp)
			{
				item.m_customData[VikingItems.CarryKey] = CarryLoot;
				humanoid.GetInventory().Changed();
				return;
			}
			if (replaced != null)
			{
				replaced.m_equipped = false;
				replaced.m_customData[VikingItems.CarryKey] = CarryLoot;
				humanoid.GetInventory().Changed();
			}
			TestLog.Count("Gear taken up", TestLog.ItemName(item));
			Plugin.Log.LogInfo(LogName() + " took up " + Localization.instance.Localize(item.m_shared.m_name) + ((replaced != null) ? " in place of " + Localization.instance.Localize(replaced.m_shared.m_name) : ""));
		}

		/// <summary>
		/// Finds the nearest of the village's chests holding gear the viking would take up over its own (see
		/// VikingCore.WouldTakeUp)
		/// </summary>
		/// <returns>The chest, or null when none holds any</returns>
		private Container FindUpgradeChest()
		{
			if (viking == null)
			{
				return(null);
			}
			return(Nearest(UsableChests().FindAll((Container chest) => UpgradeIn(chest) != null)));
		}

		/// <summary>
		/// Finds gear in a chest the viking would take up over its own - for an NPC viking, only what none of the
		/// village's PC vikings would take up, so a spare dverger circlet or strength belt goes to a PC viking first
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>The gear, or null when there's none</returns>
		private ItemDrop.ItemData UpgradeIn(Container chest)
		{
			return(chest.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => IsGear(item) && viking.WouldTakeUp(item) && !(Peaceful() && PCWants(item))));
		}

		/// <summary>
		/// Checks whether any of the village's PC vikings loaded here would take up a piece of gear
		/// </summary>
		/// <param name="item">The gear</param>
		/// <returns>True when one would</returns>
		private bool PCWants(ItemDrop.ItemData item)
		{
			return(village.Vikings().Exists((Character other) => VikingCore.IsPCViking(other) && other.TryGetComponent(out VikingCore core) && core.WouldTakeUp(item)));
		}

		/// <summary>
		/// Takes gear the viking would take up out of a chest it has reached and takes it up, carrying what it replaces
		/// to put back
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>False when there was no such gear left in it, no room to carry it, or the chest couldn't be used</returns>
		private bool TakeUpgrade(Container chest)
		{
			if (!OpenChest(chest))
			{
				return(false);
			}
			ItemDrop.ItemData item = UpgradeIn(chest);
			if (item == null || Carry(item, 1, CarryLoot) <= 0)
			{
				return(false);
			}
			chest.GetInventory().RemoveItem(item);
			TakeUpGear();
			return(true);
		}

		/// <summary>
		/// Checks whether an item is gear a viking could take up - armor, a shield, a utility item like a strength belt,
		/// or a weapon other than a torch
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		private static bool IsGear(ItemDrop.ItemData item)
		{
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			bool armor = (type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Chest || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Shoulder || type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.Utility);
			return(armor || (item.IsWeapon() && type != ItemDrop.ItemData.ItemType.Torch));
		}

		/// <summary>
		/// Finds the nearest item lying within CollectRadius of a hunter's kill - its meat, hide and the rest, dropped as
		/// it or its zombie died
		/// </summary>
		/// <param name="kill">Where the kill was made</param>
		/// <returns>The item, or null when there's none</returns>
		private ZNetView FindDrop(Vector3 kill)
		{
			ZNetView nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view == null || !view.IsValid() || Utils.DistanceXZ(view.transform.position, kill) > CollectRadius || !IsKillDrop(view))
				{
					continue;
				}
				ItemDrop.ItemData item = drop.m_itemData;
				float distance = Vector3.Distance(view.transform.position, transform.position);
				if (distance < nearestDistance && CarryRoom(item, CarryLoot) > 0 && !Avoided(view.GetZDO().m_uid))
				{
					nearest = view;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether an object is an item a hunter gathers from its kill - one lying on the ground that isn't set
		/// out as a piece, a fish, or one a viking dropped outside the village, however long it has lain there
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>True when it is</returns>
		private static bool IsKillDrop(ZNetView view)
		{
			if (!view.IsValid() || view.GetZDO().GetBool(DiscardKey))
			{
				return(false);
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			return(drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null && !drop.IsPiece() && view.GetComponent<Fish>() == null && ItemOf(drop) != null);
		}

		/// <summary>
		/// Picks up an item from a hunter's kill, as much of it as the viking has room for
		/// </summary>
		/// <param name="view">The item</param>
		/// <returns>False when it was gone or there was no room</returns>
		private bool PickUpDrop(ZNetView view)
		{
			if (view == null || !IsKillDrop(view))
			{
				return(false);
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			if (!OwnItem(view))
			{
				return(false);
			}
			drop.Load();
			ItemDrop.ItemData item = ItemOf(drop);
			int stack = item.m_stack;
			int taken = Carry(item, stack, CarryLoot);
			if (taken <= 0)
			{
				return(false);
			}
			if (taken >= stack)
			{
				ZNetScene.instance.Destroy(drop.gameObject);
			}
			else
			{
				drop.SetStack(stack - taken);
			}
			TestLog.Count("Looted from kills", TestLog.ItemName(item), taken);
			return(true);
		}
	}
}
