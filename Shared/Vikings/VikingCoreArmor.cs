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
		/// its biome's punch, and a zombie viking of the world's melee weapons by its biome's MeleeScale (see
		/// GetsMeleeScale)
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
			return(MeleeScaled(ranged) ? gear.MeleeScale : 1f);
		}

		/// <summary>
		/// Checks whether the viking's hit with a weapon is scaled by its biome's MeleeScale (see GetsMeleeScale)
		/// </summary>
		/// <param name="ranged">Whether the weapon shoots</param>
		/// <returns>True when it is</returns>
		private bool MeleeScaled(bool ranged)
		{
			bool hasData = (GetData() != null);
			return(GetsMeleeScale(hasData, hasData && data.Tombstone, IsLivingOrDvergr(humanoid), ranged));
		}

		/// <summary>
		/// Checks whether a viking's hit with a weapon is scaled by its biome's MeleeScale - only a zombie viking of the
		/// world's hand to hand, so it hits about as hard as its biome's creatures. A living or dvergr viking hits like a
		/// player of its skill, a tombstone viking like the player it rose from, a shot isn't scaled, and a viking with
		/// no data yet has no biome to scale by
		/// </summary>
		/// <param name="hasData">Whether the viking has its data yet</param>
		/// <param name="tombstone">Whether it's a tombstone viking</param>
		/// <param name="livingOrDvergr">Whether it's a living or dvergr viking</param>
		/// <param name="ranged">Whether the weapon shoots</param>
		/// <returns>True when it's scaled</returns>
		internal static bool GetsMeleeScale(bool hasData, bool tombstone, bool livingOrDvergr, bool ranged)
		{
			return(hasData && !tombstone && !livingOrDvergr && !ranged);
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
