using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// A viking's weapons - which it wields for each fight, how hard each hits the enemy in front of it through its
	/// resistances, the ammo it shoots, and its AI's reach and pace with them
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Checks whether a living viking is an archer, from its ZDO - one marked to get a bow, or one whose gear holds a
		/// bow of its own
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <returns>True when it is</returns>
		public static bool IsArcher(ZDO zdo)
		{
			if (zdo.GetBool(ArcherKey))
			{
				return(true);
			}
			VikingData saved = VikingData.FromBytes(zdo.GetByteArray(DataKey));
			byte[] bytes = zdo.GetByteArray(InventoryKey);
			if (saved == null || bytes == null)
			{
				return(false);
			}
			Inventory inventory = new Inventory(ZombiePrefabName, null, saved.Width, saved.Height);
			inventory.Load(new ZPackage(bytes));
			return(inventory.GetAllItems().Exists((ItemDrop.ItemData item) => IsBow(item) && !VikingItems.IsCarried(item)));
		}

		/// <summary>
		/// Checks whether an item is a bow, one that shoots arrows
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		public static bool IsBow(ItemDrop.ItemData item)
		{
			return(item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow && !string.IsNullOrEmpty(item.m_shared.m_ammoType));
		}

		/// <summary>
		/// Switches weapons before an attack when the one in hand has broken or run out of ammo
		/// </summary>
		public void CheckWeapon()
		{
			ItemDrop.ItemData current = CurrentWeapon();
			if (current != null && !CanUse(current))
			{
				EquipWeapon();
			}
		}

		/// <summary>
		/// Wears down the weapon the viking just attacked with, the same as a player's
		/// </summary>
		public void WearWeapon()
		{
			ItemDrop.ItemData weapon = CurrentWeapon();
			if (weapon != null && weapon.m_shared.m_useDurability)
			{
				weapon.m_durability = Mathf.Max(0f, weapon.m_durability - weapon.m_shared.m_useDurabilityDrain * Game.m_durabilityRate);
			}
		}

		/// <summary>
		/// Finds the weapon in the viking's hands
		/// </summary>
		/// <returns>The weapon, or null when it's fighting with its fists</returns>
		private ItemDrop.ItemData CurrentWeapon()
		{
			ItemDrop.ItemData weapon = humanoid.GetCurrentWeapon();
			return((weapon != null && humanoid.GetInventory().ContainsItem(weapon)) ? weapon : null);
		}

		/// <summary>
		/// How close an enemy comes before a living viking with a bow draws its melee weapon instead, and how far off it
		/// gets before the viking goes back to picking by the fight, in meters - apart, so it doesn't switch back and forth
		/// </summary>
		private const float MeleeDrawRange = 3f;
		private const float MeleeKeepRange = 6f;

		/// <summary>
		/// How much of its damage a bow or crossbow is counted for against a melee weapon, since it shoots more slowly
		/// than a melee weapon swings
		/// </summary>
		private const float RangedPace = 0.6f;

		/// <summary>
		/// Picks the weapon the viking should wield. A zombie viking wields the one the player was wielding while it
		/// still works, or else the one that deals the most damage. A living viking with both a melee weapon and a bow
		/// or crossbow with ammo picks between them for the fight in front of it (see ChooseForFight), and the bow or
		/// crossbow while it aims at a bird or at prey from afar (see VillageLife.AimingBow). Against a deathsquito diving
		/// at it, it picks a one-handed weapon to go beside its shield, or a two-handed one (see DiveWeapon)
		/// </summary>
		/// <returns>The weapon, or null for fists</returns>
		private ItemDrop.ItemData ChooseWeapon()
		{
			if (!IsLivingViking(humanoid))
			{
				if (data.WeaponSlot.x >= 0)
				{
					ItemDrop.ItemData held = humanoid.GetInventory().GetItemAt(data.WeaponSlot.x, data.WeaponSlot.y);
					if (held != null && CanUse(held))
					{
						return(held);
					}
				}
				return(BestWeapon(false));
			}
			// A deathsquito diving at it is met with a shield and a one-handed weapon, or a two-handed one (see
			// UpdateDiveDefense)
			if (diver != null && !diver.IsDead())
			{
				ItemDrop.ItemData diveWeapon = DiveWeapon();
				if (diveWeapon != null)
				{
					return(diveWeapon);
				}
			}
			// Each weapon is weighed against the enemy in front of it, through its resistances
			Character target = ((ai != null) ? ai.GetTargetCreature() : null);
			HitData.DamageModifiers? resistances = Resistances(target);
			ItemDrop.ItemData melee = BestWeapon(true, resistances);
			ItemDrop.ItemData ranged = BestRanged(resistances);
			if (melee == null || ranged == null)
			{
				return(melee ?? ranged);
			}
			// Aiming at a bird or at prey from afar, it shoots
			if (life != null && life.AimingBow())
			{
				return(ranged);
			}
			return(ChooseForFight(melee, ranged, target));
		}

		/// <summary>
		/// Picks between a living viking's melee weapon and its bow for the fight in front of it - the bow against a
		/// root, its melee weapon with its enemy up close (see CloseQuarters), the bow against an enemy it can't reach
		/// or get to through the crowd around it, or one it's keeping away from (see VillageLife.WantsBow), and the bow
		/// while it's below HurtRangedShare of its health, even one that hurts less - and otherwise the one that hurts
		/// most, by its damage through the enemy's resistances and the viking's skill with it
		/// </summary>
		/// <param name="melee">Its best melee weapon against the enemy</param>
		/// <param name="ranged">Its best bow or crossbow it has ammo for against the enemy</param>
		/// <param name="target">The enemy, or null</param>
		/// <returns>The weapon</returns>
		private ItemDrop.ItemData ChooseForFight(ItemDrop.ItemData melee, ItemDrop.ItemData ranged, Character target)
		{
			if (target != null)
			{
				if (MonsterThreat.IsRoot(target))
				{
					return(ranged);
				}
				if (CloseQuarters(target))
				{
					return(melee);
				}
				if (life != null && life.WantsBow(target))
				{
					return(ranged);
				}
				// Hurt, it would rather keep its distance than trade blows
				if (humanoid.GetHealthPercentage() < HurtRangedShare)
				{
					return(ranged);
				}
			}
			HitData.DamageModifiers? resistances = Resistances(target);
			return(PrefersRanged(Hurt(ranged, resistances), Hurt(melee, resistances)) ? ranged : melee);
		}

		/// <summary>
		/// The share of its health a living viking has to be below to shoot its enemy rather than fight it hand to hand
		/// </summary>
		private const float HurtRangedShare = 0.5f;

		/// <summary>
		/// Checks whether a bow or crossbow hurts more than a melee weapon, counting it for RangedPace of its damage since
		/// it shoots more slowly - the melee weapon when they come out even
		/// </summary>
		/// <param name="ranged">How hard the viking hits with the bow (see Hurt)</param>
		/// <param name="melee">How hard it hits with the melee weapon</param>
		/// <returns>True for the bow</returns>
		internal static bool PrefersRanged(float ranged, float melee)
		{
			return(ranged * RangedPace > melee);
		}

		/// <summary>
		/// Measures how hard the viking hits a creature with a weapon - its damage, counting its ammo, through the
		/// creature's resistances, scaled by the viking's skill with it the way a player's damage is, and a melee weapon
		/// by its biome's MeleeScale (see DamageScale)
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <param name="resistances">The creature's resistances, or null for its damage as it is</param>
		/// <returns>The damage</returns>
		private float Hurt(ItemDrop.ItemData weapon, HitData.DamageModifiers? resistances)
		{
			float scale = ((!IsRanged(weapon) && GetData() != null && !data.Tombstone) ? VikingGear.Get(BiomeIndex()).MeleeScale : 1f);
			return(SkillScaled(Damage(weapon, resistances) * scale, SkillLevel(weapon.m_shared.m_skillType)));
		}

		/// <summary>
		/// The share of a weapon's damage a viking with no skill in it deals - the rest comes with skill, the same as a
		/// player's
		/// </summary>
		public const float SkillDamageBase = 0.4f;

		/// <summary>
		/// Works out the damage a viking deals with a skill, the way a player's is scaled by theirs - SkillDamageBase of
		/// it with no skill, rising to all of it at 100
		/// </summary>
		/// <param name="damage">The full damage</param>
		/// <param name="level">The viking's level in the skill, where below 0 counts as 0 and above 100 as 100</param>
		/// <returns>The damage it deals</returns>
		internal static float SkillScaled(float damage, float level)
		{
			float skill = Mathf.Clamp01(level / 100f);
			return(damage * (SkillDamageBase + (1f - SkillDamageBase) * skill));
		}

		/// <summary>
		/// Works out how much a skill raises what it boosts, the way a player's Run and Jump skills do - nothing with no
		/// skill, rising to the whole bonus at 100
		/// </summary>
		/// <param name="level">The viking's level in the skill, where below 0 counts as 0 and above 100 as 100</param>
		/// <param name="bonus">How much more it adds at full skill, like 0.4 for 40% more</param>
		/// <returns>The factor to multiply by, from 1 to 1 + bonus</returns>
		internal static float SkillBoost(float level, float bonus)
		{
			return(1f + Mathf.Clamp01(level / 100f) * bonus);
		}

		/// <summary>
		/// Finds the viking's best bow or crossbow it has ammo for, by the damage it deals with that ammo
		/// </summary>
		/// <param name="resistances">The resistances of the creature it's for, which the damage goes through, or null</param>
		/// <returns>The weapon, or null when it has none it can shoot</returns>
		private ItemDrop.ItemData BestRanged(HitData.DamageModifiers? resistances = null)
		{
			ItemDrop.ItemData best = null;
			float bestDamage = 0f;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (!IsRanged(item) || !CanUse(item))
				{
					continue;
				}
				float damage = Damage(item, resistances);
				if (best == null || damage > bestDamage)
				{
					best = item;
					bestDamage = damage;
				}
			}
			return(best);
		}

		/// <summary>
		/// Measures the hardest the viking hits a creature with any weapon it can fight with, by its damage through the
		/// creature's resistances and its skill, or with its fists when it has none
		/// </summary>
		/// <param name="target">The creature, or null for the damage as it is</param>
		/// <returns>The damage</returns>
		public float StrikeDamage(Character target = null)
		{
			return(Strike(Resistances(target), 1f));
		}

		/// <summary>
		/// Measures how much the viking deals a creature in the time a melee swing takes - its hardest hit (see
		/// StrikeDamage), counting a bow or crossbow at RangedPace of its damage, since it shoots more slowly
		/// </summary>
		/// <param name="resistances">The creature's resistances</param>
		/// <returns>The damage</returns>
		public float SwingDamage(HitData.DamageModifiers resistances)
		{
			return(Strike(resistances, RangedPace));
		}

		/// <summary>
		/// Measures the hardest the viking hits a creature with its best melee weapon, its best bow or crossbow or its
		/// fists, through the creature's resistances and its skill
		/// </summary>
		/// <param name="resistances">The creature's resistances, or null for the damage as it is</param>
		/// <param name="rangedShare">The share of a bow's or crossbow's damage it's counted for</param>
		/// <returns>The damage</returns>
		private float Strike(HitData.DamageModifiers? resistances, float rangedShare)
		{
			ItemDrop.ItemData melee = BestWeapon(true, resistances);
			ItemDrop.ItemData ranged = BestRanged(resistances);
			HitData.DamageTypes fists = new HitData.DamageTypes();
			fists.m_blunt = ((GetData() != null) ? VikingGear.Get(BiomeIndex()).Unarmed : 0f);
			float best = SkillScaled(CombatDamage(Resisted(fists, resistances)), SkillLevel(Skills.SkillType.Unarmed));
			if (melee != null)
			{
				best = Mathf.Max(best, Hurt(melee, resistances));
			}
			if (ranged != null)
			{
				best = Mathf.Max(best, Hurt(ranged, resistances) * rangedShare);
			}
			return(best);
		}

		/// <summary>
		/// Measures how much a working shield of the viking's holds back of a blocked hit - its block power at the
		/// viking's blocking skill, the way the game works it out as it blocks (see Humanoid.BlockAttack)
		/// </summary>
		/// <returns>The block power, or 0 with no shield that works</returns>
		public float BlockPower()
		{
			return(ShieldWorks() ? shield.GetBlockPower(Mathf.Clamp01(SkillLevel(Skills.SkillType.Blocking) / 100f)) : 0f);
		}

		/// <summary>
		/// Gets a creature's resistances, as the game puts a hit on it through them
		/// </summary>
		/// <param name="target">The creature, or null</param>
		/// <returns>Its resistances, or null with no creature</returns>
		private static HitData.DamageModifiers? Resistances(Character target)
		{
			return((target != null) ? target.GetDamageModifiers() : (HitData.DamageModifiers?)null);
		}

		/// <summary>
		/// Measures the viking's best melee weapon by the damage it deals
		/// </summary>
		/// <returns>The damage, or 0 when it fights with its fists</returns>
		public float MeleeDamage()
		{
			ItemDrop.ItemData weapon = BestWeapon(true);
			return((weapon != null) ? Damage(weapon) : 0f);
		}

		/// <summary>
		/// Finds the weapon the viking can fight with that deals the most damage
		/// </summary>
		/// <param name="melee">True to leave out bows and crossbows</param>
		/// <param name="resistances">The resistances of the creature it's for, which the damage goes through - so a
		/// weapon of what it's weak to over one of what it resists - or null</param>
		/// <returns>The weapon, or null when it has none</returns>
		private ItemDrop.ItemData BestWeapon(bool melee, HitData.DamageModifiers? resistances = null)
		{
			ItemDrop.ItemData best = null;
			float bestDamage = 0f;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (!CanUse(item) || (melee && IsRanged(item)))
				{
					continue;
				}
				float damage = Damage(item, resistances);
				if (best == null || damage > bestDamage)
				{
					best = item;
					bestDamage = damage;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether the viking's target is close enough to fight hand to hand - within MeleeDrawRange, or
		/// MeleeKeepRange while it already has a melee weapon in its hands
		/// </summary>
		/// <param name="target">The target</param>
		/// <returns>True when it is</returns>
		private bool CloseQuarters(Character target)
		{
			ItemDrop.ItemData current = CurrentWeapon();
			float range = ((current != null && !IsRanged(current)) ? MeleeKeepRange : MeleeDrawRange);
			return(Utils.DistanceXZ(target.transform.position, transform.position) <= range);
		}

		/// <summary>
		/// Checks whether the viking can fight with an item - a weapon of its own, not loot it carries or a tool it holds
		/// for show, other than a torch that isn't broken, has ammo when it takes ammo, doesn't need eitr, which the viking has none of, and
		/// deals damage to more than tamed creatures. A living viking keeps a pickaxe for mining and never fights with one
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it can</returns>
		private bool CanUse(ItemDrop.ItemData item)
		{
			return(CanUse(item, true));
		}

		/// <summary>
		/// Checks whether an item is a pickaxe - one that mines with the Pickaxes skill
		/// </summary>
		/// <param name="item">The item, or null</param>
		/// <returns>True when it is</returns>
		public static bool IsPickaxe(ItemDrop.ItemData item)
		{
			return(item != null && item.m_shared.m_skillType == Skills.SkillType.Pickaxes && item.m_shared.m_damages.m_pickaxe > 0f);
		}

		/// <summary>
		/// Checks whether the viking can fight with an item, or could once it has ammo for it
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="needAmmo">False to leave out whether it has ammo for a weapon that takes ammo</param>
		/// <returns>True when it can</returns>
		private bool CanUse(ItemDrop.ItemData item, bool needAmmo)
		{
			if (!item.IsWeapon() || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch || VikingItems.IsCarried(item) || VikingItems.IsProp(item))
			{
				return(false);
			}
			// A weapon that only hurts tamed creatures, like the butcher knife, can't hurt the viking's enemies (see
			// Attack.DoMeleeAttack and DoAreaAttack)
			if (item.m_shared.m_tamedOnly)
			{
				return(false);
			}
			if (IsPickaxe(item) && IsLivingViking(humanoid))
			{
				return(false);
			}
			ItemDrop.ItemData.SharedData shared = item.m_shared;
			if (shared.m_useDurability && item.m_durability <= 0f)
			{
				return(false);
			}
			Attack attack = shared.m_attack;
			if (attack == null || string.IsNullOrEmpty(attack.m_attackAnimation) || attack.m_attackEitr > 0f)
			{
				return(false);
			}
			if (!string.IsNullOrEmpty(shared.m_ammoType) && FindAmmo(item) == null)
			{
				return(!needAmmo);
			}
			return(Damage(item) > 0f);
		}

		/// <summary>
		/// Checks whether a weapon fights from afar - a bow or crossbow, which shoots ammo
		/// </summary>
		/// <param name="item">The weapon, or null</param>
		/// <returns>True when it does</returns>
		public static bool IsRanged(ItemDrop.ItemData item)
		{
			return(item != null && !string.IsNullOrEmpty(item.m_shared.m_ammoType) && item.m_shared.m_attack != null && item.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile);
		}

		/// <summary>
		/// Finds the weapon the viking fights with as a rule - the one it's set to wield while it isn't broken, even
		/// while it's out of ammo, or else the one it would wield now
		/// </summary>
		/// <returns>The weapon, or null for its fists</returns>
		private ItemDrop.ItemData MainWeapon()
		{
			if (GetData() == null)
			{
				return(null);
			}
			if (data.WeaponSlot.x >= 0)
			{
				ItemDrop.ItemData held = humanoid.GetInventory().GetItemAt(data.WeaponSlot.x, data.WeaponSlot.y);
				if (held != null && CanUse(held, false))
				{
					return(held);
				}
			}
			return(ChooseWeapon());
		}

		/// <summary>
		/// Checks whether the viking can shoot - a living viking with a bow or crossbow it has ammo for, which it picks
		/// up as the fight calls for it, or a zombie viking that fights with one as a rule
		/// </summary>
		/// <returns>True when it can</returns>
		public bool IsArcher()
		{
			return(IsLivingViking(humanoid) ? (BestRanged() != null) : IsRanged(MainWeapon()));
		}

		/// <summary>
		/// Finds the bow or crossbow the viking shoots with - the one in its hands, or else its best one it has ammo for
		/// </summary>
		/// <returns>The weapon, or null when it has none</returns>
		public ItemDrop.ItemData Bow()
		{
			ItemDrop.ItemData current = CurrentWeapon();
			return((current != null && IsRanged(current)) ? current : BestRanged());
		}

		/// <summary>
		/// Rates a piece of gear against others of its kind, by the same measures it's taken up by (see WouldTakeUp) - a
		/// shield by how well it blocks, a weapon by the damage it deals with the ammo it would shoot, and armor by how
		/// much armor it gives
		/// </summary>
		/// <param name="item">The piece</param>
		/// <returns>Its rating</returns>
		public float GearValue(ItemDrop.ItemData item)
		{
			if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
			{
				return(item.GetBlockPower(0f));
			}
			return(item.IsWeapon() ? Damage(item) : item.GetArmor());
		}

		/// <summary>
		/// Adds up the damage a weapon deals to creatures, with the ammo it would shoot
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <returns>Its damage, leaving out chopping and mining</returns>
		private float Damage(ItemDrop.ItemData weapon)
		{
			return(Damage(weapon, null));
		}

		/// <summary>
		/// Adds up the damage a weapon deals to a creature, with the ammo it would shoot at it (see BestAmmo), through
		/// the creature's resistances the way the game applies them - more of what it's weak to, less of what it resists
		/// and none of what it's immune to, its armor and status effects included - or as it is with no creature
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <param name="resistances">The creature's resistances, or null</param>
		/// <returns>Its damage, leaving out chopping and mining</returns>
		private float Damage(ItemDrop.ItemData weapon, HitData.DamageModifiers? resistances)
		{
			HitData.DamageTypes damage = weapon.GetDamage();
			ItemDrop.ItemData ammo = (BestAmmo(weapon, resistances) ?? FindAmmo(weapon));
			if (ammo != null)
			{
				damage.Add(ammo.GetDamage());
			}
			return(CombatDamage(Resisted(damage, resistances)));
		}

		/// <summary>
		/// Finds the ammo the viking has that a bow or crossbow hurts a creature most with, along with the weapon's own
		/// damage, through the creature's resistances - or with no creature, the ammo that hurts the living most, which
		/// are immune to spirit damage, so silver arrows are kept for the dead. Of ammo that hurts as much, the one with
		/// the least spirit damage
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <param name="resistances">The creature's resistances, or null with no creature</param>
		/// <returns>The ammo, or null when the weapon takes none or the viking has none it can ready</returns>
		private ItemDrop.ItemData BestAmmo(ItemDrop.ItemData weapon, HitData.DamageModifiers? resistances)
		{
			string ammoType = weapon.m_shared.m_ammoType;
			if (string.IsNullOrEmpty(ammoType))
			{
				return(null);
			}
			HitData.DamageTypes own = weapon.GetDamage();
			ItemDrop.ItemData best = null;
			float bestDamage = 0f;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo || item.m_shared.m_ammoType != ammoType || VikingItems.IsCarried(item) || VikingItems.IsProp(item))
				{
					continue;
				}
				HitData.DamageTypes damage = own;
				damage.Add(item.GetDamage());
				if (resistances == null)
				{
					damage.m_spirit = 0f;
				}
				float hurt = CombatDamage(Resisted(damage, resistances));
				if (best == null || hurt > bestDamage || (hurt == bestDamage && item.GetDamage().m_spirit < best.GetDamage().m_spirit))
				{
					best = item;
					bestDamage = hurt;
				}
			}
			return(best);
		}

		/// <summary>
		/// Puts damage through a creature's resistances, the way the game does as it's hit
		/// </summary>
		/// <param name="damage">The damage</param>
		/// <param name="resistances">The creature's resistances, or null to leave the damage as it is</param>
		/// <returns>The damage it would take</returns>
		private static HitData.DamageTypes Resisted(HitData.DamageTypes damage, HitData.DamageModifiers? resistances)
		{
			if (resistances == null)
			{
				return(damage);
			}
			HitData hit = new HitData();
			hit.m_damage = damage;
			hit.ApplyResistance(resistances.Value, out HitData.DamageModifier _);
			return(hit.m_damage);
		}

		/// <summary>
		/// Adds up the damage an item deals to creatures
		/// </summary>
		/// <param name="item">The weapon or ammo</param>
		/// <returns>Its damage, leaving out chopping and mining</returns>
		private static float CombatDamage(ItemDrop.ItemData item)
		{
			return(CombatDamage(item.GetDamage()));
		}

		/// <summary>
		/// Adds up damage that hurts creatures
		/// </summary>
		/// <param name="damage">The damage</param>
		/// <returns>Its total, leaving out chopping and mining</returns>
		private static float CombatDamage(HitData.DamageTypes damage)
		{
			return(damage.m_damage + damage.GetTotalPhysicalDamage() + damage.GetTotalElementalDamage() + damage.m_poison + damage.m_spirit);
		}

		/// <summary>
		/// Finds the ammo a weapon would shoot, the way an attack picks it
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <returns>The ammo, or null when the weapon takes none or there's none left</returns>
		private ItemDrop.ItemData FindAmmo(ItemDrop.ItemData weapon)
		{
			string ammoType = weapon.m_shared.m_ammoType;
			return(string.IsNullOrEmpty(ammoType) ? null : humanoid.GetInventory().GetAmmoItem(ammoType));
		}

		/// <summary>
		/// Gives the weapon in the viking's hands an AI that knows its reach for the length of its AI's update - a ranged
		/// weapon an archer's range and pace, and a melee weapon, fists included, the reach of its swing (see MeleeReach),
		/// so a knife strikes from up close and a polearm from farther off, and for a living viking, a player's pace, with
		/// no wait between swings
		/// </summary>
		/// <remarks>
		/// A player's weapons share their settings with every copy of the item and only the AI reads these ones, so
		/// they're changed just while this viking's AI runs and put back by RestoreWeaponAI. The AI strikes once its
		/// enemy is within the weapon's AI range, which a player's weapons don't set to the reach of their swing
		/// </remarks>
		public void TuneWeaponAI()
		{
			RestoreWeaponAI();
			ItemDrop.ItemData weapon = humanoid.GetCurrentWeapon();
			Attack attack = ((weapon != null) ? weapon.m_shared.m_attack : null);
			if (attack == null)
			{
				return;
			}
			Attack.AttackType type = attack.m_attackType;
			bool ranged = (type == Attack.AttackType.Projectile && CurrentWeapon() != null);
			bool melee = (type == Attack.AttackType.Horizontal || type == Attack.AttackType.Vertical || type == Attack.AttackType.Area);
			if (!ranged && !melee)
			{
				return;
			}
			ItemDrop.ItemData.SharedData shared = weapon.m_shared;
			tunedShared = shared;
			tunedRange = shared.m_aiAttackRange;
			tunedRangeMin = shared.m_aiAttackRangeMin;
			tunedAngle = shared.m_aiAttackMaxAngle;
			tunedInterval = shared.m_aiAttackInterval;
			if (melee)
			{
				shared.m_aiAttackRange = MeleeReach(attack);
				// A living viking swings as fast as a player does - again as soon as its last swing ends, which carries
				// on the weapon's combo - rather than waiting out the AI's interval between attacks
				if (IsLivingViking(humanoid))
				{
					shared.m_aiAttackInterval = 0f;
				}
				return;
			}
			shared.m_aiAttackRange = RangedAttackRange;
			shared.m_aiAttackRangeMin = 0f;
			shared.m_aiAttackMaxAngle = RangedAttackAngle;
			shared.m_aiAttackInterval = (shared.m_attack.m_requiresReload ? ReloadAttackInterval : RangedAttackInterval);
		}

		/// <summary>
		/// Works out how near an enemy has to be, past its own radius, for a melee swing to reach it - the attack's range,
		/// how far a swing reaches, or for one that strikes all around a point ahead, like a sledge's, how far ahead that
		/// point is, so the blow lands on the enemy - less a margin (see MeleeReachShare), since the AI measures from the
		/// viking's middle and the swing from its hand
		/// </summary>
		/// <param name="attack">The weapon's attack</param>
		/// <returns>The reach, in meters</returns>
		private static float MeleeReach(Attack attack)
		{
			return(Mathf.Max(MeleeReachMin, attack.m_attackRange * MeleeReachShare));
		}

		/// <summary>
		/// Puts back the settings TuneWeaponAI changed
		/// </summary>
		public void RestoreWeaponAI()
		{
			if (tunedShared == null)
			{
				return;
			}
			tunedShared.m_aiAttackRange = tunedRange;
			tunedShared.m_aiAttackRangeMin = tunedRangeMin;
			tunedShared.m_aiAttackMaxAngle = tunedAngle;
			tunedShared.m_aiAttackInterval = tunedInterval;
			tunedShared = null;
		}
	}
}
