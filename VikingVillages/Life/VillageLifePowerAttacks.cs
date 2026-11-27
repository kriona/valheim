using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A viking's power attacks - when its melee blow at an enemy is its weapon's secondary attack (see
	/// PowerAttackRules)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// How far from an enemy's edge someone stands to count as at it, fighting it hand to hand, in meters
		/// </summary>
		private const float PowerFellowReach = 4f;

		/// <summary>
		/// When the viking can make its next power attack
		/// </summary>
		private float powerAttackAt;

		/// <summary>
		/// Checks whether the viking's next blow at an enemy is its weapon's power attack - one that's ready, within the
		/// power attack's reach, on an enemy that doesn't fly, when it's a smart move (see PowerAttackRules.Uses)
		/// </summary>
		/// <param name="target">The enemy it attacks</param>
		/// <returns>True when it uses its power attack</returns>
		public bool UsesPowerAttack(Character target)
		{
			if (!active || humanoid == null || target == null || target.IsDead() || target.IsFlying() || Time.time < powerAttackAt)
			{
				return(false);
			}
			ItemDrop.ItemData weapon = humanoid.GetCurrentWeapon();
			if (weapon == null || !weapon.HaveSecondaryAttack())
			{
				return(false);
			}
			Attack power = weapon.m_shared.m_secondaryAttack;
			PowerAttackRules.Kind kind = PowerAttackRules.Classify(weapon.m_shared.m_attack, power);
			if (kind == PowerAttackRules.Kind.None)
			{
				return(false);
			}
			float reach = power.m_attackRange + target.GetRadius();
			if (Utils.DistanceXZ(target.transform.position, transform.position) > reach)
			{
				return(false);
			}
			int enemies = 0;
			bool helped = false;
			bool staggerComing = false;
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(transform.position, Mathf.Max(reach, Utils.DistanceXZ(target.transform.position, transform.position) + target.GetRadius() + PowerFellowReach), near);
			foreach (Character other in near)
			{
				if (other == humanoid || other.IsDead())
				{
					continue;
				}
				if (BaseAI.IsEnemy(humanoid, other))
				{
					if (Utils.DistanceXZ(other.transform.position, transform.position) <= power.m_attackRange + other.GetRadius())
					{
						enemies++;
					}
					continue;
				}
				if (!other.IsPlayer() && !VikingCore.IsPlayerLikeViking(other))
				{
					continue;
				}
				if (Utils.DistanceXZ(other.transform.position, target.transform.position) - target.GetRadius() > PowerFellowReach)
				{
					continue;
				}
				helped = true;
				staggerComing |= StaggersWith(other);
			}
			return(PowerAttackRules.Uses(kind, PowerAttackRules.Wide(power.m_attackAngle), target.IsStaggering(), target.m_staggerDamageFactor > 0f, enemies, helped, staggerComing));
		}

		/// <summary>
		/// Notes a power attack the viking has started, so it waits as long as a player would for the stamina before
		/// its next (see PowerAttackRules.Rest), and counts it for the test log
		/// </summary>
		/// <param name="target">The enemy it attacks</param>
		public void PowerAttacked(Character target)
		{
			ItemDrop.ItemData weapon = humanoid.GetCurrentWeapon();
			if (weapon == null)
			{
				return;
			}
			Attack power = weapon.m_shared.m_secondaryAttack;
			powerAttackAt = Time.time + PowerAttackRules.Rest(power.m_attackStamina);
			PowerAttackRules.Kind kind = PowerAttackRules.Classify(weapon.m_shared.m_attack, power);
			bool staggering = (target != null && target.IsStaggering());
			string why = (staggering ? "on a staggering enemy" : (((kind & PowerAttackRules.Kind.Staggers) != 0) ? "to stagger" : "to hit hard"));
			TestLog.Count("Fights", "power attacks " + why);
			TestLog.Count("Fights", "power attacks with " + weapon.m_shared.m_skillType);
		}

		/// <summary>
		/// Checks whether a player or viking holds a weapon whose power attack staggers - by the weapon in their right
		/// hand that their ZDO shows, known on every client
		/// </summary>
		/// <param name="other">The player or viking</param>
		/// <returns>True when they do</returns>
		private static bool StaggersWith(Character other)
		{
			ZDO zdo = ((other.m_nview != null && other.m_nview.IsValid()) ? other.m_nview.GetZDO() : null);
			int hash = ((zdo != null) ? zdo.GetInt(ZDOVars.s_rightItem, 0) : 0);
			GameObject prefab = ((hash != 0 && ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(hash) : null);
			ItemDrop item = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
			if (item == null)
			{
				return(false);
			}
			ItemDrop.ItemData.SharedData shared = item.m_itemData.m_shared;
			return((PowerAttackRules.Classify(shared.m_attack, shared.m_secondaryAttack) & PowerAttackRules.Kind.Staggers) != 0);
		}
	}
}
