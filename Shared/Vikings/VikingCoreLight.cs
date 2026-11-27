using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// Lighting a viking's way - the torch it burns and the circlet it swaps its helmet for in the dark
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Has a PC or dvergr viking pull out a torch when it's dark while it isn't fighting, or while it fights with
		/// a one-handed weapon and nothing in its other hand, and put it away otherwise - in its village from just before
		/// sunset until dawn while it's away from the fire and not resting, and elsewhere at night - and always by day
		/// </summary>
		/// <remarks>
		/// The torch goes in the hand beside a one-handed weapon, or in place of another weapon. When the viking spots an
		/// enemy its AI wields its weapon again, which puts the torch away for a shield or a weapon that takes both
		/// hands, and keeps it beside a one-handed weapon with no shield - so a viking woken to fight pulls one out
		/// beside a one-handed weapon too. It's a torch of its own, which burns down while it's held (see BurnTorch) - a
		/// viking with none goes without, and crafts another (see VillageLife.FindTorchWork). A viking with a dverger
		/// circlet of its own wears that in place of its helmet instead, and never pulls out a torch (see WearCirclet)
		/// </remarks>
		public void UpdateTorch()
		{
			if (!IsPCOrDvergr(humanoid) || ai == null || GearHeld())
			{
				return;
			}
			ItemDrop.ItemData torch = FindTorch();
			bool night = ((life != null && life.IsActive()) ? life.WantsTorch() : EnvMan.IsNight());
			ItemDrop.ItemData circlet = FindCirclet(null);
			if (circlet != null)
			{
				if (torch != null && humanoid.IsItemEquiped(torch))
				{
					humanoid.UnequipItem(torch, false);
					if (InDanger())
					{
						EquipWeapon();
					}
				}
				WearCirclet(circlet, night && !InDanger());
				return;
			}
			bool fighting = (ai.IsAlerted() || ai.GetTargetCreature() != null);
			ItemDrop.ItemData right = humanoid.m_rightItem;
			bool handFree = (right != null && right.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon && humanoid.m_leftItem == null);
			if (night && (!fighting || handFree))
			{
				if (torch != null && !humanoid.IsItemEquiped(torch))
				{
					humanoid.EquipItem(torch, false);
				}
			}
			else if (!night && torch != null && humanoid.IsItemEquiped(torch))
			{
				humanoid.UnequipItem(torch, false);
				// With no danger the weapon stays sheathed on its back
				if (InDanger())
				{
					EquipWeapon();
				}
			}
		}

		/// <summary>
		/// Finds the viking's own torch that's burnt down the most, so it uses that one up first
		/// </summary>
		/// <returns>The torch, or null when it has none</returns>
		private ItemDrop.ItemData FindTorch()
		{
			ItemDrop.ItemData found = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && !VikingItems.IsCarried(item) && (found == null || item.m_durability < found.m_durability))
				{
					found = item;
				}
			}
			return(found);
		}

		/// <summary>
		/// Checks whether an item is a dverger circlet, which a PC viking wears for light in place of a torch
		/// </summary>
		/// <param name="item">The item, or null</param>
		/// <returns>True when it is</returns>
		public static bool IsCirclet(ItemDrop.ItemData item)
		{
			return(item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == VikingGear.CircletPrefab);
		}

		/// <summary>
		/// Finds the viking's own dverger circlet - not one it carries for a chest - the one it wears when it has several
		/// </summary>
		/// <param name="except">A circlet to leave out, like one being weighed up, or null</param>
		/// <returns>The circlet, or null when it has none</returns>
		private ItemDrop.ItemData FindCirclet(ItemDrop.ItemData except)
		{
			if (IsCirclet(humanoid.m_helmetItem) && humanoid.m_helmetItem != except)
			{
				return(humanoid.m_helmetItem);
			}
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => item != except && IsCirclet(item) && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item)));
		}

		/// <summary>
		/// Checks whether the viking has a dverger circlet of its own, which lights its way so it never needs a torch
		/// </summary>
		/// <returns>True when it has</returns>
		public bool HasCirclet()
		{
			return(FindCirclet(null) != null);
		}

		/// <summary>
		/// Finds the helmet of its own with the most armor that the viking wears when its dverger circlet is off - not one
		/// it carries for a chest
		/// </summary>
		/// <param name="except">A helmet to leave out, like one being weighed against it, or null</param>
		/// <returns>The helmet, or null when it has none</returns>
		private ItemDrop.ItemData OwnHelmet(ItemDrop.ItemData except)
		{
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item == except || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Helmet || IsCirclet(item) || VikingItems.IsCarried(item) || VikingItems.IsProp(item))
				{
					continue;
				}
				if (best == null || item.GetArmor() > best.GetArmor())
				{
					best = item;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether the viking puts an item on and takes it off as it needs light, rather than wearing whatever is
		/// best - its dverger circlet, and any helmet while the circlet is on in its place (see WearCirclet)
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it does</returns>
		public bool SwapsForLight(ItemDrop.ItemData item)
		{
			if (item == null)
			{
				return(false);
			}
			return(IsCirclet(item) || (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet && IsCirclet(humanoid.m_helmetItem)));
		}

		/// <summary>
		/// Puts the viking's dverger circlet on in place of its helmet while it wants light, or takes it off and puts its
		/// helmet back on (see OwnHelmet)
		/// </summary>
		/// <param name="circlet">The circlet</param>
		/// <param name="lit">True to wear the circlet</param>
		private void WearCirclet(ItemDrop.ItemData circlet, bool lit)
		{
			if (lit)
			{
				if (!humanoid.IsItemEquiped(circlet))
				{
					humanoid.EquipItem(circlet, false);
				}
				return;
			}
			if (!humanoid.IsItemEquiped(circlet))
			{
				return;
			}
			humanoid.UnequipItem(circlet, false);
			ItemDrop.ItemData helmet = OwnHelmet(null);
			if (helmet != null)
			{
				humanoid.EquipItem(helmet, false);
			}
		}

		/// <summary>
		/// Has a PC or dvergr viking wearing its dverger circlet put its helmet back on as soon as it's in danger,
		/// rather than at its next UpdateTorch
		/// </summary>
		private void UpdateCircletDanger()
		{
			if (!IsCirclet(humanoid.m_helmetItem) || !IsPCOrDvergr(humanoid) || ai == null || GearHeld() || !InDanger())
			{
				return;
			}
			WearCirclet(humanoid.m_helmetItem, false);
		}

		/// <summary>
		/// Burns down the torch a PC or dvergr viking holds the way a player's does, since the game only wears down
		/// a player's equipment - putting it away and dropping it from its inventory once it's burnt out
		/// </summary>
		private void BurnTorch()
		{
			if (!IsPCOrDvergr(humanoid))
			{
				return;
			}
			ItemDrop.ItemData torch = ((humanoid.m_rightItem != null && humanoid.m_rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch) ? humanoid.m_rightItem : humanoid.m_leftItem);
			if (torch == null || torch.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Torch || !torch.m_shared.m_useDurability)
			{
				return;
			}
			humanoid.DrainEquipedItemDurability(torch, Time.deltaTime);
		}
	}
}
