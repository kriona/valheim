using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// Merges the unfinished stacks of an item in a chest, so the vikings' chests hold each item in as few slots as it
	/// fits in - two stacks of 30 and 25 wood becoming 50 and 5 - stacking them the way the game does, by item,
	/// quality and world level
	/// </summary>
	internal static class StackMerge
	{
		/// <summary>
		/// Merges the unfinished stacks of each item in an inventory, the largest filled first, taking out stacks left
		/// empty
		/// </summary>
		/// <param name="inventory">The inventory</param>
		/// <returns>True when anything moved</returns>
		public static bool Merge(Inventory inventory)
		{
			bool changed = false;
			HashSet<ItemDrop.ItemData> seen = new HashSet<ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.m_inventory))
			{
				int maxStack = item.m_shared.m_maxStackSize;
				if (maxStack <= 1 || seen.Contains(item))
				{
					continue;
				}
				List<ItemDrop.ItemData> group = inventory.m_inventory.FindAll((ItemDrop.ItemData other) => SameStack(other, item));
				foreach (ItemDrop.ItemData member in group)
				{
					seen.Add(member);
				}
				if (group.Count < 2)
				{
					continue;
				}
				group.Sort((ItemDrop.ItemData a, ItemDrop.ItemData b) => b.m_stack.CompareTo(a.m_stack));
				int[] counts = group.ConvertAll((ItemDrop.ItemData member) => member.m_stack).ToArray();
				int[] merged = Consolidate(counts, maxStack);
				for (int i = 0; i < group.Count; i++)
				{
					if (merged[i] == counts[i])
					{
						continue;
					}
					changed = true;
					if (merged[i] > 0)
					{
						group[i].m_stack = merged[i];
					}
					else
					{
						inventory.m_inventory.Remove(group[i]);
					}
				}
			}
			if (changed)
			{
				inventory.Changed();
			}
			return(changed);
		}

		/// <summary>
		/// Checks whether one item stacks onto another, the way the game stacks them - the same item, quality and world
		/// level
		/// </summary>
		/// <param name="stored">The item already there</param>
		/// <param name="item">The item put with it</param>
		/// <returns>True when it does</returns>
		public static bool SameStack(ItemDrop.ItemData stored, ItemDrop.ItemData item)
		{
			return(stored.m_shared.m_name == item.m_shared.m_name && stored.m_quality == item.m_quality && stored.m_worldLevel == item.m_worldLevel);
		}

		/// <summary>
		/// Works out how stacks of one item hold it once merged - the first filled up to a full stack, then the next,
		/// until all of it is held, leaving the rest empty
		/// </summary>
		/// <param name="stacks">How many each stack holds, the largest first</param>
		/// <param name="maxStack">How many make a full stack</param>
		/// <returns>How many each holds once merged, in the same order</returns>
		internal static int[] Consolidate(int[] stacks, int maxStack)
		{
			int total = 0;
			foreach (int stack in stacks)
			{
				total += stack;
			}
			int[] merged = new int[stacks.Length];
			for (int i = 0; i < stacks.Length; i++)
			{
				merged[i] = ((total < maxStack) ? total : maxStack);
				total -= merged[i];
			}
			return(merged);
		}
	}
}
