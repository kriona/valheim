using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A viking weighing up a fight - running from one it can't win, hanging back from enemies that shoot, flanking
	/// an enemy a fellow is fighting and shouting for help
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Weighs up a fight and keeps the viking out of one it shouldn't take on where it is - away from the village's
		/// core it runs back to the village's middle for help, and at home, badly hurt, it hangs back and lets the others
		/// fight
		/// </summary>
		/// <remarks>
		/// Away from home it runs back when it's foraging or getting wood or loot and an enemy comes for it, when it's
		/// alone and attacked, when FightOutnumbered or more enemies are on it, when it's badly hurt (see BadlyHurtLine),
		/// or when a viking with it is - though a single greyling with no other enemy near it stays and fights. A viking that ran back for being alone turns to fight as soon as another viking is with it, and every
		/// other once it's home with another viking of its village within RegroupRange - home with no one beside it, it
		/// runs on to the nearest of them, and only turns to fight alone when none of them is left. At home, a badly hurt viking hangs back while its fellows can take the fight -
		/// unless there are more enemies than healthy vikings near, or none of them are healthy - until it's back
		/// RecoveredMargin above its badly hurt line. Hanging back, it runs from any enemy coming after it, and otherwise circles behind an enemy busy
		/// with someone else to strike it there. A viking running home, hanging back badly hurt or fighting a monster too
		/// tough for it shouts for help, and one with no enemy of its own joins a fight it sees its fellows in (see
		/// TryJoinFight)
		/// </remarks>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The enemy its AI is after, or null</param>
		/// <returns>True while it runs or hangs back, so the AI's own update is skipped</returns>
		private bool UpdateFlee(float dt, Character target)
		{
			bool home = AtHome(transform.position);
			if (fleeing && fleeReason == FleeReason.None)
			{
				fleeReason = FleeReason.Forage;
			}
			// A single greyling with nothing else near is no danger, so it stays and fights that
			bool loneGreyling = LoneGreyling(target);
			if (!fleeing && job is ForageJob && !home && (target != null || ai.m_timeSinceHurt < 1f) && !loneGreyling)
			{
				fleeing = true;
				fleeReason = FleeReason.Forage;
			}
			// Out of the village getting wood or loot, it heads home rather than take on a fight out there alone
			if (!fleeing && !home && target != null && job != null && !(job is HuntJob) && job.Outing() && !loneGreyling)
			{
				Flee(FleeReason.Forage);
			}
			// A hunter runs from any enemy but its prey and greylings, and gives up the hunt below HuntRetreatShare
			if (job is HuntJob hunt)
			{
				bool foe = (target != null && !hunt.Allows(target));
				if (humanoid.GetHealthPercentage() < HuntRetreatShare || (foe && !home))
				{
					if (target != null && hunt.Allows(target))
					{
						ai.m_targetCreature = null;
					}
					Interrupt("giving up a hunt");
					if (!home)
					{
						Flee(FleeReason.Wounded);
					}
				}
			}
			bool fighting = (target != null || ai.m_timeSinceHurt < AttackedTime || fleeing);
			if (!fighting)
			{
				hangingBack = false;
				badlyHurtLine = BadlyHurtShare;
				TryJoinFight();
				return(false);
			}
			if (Time.time >= nextTactics)
			{
				nextTactics = Time.time + TacticsInterval;
				WeighFight(home);
			}
			// A monster too tough for it to bring down alone has it calling the others in
			if (!fleeing && target != null && Tough(target))
			{
				ShoutForHelp(target);
			}
			if (fleeing)
			{
				if (job != null)
				{
					Interrupt("fleeing");
				}
				SetDulled(false);
				mover = "running home";
				// Running from a fight it won't take on alone, it shouts for the others, whatever it's running for
				ShoutForHelp(target);
				if (fleeTo != null && !fleeTo.IsDead())
				{
					Go(dt, fleeTo.transform.position, RegroupRange / 2f, true);
				}
				else
				{
					Go(dt, village.RallyPoint(), 2f, true);
				}
				if (moveFailed)
				{
					// With no way home it stands and fights
					moveFailed = false;
					StopFleeing();
				}
				return(fleeing);
			}
			if (!hangingBack)
			{
				flanked = null;
				return(false);
			}
			// Badly hurt, it runs from any enemy coming after it, and otherwise works its way behind one busy with
			// someone else to strike it there
			Character pursuer = Pursuer();
			if (pursuer != null)
			{
				runUntil = Time.time + RunTime;
			}
			ShoutForHelp(pursuer ?? target);
			if (Time.time >= runUntil)
			{
				Character mark = FindFlankMark();
				if (mark != null)
				{
					return(Flank(dt, mark));
				}
			}
			flanked = null;
			mover = "hanging back";
			HangBack(dt);
			return(true);
		}

		/// <summary>
		/// Finds the nearest enemy within RangedHangBackDistance coming after the viking
		/// </summary>
		/// <returns>The enemy, or null when there's none</returns>
		private Character Pursuer()
		{
			enemiesNear.Clear();
			Character.GetCharactersInRange(transform.position, RangedHangBackDistance, enemiesNear);
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Character other in enemiesNear)
			{
				if (other == null || other == humanoid || other.IsDead() || !BaseAI.IsEnemy(humanoid, other))
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
		/// Finds an enemy in the village's core for a badly hurt viking to strike from behind - one within EnemyRange busy with
		/// someone else, that it can hurt and reach - keeping to the one it's already working its way behind
		/// </summary>
		/// <returns>The enemy, or null when there's none</returns>
		private Character FindFlankMark()
		{
			if (flanked != null && Flankable(flanked) && Vector3.Distance(flanked.transform.position, transform.position) <= EnemyRange)
			{
				return(flanked);
			}
			enemiesNear.Clear();
			Character.GetCharactersInRange(transform.position, EnemyRange, enemiesNear);
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Character other in enemiesNear)
			{
				if (other == null || other == humanoid || !Flankable(other))
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
		/// Checks whether a badly hurt viking can strike an enemy from behind - it's alive, an enemy, in the village's core,
		/// can be hurt and reached, and is after someone other than the viking
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it can</returns>
		private bool Flankable(Character enemy)
		{
			if (enemy.IsDead() || !BaseAI.IsEnemy(humanoid, enemy) || ZombieState.IsRising(enemy) || OutOfReach(enemy) || !AtHome(enemy.transform.position))
			{
				return(false);
			}
			BaseAI enemyAI = enemy.GetBaseAI();
			Character busyWith = ((enemyAI != null) ? enemyAI.GetTargetCreature() : null);
			return(busyWith != null && busyWith != humanoid);
		}

		/// <summary>
		/// Works a badly hurt viking around behind an enemy busy with someone else - circling it FlankRadius out, on the
		/// side it's already on, so it never crosses in front of it - then walks in and strikes once it's behind it
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="mark">The enemy</param>
		/// <returns>True while it's working its way around, so the AI's own update is skipped, and false once it's
		/// behind the enemy and near enough for the AI to strike</returns>
		private bool Flank(float dt, Character mark)
		{
			flanked = mark;
			ai.m_targetCreature = mark;
			ai.SetAlerted(true);
			Vector3 from = mark.transform.position;
			Vector3 back = -mark.transform.forward;
			back.y = 0f;
			back = ((back.sqrMagnitude < 0.01f) ? Vector3.back : back.normalized);
			Vector3 to = transform.position - from;
			to.y = 0f;
			float distance = to.magnitude;
			float bearing = ((distance < 0.01f) ? 0f : Vector3.SignedAngle(back, to, Vector3.up));
			if (Mathf.Abs(bearing) <= BehindAngle)
			{
				if (distance <= FlankStrike)
				{
					mover = "striking " + mark.GetHoverName() + " from behind";
					return(false);
				}
				mover = "closing in behind " + mark.GetHoverName();
				Go(dt, from + back * FlankClose, 0.5f, true);
				moveFailed = false;
				return(true);
			}
			// Each step takes it up to FlankStep further around the enemy toward its back
			float step = Mathf.Sign(bearing) * Mathf.Min(Mathf.Abs(bearing), FlankStep);
			Vector3 spot = from + Quaternion.Euler(0f, bearing - step, 0f) * back * FlankRadius;
			mover = "circling behind " + mark.GetHoverName();
			Go(dt, spot, 1f, true);
			moveFailed = false;
			return(true);
		}

		/// <summary>
		/// Has a viking shout for help every ShoutInterval seconds while there's an enemy near that fights back (see
		/// FightsBack) - badly hurt and hanging back, running home from a fight, or fighting a monster too tough for it
		/// (see Tough) - words over its head on every client, and a call that brings the vikings within DefendRange of
		/// it to help against the enemy. Prey that only runs, like a deer, is no reason to shout
		/// </summary>
		/// <param name="enemy">The enemy it's shouting about, or null for the nearest one it can hurt that fights back</param>
		private void ShoutForHelp(Character enemy)
		{
			if (Time.time < nextShout)
			{
				return;
			}
			if (enemy == null || enemy.IsDead() || !FightsBack(enemy))
			{
				enemy = NearestHurtableEnemy(true);
			}
			if (enemy == null)
			{
				return;
			}
			nextShout = Time.time + ShoutInterval;
			VillageSignals.SendShout(humanoid, enemy);
		}

		/// <summary>
		/// Decides whether the viking runs home, stops running, or hangs back, from who's around it - and works out how
		/// low its health has to be for it to be badly hurt (see BadlyHurtLine), from the hardest hit of the enemies near
		/// it that are fighting
		/// </summary>
		/// <param name="home">Whether it's in its village's core</param>
		private void WeighFight(bool home)
		{
			int allies = 0;
			int healthyAllies = 0;
			bool woundedAlly = false;
			int enemies = 0;
			float hardest = 0f;
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character == humanoid || character.IsDead())
				{
					continue;
				}
				float distance = Vector3.Distance(character.transform.position, transform.position);
				if (VikingCore.IsPCViking(character) && distance <= AllyRange)
				{
					allies++;
					float share = character.GetHealthPercentage();
					if (share >= HealthyShare)
					{
						healthyAllies++;
					}
					if (share < BadlyHurtLineOf(character) && !AtHome(character.transform.position))
					{
						woundedAlly = true;
					}
				}
				else if (distance <= EnemyRange && BaseAI.IsEnemy(humanoid, character))
				{
					enemies++;
					BaseAI enemyAI = character.GetBaseAI();
					if (enemyAI != null && (enemyAI.GetTargetCreature() != null || enemyAI.IsAlerted()))
					{
						hardest = Mathf.Max(hardest, CachedHit(character));
					}
				}
			}
			float armor = ((viking != null) ? viking.BodyArmor() : 0f);
			float taken = ((hardest > 0f) ? HitData.DamageTypes.ApplyArmor(hardest, armor) : 0f);
			badlyHurtLine = BadlyHurtLine(humanoid.GetMaxHealth(), taken, BadlyHurtShare, NextHitMargin, BadlyHurtCap);
			float health = humanoid.GetHealthPercentage();
			fleeTo = null;
			if (home)
			{
				// Home with no one beside it, it keeps running to the nearest of its village's vikings rather than
				// turning to fight alone at the edge of the village
				if (fleeing)
				{
					Character fellow = NearestFellow(out float fellowDistance);
					if (fellow != null && fellowDistance > RegroupRange)
					{
						fleeTo = fellow;
						hangingBack = false;
						return;
					}
				}
				StopFleeing();
				hangingBack = HangsBack(health, badlyHurtLine, hangingBack, enemies, healthyAllies);
				return;
			}
			hangingBack = false;
			if (fleeing)
			{
				if (StopsFleeing(fleeReason, allies))
				{
					StopFleeing();
				}
				return;
			}
			bool hunting = (job is HuntJob);
			bool attackedAlone = (allies == 0 && ai.m_timeSinceHurt < AttackedTime && !hunting && !LoneGreyling(ai.m_targetCreature));
			FleeReason reason = FleeFor(health, badlyHurtLine, hunting, woundedAlly, enemies, attackedAlone);
			if (reason != FleeReason.None)
			{
				Flee(reason);
			}
		}

		/// <summary>
		/// Checks whether a viking at home hangs back from a fight - when it's badly hurt, or still not recovered once
		/// hanging back (see IsBadlyHurt), and its fellows can take the fight without it, with at least one healthy
		/// ally and no more enemies than healthy allies
		/// </summary>
		/// <param name="health">Its share of its health</param>
		/// <param name="line">The share of its health below which it's badly hurt (see BadlyHurtLine)</param>
		/// <param name="hangingBack">Whether it's hanging back already</param>
		/// <param name="enemies">How many enemies are near it</param>
		/// <param name="healthyAllies">How many of the vikings beside it have at least HealthyShare of their
		/// health</param>
		/// <returns>True when it hangs back</returns>
		internal static bool HangsBack(float health, float line, bool hangingBack, int enemies, int healthyAllies)
		{
			bool low = IsBadlyHurt(health, line, hangingBack, RecoveredMargin);
			bool needed = (enemies > healthyAllies || healthyAllies == 0);
			return(low && !needed);
		}

		/// <summary>
		/// Works out the share of a viking's health below which it's badly hurt - where the next hit could bring it down:
		/// margin times the hardest hit, through its armor, of an enemy fighting near it, but never below floor of its
		/// health, and never above cap, so a monster that kills in one hit anyway is left to the tough-monster weigh-up
		/// rather than having vikings hang back from near full health
		/// </summary>
		/// <param name="maxHealth">Its max health</param>
		/// <param name="hardestTaken">The hardest hit of an enemy fighting near it, through its armor, or 0 with none</param>
		/// <param name="floor">The least share the line is at, like BadlyHurtShare</param>
		/// <param name="margin">How many of that hit it wants to have left, like NextHitMargin</param>
		/// <param name="cap">The most share the line is at, like BadlyHurtCap</param>
		/// <returns>The share of its health</returns>
		internal static float BadlyHurtLine(float maxHealth, float hardestTaken, float floor, float margin, float cap)
		{
			if (maxHealth <= 0f)
			{
				return(floor);
			}
			float share = hardestTaken * margin / maxHealth;
			return(Mathf.Max(floor, Mathf.Min(share, cap)));
		}

		/// <summary>
		/// Checks whether a viking is badly hurt - below its line, or once it counts as badly hurt, until it's back
		/// recoveredMargin above it, so it doesn't flicker in and out
		/// </summary>
		/// <param name="health">Its share of its health</param>
		/// <param name="line">The share of its health below which it's badly hurt (see BadlyHurtLine)</param>
		/// <param name="recovering">Whether it already counts as badly hurt</param>
		/// <param name="recoveredMargin">How far above the line it has to climb to count as recovered, like
		/// RecoveredMargin</param>
		/// <returns>True when it is</returns>
		internal static bool IsBadlyHurt(float health, float line, bool recovering, float recoveredMargin)
		{
			return(health < (recovering ? line + recoveredMargin : line));
		}

		/// <summary>
		/// Gets the share of a PC viking's health below which it's badly hurt, as it last weighed up its fight - or
		/// BadlyHurtShare for one whose life doesn't run here
		/// </summary>
		/// <param name="fellow">The viking</param>
		/// <returns>The share of its health</returns>
		private static float BadlyHurtLineOf(Character fellow)
		{
			VillageLife life = fellow.GetComponent<VillageLife>();
			return((life != null && life.active) ? life.badlyHurtLine : BadlyHurtShare);
		}

		/// <summary>
		/// Gets an enemy's hardest hit before armor (see MonsterThreat.Hit), weighed again every HitCacheTime seconds
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>The damage, or 0 when its hit is unknown</returns>
		private static float CachedHit(Character enemy)
		{
			ZDOID id = enemy.GetZDOID();
			if (hitCache.TryGetValue(id, out KeyValuePair<float, float> known) && Time.time < known.Value)
			{
				return(known.Key);
			}
			if (hitCache.Count > RangedCacheMax)
			{
				hitCache.Clear();
			}
			float hit = MonsterThreat.Hit(enemy);
			hitCache[id] = new KeyValuePair<float, float>(hit, Time.time + HitCacheTime);
			return(hit);
		}

		/// <summary>
		/// Checks whether a viking running home away from it stops running - only one that ran for being alone, once
		/// another viking is beside it
		/// </summary>
		/// <param name="reason">Why it's running</param>
		/// <param name="allies">How many vikings are beside it</param>
		/// <returns>True when it stops</returns>
		internal static bool StopsFleeing(FleeReason reason, int allies)
		{
			return(reason == FleeReason.Alone && allies > 0);
		}

		/// <summary>
		/// Works out why a viking away from home runs home, if it does - first for being badly hurt, below its line (see
		/// BadlyHurtLine), or for a hunter, who is out on its own to fight its prey and gives up sooner, below
		/// HuntRetreatShare when that's higher, then to escort a badly hurt viking, then for FightOutnumbered or more
		/// enemies, then for being attacked alone
		/// </summary>
		/// <param name="health">Its share of its health</param>
		/// <param name="line">The share of its health below which it's badly hurt (see BadlyHurtLine)</param>
		/// <param name="hunting">Whether it's hunting</param>
		/// <param name="woundedAlly">Whether a viking beside it, away from home, is badly hurt</param>
		/// <param name="enemies">How many enemies are near it</param>
		/// <param name="attackedAlone">Whether it was just attacked with no viking beside it, while not hunting and not
		/// fighting a lone greyling</param>
		/// <returns>Why it runs, or None when it stays</returns>
		internal static FleeReason FleeFor(float health, float line, bool hunting, bool woundedAlly, int enemies, bool attackedAlone)
		{
			if (health < (hunting ? Mathf.Max(HuntRetreatShare, line) : line))
			{
				return(FleeReason.Wounded);
			}
			else if (woundedAlly)
			{
				return(FleeReason.Escort);
			}
			else if (enemies >= FightOutnumbered)
			{
				return(FleeReason.Outnumbered);
			}
			else if (attackedAlone)
			{
				return(FleeReason.Alone);
			}
			return(FleeReason.None);
		}

		/// <summary>
		/// Finds the nearest other PC viking of the viking's village that's loaded
		/// </summary>
		/// <param name="distance">How far it is, in meters</param>
		/// <returns>The viking, or null when there's none</returns>
		private Character NearestFellow(out float distance)
		{
			Character nearest = null;
			distance = float.MaxValue;
			foreach (Character other in village.Vikings())
			{
				if (other == null || other == humanoid || other.IsDead())
				{
					continue;
				}
				float away = Vector3.Distance(other.transform.position, transform.position);
				if (away < distance)
				{
					nearest = other;
					distance = away;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Starts the viking running home for help
		/// </summary>
		/// <param name="reason">Why</param>
		private void Flee(FleeReason reason)
		{
			fleeing = true;
			fleeReason = reason;
			ResetStuck();
		}

		/// <summary>
		/// Stops the viking running home, so it turns and fights
		/// </summary>
		private void StopFleeing()
		{
			fleeing = false;
			fleeReason = FleeReason.None;
			fleeTo = null;
		}

		/// <summary>
		/// Keeps a badly hurt viking out of the fight at home - at HangBackDistance from each enemy, or
		/// RangedHangBackDistance from one that shoots or throws, staying in the village's core, facing the fight
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		private void HangBack(float dt)
		{
			// It backs away from the enemy it's furthest inside the reach of, ranged or not
			Character enemy = null;
			float worst = 0f;
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			avoidNear.Clear();
			Character.GetCharactersInRange(transform.position, RangedHangBackDistance + HangBackStep, avoidNear);
			foreach (Character other in avoidNear)
			{
				if (other == null || other == humanoid || other.IsDead() || !BaseAI.IsEnemy(humanoid, other))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(other.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = other;
					nearestDistance = distance;
				}
				float keep = (HasRangedAttack(other) ? RangedHangBackDistance : HangBackDistance);
				float inside = keep - distance;
				if (inside > worst)
				{
					worst = inside;
					enemy = other;
				}
			}
			if (enemy == null)
			{
				ai.StopMoving();
				if (nearest != null)
				{
					ai.LookAt(nearest.GetCenterPoint());
				}
				return;
			}
			Vector3 away = transform.position - enemy.transform.position;
			away.y = 0f;
			Vector3 spot = transform.position + ((away.sqrMagnitude < 0.01f) ? -transform.forward : away.normalized) * HangBackStep;
			if (!AtHome(spot))
			{
				// Cornered at the edge of the core, it backs off across the core instead
				Vector3 across = village.CoreMiddle - enemy.transform.position;
				across.y = 0f;
				spot = village.CoreMiddle + ((across.sqrMagnitude < 0.01f) ? Vector3.forward : across.normalized) * village.CoreRadius * 0.5f;
			}
			Go(dt, spot, 1f, true);
			moveFailed = false;
		}

		/// <summary>
		/// Checks whether an enemy can hit from afar - it has an attack that shoots or throws, like a greydwarf's stone,
		/// a shaman's bolt or a viking's bow - remembering the answer for each enemy
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it can</returns>
		private static bool HasRangedAttack(Character enemy)
		{
			ZDOID id = enemy.GetZDOID();
			if (rangedCache.TryGetValue(id, out bool known))
			{
				return(known);
			}
			if (rangedCache.Count > RangedCacheMax)
			{
				rangedCache.Clear();
			}
			bool ranged = false;
			Humanoid armed = enemy as Humanoid;
			if (armed != null && armed.GetInventory() != null)
			{
				foreach (ItemDrop.ItemData item in armed.GetInventory().GetAllItems())
				{
					if (item.IsWeapon() && (Shoots(item.m_shared.m_attack) || Shoots(item.m_shared.m_secondaryAttack)))
					{
						ranged = true;
						break;
					}
				}
			}
			rangedCache[id] = ranged;
			return(ranged);
		}

		/// <summary>
		/// Checks whether an attack shoots or throws something
		/// </summary>
		/// <param name="attack">The attack, or null</param>
		/// <returns>True when it does</returns>
		private static bool Shoots(Attack attack)
		{
			return(attack != null && (attack.m_attackType == Attack.AttackType.Projectile || attack.m_attackType == Attack.AttackType.TriggerProjectile));
		}
	}
}
