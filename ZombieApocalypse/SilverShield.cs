using Kriona.Vikings;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Burns the undead with a silver-lined shield's spirit damage when it holds their blow
	/// </summary>
	/// <remarks>
	/// When a block holds against a melee blow, the game pushes the attacker back with a hit of its own that deals no
	/// damage. While a lined shield blocks an undead attacker, that push carries the shield's spirit damage too, so it
	/// lands whenever the game's push does - not against arrows or spells, nor when the blocker runs out of stamina or
	/// is staggered. A parry that staggers the attacker makes it a critical hit. The hit is the blocker's, so a creature
	/// it kills stays dead and counts as the blocker's kill
	/// </remarks>
	internal static class SilverShield
	{
		/// <summary>
		/// While a lined shield blocks an undead attacker, the one blocking, the shield, the attacker and whether the
		/// block is a parry that staggers it - null between blocks
		/// </summary>
		private static Humanoid blocker;
		private static ItemDrop.ItemData shield;
		private static Character attacker;
		private static bool critical;

		/// <summary>
		/// Notes a block that's starting, when it's a lined shield's against an undead attacker
		/// </summary>
		/// <param name="humanoid">The one blocking</param>
		/// <param name="hitBy">The attacker, or null</param>
		public static void StartBlock(Humanoid humanoid, Character hitBy)
		{
			EndBlock();
			if (hitBy == null || !ServerCheck.Active || !SpiritSpells.IsUndead(hitBy))
			{
				return;
			}
			ItemDrop.ItemData item = humanoid.GetCurrentBlocker();
			if (!SilverLining.IsShield(item) || !SilverLining.IsLined(item))
			{
				return;
			}
			blocker = humanoid;
			shield = item;
			attacker = hitBy;
			critical = (IsParry(item, humanoid.m_blockTimer) && hitBy.m_staggerWhenBlocked);
		}

		/// <summary>
		/// Checks whether a block is a parry, the way Humanoid.BlockAttack does
		/// </summary>
		/// <param name="item">The shield</param>
		/// <param name="blockTimer">Seconds since the shield was raised, or -1 once a block has used up the parry</param>
		/// <returns>True when the shield can parry and was raised in time</returns>
		private static bool IsParry(ItemDrop.ItemData item, float blockTimer)
		{
			return(item.m_shared.m_timedBlockBonus > 1f && blockTimer != -1f && blockTimer < 0.25f);
		}

		/// <summary>
		/// Forgets the block once it's over
		/// </summary>
		public static void EndBlock()
		{
			blocker = null;
			shield = null;
			attacker = null;
			critical = false;
		}

		/// <summary>
		/// Adds the shield's spirit damage to the game's push back at the attacker, when the hit is that push
		/// </summary>
		/// <param name="target">The creature being hit</param>
		/// <param name="hit">The hit, before it's sent to the creature's owner</param>
		public static void Deflect(Character target, HitData hit)
		{
			if (blocker == null || target != attacker || hit.GetAttacker() != null || hit.GetTotalDamage() > 0f)
			{
				return;
			}
			hit.m_damage.m_spirit += SilverLining.ShieldSpirit(shield, critical);
			hit.m_point = target.GetCenterPoint();
			hit.m_skill = Skills.SkillType.Blocking;
			hit.m_hitType = ((blocker is Player) ? HitData.HitType.PlayerHit : HitData.HitType.EnemyHit);
			hit.SetAttacker(blocker);
			if (critical)
			{
				target.m_critHitEffects.Create(hit.m_point, Quaternion.identity, target.transform, 1f, -1, blocker.GetZDOID());
			}
			EndBlock();
		}
	}
}
