using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// The marks on items in a viking's inventory that aren't its own - what a PC viking carries for its village and
	/// what it only holds for show - which a zombie viking risen from it still has
	/// </summary>
	/// <remarks>
	/// The marks are in each item's custom data, so they're saved with the inventory, and their keys can't change, since
	/// Viking Villages writes them and Zombie Apocalypse reads them, whichever versions of the two are installed
	/// </remarks>
	internal static class VikingItems
	{
		/// <summary>
		/// Custom data on an item in a PC viking's inventory that it carries rather than owns - wood for repairing,
		/// rebuilding and stacking the fire, loot it's taking to a chest, or what it crafts arrows from besides wood -
		/// which it drops when it dies, holding what it carries it for
		/// </summary>
		public const string CarryKey = "kriona.Vikings.carried";

		/// <summary>
		/// Custom data on an item the viking only holds for show, like the hammer it repairs with, which is never kept
		/// </summary>
		public const string PropKey = "kriona.Vikings.prop";

		/// <summary>
		/// Checks whether an item in a viking's inventory is one it carries rather than owns - wood for its work or loot
		/// for a chest - which it never wields
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it carries it</returns>
		public static bool IsCarried(ItemDrop.ItemData item)
		{
			return(item != null && item.m_customData.ContainsKey(CarryKey));
		}

		/// <summary>
		/// Makes an item in a viking's inventory its own rather than something it carries for a purpose
		/// </summary>
		/// <param name="item">The item</param>
		public static void Keep(ItemDrop.ItemData item)
		{
			if (item != null)
			{
				item.m_customData.Remove(CarryKey);
			}
		}

		/// <summary>
		/// Drops everything a viking carries on the ground around a point, taking it out of its inventory - as it dies
		/// </summary>
		/// <param name="inventory">The viking's inventory</param>
		/// <param name="position">Where it drops them</param>
		public static void DropCarried(Inventory inventory, Vector3 position)
		{
			List<ItemDrop.ItemData> carried = inventory.m_inventory.FindAll((ItemDrop.ItemData held) => IsCarried(held));
			foreach (ItemDrop.ItemData item in carried)
			{
				inventory.m_inventory.Remove(item);
				item.m_customData.Remove(CarryKey);
				if (item.m_dropPrefab != null)
				{
					Vector3 spot = position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f;
					ItemDrop.DropItem(item, item.m_stack, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
				}
			}
			if (carried.Count > 0)
			{
				inventory.Changed();
			}
		}

		/// <summary>
		/// Checks whether an item is only held for show
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it's a prop</returns>
		public static bool IsProp(ItemDrop.ItemData item)
		{
			return(item != null && item.m_customData.ContainsKey(PropKey));
		}

		/// <summary>
		/// Takes every prop out of an inventory
		/// </summary>
		/// <param name="inventory">The inventory</param>
		public static void RemoveProps(Inventory inventory)
		{
			inventory.m_inventory.RemoveAll((ItemDrop.ItemData item) => IsProp(item));
		}
	}
}
