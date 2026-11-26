using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Lightening a viking's load when it carries more than it can (see UnburdenJob), and shedding it before a fight
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// How far a viking carrying more than it can walks to put things away in a chest - with none that near, it
		/// drops them where it stands - in meters
		/// </summary>
		private const float UnburdenReach = 20f;

		/// <summary>
		/// Finds the heaviest thing the viking can do without - anything it carries for the village, or else something
		/// of its own it doesn't hang on to (see VikingCore.Keeps)
		/// </summary>
		/// <returns>The item, or null when there's nothing it can do without</returns>
		private ItemDrop.ItemData HeaviestSpare()
		{
			ItemDrop.ItemData heaviest = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (VikingItems.IsProp(item) || body.GearQueued(item) || item.m_shared.m_weight <= 0f)
				{
					continue;
				}
				if (!VikingItems.IsCarried(item) && (viking == null || viking.Keeps(item)))
				{
					continue;
				}
				if (heaviest == null || item.m_shared.m_weight * item.m_stack > heaviest.m_shared.m_weight * heaviest.m_stack)
				{
					heaviest = item;
				}
			}
			return(heaviest);
		}

		/// <summary>
		/// Works out how many of an item the viking has to shed to carry no more than it can
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>How many, at least one and at most its stack</returns>
		private int OverBy(ItemDrop.ItemData item)
		{
			float over = humanoid.GetInventory().GetTotalWeight() - body.GetMaxCarryWeight();
			return(Mathf.Clamp(Mathf.CeilToInt(over / Mathf.Max(0.01f, item.m_shared.m_weight)), 1, item.m_stack));
		}

		/// <summary>
		/// Finds the nearest of the village's chests within UnburdenReach of the viking with room for an item
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The chest, or null when there's none</returns>
		private Container UnburdenChest(ItemDrop.ItemData item)
		{
			return(Nearest(UsableChests().FindAll((Container chest) => !Avoided(chest.m_nview.GetZDO().m_uid) && Utils.DistanceXZ(chest.transform.position, transform.position) <= UnburdenReach && chest.GetInventory().CanAddItem(item, 1))));
		}

		/// <summary>
		/// Makes a copy of as many of an item as the viking has to shed, as its own rather than carried
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The copy</returns>
		private ItemDrop.ItemData SpareCopy(ItemDrop.ItemData item)
		{
			ItemDrop.ItemData copy = item.Clone();
			copy.m_stack = OverBy(item);
			copy.m_equipped = false;
			copy.m_customData.Remove(VikingItems.CarryKey);
			return(copy);
		}

		/// <summary>
		/// Puts as many of an item as the viking has to shed in a chest it has reached
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="item">The item</param>
		/// <returns>False when the chest couldn't take them</returns>
		private bool StoreSpare(Container chest, ItemDrop.ItemData item)
		{
			if (!OpenChest(chest))
			{
				return(false);
			}
			ItemDrop.ItemData copy = SpareCopy(item);
			int amount = copy.m_stack;
			if (!chest.GetInventory().AddItem(copy))
			{
				return(false);
			}
			humanoid.GetInventory().RemoveItem(item, amount);
			chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			return(true);
		}

		/// <summary>
		/// Drops what the viking can do without where it stands, the heaviest first, until it's no longer held back by
		/// its load - in a fight, or running from one, when there's no time to put it in a chest
		/// </summary>
		private void ShedForFight()
		{
			ItemDrop.ItemData spare = HeaviestSpare();
			// At most a drop for each slot, so a stack that won't come out of the inventory can't hold it here
			int drops = 0;
			Inventory inventory = humanoid.GetInventory();
			while (spare != null && drops < inventory.m_width * inventory.m_height && inventory.GetTotalWeight() > body.GetMaxCarryWeight())
			{
				DropSpare(spare);
				drops++;
				spare = HeaviestSpare();
			}
			body.UpdateEncumbered();
		}

		/// <summary>
		/// Drops as many of an item as the viking has to shed where it stands, for a fellow with room to carry off
		/// </summary>
		/// <param name="item">The item</param>
		private void DropSpare(ItemDrop.ItemData item)
		{
			ItemDrop.ItemData copy = SpareCopy(item);
			int amount = copy.m_stack;
			humanoid.GetInventory().RemoveItem(item, amount);
			Plugin.Log.LogInfo(LogName() + " put down " + ((amount > 1) ? amount + " " : "") + Localization.instance.Localize(copy.m_shared.m_name) + " - too heavily loaded for a fight");
			ItemDrop.DropItem(copy, amount, transform.position + transform.forward * 0.6f + Vector3.up * 0.5f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
		}

		/// <summary>
		/// Lightening its load when it carries more than it can - the heaviest thing it can do without first (see
		/// HeaviestSpare), put away in the nearest chest with room within UnburdenReach, or else dropped where it stands,
		/// as many as it has to shed, until it's no longer held back
		/// </summary>
		private class UnburdenJob : Job
		{
			private ItemDrop.ItemData item;
			private Container chest;
			private float actAt = -1f;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public UnburdenJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
			public override string Describe()
			{
				return("Lightening the load");
			}

			/// <summary>
			/// Puts away or drops what it can do without, a thing at a time
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it carries no more than it can, or has nothing left it can do without</returns>
			public override bool Tick(float dt)
			{
				Inventory inventory = life.humanoid.GetInventory();
				if (inventory.GetTotalWeight() <= life.body.GetMaxCarryWeight())
				{
					return(true);
				}
				if (item == null || !inventory.ContainsItem(item))
				{
					item = life.HeaviestSpare();
					if (item == null)
					{
						return(true);
					}
					chest = life.UnburdenChest(item);
					actAt = -1f;
				}
				if (chest == null || !life.Usable(chest))
				{
					life.DropSpare(item);
					item = null;
					return(false);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				// A chest that couldn't take it after all leaves it to be dropped
				if (!life.StoreSpare(chest, item))
				{
					chest = null;
					return(false);
				}
				item = null;
				return(false);
			}
		}
	}
}
