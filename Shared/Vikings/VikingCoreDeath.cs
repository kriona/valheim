using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// What a viking leaves when it dies - its things handed to its corpse or grave, the tombstone viking's grave,
	/// and the loot and coins a viking of the world drops
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Hands a dying living viking's data, inventory, name and the players it knows to its corpse, for the zombie
		/// viking it rises as
		/// </summary>
		/// <param name="creature">The dying creature</param>
		/// <param name="corpse">Its corpse's ZDO, owned by this client</param>
		public static void HandToCorpse(Character creature, ZDO corpse)
		{
			if (creature == null || !IsLivingViking(creature))
			{
				return;
			}
			ZDO zdo = creature.m_nview.GetZDO();
			byte[] dataBytes = zdo.GetByteArray(DataKey);
			if (dataBytes == null)
			{
				return;
			}
			// The corpse holds the viking's things until its zombie rises, so it's kept with the world
			corpse.Persistent = true;
			corpse.Set(DataKey, dataBytes);
			byte[] inventoryBytes = zdo.GetByteArray(InventoryKey);
			if (inventoryBytes != null)
			{
				corpse.Set(InventoryKey, inventoryBytes);
			}
			VikingName.Carry(zdo, corpse);
		}

		/// <summary>
		/// Gives a viking that's just been raised its data, inventory, name and the players it knows from the corpse it
		/// rose from
		/// </summary>
		/// <param name="corpse">The corpse's ZDO</param>
		/// <param name="viking">The viking, owned by this client</param>
		public static void HandOver(ZDO corpse, Character viking)
		{
			byte[] dataBytes = corpse.GetByteArray(DataKey);
			if (dataBytes != null)
			{
				HandOver(dataBytes, corpse.GetByteArray(InventoryKey), viking);
				VikingName.Carry(corpse, viking.m_nview.GetZDO());
			}
		}

		/// <summary>
		/// Gives a viking that's just been raised its data, inventory and name, before its Start loads them
		/// </summary>
		/// <param name="dataBytes">Its VikingData</param>
		/// <param name="inventoryBytes">Its inventory, or null</param>
		/// <param name="viking">The viking, owned by this client</param>
		private static void HandOver(byte[] dataBytes, byte[] inventoryBytes, Character viking)
		{
			ZDO zdo = viking.m_nview.GetZDO();
			zdo.Set(DataKey, dataBytes);
			if (inventoryBytes != null)
			{
				zdo.Set(InventoryKey, inventoryBytes);
			}
			VikingData data = VikingData.FromBytes(dataBytes);
			zdo.Set(ZDOVars.s_overrideHoverName, data.Name);
		}

		/// <summary>
		/// Drops the things a corpse holds for its viking in a tombstone, when the viking couldn't be raised - the wood
		/// and loot it carried from its life in a village on the ground beside it
		/// </summary>
		/// <param name="corpse">The corpse's ZDO</param>
		/// <param name="position">Where the tombstone goes</param>
		/// <param name="rotation">Which way it faces</param>
		public static void DropGrave(ZDO corpse, Vector3 position, Quaternion rotation)
		{
			VikingData data = VikingData.FromBytes(corpse.GetByteArray(DataKey));
			byte[] inventoryBytes = corpse.GetByteArray(InventoryKey);
			if (data == null || inventoryBytes == null)
			{
				return;
			}
			Inventory carried = new Inventory(ZombiePrefabName, null, data.Width, data.Height);
			carried.Load(new ZPackage(inventoryBytes));
			VikingItems.DropCarried(carried, position);
			SpawnGrave(position, rotation, carried, data);
		}

		/// <summary>
		/// Puts everything in an inventory but kept items in a new tombstone of the player's, the same as their own
		/// death makes, unequipped and in the same slots
		/// </summary>
		/// <param name="position">Where the tombstone goes</param>
		/// <param name="rotation">Which way it faces</param>
		/// <param name="items">The items</param>
		/// <param name="data">The viking's data, naming the player</param>
		private static void SpawnGrave(Vector3 position, Quaternion rotation, Inventory items, VikingData data)
		{
			Inventory dropped = new Inventory(ZombiePrefabName, null, data.Width, data.Height);
			foreach (ItemDrop.ItemData item in items.GetAllItems())
			{
				if (!IsKept(item))
				{
					item.m_equipped = false;
					dropped.m_inventory.Add(item);
				}
			}
			if (dropped.NrOfItems() == 0)
			{
				return;
			}
			GameObject tombstone = Game.instance.m_playerPrefab.GetComponent<Player>().m_tombstone;
			GameObject instance = Object.Instantiate(tombstone, position, rotation);
			instance.GetComponent<Container>().GetInventory().MoveInventoryToGrave(dropped);
			instance.GetComponent<TombStone>().Setup(data.Name, data.PlayerId);
		}

		/// <summary>
		/// Checks whether an item is a copy of equipment the player kept
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it's kept</returns>
		private static bool IsKept(ItemDrop.ItemData item)
		{
			return(item.m_customData.ContainsKey(KeptKey));
		}

		/// <summary>
		/// As the viking dies on its owner, drops everything a tombstone viking carries but kept items in a tombstone of
		/// the player's, everything a PC or NPC viking has, or the coins and maybe some of the gear of a viking of the
		/// world or a dvergr viking, along with the wood and loot it carried from its life in a village, on the ground -
		/// or, for a living viking that will rise, keeps everything for its zombie, which drops it in turn
		/// </summary>
		/// <param name="rises">Whether the viking's corpse will rise</param>
		public void DropGrave(bool rises)
		{
			if (!loaded)
			{
				Load();
			}
			if (!loaded)
			{
				return;
			}
			VikingItems.RemoveProps(humanoid.GetInventory());
			if (rises && IsLivingViking(humanoid))
			{
				// Its zombie takes up the shield it had equipped, and a shield sheathed on its back isn't
				if (shield != null && humanoid.GetInventory().ContainsItem(shield))
				{
					shield.m_equipped = true;
				}
				SaveInventory();
				return;
			}
			VikingItems.DropCarried(humanoid.GetInventory(), humanoid.GetCenterPoint());
			if (data.Tombstone)
			{
				SpawnGrave(humanoid.GetCenterPoint(), transform.rotation, humanoid.GetInventory(), data);
			}
			else if (IsPlayerLikeViking(humanoid))
			{
				DropEverything();
			}
			else
			{
				DropLoot();
			}
			humanoid.GetInventory().m_inventory.Clear();
		}

		/// <summary>
		/// Drops everything a PC or NPC viking that stays dead has on the ground around it - all of its gear and what's in
		/// its inventory, ammo included, and nothing else, no coins or remains
		/// </summary>
		private void DropEverything()
		{
			Vector3 position = humanoid.GetCenterPoint();
			foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(humanoid.GetInventory().GetAllItems()))
			{
				if (item.m_dropPrefab == null)
				{
					continue;
				}
				item.m_equipped = false;
				ItemDrop.DropItem(item, item.m_stack, position + Random.insideUnitSphere * 0.5f, Quaternion.identity);
			}
		}

		/// <summary>
		/// Drops the coins, remains and maybe some of the gear of a zombie viking of the world or a dvergr viking, the
		/// way its kind's settings have it (see DropWorldLoot)
		/// </summary>
		partial void DropLoot();

		/// <summary>
		/// The most of its biome's remains a viking of the world drops, before its stars
		/// </summary>
		private const int RemainsMax = 2;

		/// <summary>
		/// Drops a viking of the world's coins and remains, more for each star, and each piece of its gear by a chance,
		/// up to a most, as it is
		/// </summary>
		/// <param name="gearDropChance">The percent chance each piece of its gear drops</param>
		/// <param name="gearDropMax">The most pieces of gear it drops</param>
		private void DropWorldLoot(float gearDropChance, int gearDropMax)
		{
			Vector3 position = humanoid.GetCenterPoint();
			BiomeGear gear = VikingGear.Get(BiomeIndex());
			int level = humanoid.GetLevel();
			List<KeyValuePair<GameObject, int>> loot = new List<KeyValuePair<GameObject, int>>();
			AddLoot(loot, "Coins", Random.Range(gear.CoinsMin, gear.CoinsMax + 1) * level);
			AddLoot(loot, gear.Remains, Random.Range(0, RemainsMax + 1) * level);
			if (loot.Count > 0)
			{
				CharacterDrop.DropItems(loot, position, 0.5f, nview.GetZDO().IsCheated());
			}
			List<ItemDrop.ItemData> pieces = new List<ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
				if (type != ItemDrop.ItemData.ItemType.Ammo && type != ItemDrop.ItemData.ItemType.AmmoNonEquipable)
				{
					pieces.Add(item);
				}
			}
			int dropped = 0;
			while (pieces.Count > 0 && dropped < gearDropMax)
			{
				int index = Random.Range(0, pieces.Count);
				ItemDrop.ItemData piece = pieces[index];
				pieces.RemoveAt(index);
				if (Random.value * 100f >= gearDropChance)
				{
					continue;
				}
				piece.m_equipped = false;
				ItemDrop.DropItem(piece, piece.m_stack, position + Random.insideUnitSphere * 0.5f, Quaternion.identity);
				dropped++;
			}
		}

		/// <summary>
		/// Adds an item to a list of loot, unless there's none of it or the game has no such item
		/// </summary>
		/// <param name="loot">The loot, as each item's prefab and amount</param>
		/// <param name="prefabName">The item's prefab name, or null for none</param>
		/// <param name="amount">How many to drop</param>
		private static void AddLoot(List<KeyValuePair<GameObject, int>> loot, string prefabName, int amount)
		{
			GameObject prefab = (string.IsNullOrEmpty(prefabName) ? null : ObjectDB.instance.GetItemPrefab(prefabName));
			if (amount > 0 && prefab != null)
			{
				loot.Add(new KeyValuePair<GameObject, int>(prefab, amount));
			}
		}
	}
}
