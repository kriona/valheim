using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// A viking's armor - how much it wears, reducing the hits it takes the way a player's does, and wearing it down
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Works out how much harder the viking hits than a player of its skill with what's in its hands - its fists by
		/// its biome's punch, and a viking of the world's melee weapons by its biome's MeleeScale
		/// </summary>
		/// <returns>The multiplier for the hit's damage</returns>
		public float DamageScale()
		{
			ItemDrop.ItemData weapon = CurrentWeapon();
			BiomeGear gear = VikingGear.Get(BiomeIndex());
			if (weapon == null)
			{
				ItemDrop unarmed = humanoid.m_unarmedWeapon;
				float baseDamage = ((unarmed != null) ? unarmed.m_itemData.m_shared.m_damages.GetTotalPhysicalDamage() : 0f);
				return((baseDamage > 0f) ? gear.Unarmed / baseDamage : 1f);
			}
			bool ranged = (weapon.m_shared.m_attack != null && weapon.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile);
			return((GetData() != null && !data.Tombstone && !ranged) ? gear.MeleeScale : 1f);
		}

		/// <summary>
		/// Adds up the armor of the pieces the viking wears, with what its effects add, the way a player's adds up - all
		/// of it for a living or dvergr viking, and the config's ZombieArmorEffectiveness percent of it for a zombie
		/// viking
		/// </summary>
		/// <returns>Its armor</returns>
		public float BodyArmor()
		{
			float armor = 0f;
			foreach (ItemDrop.ItemData piece in WornArmor())
			{
				armor += piece.GetArmor();
			}
			humanoid.GetSEMan().ApplyArmorMods(ref armor);
			float effectiveness = (IsLivingOrDvergr(humanoid) ? 100f : VikingSettings.Get(VikingSettings.ZombieArmorEffectiveness, 100));
			return(armor * effectiveness / 100f);
		}

		/// <summary>
		/// Lists the armor pieces the viking wears
		/// </summary>
		/// <returns>Its chest, legs, helmet and cape, those it has on</returns>
		private List<ItemDrop.ItemData> WornArmor()
		{
			List<ItemDrop.ItemData> pieces = new List<ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData piece in new ItemDrop.ItemData[] { humanoid.m_chestItem, humanoid.m_legItem, humanoid.m_helmetItem, humanoid.m_shoulderItem })
			{
				if (piece != null)
				{
					pieces.Add(piece);
				}
			}
			return(pieces);
		}

		/// <summary>
		/// Wears down a random piece of the viking's armor by the damage a hit has left after its armor, taking it off
		/// when it breaks - the same as a player's armor, which stays on broken
		/// </summary>
		/// <param name="hit">The hit, after resistances and armor</param>
		public void WearArmor(HitData hit)
		{
			List<ItemDrop.ItemData> pieces = WornArmor();
			if (pieces.Count == 0)
			{
				return;
			}
			float damage = hit.GetTotalPhysicalDamage() + hit.GetTotalElementalDamage();
			if (damage <= 0f)
			{
				return;
			}
			ItemDrop.ItemData piece = pieces[Random.Range(0, pieces.Count)];
			if (!piece.m_shared.m_useDurability)
			{
				return;
			}
			piece.m_durability = Mathf.Max(0f, piece.m_durability - damage);
			if (piece.m_durability <= 0f)
			{
				humanoid.UnequipItem(piece, false);
			}
		}

		/// <summary>
		/// Adds the resistances of the viking's armor to its own, the same as a player's
		/// </summary>
		/// <param name="mods">Its damage modifiers</param>
		public void ApplyArmorMods(ref HitData.DamageModifiers mods)
		{
			foreach (ItemDrop.ItemData piece in WornArmor())
			{
				mods.Apply(piece.m_shared.m_damageModifiers);
			}
		}

		/// <summary>
		/// Takes off the viking's shield once it has broken
		/// </summary>
		private void DropBrokenShield()
		{
			ItemDrop.ItemData left = humanoid.m_leftItem;
			if (left != null && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield && left.m_shared.m_useDurability && left.m_durability <= 0f)
			{
				humanoid.UnequipItem(left, false);
			}
		}
	}
}
