using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Who a viking fights - the enemies and threats near it, the grudges it holds, the alarms and calls for help it
	/// answers, and those it leaves alone
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds the nearest enemy within EnemyRange the viking can hurt - not a zombie still getting up
		/// </summary>
		/// <param name="fightingBack">True to leave out prey that only runs, like a deer (see FightsBack)</param>
		/// <returns>The enemy, or null when there's none</returns>
		private Character NearestHurtableEnemy(bool fightingBack = false)
		{
			enemiesNear.Clear();
			Character.GetCharactersInRange(transform.position, EnemyRange, enemiesNear);
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Character other in enemiesNear)
			{
				if (other == null || other == humanoid || other.IsDead() || !BaseAI.IsEnemy(humanoid, other) || ZombieState.IsRising(other) || (fightingBack && !FightsBack(other)))
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
		/// Has the viking stand ready by a zombie still getting up, facing it with its weapon drawn - a melee fighter
		/// just out of its reach, an archer where it stands - so it strikes as soon as the zombie can be hurt
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zombie">The zombie</param>
		private void WaitForRise(float dt, Character zombie)
		{
			mover = "waiting for a zombie to get up";
			float distance = Utils.DistanceXZ(zombie.transform.position, transform.position);
			if (viking != null && !viking.IsArcher() && distance > RiseReadyDistance + RiseReadySlack)
			{
				Go(dt, zombie.transform.position, RiseReadyDistance, true);
				moveFailed = false;
				return;
			}
			ai.StopMoving();
			ai.LookAt(zombie.GetCenterPoint());
		}

		/// <summary>
		/// Checks whether an enemy of the viking's is within a range of a point, leaving out those a check passes
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="range">The range, in meters</param>
		/// <param name="ignored">True for an enemy that doesn't count</param>
		/// <returns>True when one is</returns>
		private bool EnemyNear(Vector3 point, float range, System.Predicate<Character> ignored)
		{
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(point, range, near);
			return(near.Exists((Character other) => other != humanoid && !other.IsDead() && BaseAI.IsEnemy(humanoid, other) && !ignored(other)));
		}

		/// <summary>
		/// Checks whether an enemy of the viking's is within a range of a point
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="range">The range, in meters</param>
		/// <returns>True when one is</returns>
		private bool EnemyNear(Vector3 point, float range)
		{
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(point, range, near);
			return(near.Exists((Character other) => other != humanoid && !other.IsDead() && BaseAI.IsEnemy(humanoid, other)));
		}

		/// <summary>
		/// Checks whether there's danger within ForageSafety of a point for a viking out of the village - any enemy of
		/// its, except a single greyling with no other enemy near, which it can take on alone
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when there is</returns>
		private bool DangerNear(Vector3 point)
		{
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(point, ForageSafety, near);
			int enemies = 0;
			bool greyling = false;
			foreach (Character other in near)
			{
				if (other == humanoid || other.IsDead() || !BaseAI.IsEnemy(humanoid, other))
				{
					continue;
				}
				enemies++;
				greyling = IsHuntedMonster(other);
			}
			return(enemies > 1 || (enemies == 1 && !greyling));
		}

		/// <summary>
		/// Checks whether an enemy is a single greyling with no other enemy near the viking, which it stays and fights
		/// rather than running home from
		/// </summary>
		/// <param name="enemy">The enemy, or null</param>
		/// <returns>True when it is</returns>
		private bool LoneGreyling(Character enemy)
		{
			return(IsHuntedMonster(enemy) && !DangerNear(transform.position));
		}

		/// <summary>
		/// Forgets an enemy the viking has picked that's too far past the village's edge to go after - beyond its
		/// leash, beyond GrudgeRange for one that hurt it or someone it's defending, or for a hunter's prey, beyond where
		/// it hunts and chases - one out in water too deep to fight it in (see OutOfReach), and the village's livestock
		/// unless it's the animal the viking is slaughtering
		/// </summary>
		public void LeashTarget()
		{
			Character target = ai.m_targetCreature;
			if (target == null || village == null)
			{
				return;
			}
			if (FarmSpawns.IsLivestock(target) && !Hunts(humanoid, target))
			{
				ai.m_targetCreature = null;
				return;
			}
			float leash = (HoldsGrudge(target) ? GrudgeRange : LeashRange);
			if (job is HuntJob hunt && hunt.Allows(target))
			{
				leash = Mathf.Max(leash, HuntReach + HuntChase);
			}
			if (village.DistanceOutside(target.transform.position) > leash || OutOfReach(target))
			{
				ai.m_targetCreature = null;
			}
		}

		/// <summary>
		/// Checks whether an enemy is out in water too deep for the viking to fight it hand to hand - swimming, or below
		/// the water level where the ground is more than WadeDepth under it - so it stays on dry land rather than swim
		/// after it. An archer still shoots at it from the shore
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it is</returns>
		public bool OutOfReach(Character enemy)
		{
			return(!HasBow(humanoid) && InDeepWater(enemy));
		}

		/// <summary>
		/// Checks whether an enemy is out in water too deep to fight it in hand to hand - swimming, or below the water
		/// level where the ground is more than WadeDepth under it
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it is</returns>
		private static bool InDeepWater(Character enemy)
		{
			if (enemy == null || ZoneSystem.instance == null)
			{
				return(false);
			}
			if (enemy.IsSwimming())
			{
				return(true);
			}
			float water = ZoneSystem.instance.m_waterLevel;
			Vector3 position = enemy.transform.position;
			return(position.y < water && ZoneSystem.instance.GetGroundHeight(position) < water - WadeDepth);
		}

		/// <summary>
		/// Notes an enemy that hurt the viking, so it stops what it's doing and chases them past its leash
		/// </summary>
		/// <param name="attacker">Who hurt it, or null</param>
		public void OnHurt(Character attacker)
		{
			if (!active)
			{
				return;
			}
			Interrupt("being hurt");
			if (attacker != null && !attacker.IsDead() && BaseAI.IsEnemy(humanoid, attacker))
			{
				grudge = attacker;
				grudgeUntil = Time.time + GrudgeTime;
			}
		}

		/// <summary>
		/// Rushes the viking to help when someone near it is attacked - through a raid, anywhere in its village (see
		/// Hears)
		/// </summary>
		/// <param name="where">Where they were attacked</param>
		/// <param name="attacker">Who attacked them</param>
		public void OnDistress(Vector3 where, Character attacker)
		{
			if (!active || nview == null || !nview.IsValid() || !nview.IsOwner() || humanoid.IsDead())
			{
				return;
			}
			if (attacker == humanoid || !Hears(where) || !BaseAI.IsEnemy(humanoid, attacker))
			{
				return;
			}
			Character current = ai.m_targetCreature;
			if (current != null && current != attacker && Vector3.Distance(current.transform.position, transform.position) < Vector3.Distance(attacker.transform.position, transform.position))
			{
				return;
			}
			Interrupt("helping one attacked");
			grudge = attacker;
			grudgeUntil = Time.time + GrudgeTime;
			Engage(attacker);
		}

		/// <summary>
		/// Checks whether a fight or a call for help at a place reaches the viking - within DefendRange of it, or through
		/// a raid it's ready for, anywhere in its village or within RaidCallReach past its edge, so the whole village
		/// fights a raid together rather than leaving one of its own to it at the edge
		/// </summary>
		/// <param name="where">Where the fight or call is</param>
		/// <returns>True when it reaches the viking</returns>
		private bool Hears(Vector3 where)
		{
			if (Vector3.Distance(where, transform.position) <= DefendRange)
			{
				return(true);
			}
			return(RaidReady() && village.DistanceOutside(where) <= RaidCallReach);
		}

		/// <summary>
		/// Wakes the viking when its village's watch raises the alarm, and sends it after the enemy nearest the village
		/// </summary>
		/// <param name="farm">The village's farm</param>
		public void OnAlarm(Vector3 farm)
		{
			if (!active || village == null || Utils.DistanceXZ(village.Farm, farm) > 1f || nview == null || !nview.IsOwner())
			{
				return;
			}
			Interrupt("an alarm");
			SetDulled(false);
			Character enemy = NearestEnemy(AlarmRange);
			if (enemy != null)
			{
				Engage(enemy);
			}
			else
			{
				ai.SetAlerted(true);
			}
		}

		/// <summary>
		/// Sends the viking after an enemy
		/// </summary>
		/// <param name="enemy">The enemy</param>
		private void Engage(Character enemy)
		{
			ai.m_targetCreature = enemy;
			ai.m_lastKnownTargetPos = enemy.transform.position;
			ai.m_beenAtLastPos = false;
			ai.m_targetStatic = null;
			ai.SetAlerted(true);
		}

		/// <summary>
		/// Has the night watch stop and roar at an enemy it has spotted near the village, calling out to wake the others
		/// </summary>
		/// <param name="enemy">The enemy</param>
		private void RaiseAlarm(Character enemy)
		{
			lastAlarm = Time.time;
			alarmUntil = Time.time + AlarmPose;
			ai.StopMoving();
			ai.LookAt(enemy.GetCenterPoint());
			VikingPoses.Roar(humanoid);
			VikingSpeech.SayFirst(humanoid, VikingSpeech.Alarm(enemy, VillageNames.Get(village)), VikingSpeech.AlarmTime, VikingSpeech.AlarmCull);
			calledOut[enemy.GetZDOID()] = Time.time;
			VillageSignals.SendAlarm(village);
		}

		/// <summary>
		/// Checks whether the viking is still after an enemy that hurt it or someone it's defending
		/// </summary>
		/// <param name="target">The enemy</param>
		/// <returns>True when it is</returns>
		private bool HoldsGrudge(Character target)
		{
			return(target != null && target == grudge && Time.time <= grudgeUntil);
		}

		/// <summary>
		/// Finds the nearest enemy of the viking within a distance of its village's edge
		/// </summary>
		/// <param name="reach">How far past the edge to look, in meters</param>
		/// <returns>The enemy, or null</returns>
		private Character NearestEnemy(float reach)
		{
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character == humanoid || character.IsDead() || !BaseAI.IsEnemy(humanoid, character) || character.IsPlayer())
				{
					continue;
				}
				if (village.DistanceOutside(character.transform.position) > reach)
				{
					continue;
				}
				float distance = Vector3.Distance(character.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = character;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Finds the viking this client owns nearest where one keeping watch for its village fell that saw it - awake,
		/// within WitnessRange with nothing in the way, not keeping watch and not having kept it tonight - to take its
		/// place
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="where">Where the viking keeping watch fell</param>
		/// <param name="dead">The dead viking's ZDO</param>
		/// <returns>The viking, or null when none saw it</returns>
		public static Character WatchWitness(Village village, Vector3 where, ZDOID dead)
		{
			if (witnessMask == 0)
			{
				witnessMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
			}
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (VillageLife life in All)
			{
				if (!life.active || life.village != village || life.humanoid == null || life.humanoid.IsDead() || !life.nview.IsOwner())
				{
					continue;
				}
				ZDOID id = life.nview.GetZDO().m_uid;
				if (id == dead || VillageWatch.IsWatch(village, id) || VillageWatch.HasWatched(village, id))
				{
					continue;
				}
				// One asleep, or resting with its senses dulled, sees nothing
				if (life.inBed || life.dulled || (life.job != null && life.job.Resting()))
				{
					continue;
				}
				Vector3 eye = life.humanoid.m_eye.position;
				float distance = Vector3.Distance(eye, where);
				if (distance > WitnessRange || distance >= nearestDistance || Physics.Linecast(eye, where, witnessMask, QueryTriggerInteraction.Ignore))
				{
					continue;
				}
				nearest = life.humanoid;
				nearestDistance = distance;
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether this viking is its village's night watch tonight
		/// </summary>
		/// <returns>True when it is</returns>
		private bool IsWatch()
		{
			return(village != null && (IsNight() || IsDusk()) && VillageWatch.IsWatch(village, nview.GetZDO().m_uid));
		}

		/// <summary>
		/// Checks whether the viking only picks fights with monsters that are a threat - any PC viking
		/// </summary>
		/// <returns>True when it does</returns>
		public bool PicksFightsWithThreats()
		{
			return(humanoid != null && VikingCore.IsPCViking(humanoid));
		}

		/// <summary>
		/// Checks whether a monster is a threat a PC viking goes after - one in its village, within ThreatRange of
		/// it, alerted, or after someone or something. A living animal that only ever runs, like a deer or a hare, is
		/// never one, even alerted as it runs from a hunter's arrows - only the prey the viking is hunting itself, or a
		/// zombie one, which comes after people instead. Neither is a leech that hasn't hurt anyone (see QuietMonsters)
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when it is</returns>
		public bool IsThreat(Character monster)
		{
			// Prey an archer shoots from afar is left to its hunt until the AI takes the fight over
			if (job is HuntJob hunt && hunt.Allows(monster))
			{
				return(!hunt.Snipes(monster));
			}
			if (!FightsBack(monster) || QuietMonsters.LeftAlone(monster))
			{
				return(false);
			}
			Vector3 position = monster.transform.position;
			if ((village != null && village.Contains(position)) || Utils.DistanceXZ(position, transform.position) <= ThreatRange)
			{
				return(true);
			}
			BaseAI other = monster.GetBaseAI();
			return(other != null && (other.IsAlerted() || other.GetTargetCreature() != null || (other is MonsterAI hunter && hunter.m_targetStatic != null)));
		}

		/// <summary>
		/// Checks whether a creature fights back - anything but a living animal that only ever runs, like a deer, a hare
		/// or a bird, whose AI never attacks. A zombie one does, since it comes after people instead
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <returns>True when it does</returns>
		private static bool FightsBack(Character creature)
		{
			return(!(creature.GetBaseAI() is AnimalAI) || ZombieState.IsZombie(creature));
		}

		/// <summary>
		/// Checks whether the viking is an archer, which slightly prefers enemy archers when picking which threat to go
		/// after (see NearestThreat)
		/// </summary>
		/// <returns>True when it is</returns>
		public bool PrefersArchers()
		{
			return(HasBow(humanoid));
		}

		/// <summary>
		/// Finds the nearest enemy the viking's AI can sense that's a threat, the way the AI finds its nearest enemy - for
		/// an archer, with enemy archers counted a little nearer than they are (see TargetDistance)
		/// </summary>
		/// <returns>The enemy, or null when there's none</returns>
		public Character NearestThreat()
		{
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			bool archer = PrefersArchers();
			foreach (Character other in Character.GetAllCharacters())
			{
				if (other == null || other.IsDead() || other.m_aiSkipTarget || !BaseAI.IsEnemy(humanoid, other))
				{
					continue;
				}
				BaseAI otherAI = other.GetBaseAI();
				if ((otherAI != null && otherAI.IsSleeping()) || !ai.CanSenseTarget(other) || !IsThreat(other) || OutOfReach(other))
				{
					continue;
				}
				float distance = Vector3.Distance(other.transform.position, transform.position);
				distance = TargetDistance(distance, archer && IsEnemyArcher(other));
				if (distance < nearestDistance)
				{
					nearest = other;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Works out how near a viking counts an enemy as when picking which to go after - an archer counts an enemy
		/// archer as ArcherTargetWeight of its distance, and every other enemy as its distance
		/// </summary>
		/// <param name="distance">How far the enemy is, in meters</param>
		/// <param name="preferred">Whether it's an enemy archer and the viking an archer</param>
		/// <returns>The distance it counts as, in meters</returns>
		internal static float TargetDistance(float distance, bool preferred)
		{
			return(preferred ? (distance * ArcherTargetWeight) : distance);
		}

		/// <summary>
		/// Checks whether an enemy is an archer - one holding a bow or crossbow that shoots, like a draugr or skeleton
		/// archer, or a zombie viking with a bow
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it is</returns>
		private static bool IsEnemyArcher(Character enemy)
		{
			ItemDrop.ItemData weapon = ((enemy is Humanoid armed) ? armed.GetCurrentWeapon() : null);
			if (weapon == null || weapon.m_shared.m_attack == null || weapon.m_shared.m_attack.m_attackType != Attack.AttackType.Projectile)
			{
				return(false);
			}
			Skills.SkillType skill = weapon.m_shared.m_skillType;
			return(weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow || skill == Skills.SkillType.Bows || skill == Skills.SkillType.Crossbows);
		}
	}
}
