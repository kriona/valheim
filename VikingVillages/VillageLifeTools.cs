using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The tools a village keeps for its vikings to cut trees and mine with, and for its quests - AxesWanted axes, the
	/// ones its vikings carry counted, and from the Black Forest on PickaxesWanted pickaxes and an axe good enough for
	/// fine wood - crafted one at a time, the best the village has what it takes for, and put away in its chests
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// How many axes a village keeps, counting those its vikings carry, and how many pickaxes a village from the Black
		/// Forest on keeps, when it can go mining
		/// </summary>
		internal const int AxesWanted = 3;
		internal const int PickaxesWanted = 4;

		/// <summary>
		/// Seconds between a viking's looks at whether its village is short of tools
		/// </summary>
		private const float ToolCheckInterval = 20f;

		/// <summary>
		/// When the viking next looks at whether its village is short of tools, in Time.time seconds
		/// </summary>
		private float nextToolCheck;

		/// <summary>
		/// Gets how many of a kind of tool a village keeps - AxesWanted axes, and PickaxesWanted pickaxes from the Black
		/// Forest on, where there's ore to mine
		/// </summary>
		/// <param name="kind">The kind of tool</param>
		/// <param name="biome">The village's biome, by VikingGear's order</param>
		/// <returns>How many</returns>
		internal static int ToolsWanted(ToolKind kind, int biome)
		{
			if (kind == ToolKind.Axe)
			{
				return(AxesWanted);
			}
			return((kind == ToolKind.Pickaxe && biome >= ResourceBook.BlackForest) ? PickaxesWanted : 0);
		}

		/// <summary>
		/// Checks whether an item is a pickaxe - one that mines, not held for show
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		private static bool IsPickaxe(ItemDrop.ItemData item)
		{
			return(item != null && !VikingItems.IsProp(item) && item.m_shared.m_skillType == Skills.SkillType.Pickaxes && item.m_shared.m_damages.m_pickaxe > 0f);
		}

		/// <summary>
		/// Gets how many stacks of an item the village's chests keep of something the vikings don't use themselves - as
		/// many axes and pickaxes as the village keeps, each its own stack, and VillageStacksMax of anything else
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>How many stacks</returns>
		private static int KeptStacks(ItemDrop.ItemData item)
		{
			if (IsPickaxe(item))
			{
				return(PickaxesWanted);
			}
			return(IsAxe(item) ? AxesWanted : VillageStacksMax);
		}

		/// <summary>
		/// Finds a tool for the viking to craft for the village's chests, one viking arming itself or the village at a
		/// time - an axe while the village has fewer than AxesWanted, the best it has what it takes for, or else a stone
		/// axe, gathering the stone for it; from the Black Forest on, an axe good enough for fine wood when it has none,
		/// and a pickaxe while it has fewer than PickaxesWanted, each only when the village has what it takes
		/// </summary>
		/// <returns>The job, or null when the village has the tools it keeps, or can't make the ones it's short of</returns>
		private Job FindToolStockWork()
		{
			if (viking == null || Time.time < nextToolCheck || OtherDoing(ArmingKey))
			{
				return(null);
			}
			nextToolCheck = Time.time + ToolCheckInterval;
			ResourceBook book = ResourceBookGame.Get();
			int biome = VillageCooking.VillageBiome(village);
			int fineTier = FineWoodTier(book);
			CountTools(fineTier, out int axes, out bool fineAxe, out int pickaxes);
			if (axes < ToolsWanted(ToolKind.Axe, biome))
			{
				Job axe = ToolCraftWork(book, ToolKind.Axe, 0, "Crafting an axe for the chests");
				return(axe ?? CraftWeaponWork(StoneAxePrefab, "Crafting an axe for the chests", true));
			}
			if (biome >= ResourceBook.BlackForest && !fineAxe)
			{
				Job fine = ToolCraftWork(book, ToolKind.Axe, fineTier, "Crafting an axe for fine wood");
				if (fine != null)
				{
					return(fine);
				}
			}
			if (pickaxes < ToolsWanted(ToolKind.Pickaxe, biome))
			{
				return(ToolCraftWork(book, ToolKind.Pickaxe, 0, "Crafting a pickaxe for the chests"));
			}
			return(null);
		}

		/// <summary>
		/// Gets the tool tier an axe needs to cut the trees fine wood comes from (see ResourceBookGame)
		/// </summary>
		/// <param name="book">Every way to get each item, or null</param>
		/// <returns>The tier, or int.MaxValue when it isn't known, so no axe counts as good enough</returns>
		private static int FineWoodTier(ResourceBook book)
		{
			ResourceSource source = ((book != null) ? book.SourcesOf("FineWood").Find((ResourceSource candidate) => candidate.Tool == ToolKind.Axe) : null);
			return((source != null) ? source.ToolTier : int.MaxValue);
		}

		/// <summary>
		/// Counts the village's axes and pickaxes - in its chests and carried by its vikings, as their own or to put
		/// away - and whether one of its axes is good enough for fine wood
		/// </summary>
		/// <param name="fineTier">The tool tier an axe needs for fine wood</param>
		/// <param name="axes">Set to how many axes</param>
		/// <param name="fineAxe">Set to whether one is good enough for fine wood</param>
		/// <param name="pickaxes">Set to how many pickaxes</param>
		private void CountTools(int fineTier, out int axes, out bool fineAxe, out int pickaxes)
		{
			List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
			foreach (Container chest in UsableChests())
			{
				items.AddRange(chest.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => !VikingItems.IsCarried(item)));
			}
			foreach (Character character in village.Vikings())
			{
				Humanoid other = character as Humanoid;
				if (other != null)
				{
					items.AddRange(other.GetInventory().GetAllItems());
				}
			}
			axes = 0;
			fineAxe = false;
			pickaxes = 0;
			foreach (ItemDrop.ItemData item in items)
			{
				if (IsPickaxe(item))
				{
					pickaxes++;
				}
				else if (IsAxe(item))
				{
					axes++;
					fineAxe |= (item.m_shared.m_toolTier >= fineTier);
				}
			}
		}

		/// <summary>
		/// Finds the job crafting the best tool of a kind at a tier or above that the village has everything but wood
		/// for, to put away in a chest - never a battleaxe
		/// </summary>
		/// <param name="book">Every way to get each item, with the game's tools, or null</param>
		/// <param name="kind">The kind of tool</param>
		/// <param name="minTier">The lowest tier it may be</param>
		/// <param name="label">What the job shows as in the debug hover text</param>
		/// <returns>The job, or null when the village can make none</returns>
		private Job ToolCraftWork(ResourceBook book, ToolKind kind, int minTier, string label)
		{
			if (book == null)
			{
				return(null);
			}
			for (int i = book.Tools.Count - 1; i >= 0; i--)
			{
				ToolEntry tool = book.Tools[i];
				ItemDrop.ItemData item = ((tool.Kind == kind && tool.Tier >= minTier) ? ItemOf(tool.Prefab) : null);
				// A battleaxe chops too, but it's a weapon taking both hands rather than a tool to keep in a chest
				bool battleaxe = (item != null && kind == ToolKind.Axe && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon);
				if (item == null || battleaxe || PlanCraft(tool.Prefab, out string _) == null)
				{
					continue;
				}
				Job job = CraftWeaponWork(tool.Prefab, label, true);
				if (job != null)
				{
					return(job);
				}
			}
			return(null);
		}
	}
}
