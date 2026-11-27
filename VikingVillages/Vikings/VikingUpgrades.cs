using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a PC viking can start with beyond its gear, more often the harder its biome - a strength belt
	/// (Megingjord), and the wider and deeper pockets Haldor sells, each a row more in its inventory, the way they are
	/// for a player - which its village buys from Haldor for a viking without them (see VillageQuests.PurchaseChoice)
	/// </summary>
	/// <remarks>
	/// A viking's rows are its VikingData's Height, so they're saved with it and kept by a zombie viking risen from it.
	/// Wider pockets are its fifth row and deeper pockets its sixth, so a viking has deeper pockets only along with wider
	/// ones
	/// </remarks>
	internal static class VikingUpgrades
	{
		/// <summary>
		/// The strength belt's prefab name
		/// </summary>
		public const string BeltPrefab = "BeltStrength";

		/// <summary>
		/// The rows of a viking's inventory with no pockets bought, with wider pockets, and with deeper pockets as well
		/// </summary>
		public const int PlainRows = Humanoid.DefaultInventoryHeight;
		public const int WiderRows = PlainRows + 1;
		public const int DeeperRows = PlainRows + 2;

		/// <summary>
		/// The chance that a PC viking starts with a strength belt, wider pockets and deeper pockets, by its biome's
		/// index (see VikingGear.Biomes) - from the Meadows to the Deep North
		/// </summary>
		internal static readonly float[] BeltChances = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f, 1f, 1f };
		internal static readonly float[] WiderPocketsChances = { 0f, 0f, 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f };
		internal static readonly float[] DeeperPocketsChances = { 0f, 0f, 0f, 0f, 0f, 0.2f, 0.4f, 0.6f };

		/// <summary>
		/// Gets a biome's chance from one of the tables, an index past either end taking the nearest biome's
		/// </summary>
		/// <param name="chances">The table</param>
		/// <param name="index">The biome's index</param>
		/// <returns>The chance, from 0 to 1</returns>
		internal static float ChanceOf(float[] chances, int index)
		{
			return(chances[Mathf.Clamp(index, 0, chances.Length - 1)]);
		}

		/// <summary>
		/// Checks whether a PC viking of a biome starts with a strength belt, by BeltChances
		/// </summary>
		/// <param name="index">The index of its biome</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>True for a belt</returns>
		internal static bool PCBelt(int index, float roll)
		{
			return(roll < ChanceOf(BeltChances, index));
		}

		/// <summary>
		/// Works out how many rows a PC viking of a biome starts with - deeper pockets by DeeperPocketsChances, and
		/// otherwise wider pockets by WiderPocketsChances. Both come from the one roll, and deeper pockets are never likelier
		/// than wider ones, so each table's chance holds on its own while deeper pockets only come with wider ones
		/// </summary>
		/// <param name="index">The index of its biome</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>Its rows</returns>
		internal static int PCRows(int index, float roll)
		{
			if (roll < ChanceOf(DeeperPocketsChances, index))
			{
				return(DeeperRows);
			}
			return((roll < ChanceOf(WiderPocketsChances, index)) ? WiderRows : PlainRows);
		}

		/// <summary>
		/// Checks whether a viking with so many rows has what a quest to Haldor buys - wider pockets, or deeper pockets
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="rows">The viking's rows</param>
		/// <returns>True when it has them, false for any other quest</returns>
		internal static bool HasPockets(QuestKind kind, int rows)
		{
			switch (kind)
			{
				case QuestKind.BuyWiderPockets:
					return(rows >= WiderRows);
				case QuestKind.BuyDeeperPockets:
					return(rows >= DeeperRows);
				default:
					return(false);
			}
		}

		/// <summary>
		/// Checks whether a viking with so many rows can be sewn the pockets a quest to Haldor buys - wider pockets with
		/// none yet, deeper pockets with wider ones and no deeper
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="rows">The viking's rows</param>
		/// <returns>True when it can</returns>
		internal static bool CanTakePockets(QuestKind kind, int rows)
		{
			switch (kind)
			{
				case QuestKind.BuyWiderPockets:
					return(rows < WiderRows);
				case QuestKind.BuyDeeperPockets:
					return(rows == WiderRows);
				default:
					return(false);
			}
		}

		/// <summary>
		/// Works out how much more a viking carries on a quest by wearing a belt it only carries at home - true of a
		/// Mistlands viking, who wears its wisp light in the utility slot against the mist and keeps its strength belt
		/// in its pack, and puts the belt on for a quest that goes outside the Mistlands
		/// </summary>
		/// <param name="wornWisp">Whether it wears a wisp light</param>
		/// <param name="leavesMist">Whether the quest goes to a biome other than the Mistlands</param>
		/// <param name="wornAdds">What its worn utility item adds to the weight it carries</param>
		/// <param name="carriedAdds">The most an unworn utility item of its own adds</param>
		/// <returns>The weight added, before the world's carry weight rate</returns>
		internal static float QuestCarryBonus(bool wornWisp, bool leavesMist, float wornAdds, float carriedAdds)
		{
			return((wornWisp && leavesMist) ? Mathf.Max(0f, carriedAdds - wornAdds) : 0f);
		}

		/// <summary>
		/// Works out how much more a viking carries on a quest going to a biome by wearing the belt it carries in place
		/// of its wisp light (see QuestCarryBonus), scaled by the world's carry weight rate the way GetMaxCarryWeight is
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <param name="questBiome">The biome the quest goes to, by VikingGear's order</param>
		/// <returns>The weight added</returns>
		internal static float QuestCarryBonus(Humanoid humanoid, int questBiome)
		{
			ItemDrop.ItemData worn = humanoid.m_utilityItem;
			bool wornWisp = (worn != null && worn.m_dropPrefab != null && worn.m_dropPrefab.name == VikingGear.WispPrefab);
			bool leavesMist = (questBiome != VikingGear.IndexOf(Heightmap.Biome.Mistlands));
			if (!wornWisp || !leavesMist)
			{
				return(0f);
			}
			float carried = 0f;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item != worn && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item))
				{
					carried = Mathf.Max(carried, CarryAdds(item));
				}
			}
			return(QuestCarryBonus(wornWisp, leavesMist, CarryAdds(worn), carried) * Game.m_carryWeightRate);
		}

		/// <summary>
		/// Gets what a utility item adds to the weight its wearer carries, through the status effect it gives when worn
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The weight, 0 for one that adds none</returns>
		private static float CarryAdds(ItemDrop.ItemData item)
		{
			SE_Stats stats = ((item != null) ? item.m_shared.m_equipStatusEffect as SE_Stats : null);
			return((stats != null) ? stats.m_addMaxCarryWeight : 0f);
		}
	}
}
