using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SwapGear
{
	/// <summary>
	/// Swaps the player's worn gear with the items on an armor stand
	/// </summary>
	/// <remarks>
	/// The swap takes ownership of the armor stand before dropping its items so dropping and attaching run locally
	/// in order instead of racing RPCs to another player's client. Ownership is requested at the start so it
	/// arrives while the worn gear is being unequipped
	/// </remarks>
	internal static class Swap
	{
		private const float OwnershipTimeout = 5f;
		private const float AttachTimeout = 2f;

		/// <summary>
		/// Whether a swap is in progress
		/// </summary>
		public static bool Running;

		/// <summary>
		/// One item on the armor stand and the worn item it is swapped with
		/// </summary>
		private class StandItem
		{
			public int Slot;
			public string PrefabName;
			public string Name;
			public ItemDrop.ItemData.ItemType Type;
			public ItemDrop.ItemData Worn;
			public ItemDrop.ItemData PickedUp;
			public bool Queued;
			public bool Done;
		}

		/// <summary>
		/// The armor stand the player is looking at, if it has items on it and they have access to it
		/// </summary>
		/// <param name="player">The local player</param>
		/// <returns>The armor stand, or null</returns>
		public static ArmorStand GetHoveredStand(Player player)
		{
			GameObject hover = player.GetHoverObject();
			if (hover == null)
			{
				return(null);
			}
			ArmorStand stand = hover.GetComponentInParent<ArmorStand>();
			return((stand != null && CanSwap(stand)) ? stand : null);
		}

		/// <summary>
		/// Whether the armor stand has items on it and the player has access to it
		/// </summary>
		/// <param name="stand">The armor stand</param>
		/// <returns>True when a swap can start</returns>
		public static bool CanSwap(ArmorStand stand)
		{
			if (!stand.m_nview.IsValid() || !PrivateArea.CheckAccess(stand.transform.position, 0f, false))
			{
				return(false);
			}
			for (int i = 0; i < stand.m_slots.Count; i++)
			{
				if (stand.HaveAttachment(i))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Unequips the worn items matching the armor stand's items, drops the armor stand's items, puts the worn
		/// items on the armor stand and equips the dropped items as they are picked up
		/// </summary>
		/// <param name="player">The local player</param>
		/// <param name="stand">The armor stand to swap with</param>
		/// <returns>The coroutine steps</returns>
		public static IEnumerator Run(Player player, ArmorStand stand)
		{
			Running = true;
			try
			{
				List<StandItem> items = GetStandItems(stand);
				if (items.Count == 0)
				{
					yield break;
				}
				foreach (StandItem item in items)
				{
					ItemDrop.ItemData worn = GetWornItem(player, item.Type);
					if (worn != null && !items.Exists(other => other.Worn == worn))
					{
						item.Worn = worn;
					}
				}

				if (!stand.m_nview.IsOwner())
				{
					stand.m_nview.InvokeRPC("RPC_RequestOwn");
				}

				foreach (StandItem item in items)
				{
					if (item.Worn != null)
					{
						StartUnequip(player, item.Worn);
					}
				}
				while (items.Exists(item => item.Worn != null && player.IsItemEquiped(item.Worn)))
				{
					if (IsCanceled(player) || !IsStandValid(stand))
					{
						CancelQueued(player, items);
						yield break;
					}
					if (items.Exists(item => item.Worn != null && player.IsItemEquiped(item.Worn) && !player.IsEquipActionQueued(item.Worn)))
					{
						CancelQueued(player, items);
						player.Message(MessageHud.MessageType.Center, "Swap gear interrupted");
						yield break;
					}
					yield return null;
				}

				float deadline = Time.time + OwnershipTimeout;
				while (!stand.m_nview.IsOwner())
				{
					if (IsCanceled(player) || !IsStandValid(stand))
					{
						yield break;
					}
					if (Time.time > deadline)
					{
						player.Message(MessageHud.MessageType.Center, "Swap gear failed - the armor stand is busy");
						yield break;
					}
					yield return null;
				}

				HashSet<ItemDrop.ItemData> before = new HashSet<ItemDrop.ItemData>(player.GetInventory().GetAllItems());
				foreach (StandItem item in items)
				{
					stand.DropItem(item.Slot);
				}

				foreach (StandItem item in items)
				{
					if (item.Worn == null)
					{
						continue;
					}
					int slot = FindSlot(stand, item.Slot, item.Worn);
					if (slot < 0 || !stand.UseItem(stand.m_slots[slot].m_switch, player, item.Worn))
					{
						continue;
					}
					deadline = Time.time + AttachTimeout;
					while (!stand.HaveAttachment(slot) && player.GetInventory().ContainsItem(item.Worn) && Time.time < deadline)
					{
						if (IsCanceled(player) || !IsStandValid(stand))
						{
							yield break;
						}
						yield return null;
					}
				}

				deadline = Time.time + Plugin.PickupTimeout.Value;
				while (!EquipPickedUp(player, items, before, Time.time > deadline))
				{
					if (IsCanceled(player))
					{
						CancelQueued(player, items);
						yield break;
					}
					yield return null;
				}

				List<string> missing = new List<string>();
				foreach (StandItem item in items)
				{
					if (item.PickedUp == null)
					{
						missing.Add(item.Name);
					}
				}
				string message = ((missing.Count == 0) ? "Gear swapped" : "Swap gear - not picked up: " + string.Join(", ", missing));
				player.Message(MessageHud.MessageType.Center, Localization.instance.Localize(message));
			}
			finally
			{
				Running = false;
			}
		}

		/// <summary>
		/// Lists the items on the armor stand by slot
		/// </summary>
		/// <param name="stand">The armor stand</param>
		/// <returns>One entry per occupied slot with a known item prefab</returns>
		private static List<StandItem> GetStandItems(ArmorStand stand)
		{
			List<StandItem> items = new List<StandItem>();
			for (int i = 0; i < stand.m_slots.Count; i++)
			{
				if (!stand.HaveAttachment(i))
				{
					continue;
				}
				GameObject prefab = ObjectDB.instance.GetItemPrefab(stand.GetAttachedItem(i));
				ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
				if (drop == null)
				{
					continue;
				}
				items.Add(new StandItem
				{
					Slot = i,
					PrefabName = prefab.name,
					Name = drop.m_itemData.m_shared.m_name,
					Type = drop.m_itemData.m_shared.m_itemType
				});
			}
			return(items);
		}

		/// <summary>
		/// The item the player has equipped in the place an item of this type is equipped
		/// </summary>
		/// <remarks>
		/// Weapons put away with the hide key still count as held in their hand
		/// </remarks>
		/// <param name="player">The local player</param>
		/// <param name="type">The type of the armor stand's item</param>
		/// <returns>The worn item, or null if nothing is worn there</returns>
		private static ItemDrop.ItemData GetWornItem(Player player, ItemDrop.ItemData.ItemType type)
		{
			ItemDrop.ItemData right = (player.m_rightItem ?? player.m_hiddenRightItem);
			ItemDrop.ItemData left = (player.m_leftItem ?? player.m_hiddenLeftItem);
			switch (type)
			{
				case ItemDrop.ItemData.ItemType.Helmet:
					return(player.m_helmetItem);
				case ItemDrop.ItemData.ItemType.Chest:
					return(player.m_chestItem);
				case ItemDrop.ItemData.ItemType.Legs:
					return(player.m_legItem);
				case ItemDrop.ItemData.ItemType.Shoulder:
					return(player.m_shoulderItem);
				case ItemDrop.ItemData.ItemType.Utility:
					return(player.m_utilityItem);
				case ItemDrop.ItemData.ItemType.Trinket:
					return(player.m_trinketItem);
				case ItemDrop.ItemData.ItemType.Shield:
				case ItemDrop.ItemData.ItemType.Bow:
				case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
					return(left);
				case ItemDrop.ItemData.ItemType.Torch:
					if (left != null && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch)
					{
						return(left);
					}
					return(right);
				case ItemDrop.ItemData.ItemType.OneHandedWeapon:
				case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
				case ItemDrop.ItemData.ItemType.Tool:
					return(right);
				default:
					return(null);
			}
		}

		/// <summary>
		/// Picks an empty armor stand slot for the item, preferring the slot its replacement came from
		/// </summary>
		/// <param name="stand">The armor stand</param>
		/// <param name="preferred">The slot the swapped item was in</param>
		/// <param name="item">The item to attach</param>
		/// <returns>The slot index, or -1 if no empty slot takes the item</returns>
		private static int FindSlot(ArmorStand stand, int preferred, ItemDrop.ItemData item)
		{
			if (Fits(stand, preferred, item))
			{
				return(preferred);
			}
			for (int i = 0; i < stand.m_slots.Count; i++)
			{
				if (Fits(stand, i, item))
				{
					return(i);
				}
			}
			return(-1);
		}

		/// <summary>
		/// Whether the armor stand slot is empty and takes the item
		/// </summary>
		/// <param name="stand">The armor stand</param>
		/// <param name="slot">The slot index</param>
		/// <param name="item">The item to attach</param>
		/// <returns>True when the item can go in the slot</returns>
		private static bool Fits(ArmorStand stand, int slot, ItemDrop.ItemData item)
		{
			return(!stand.HaveAttachment(slot) && stand.CanAttach(stand.m_slots[slot], item));
		}

		/// <summary>
		/// Whether gear changes happen at once rather than taking the item's equip time
		/// </summary>
		/// <param name="item">The item being equipped or unequipped</param>
		/// <returns>True when the item is equipped or unequipped directly</returns>
		private static bool IsInstant(ItemDrop.ItemData item)
		{
			return(Plugin.InstantEquip.Value || item.m_shared.m_equipDuration <= 0f);
		}

		/// <summary>
		/// Unequips the item, or queues the unequip so it takes as long as it does from the inventory
		/// </summary>
		/// <remarks>
		/// A weapon put away with the hide key isn't equipped, so it is always cleared at once
		/// </remarks>
		/// <param name="player">The local player</param>
		/// <param name="item">The worn item</param>
		private static void StartUnequip(Player player, ItemDrop.ItemData item)
		{
			if (IsInstant(item) || !player.IsItemEquiped(item))
			{
				player.UnequipItem(item);
			}
			else if (!player.IsEquipActionQueued(item))
			{
				player.QueueUnequipAction(item);
			}
		}

		/// <summary>
		/// Removes any equip or unequip actions the swap queued
		/// </summary>
		/// <param name="player">The local player</param>
		/// <param name="items">The armor stand's items</param>
		private static void CancelQueued(Player player, List<StandItem> items)
		{
			foreach (StandItem item in items)
			{
				player.RemoveEquipAction(item.Worn);
				player.RemoveEquipAction(item.PickedUp);
			}
		}

		/// <summary>
		/// Equips each dropped item that has turned up in the inventory, queuing the equip unless it is instant
		/// </summary>
		/// <remarks>
		/// A picked up item is told apart from one the player already had by comparing against the inventory from
		/// before the drop. A queued equip that leaves the queue without the item equipped was interrupted by the
		/// game, e.g. by attacking, and is left for the player to redo
		/// </remarks>
		/// <param name="player">The local player</param>
		/// <param name="items">The armor stand's items</param>
		/// <param name="before">The inventory's items before the armor stand's items were dropped</param>
		/// <param name="timedOut">Whether to stop waiting for items that haven't been picked up</param>
		/// <returns>True when every item has been picked up and handled</returns>
		private static bool EquipPickedUp(Player player, List<StandItem> items, HashSet<ItemDrop.ItemData> before, bool timedOut)
		{
			bool done = true;
			foreach (StandItem item in items)
			{
				if (item.Done)
				{
					continue;
				}
				if (item.PickedUp == null)
				{
					item.PickedUp = FindPickedUp(player, items, before, item.PrefabName);
				}
				if (item.PickedUp == null)
				{
					if (timedOut)
					{
						item.Done = true;
					}
					else
					{
						done = false;
					}
					continue;
				}
				if (item.Queued)
				{
					if (player.IsItemEquiped(item.PickedUp) || !player.IsEquipActionQueued(item.PickedUp))
					{
						item.Done = true;
					}
					else
					{
						done = false;
					}
					continue;
				}
				if (!IsInstant(item.PickedUp) && !player.IsItemEquiped(item.PickedUp))
				{
					player.QueueEquipAction(item.PickedUp);
					item.Queued = true;
					done = false;
					continue;
				}
				if (player.IsItemEquiped(item.PickedUp) || player.EquipItem(item.PickedUp))
				{
					item.Done = true;
				}
				else if (player.InAttack() || player.InDodge())
				{
					done = false;
				}
				else
				{
					item.Done = true;
				}
			}
			return(done);
		}

		/// <summary>
		/// Finds an inventory item of the prefab that wasn't there before the drop and isn't already claimed
		/// </summary>
		/// <param name="player">The local player</param>
		/// <param name="items">The armor stand's items</param>
		/// <param name="before">The inventory's items before the armor stand's items were dropped</param>
		/// <param name="prefabName">The item prefab's name</param>
		/// <returns>The picked up item, or null</returns>
		private static ItemDrop.ItemData FindPickedUp(Player player, List<StandItem> items, HashSet<ItemDrop.ItemData> before, string prefabName)
		{
			foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
			{
				if (before.Contains(item) || item.m_dropPrefab == null || item.m_dropPrefab.name != prefabName || items.Exists(other => other.PickedUp == item))
				{
					continue;
				}
				return(item);
			}
			return(null);
		}

		/// <summary>
		/// Whether Esc was pressed or the player is gone, showing a message when canceled by Esc
		/// </summary>
		/// <param name="player">The player running the swap</param>
		/// <returns>True when the swap should stop</returns>
		private static bool IsCanceled(Player player)
		{
			if (player == null || Player.m_localPlayer != player || player.IsDead())
			{
				return(true);
			}
			if (ZInput.GetKeyDown(KeyCode.Escape, false))
			{
				player.Message(MessageHud.MessageType.Center, "Swap gear canceled");
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Whether the armor stand still exists in the world
		/// </summary>
		/// <param name="stand">The armor stand</param>
		/// <returns>True when it can still be used</returns>
		private static bool IsStandValid(ArmorStand stand)
		{
			return(stand != null && stand.m_nview.IsValid());
		}
	}
}
