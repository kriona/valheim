using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// A viking's gear - taking up the best armor, shield and weapons it has, wearing and drawing them, and sheathing
	/// its weapon when all is quiet
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Checks whether a PC viking leaves its gear as it is, while a player has its inventory open (see
		/// VikingInventory)
		/// </summary>
		/// <returns>True while it does</returns>
		private bool GearHeld()
		{
			return(life != null && life.GearHeld());
		}

		/// <summary>
		/// Wields the viking's weapon - what the player was wielding while it still works, or else its best one, or its
		/// fists when it has none left - with the player's shield beside a one-handed weapon, unless it's leaving its
		/// gear as it is (see GearHeld)
		/// </summary>
		/// <param name="force">True to wield it even while the viking leaves its gear as it is, for gear a player had
		/// it put on</param>
		public void EquipWeapon(bool force = false)
		{
			if (GetData() == null || (GearHeld() && !force))
			{
				return;
			}
			ItemDrop.ItemData choice = ChooseWeapon();
			ItemDrop.ItemData current = CurrentWeapon();
			if (choice != current)
			{
				if (choice != null)
				{
					humanoid.EquipItem(choice, false);
				}
				else
				{
					if (humanoid.m_rightItem != null && humanoid.m_rightItem.IsWeapon())
					{
						humanoid.UnequipItem(humanoid.m_rightItem, false);
					}
					if (humanoid.m_leftItem != null && humanoid.m_leftItem.IsWeapon())
					{
						humanoid.UnequipItem(humanoid.m_leftItem, false);
					}
				}
			}
			bool oneHanded = (choice == null || choice.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon);
			bool shieldWorks = (shield != null && (!shield.m_shared.m_useDurability || shield.m_durability > 0f));
			if (shieldWorks && oneHanded && !humanoid.IsItemEquiped(shield) && humanoid.GetInventory().ContainsItem(shield))
			{
				humanoid.EquipItem(shield, false);
			}
			ReadyAmmo(choice);
		}

		/// <summary>
		/// Readies the ammo a bow or crossbow hurts the creature the viking is fighting most with (see BestAmmo), so its
		/// next shot is that
		/// </summary>
		/// <param name="weapon">The weapon in its hands, or null</param>
		private void ReadyAmmo(ItemDrop.ItemData weapon)
		{
			if (!IsRanged(weapon))
			{
				return;
			}
			Character target = ((ai != null) ? ai.GetTargetCreature() : null);
			ItemDrop.ItemData best = BestAmmo(weapon, Resistances(target));
			if (best != null && best != humanoid.GetAmmoItem())
			{
				humanoid.EquipItem(best, false);
			}
		}

		/// <summary>
		/// Has a PC viking sheath its weapon and shield on its back while there's no danger, and draw them again as
		/// soon as there is - a torch or a hammer it holds stays in its hands
		/// </summary>
		/// <remarks>
		/// The weapon and shield shown on its back are the ones it would draw, noted as the hands' hidden items the way
		/// a player's sheathed weapons are. Equipping a torch or hammer forgets the hidden items, so they're noted again
		/// whenever they've changed
		/// </remarks>
		/// <param name="force">True to sheath it even while the viking leaves its gear as it is, for gear a player had it
		/// put on</param>
		public void UpdateSheath(bool force = false)
		{
			if (ai == null || !IsPCViking(humanoid) || GetData() == null || humanoid.IsDead() || (GearHeld() && !force))
			{
				return;
			}
			if (InDanger())
			{
				if (sheathed)
				{
					sheathed = false;
					humanoid.m_hiddenLeftItem = null;
					humanoid.m_hiddenRightItem = null;
					EquipWeapon();
					humanoid.SetupVisEquipment(humanoid.m_visEquipment, false);
					humanoid.m_zanim.SetTrigger("equip_hip");
				}
				return;
			}
			bool putAway = false;
			foreach (ItemDrop.ItemData held in new ItemDrop.ItemData[] { humanoid.m_rightItem, humanoid.m_leftItem })
			{
				if (IsArmament(held))
				{
					humanoid.UnequipItem(held, false);
					putAway = true;
				}
			}
			ItemDrop.ItemData weapon = ChooseWeapon();
			bool leftHanded = (weapon != null && weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow);
			bool oneHanded = (weapon == null || weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon);
			bool shieldWorks = (shield != null && (!shield.m_shared.m_useDurability || shield.m_durability > 0f) && humanoid.GetInventory().ContainsItem(shield));
			ItemDrop.ItemData backRight = (leftHanded ? null : weapon);
			ItemDrop.ItemData backLeft = (leftHanded ? weapon : ((oneHanded && shieldWorks) ? shield : null));
			if (!sheathed || putAway || humanoid.m_hiddenRightItem != backRight || humanoid.m_hiddenLeftItem != backLeft)
			{
				sheathed = true;
				humanoid.m_hiddenRightItem = backRight;
				humanoid.m_hiddenLeftItem = backLeft;
				humanoid.SetupVisEquipment(humanoid.m_visEquipment, false);
				if (putAway)
				{
					humanoid.m_zanim.SetTrigger("equip_hip");
				}
			}
		}

		/// <summary>
		/// Finds the best shield a PC viking has of its own - not one it carries for a chest - that isn't broken
		/// </summary>
		/// <returns>The shield, or null when it has none</returns>
		private ItemDrop.ItemData BestShield()
		{
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield || VikingItems.IsCarried(item) || (item.m_shared.m_useDurability && item.m_durability <= 0f))
				{
					continue;
				}
				if (best == null || item.GetBlockPower(0f) > best.GetBlockPower(0f))
				{
					best = item;
				}
			}
			return(best);
		}

		/// <summary>
		/// Has a PC viking take up gear it picked up when it's better than its own - armor for a slot it has nothing
		/// in or more armor than what it wears there, a shield that blocks better, a utility item like a strength belt
		/// for an empty utility slot, or a weapon that deals more damage, counting its ammo, than its best of the same
		/// kind - melee, or a bow or crossbow - since every PC viking carries one of each and picks between them as
		/// it fights (see ChooseWeapon). A dverger circlet, when it has none, it keeps for when it wants light
		/// </summary>
		/// <remarks>
		/// Armor, a shield and a weapon it takes up replace what it had of the kind, which it no longer wants. With no
		/// danger near, gear it wields goes on its back
		/// </remarks>
		/// <param name="item">The item, already in its inventory</param>
		/// <param name="replaced">The armor, shield or weapon it no longer wants, or null</param>
		/// <returns>True when it took the item up</returns>
		public bool TakeUpGear(ItemDrop.ItemData item, out ItemDrop.ItemData replaced)
		{
			replaced = null;
			if (item == null || GearHeld() || !humanoid.GetInventory().ContainsItem(item) || !WouldTakeUp(item))
			{
				return(false);
			}
			// A dverger circlet is kept for when the viking wants light (see UpdateTorch)
			if (IsCirclet(item))
			{
				SaveInventory();
				return(true);
			}
			ItemDrop.ItemData worn = WornIn(item.m_shared.m_itemType, out bool armor, item);
			if (armor)
			{
				humanoid.EquipItem(item, false);
				replaced = worn;
				SaveInventory();
				return(true);
			}
			if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility)
			{
				humanoid.EquipItem(item, false);
				SaveInventory();
				return(true);
			}
			if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
			{
				ItemDrop.ItemData old = (ShieldWorks() ? shield : null);
				if (old != null && humanoid.IsItemEquiped(old))
				{
					humanoid.UnequipItem(old, false);
				}
				shield = item;
				replaced = old;
				if (InDanger())
				{
					EquipWeapon();
				}
				SaveInventory();
				return(true);
			}
			replaced = BestOfKind(IsRanged(item), item);
			data.WeaponSlot = item.m_gridPos;
			nview.GetZDO().Set(DataKey, data.ToBytes());
			if (InDanger())
			{
				EquipWeapon();
			}
			SaveInventory();
			return(true);
		}

		/// <summary>
		/// Has a PC viking put on a piece of gear a player picked for it, whether or not it's better than its own -
		/// armor or a utility item where it goes, ammo as what it shoots, and a shield or weapon in its hands the way a
		/// player wields one (see Draw), even while it leaves its gear as it is. Left to itself, it later switches to
		/// anything better it has (see VillageLife.UpdatePutOn)
		/// </summary>
		/// <param name="item">The piece, in its inventory</param>
		/// <returns>False when it isn't something the viking can put on</returns>
		public bool Wear(ItemDrop.ItemData item)
		{
			if (item == null || !IsPCViking(humanoid) || GetData() == null || !humanoid.GetInventory().ContainsItem(item) || VikingItems.IsProp(item))
			{
				return(false);
			}
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			WornIn(type, out bool armor);
			if (armor || type == ItemDrop.ItemData.ItemType.Utility || type == ItemDrop.ItemData.ItemType.Ammo)
			{
				humanoid.EquipItem(item, true);
				SaveInventory();
				return(true);
			}
			bool isShield = (type == ItemDrop.ItemData.ItemType.Shield);
			if (!isShield && (!item.IsWeapon() || type == ItemDrop.ItemData.ItemType.Torch))
			{
				return(false);
			}
			// Handed over by a player, it's the viking's own rather than loot it carries for a chest
			VikingItems.Keep(item);
			if (isShield)
			{
				shield = item;
			}
			else
			{
				data.WeaponSlot = item.m_gridPos;
				nview.GetZDO().Set(DataKey, data.ToBytes());
			}
			Draw(item);
			SaveInventory();
			return(true);
		}

		/// <summary>
		/// Has the viking take a weapon or shield a player had it put on into its hands, the way a player wields one,
		/// drawing what it had sheathed on its back - a shield beside a one-handed weapon, and its weapon beside a shield.
		/// It sheathes them again once there's no danger and it looks after its gear again (see UpdateSheath)
		/// </summary>
		/// <param name="item">The weapon or shield</param>
		private void Draw(ItemDrop.ItemData item)
		{
			bool wasSheathed = sheathed;
			sheathed = false;
			humanoid.m_hiddenLeftItem = null;
			humanoid.m_hiddenRightItem = null;
			if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
			{
				EquipWeapon(true);
			}
			if (!humanoid.IsItemEquiped(item))
			{
				humanoid.EquipItem(item, true);
			}
			if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon && ShieldWorks() && !humanoid.IsItemEquiped(shield))
			{
				humanoid.EquipItem(shield, false);
			}
			humanoid.SetupVisEquipment(humanoid.m_visEquipment, false);
			if (wasSheathed)
			{
				humanoid.m_zanim.SetTrigger("equip_hip");
			}
		}

		/// <summary>
		/// Finds the weapon the viking wields as a rule
		/// </summary>
		/// <returns>The weapon, or null when it has none</returns>
		public ItemDrop.ItemData MainWeaponItem()
		{
			return(MainWeapon());
		}

		/// <summary>
		/// Gets where the weapon the viking wields as a rule lies in its inventory
		/// </summary>
		/// <returns>The slot, or (-1, -1) when it has none noted</returns>
		public Vector2i WeaponSlot()
		{
			return((GetData() != null) ? data.WeaponSlot : new Vector2i(-1, -1));
		}

		/// <summary>
		/// Keeps the weapon the viking wields as a rule its own after it has moved within its inventory - by a player
		/// rearranging it, say - since it's noted by where it lies
		/// </summary>
		/// <param name="weapon">The weapon</param>
		public void FollowWeapon(ItemDrop.ItemData weapon)
		{
			if (weapon == null || GetData() == null || !humanoid.GetInventory().ContainsItem(weapon) || data.WeaponSlot == weapon.m_gridPos)
			{
				return;
			}
			data.WeaponSlot = weapon.m_gridPos;
			nview.GetZDO().Set(DataKey, data.ToBytes());
		}

		/// <summary>
		/// Checks whether the viking would take up a piece of gear it doesn't have yet, by the same rules as TakeUpGear -
		/// armor for a slot it has nothing in or more armor than what it wears there, a shield that blocks better, a
		/// utility item for an empty utility slot, or a weapon better than its best of the same kind. In a biome whose
		/// weather freezes, armor that resists frost comes first while nothing else it wears does, whatever its armor. A
		/// dverger circlet it would take up only when it has none, for its light
		/// </summary>
		/// <param name="item">The item, in its inventory or not</param>
		/// <returns>True when it would</returns>
		public bool WouldTakeUp(ItemDrop.ItemData item)
		{
			if (item == null || !IsPCViking(humanoid) || GetData() == null || humanoid.IsDead())
			{
				return(false);
			}
			if (item.m_shared.m_useDurability && item.m_durability <= 0f)
			{
				return(false);
			}
			// A dverger circlet is wanted for its light, by a viking with none, rather than for its armor
			if (IsCirclet(item))
			{
				return(FindCirclet(item) == null);
			}
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			ItemDrop.ItemData worn = WornIn(type, out bool armor, item);
			if (armor)
			{
				bool warm = VikingGear.KeepsOffFrost(item);
				bool wornWarm = VikingGear.KeepsOffFrost(worn);
				// Whether it freezes and what else keeps the frost off only matter between pieces that differ in it
				bool freezing = (warm != wornWarm && VikingGear.FreezesAt(transform.position));
				bool othersWarm = (freezing && OthersKeepOffFrost(worn));
				return(WantsArmor(warm, wornWarm, freezing, othersWarm, worn != null, item.GetArmor(), ((worn != null) ? worn.GetArmor() : 0f)));
			}
			if (type == ItemDrop.ItemData.ItemType.Shield)
			{
				return(!ShieldWorks() || item.GetBlockPower(0f) > shield.GetBlockPower(0f));
			}
			// A utility item that does something when worn, like a strength belt, goes in an empty utility slot - a viking
			// keeps to the one it wears, so a spare goes to a chest for another
			if (type == ItemDrop.ItemData.ItemType.Utility)
			{
				return(humanoid.m_utilityItem == null && item.m_shared.m_equipStatusEffect != null);
			}
			if (!item.IsWeapon() || type == ItemDrop.ItemData.ItemType.Torch)
			{
				return(false);
			}
			// A bow is weighed with the ammo it would shoot, and wanted before the viking has arrows for it
			if (!CanUse(item, false))
			{
				return(false);
			}
			ItemDrop.ItemData best = BestOfKind(IsRanged(item), item);
			return(best == null || Damage(item) > Damage(best));
		}

		/// <summary>
		/// Checks whether the viking would take up a piece of armor over what it wears in that slot - one with more armor,
		/// or in a slot it has nothing in. Where the weather freezes, a piece that keeps the frost off comes before armor
		/// while nothing else it wears does - one is taken up whatever its armor, and never given up for one that doesn't
		/// </summary>
		/// <param name="warm">Whether the piece keeps the frost off</param>
		/// <param name="wornWarm">Whether what it wears in the slot keeps the frost off, false when it wears nothing there</param>
		/// <param name="freezing">Whether the weather freezes where the viking is</param>
		/// <param name="othersWarm">Whether anything else it wears keeps the frost off</param>
		/// <param name="hasWorn">Whether it wears anything in the slot</param>
		/// <param name="armor">The piece's armor</param>
		/// <param name="wornArmor">The armor of what it wears in the slot</param>
		/// <returns>True when it would</returns>
		internal static bool WantsArmor(bool warm, bool wornWarm, bool freezing, bool othersWarm, bool hasWorn, float armor, float wornArmor)
		{
			if (warm != wornWarm && freezing && !othersWarm)
			{
				return(warm);
			}
			return(!hasWorn || armor > wornArmor);
		}

		/// <summary>
		/// Finds the viking's best weapon of a kind by the damage it deals, counting a bow or crossbow it has no ammo for
		/// yet
		/// </summary>
		/// <param name="ranged">True for bows and crossbows, false for melee weapons</param>
		/// <param name="except">A weapon to leave out, like the one being weighed against the rest, or null</param>
		/// <returns>The weapon, or null when it has none</returns>
		private ItemDrop.ItemData BestOfKind(bool ranged, ItemDrop.ItemData except)
		{
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item == except || IsRanged(item) != ranged || !CanUse(item, false))
				{
					continue;
				}
				if (best == null || Damage(item) > Damage(best))
				{
					best = item;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether any armor or utility item the viking wears keeps the frost off, leaving one piece out - or the
		/// viking is immune to frost of itself, so it needs nothing to keep it off
		/// </summary>
		/// <param name="except">The piece to leave out, like one it would swap, or null</param>
		/// <returns>True when one does, or it's immune</returns>
		private bool OthersKeepOffFrost(ItemDrop.ItemData except)
		{
			if (humanoid.m_damageModifiers.m_frost == HitData.DamageModifier.Immune)
			{
				return(true);
			}
			foreach (ItemDrop.ItemData piece in new ItemDrop.ItemData[] { humanoid.m_chestItem, humanoid.m_legItem, humanoid.m_helmetItem, humanoid.m_shoulderItem, humanoid.m_utilityItem })
			{
				if (piece != null && piece != except && VikingGear.KeepsOffFrost(piece))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Finds the armor the viking wears in the slot an item of a type goes in - for the head, the helmet it wears
		/// when its dverger circlet is off, while the circlet is on in its place (see OwnHelmet)
		/// </summary>
		/// <param name="type">The item's type</param>
		/// <param name="armor">Set to whether the type is armor</param>
		/// <param name="except">The item being weighed up, left out of the helmets under a circlet, or null</param>
		/// <returns>What it wears there, or null when it wears nothing there or the type isn't armor</returns>
		private ItemDrop.ItemData WornIn(ItemDrop.ItemData.ItemType type, out bool armor, ItemDrop.ItemData except = null)
		{
			armor = true;
			if (type == ItemDrop.ItemData.ItemType.Helmet)
			{
				return(IsCirclet(humanoid.m_helmetItem) ? OwnHelmet(except) : humanoid.m_helmetItem);
			}
			if (type == ItemDrop.ItemData.ItemType.Chest)
			{
				return(humanoid.m_chestItem);
			}
			if (type == ItemDrop.ItemData.ItemType.Legs)
			{
				return(humanoid.m_legItem);
			}
			if (type == ItemDrop.ItemData.ItemType.Shoulder)
			{
				return(humanoid.m_shoulderItem);
			}
			armor = false;
			return(null);
		}

		/// <summary>
		/// Checks whether the viking has a shield that still works
		/// </summary>
		/// <returns>True when it has</returns>
		private bool ShieldWorks()
		{
			return(shield != null && humanoid.GetInventory().ContainsItem(shield) && (!shield.m_shared.m_useDurability || shield.m_durability > 0f));
		}

		/// <summary>
		/// Wields the viking's weapon when its AI picks one, unless it's a PC viking with its weapon sheathed and no
		/// danger near
		/// </summary>
		public void PickWeapon()
		{
			if (sheathed && !InDanger())
			{
				return;
			}
			EquipWeapon();
		}

		/// <summary>
		/// Checks whether an item in the viking's hands is something it fights with - a weapon other than a torch, or
		/// a shield - rather than a torch or a prop
		/// </summary>
		/// <param name="item">The item, or null</param>
		/// <returns>True when it is</returns>
		private static bool IsArmament(ItemDrop.ItemData item)
		{
			if (item == null || VikingItems.IsProp(item))
			{
				return(false);
			}
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			return(type == ItemDrop.ItemData.ItemType.Shield || (item.IsWeapon() && type != ItemDrop.ItemData.ItemType.Torch));
		}
	}
}
