using HarmonyLib;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Makes a zombie animal (e.g., a deer or hare) hunt its enemies and ram them, since animals have no attacks
	/// </summary>
	/// <remarks>
	/// An animal's AnimalAI only flees, so ZombieChargePatch hands a zombie animal's update to this instead. It looks
	/// for the nearest enemy it can sense, runs at it and, once close enough, rams it for AnimalDamage blunt damage
	/// with a knockback. After each ram it runs off until it's RetreatDistance away, or for a few seconds when
	/// something's in its way, then turns and charges again, like a boar. It only thinks on the creature's owner
	/// </remarks>
	internal class ZombieCharge : MonoBehaviour
	{
		/// <summary>
		/// Seconds between looking for a closer enemy
		/// </summary>
		private const float TargetInterval = 1f;

		/// <summary>
		/// Seconds between rams
		/// </summary>
		private const float RamInterval = 1.5f;

		/// <summary>
		/// How far past touching an enemy a ram still reaches
		/// </summary>
		private const float RamReach = 0.6f;

		/// <summary>
		/// How hard a ram pushes the enemy back
		/// </summary>
		private const float RamPushForce = 40f;

		/// <summary>
		/// The extra damage of each star level beyond the first, as a fraction of the damage
		/// </summary>
		private const float DamagePerStar = 0.5f;

		/// <summary>
		/// How far an enemy can get before the zombie loses interest
		/// </summary>
		private const float GiveUpDistance = 60f;

		/// <summary>
		/// How far the zombie runs from its enemy after a ram before it turns to charge again
		/// </summary>
		private const float RetreatDistance = 10f;

		/// <summary>
		/// The longest it runs off for, in seconds, when it can't get RetreatDistance away - picked at random between
		/// these for each ram
		/// </summary>
		private const float MinRetreatTime = 2f;
		private const float MaxRetreatTime = 3.5f;

		private Character character;
		private AnimalAI ai;
		private Character target;
		private float targetTimer;
		private float ramTimer;

		/// <summary>
		/// Seconds left running off after a ram, or 0 or less when it's charging
		/// </summary>
		private float retreatTimer;

		/// <summary>
		/// Finds the zombie's creature and AI
		/// </summary>
		private void Awake()
		{
			character = GetComponent<Character>();
			ai = GetComponent<AnimalAI>();
		}

		/// <summary>
		/// Sets the zombie on an enemy, sticking to it for a while before looking for the nearest again
		/// </summary>
		/// <param name="enemy">The enemy to go after</param>
		/// <param name="focusTime">Seconds before it looks for the nearest enemy again</param>
		public void SetTarget(Character enemy, float focusTime)
		{
			target = enemy;
			targetTimer = focusTime;
		}

		/// <summary>
		/// Hunts and rams the nearest enemy, or wanders when there's none, on the owner
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		public void UpdateAI(float dt)
		{
			targetTimer -= dt;
			ramTimer -= dt;
			if (targetTimer <= 0f)
			{
				targetTimer = TargetInterval;
				Character enemy = ai.FindEnemy();
				if (enemy != null)
				{
					target = enemy;
				}
			}
			if (target != null && (target.IsDead() || Vector3.Distance(target.transform.position, transform.position) > GiveUpDistance))
			{
				target = null;
			}
			// Kept in the animal's own target too, where other mods (e.g., Creature Marker) look for it
			ai.m_target = target;
			if (target == null)
			{
				ai.SetTargetInfo(ZDOID.None);
				ai.IdleMovement(dt);
				return;
			}
			ai.SetTargetInfo(target.GetZDOID());
			ai.SetAlerted(true);
			target.OnTargeted(true, true);
			if (retreatTimer > 0f)
			{
				retreatTimer -= dt;
				if (Vector3.Distance(target.transform.position, transform.position) < RetreatDistance)
				{
					ai.Flee(dt, target.transform.position);
					return;
				}
				retreatTimer = 0f;
			}
			float reach = character.GetRadius() + target.GetRadius() + RamReach;
			Vector3 toTarget = target.transform.position - transform.position;
			toTarget.y = 0f;
			if (toTarget.magnitude > reach)
			{
				ai.MoveTo(dt, target.transform.position, reach * 0.5f, true);
				return;
			}
			ai.StopMoving();
			ai.LookAt(target.GetCenterPoint());
			if (ramTimer <= 0f)
			{
				ramTimer = RamInterval;
				Ram(toTarget.normalized);
				retreatTimer = Random.Range(MinRetreatTime, MaxRetreatTime);
			}
		}

		/// <summary>
		/// Hits the target with blunt damage and a knockback
		/// </summary>
		/// <param name="direction">The flat direction from the zombie to the target</param>
		private void Ram(Vector3 direction)
		{
			HitData hit = new HitData();
			hit.m_damage.m_blunt = Plugin.AnimalDamage.Value * (1f + DamagePerStar * (character.GetLevel() - 1));
			hit.m_point = target.GetCenterPoint();
			hit.m_dir = direction;
			hit.m_pushForce = RamPushForce;
			hit.m_hitType = HitData.HitType.EnemyHit;
			hit.m_dodgeable = true;
			hit.m_blockable = true;
			hit.SetAttacker(character);
			target.Damage(hit);
		}
	}

	/// <summary>
	/// Runs a zombie animal's AI as a ZombieCharge, and holds it still while it gets up
	/// </summary>
	[HarmonyPatch(typeof(AnimalAI), nameof(AnimalAI.UpdateAI))]
	public static class ZombieChargePatch
	{
		/// <summary>
		/// Replaces a zombie animal's fleeing with hunting and ramming
		/// </summary>
		/// <param name="__instance">The AI updating</param>
		/// <param name="dt">Seconds since the last update</param>
		/// <param name="__result">Whether the AI handled the update</param>
		/// <returns>False to skip the animal's own AI for a zombie</returns>
		private static bool Prefix(AnimalAI __instance, float dt, ref bool __result)
		{
			ZombieCharge charge = __instance.GetComponent<ZombieCharge>();
			if (charge == null)
			{
				return(true);
			}
			__result = BaseUpdateAI(__instance, dt);
			if (!__result)
			{
				return(false);
			}
			if (RiseBlend.IsHeld(__instance))
			{
				__instance.StopMoving();
				return(false);
			}
			charge.UpdateAI(dt);
			return(false);
		}

		/// <summary>
		/// Runs the update every AI shares - the owner check, regeneration and timers - without the animal's own
		/// </summary>
		/// <param name="instance">The AI updating</param>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>False when this client doesn't own the creature, so it shouldn't think</returns>
		[HarmonyReversePatch]
		[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.UpdateAI))]
		private static bool BaseUpdateAI(BaseAI instance, float dt)
		{
			throw new System.NotImplementedException("Replaced by Harmony with BaseAI.UpdateAI");
		}
	}
}
