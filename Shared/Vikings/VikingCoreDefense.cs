using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// Defending itself - raising its shield against attacks, better timed the more skilled it is, and turning on a
	/// monster diving at it
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Raises the viking's shield against an enemy's attack it sees starting - more often and closer to the moment of
		/// the hit the more skilled it is, but never every time or perfectly - and lowers it again after a moment
		/// </summary>
		private void UpdateBlock()
		{
			ItemDrop.ItemData left = humanoid.m_leftItem;
			bool hasShield = (left != null && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield);
			if (!hasShield || ai == null || humanoid.IsDead())
			{
				StopBlocking();
				enemyWasAttacking = false;
				return;
			}
			// Guarding against a deathsquito's dive, it holds the shield up until the sting is spent (see Guard)
			if (guarding)
			{
				humanoid.m_blocking = true;
				return;
			}
			float now = Time.time;
			if (blockAt >= 0f && now >= blockAt)
			{
				blockAt = -1f;
				humanoid.m_blocking = true;
				blockUntil = now + Random.Range(BlockHoldMin, BlockHoldMax);
			}
			if (blockUntil >= 0f && now >= blockUntil)
			{
				StopBlocking();
			}
			Character enemy = ai.m_targetCreature;
			bool attacking = (enemy != null && enemy.InAttack() && Vector3.Distance(enemy.transform.position, transform.position) <= ReactRange(enemy));
			if (attacking && !enemyWasAttacking && blockAt < 0f && blockUntil < 0f && !humanoid.InAttack())
			{
				float skill = Mathf.Clamp01(SkillLevel(Skills.SkillType.Blocking) / 100f);
				if (Random.value < Mathf.Lerp(BlockChanceMin, BlockChanceMax, skill))
				{
					float delay = Mathf.Lerp(BlockDelayMin, BlockDelayMax, skill) + Random.Range(-BlockDelayJitter, BlockDelayJitter);
					blockAt = now + Mathf.Max(0.05f, delay);
				}
			}
			enemyWasAttacking = attacking;
		}

		/// <summary>
		/// Works out how near an enemy's attack has to start for the viking to raise its shield
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>The distance - far for an enemy with a bow or other ranged weapon</returns>
		private static float ReactRange(Character enemy)
		{
			Humanoid armed = enemy as Humanoid;
			ItemDrop.ItemData weapon = ((armed != null) ? armed.GetCurrentWeapon() : null);
			bool ranged = (weapon != null && weapon.m_shared.m_attack != null && weapon.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile);
			return(ranged ? BlockRangedRange : BlockMeleeRange);
		}

		/// <summary>
		/// Lowers the viking's shield and forgets any block it was about to make
		/// </summary>
		private void StopBlocking()
		{
			humanoid.m_blocking = false;
			blockAt = -1f;
			blockUntil = -1f;
		}

		/// <summary>
		/// How a living viking meets a deathsquito diving at it - the diver's prefab, how near one after the viking is
		/// watched for, in meters, and how often, in seconds, how near it comes before the viking raises its shield and
		/// holds it, in meters, how long after its sting the viking has to strike back, in seconds, how fast it closes
		/// in on the viking to count as diving, in meters a second, and how long after it comes within reach a
		/// two-handed swing lands, in seconds
		/// </summary>
		private const string DiverPrefab = "Deathsquito";
		private const float DiverWatchRange = 15f;
		private const float DiverCheckInterval = 0.25f;
		private const float ShieldUpRange = 8f;
		private const float CounterWindow = 0.6f;
		private const float DiveSpeed = 3f;
		private const float SwingLate = 0.05f;

		/// <summary>
		/// How long a swing takes to land after it starts when the viking hasn't swung the weapon yet, in seconds, and
		/// how much each swing it sees moves what it remembers toward that swing's
		/// </summary>
		private const float SwingDelayDefault = 0.5f;
		private const float SwingDelayLearn = 0.3f;

		/// <summary>
		/// How long each weapon's swing takes to land after it starts, by the weapon's name, learned from living vikings'
		/// swings (see NoteSwing)
		/// </summary>
		private static readonly Dictionary<string, float> swingDelays = new Dictionary<string, float>();

		/// <summary>
		/// The deathsquito diving at the viking, whether it was stinging at the last update, whether the viking holds
		/// its shield up against it, until when it strikes back after a sting, in Time.time seconds, and when it next
		/// looks for one
		/// </summary>
		private Character diver;
		private bool diverWasStinging;
		private bool guarding;
		private float counterUntil = -1f;
		private float nextDiverCheck;

		/// <summary>
		/// Meets a deathsquito diving at a living viking, taking the AI's update over while it does - with a shield
		/// beside a one-handed weapon it raises the shield as the deathsquito comes within ShieldUpRange and holds it, then
		/// strikes back once the sting is spent, and with a two-handed weapon it stands its ground as the deathsquito
		/// dives and times its swing to land just after it comes within reach (see SwingNow). Its weapon is picked for
		/// this while one is after it (see DiveWeapon)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's meeting the dive, so the AI's own update is skipped</returns>
		public bool UpdateDiveDefense(float dt)
		{
			if (!IsLivingViking(humanoid) || ai == null || humanoid.IsDead() || humanoid.IsAttached() || GearHeld())
			{
				EndDiveDefense();
				return(false);
			}
			if (Time.time >= nextDiverCheck)
			{
				nextDiverCheck = Time.time + DiverCheckInterval;
				Character found = FindDiver();
				if (found != diver)
				{
					diver = found;
					guarding = false;
					counterUntil = -1f;
					EquipWeapon();
				}
			}
			if (diver == null || diver.IsDead())
			{
				EndDiveDefense();
				return(false);
			}
			ItemDrop.ItemData weapon = CurrentWeapon();
			if (weapon == null || IsRanged(weapon) || weapon.m_shared.m_attack == null)
			{
				guarding = false;
				return(false);
			}
			Vector3 center = diver.GetCenterPoint();
			Vector3 toward = humanoid.GetCenterPoint() - center;
			float distance = Mathf.Max(0f, toward.magnitude - diver.GetRadius());
			bool stinging = diver.InAttack();
			bool stung = (diverWasStinging && !stinging);
			diverWasStinging = stinging;
			float reach = MeleeReach(weapon.m_shared.m_attack);
			ItemDrop.ItemData left = humanoid.m_leftItem;
			if (left != null && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
			{
				return(Guard(center, distance, reach, stung));
			}
			if (weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon)
			{
				float closing = ((toward.sqrMagnitude > 0.0001f) ? Vector3.Dot(diver.GetVelocity(), toward.normalized) : 0f);
				return(TimeSwing(weapon, center, distance, reach, closing));
			}
			return(false);
		}

		/// <summary>
		/// Holds the viking's shield up facing the deathsquito while it's within ShieldUpRange, and once its sting is
		/// spent, lowers the shield and strikes back within CounterWindow while it's in reach - leaving the AI to go
		/// after it when it's out of reach by then
		/// </summary>
		/// <param name="center">The deathsquito's middle</param>
		/// <param name="distance">How far it is, past its size, in meters</param>
		/// <param name="reach">How far the viking's weapon reaches, in meters</param>
		/// <param name="stung">Whether its sting has just ended</param>
		/// <returns>True while the viking guards or strikes back, so the AI's own update is skipped</returns>
		private bool Guard(Vector3 center, float distance, float reach, bool stung)
		{
			if (stung && distance <= ShieldUpRange)
			{
				counterUntil = Time.time + CounterWindow;
			}
			if (counterUntil >= 0f)
			{
				guarding = false;
				StopBlocking();
				if (humanoid.InAttack())
				{
					counterUntil = -1f;
					return(true);
				}
				if (Time.time >= counterUntil || distance > reach)
				{
					counterUntil = ((Time.time >= counterUntil) ? -1f : counterUntil);
					return(false);
				}
				ai.StopMoving();
				ai.LookAt(center);
				if (humanoid.StartAttack(diver, false))
				{
					counterUntil = -1f;
				}
				return(true);
			}
			if (humanoid.InAttack())
			{
				return(true);
			}
			if (distance > ShieldUpRange)
			{
				guarding = false;
				return(false);
			}
			guarding = true;
			ai.StopMoving();
			ai.LookAt(center);
			humanoid.m_blocking = true;
			return(true);
		}

		/// <summary>
		/// Stands the viking's ground with a two-handed weapon facing a deathsquito diving at it - closing in at
		/// DiveSpeed or faster within DiverWatchRange, or hovering within reach - and swings so the blow lands just after
		/// it comes within reach (see SwingNow), leaving the AI to go after one that's neither
		/// </summary>
		/// <param name="weapon">The viking's weapon</param>
		/// <param name="center">The deathsquito's middle</param>
		/// <param name="distance">How far it is, past its size, in meters</param>
		/// <param name="reach">How far the weapon reaches, in meters</param>
		/// <param name="closing">How fast it's closing in on the viking, in meters a second</param>
		/// <returns>True while the viking stands its ground or swings, so the AI's own update is skipped</returns>
		private bool TimeSwing(ItemDrop.ItemData weapon, Vector3 center, float distance, float reach, float closing)
		{
			guarding = false;
			if (humanoid.InAttack())
			{
				return(true);
			}
			bool diving = (closing >= DiveSpeed && distance <= DiverWatchRange);
			if (!diving && distance > reach)
			{
				return(false);
			}
			ai.StopMoving();
			ai.LookAt(center);
			if (SwingNow(distance, (diving ? closing : 0f), reach, SwingDelay(weapon), SwingLate))
			{
				humanoid.StartAttack(diver, false);
			}
			return(true);
		}

		/// <summary>
		/// Checks whether to start a swing at something coming in so the blow lands a moment after it comes within reach
		/// - now when it's already within reach, and otherwise once it's due within reach no later than the swing would
		/// land, less a moment
		/// </summary>
		/// <param name="distance">How far it is, in meters</param>
		/// <param name="closing">How fast it's closing in, in meters a second</param>
		/// <param name="reach">How far the swing reaches, in meters</param>
		/// <param name="delay">How long the swing takes to land after it starts, in seconds</param>
		/// <param name="late">How long after it comes within reach the blow should land, in seconds</param>
		/// <returns>True to swing now</returns>
		internal static bool SwingNow(float distance, float closing, float reach, float delay, float late)
		{
			if (distance <= reach)
			{
				return(true);
			}
			if (closing <= 0f)
			{
				return(false);
			}
			return((distance - reach) / closing <= delay - late);
		}

		/// <summary>
		/// Finds how long a weapon's swing takes to land after it starts, from living vikings' swings with it (see
		/// NoteSwing), or SwingDelayDefault before any
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <returns>The time, in seconds</returns>
		private static float SwingDelay(ItemDrop.ItemData weapon)
		{
			return(swingDelays.TryGetValue(weapon.m_shared.m_name, out float delay) ? delay : SwingDelayDefault);
		}

		/// <summary>
		/// Notes how long a living viking's swing took to land after it started, moving what's remembered for the weapon
		/// SwingDelayLearn of the way toward it
		/// </summary>
		/// <param name="weapon">The weapon swung</param>
		/// <param name="delay">How long the swing took to land, in seconds</param>
		public static void NoteSwing(ItemDrop.ItemData weapon, float delay)
		{
			if (weapon == null || delay <= 0f)
			{
				return;
			}
			string name = weapon.m_shared.m_name;
			swingDelays[name] = (swingDelays.TryGetValue(name, out float known) ? Mathf.Lerp(known, delay, SwingDelayLearn) : delay);
		}

		/// <summary>
		/// Finds the nearest deathsquito within DiverWatchRange that's after the viking
		/// </summary>
		/// <returns>The deathsquito, or null when there's none</returns>
		private Character FindDiver()
		{
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(transform.position, DiverWatchRange, near);
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Character other in near)
			{
				if (other == null || other.IsDead() || Utils.GetPrefabName(other.gameObject) != DiverPrefab)
				{
					continue;
				}
				BaseAI otherAI = other.GetBaseAI();
				if (otherAI == null || otherAI.GetTargetCreature() != humanoid)
				{
					continue;
				}
				float distance = Vector3.Distance(other.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = other;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Picks the weapon a living viking meets a deathsquito with - its best one-handed melee weapon while it has a
		/// shield that works, so the shield goes beside it, or else its best two-handed melee weapon
		/// </summary>
		/// <returns>The weapon, or null when it has neither</returns>
		private ItemDrop.ItemData DiveWeapon()
		{
			if (ShieldWorks())
			{
				ItemDrop.ItemData oneHanded = BestMelee(ItemDrop.ItemData.ItemType.OneHandedWeapon, diver);
				if (oneHanded != null)
				{
					return(oneHanded);
				}
			}
			return(BestMelee(ItemDrop.ItemData.ItemType.TwoHandedWeapon, diver));
		}

		/// <summary>
		/// Finds the viking's melee weapon of one kind that hurts a creature the most
		/// </summary>
		/// <param name="type">The kind of weapon, like one-handed</param>
		/// <param name="target">The creature</param>
		/// <returns>The weapon, or null when it has none of that kind</returns>
		private ItemDrop.ItemData BestMelee(ItemDrop.ItemData.ItemType type, Character target)
		{
			ItemDrop.ItemData best = null;
			float bestDamage = 0f;
			HitData.DamageModifiers? resistances = Resistances(target);
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item.m_shared.m_itemType != type || !CanUse(item) || IsRanged(item))
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
		/// Stops meeting a deathsquito's dive - lowering the shield it held up against one and picking its weapon for
		/// the fight in front of it again
		/// </summary>
		private void EndDiveDefense()
		{
			bool had = (diver != null);
			diver = null;
			counterUntil = -1f;
			diverWasStinging = false;
			if (guarding)
			{
				guarding = false;
				StopBlocking();
			}
			if (had)
			{
				EquipWeapon();
			}
		}
	}
}
