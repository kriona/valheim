using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a viking carries - the wood and loot in its inventory, how much more it has room for by count and weight,
	/// and taking it into its inventory
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Checks whether an item in a viking's inventory is one it carries for a purpose
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="purpose">CarryWood or CarryLoot</param>
		/// <returns>True when it carries it for that</returns>
		private static bool IsCarriedFor(ItemDrop.ItemData item, string purpose)
		{
			return(item != null && item.m_customData.TryGetValue(VikingItems.CarryKey, out string carried) && carried == purpose);
		}

		/// <summary>
		/// Gets how much wood the viking carries, of every kind
		/// </summary>
		/// <returns>The wood</returns>
		private int CarriedWood()
		{
			int wood = 0;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (IsCarriedFor(item, CarryWood))
				{
					wood += item.m_stack;
				}
			}
			return(wood);
		}

		/// <summary>
		/// Works out how much more wood of a kind the viking can carry - up to CarryMax in all, and as much as its
		/// inventory has room for
		/// </summary>
		/// <param name="wood">The kind of wood</param>
		/// <returns>How much it can take</returns>
		private int WoodRoom(ItemDrop.ItemData wood)
		{
			return(Mathf.Min(CarryMax - CarriedWood(), CarryRoom(wood, CarryWood)));
		}

		/// <summary>
		/// Checks whether the viking can carry any more wood
		/// </summary>
		/// <returns>True when it's below CarryMax and its inventory has room</returns>
		private bool CanCarryWood()
		{
			ItemDrop.ItemData plain = ItemOf("Wood");
			return(plain != null && WoodRoom(plain) > 0);
		}

		/// <summary>
		/// Adds wood the viking gathered to what it carries
		/// </summary>
		/// <param name="wood">The kind of wood, whose stack is ignored</param>
		/// <param name="amount">How much</param>
		/// <returns>How much it took, up to WoodRoom</returns>
		private int AddWood(ItemDrop.ItemData wood, int amount)
		{
			return(Carry(wood, Mathf.Min(amount, WoodRoom(wood)), CarryWood));
		}

		/// <summary>
		/// Uses up wood the viking carries, of any kind
		/// </summary>
		/// <param name="amount">How much</param>
		private void UseWood(int amount)
		{
			Inventory inventory = humanoid.GetInventory();
			foreach (ItemDrop.ItemData item in inventory.m_inventory.FindAll((ItemDrop.ItemData carried) => IsCarriedFor(carried, CarryWood)))
			{
				if (amount <= 0)
				{
					break;
				}
				int used = Mathf.Min(amount, item.m_stack);
				inventory.RemoveItem(item, used);
				amount -= used;
			}
		}

		/// <summary>
		/// Finds the loot the viking carries for a chest
		/// </summary>
		/// <returns>The loot, or null when it carries none</returns>
		private ItemDrop.ItemData CarriedLoot()
		{
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => IsCarriedFor(item, CarryLoot)));
		}

		/// <summary>
		/// Works out how many of an item the viking's inventory has room for, carried for a purpose - on its carried
		/// stacks of the item and in its empty slots, never on its own items
		/// </summary>
		/// <param name="template">The item</param>
		/// <param name="purpose">CarryWood or CarryLoot</param>
		/// <returns>How many it has room for</returns>
		private int CarryRoom(ItemDrop.ItemData template, string purpose)
		{
			Inventory inventory = humanoid.GetInventory();
			int maxStack = Mathf.Max(1, template.m_shared.m_maxStackSize);
			int room = (inventory.m_width * inventory.m_height - inventory.m_inventory.Count) * maxStack;
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (StacksWith(item, template, purpose))
				{
					room += Mathf.Max(0, maxStack - item.m_stack);
				}
			}
			return(Mathf.Min(room, WeightRoom(template)));
		}

		/// <summary>
		/// Works out how many of an item the viking can carry before it's as laden as a player can be - its whole
		/// inventory counts, what it wears included, the way a player's does
		/// </summary>
		/// <param name="template">The item</param>
		/// <returns>How many</returns>
		private int WeightRoom(ItemDrop.ItemData template)
		{
			float each = template.m_shared.m_weight;
			if (each <= 0f)
			{
				return(int.MaxValue);
			}
			float left = body.GetMaxCarryWeight() - humanoid.GetInventory().GetTotalWeight();
			return(Mathf.Max(0, Mathf.FloorToInt(left / each)));
		}

		/// <summary>
		/// Checks whether more of an item can go on a stack the viking carries
		/// </summary>
		/// <param name="item">The stack in its inventory</param>
		/// <param name="template">The item</param>
		/// <param name="purpose">CarryWood or CarryLoot</param>
		/// <returns>True when it's a carried stack of the same item</returns>
		private static bool StacksWith(ItemDrop.ItemData item, ItemDrop.ItemData template, string purpose)
		{
			return(IsCarriedFor(item, purpose) && item.m_shared.m_name == template.m_shared.m_name && item.m_quality == template.m_quality && item.m_variant == template.m_variant && item.m_worldLevel == template.m_worldLevel);
		}

		/// <summary>
		/// Puts items in the viking's inventory as carried for a purpose - onto its carried stacks of the item first,
		/// then into empty slots, never onto its own items
		/// </summary>
		/// <param name="template">The item, whose stack is ignored</param>
		/// <param name="amount">How many</param>
		/// <param name="purpose">CarryWood or CarryLoot</param>
		/// <returns>How many it had room for</returns>
		private int Carry(ItemDrop.ItemData template, int amount, string purpose)
		{
			if (amount <= 0 || template == null || template.m_dropPrefab == null)
			{
				return(0);
			}
			Inventory inventory = humanoid.GetInventory();
			int maxStack = Mathf.Max(1, template.m_shared.m_maxStackSize);
			amount = Mathf.Min(amount, WeightRoom(template));
			int placed = 0;
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (placed >= amount)
				{
					break;
				}
				if (StacksWith(item, template, purpose) && item.m_stack < maxStack)
				{
					int added = Mathf.Min(amount - placed, maxStack - item.m_stack);
					item.m_stack += added;
					placed += added;
				}
			}
			while (placed < amount)
			{
				Vector2i slot = inventory.FindEmptySlot(false);
				if (slot.x < 0)
				{
					break;
				}
				ItemDrop.ItemData copy = template.Clone();
				copy.m_stack = Mathf.Min(amount - placed, maxStack);
				copy.m_gridPos = slot;
				copy.m_equipped = false;
				copy.m_customData[VikingItems.CarryKey] = purpose;
				inventory.m_inventory.Add(copy);
				placed += copy.m_stack;
			}
			if (placed > 0)
			{
				inventory.Changed();
			}
			return(placed);
		}

		/// <summary>
		/// Makes an item of a prefab to carry, the way the game adds one to an inventory
		/// </summary>
		/// <param name="prefabName">The item's prefab</param>
		/// <returns>The item, or null when there's no such prefab</returns>
		private static ItemDrop.ItemData ItemOf(string prefabName)
		{
			GameObject prefab = ((ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(prefabName) : null);
			ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
			if (drop == null)
			{
				return(null);
			}
			ItemDrop.ItemData item = drop.m_itemData.Clone();
			item.m_dropPrefab = prefab;
			item.m_stack = 1;
			item.m_worldLevel = (byte)Game.m_worldLevel;
			return(item);
		}

		/// <summary>
		/// Gets the item lying on the ground as an item to carry, with its prefab set
		/// </summary>
		/// <param name="drop">The item on the ground</param>
		/// <returns>The item, or null when its prefab is unknown</returns>
		private static ItemDrop.ItemData ItemOf(ItemDrop drop)
		{
			ItemDrop.ItemData item = drop.m_itemData;
			if (item.m_dropPrefab == null && ObjectDB.instance != null)
			{
				item.m_dropPrefab = ObjectDB.instance.GetItemPrefab(Utils.GetPrefabName(drop.gameObject));
			}
			return((item.m_dropPrefab != null) ? item : null);
		}
	}
}
