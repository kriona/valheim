using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Silver against the dead, with Zombie Apocalypse - the silver-lined weapons and silver arrows a living or dvergr
	/// viking starts with, and which silver arrows hurt the undead more than its own
	/// </summary>
	/// <remarks>
	/// Zombie Apocalypse lines the weapons and decides which can be lined (see ZombieLink.LiningRecipe), so nothing is
	/// lined without it. Silver arrows are any arrows that deal spirit damage
	/// </remarks>
	internal static class VikingSilver
	{
		/// <summary>
		/// How many silver arrows an archer starts with, and restocks up to
		/// </summary>
		public const int ArrowStack = 50;

		/// <summary>
		/// The chance that each weapon a viking of a biome starts with is silver-lined, and that an archer of it starts
		/// with silver arrows - a biome before the Ashlands that isn't listed never does, and from the Ashlands on, one
		/// always does
		/// </summary>
		internal static readonly Dictionary<Heightmap.Biome, float> LiningChances = new Dictionary<Heightmap.Biome, float>()
		{
			{ Heightmap.Biome.Mountain, 0.25f },
			{ Heightmap.Biome.Plains, 0.5f },
			{ Heightmap.Biome.Mistlands, 0.75f }
		};

		/// <summary>
		/// Gets the chance that each piece of silver gear a viking of a biome could start with is its own (see
		/// LiningChances)
		/// </summary>
		/// <param name="index">The index of its biome, by VikingGear's order</param>
		/// <returns>The chance, from 0 to 1</returns>
		internal static float LiningChance(int index)
		{
			if (index >= VikingGear.IndexOf(Heightmap.Biome.AshLands))
			{
				return(1f);
			}
			return(LiningChances.TryGetValue(VikingGear.Get(index).Biome, out float chance) ? chance : 0f);
		}

		/// <summary>
		/// Lines each weapon a new viking starts with in silver, and gives an archer whose arrows deal no spirit damage
		/// ArrowStack silver arrows when they'd hurt the undead more (see SilverArrowsFor), each by its biome's
		/// LiningChance, while Zombie Apocalypse is running
		/// </summary>
		/// <param name="gear">The gear it starts with</param>
		/// <param name="index">The index of its biome, by VikingGear's order</param>
		public static void Arm(Inventory gear, int index)
		{
			float chance = LiningChance(index);
			if (chance <= 0f || !ZombieLink.Active())
			{
				return;
			}
			ItemDrop.ItemData bow = null;
			foreach (ItemDrop.ItemData item in gear.GetAllItems())
			{
				if (!item.IsWeapon() || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch)
				{
					continue;
				}
				if (VikingCore.IsRanged(item))
				{
					bow = item;
				}
				if (Random.value < chance)
				{
					ZombieLink.Line(item);
				}
			}
			if (bow == null || Random.value >= chance)
			{
				return;
			}
			ItemDrop.ItemData own = BestArrows(gear, bow.m_shared.m_ammoType);
			string prefab = SilverArrowsFor(bow, own);
			ItemDrop.ItemData arrows = ((prefab != null) ? VikingGear.MakeItem(prefab, 1, ArrowStack) : null);
			if (arrows != null)
			{
				gear.AddItem(arrows);
			}
		}

		/// <summary>
		/// Checks whether an item is silver arrows - ammo that deals spirit damage
		/// </summary>
		/// <param name="item">The item, or null</param>
		/// <returns>True when it is</returns>
		public static bool IsSilverArrows(ItemDrop.ItemData item)
		{
			return(item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo && item.GetDamage().m_spirit > 0f);
		}

		/// <summary>
		/// Finds the arrows in an inventory of an ammo type that deal the most damage, leaving out silver arrows
		/// </summary>
		/// <param name="inventory">The inventory</param>
		/// <param name="ammoType">The ammo type</param>
		/// <returns>The arrows, or null when it has none</returns>
		private static ItemDrop.ItemData BestArrows(Inventory inventory, string ammoType)
		{
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo || item.m_shared.m_ammoType != ammoType || IsSilverArrows(item))
				{
					continue;
				}
				if (best == null || item.GetDamage().GetTotalDamage() > best.GetDamage().GetTotalDamage())
				{
					best = item;
				}
			}
			return(best);
		}

		/// <summary>
		/// Finds the silver arrows of the game that a bow hurts the undead most with, when that's more than it hurts
		/// them with arrows of its own - through the resistances Zombie Apocalypse gives zombies of creatures that resist
		/// nothing
		/// </summary>
		/// <param name="bow">The bow</param>
		/// <param name="own">Its own arrows, or null for none</param>
		/// <returns>The silver arrows' prefab name, or null when none hurt the undead more</returns>
		public static string SilverArrowsFor(ItemDrop.ItemData bow, ItemDrop.ItemData own)
		{
			string ammoType = bow.m_shared.m_ammoType;
			if (string.IsNullOrEmpty(ammoType) || ObjectDB.instance == null)
			{
				return(null);
			}
			HitData.DamageModifiers undead = ZombieLink.UndeadModifiers(new HitData.DamageModifiers());
			HitData.DamageTypes weapon = bow.GetDamage();
			HitData.DamageTypes ownDamage = ((own != null) ? own.GetDamage() : new HitData.DamageTypes());
			string best = null;
			HitData.DamageTypes bestDamage = ownDamage;
			foreach (GameObject prefab in ObjectDB.instance.m_items)
			{
				ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
				ItemDrop.ItemData item = ((drop != null) ? drop.m_itemData : null);
				if (item == null || item.m_shared.m_ammoType != ammoType || !IsSilverArrows(item))
				{
					continue;
				}
				HitData.DamageTypes damage = item.GetDamage();
				if (BeatsUndead(weapon, bestDamage, damage, undead))
				{
					best = prefab.name;
					bestDamage = damage;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether a weapon hurts the undead more with silver arrows than with other arrows
		/// </summary>
		/// <param name="weapon">The weapon's damage</param>
		/// <param name="own">The other arrows' damage</param>
		/// <param name="silver">The silver arrows' damage</param>
		/// <param name="undead">How the undead take damage</param>
		/// <returns>True when the silver arrows hurt them more</returns>
		internal static bool BeatsUndead(HitData.DamageTypes weapon, HitData.DamageTypes own, HitData.DamageTypes silver, HitData.DamageModifiers undead)
		{
			HitData.DamageTypes withOwn = weapon;
			withOwn.Add(own);
			HitData.DamageTypes withSilver = weapon;
			withSilver.Add(silver);
			return(Hurt(withSilver, undead) > Hurt(withOwn, undead));
		}

		/// <summary>
		/// Adds up the damage a creature takes from a hit, through its resistances the way the game applies them
		/// </summary>
		/// <param name="damage">The hit's damage</param>
		/// <param name="modifiers">The creature's resistances</param>
		/// <returns>The damage it takes, leaving out chopping and mining</returns>
		internal static float Hurt(HitData.DamageTypes damage, HitData.DamageModifiers modifiers)
		{
			HitData hit = new HitData();
			hit.m_damage = damage;
			hit.ApplyResistance(modifiers, out HitData.DamageModifier _);
			HitData.DamageTypes taken = hit.m_damage;
			return(taken.m_damage + taken.GetTotalPhysicalDamage() + taken.GetTotalElementalDamage() + taken.m_poison + taken.m_spirit);
		}
	}
}
