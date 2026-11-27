using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// A viking's home and the grudges it holds - guarding and strolling near home, leashing to it and walking back
	/// when it's chased an enemy too far
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Makes the player's tombstone the viking's home, which its AI wanders around and its spawn point, so it's
		/// where the game measures how far it has chased from
		/// </summary>
		private void SetHome()
		{
			if (ai == null || !data.Tombstone)
			{
				return;
			}
			home = (data.HasHome ? data.Home : ai.m_spawnPoint);
			hasHome = true;
			ai.m_spawnPoint = home;
			nview.GetZDO().Set(ZDOVars.s_spawnPoint, home);
			ai.SetPatrolPoint(home);
		}

		/// <summary>
		/// Checks whether the viking guards a tombstone, rather than roaming like a viking of the world
		/// </summary>
		/// <returns>True once a tombstone viking has its home</returns>
		public bool IsGuard()
		{
			return(hasHome);
		}

		/// <summary>
		/// Checks whether a point is within a distance of the viking's home
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="range">The distance</param>
		/// <returns>True when it is, or when the viking has no home yet</returns>
		private bool NearHome(Vector3 point, float range)
		{
			return(!hasHome || Utils.DistanceXZ(point, home) <= range);
		}

		/// <summary>
		/// Checks whether the viking is still after an enemy that hurt it
		/// </summary>
		/// <param name="target">The enemy</param>
		/// <returns>True when the enemy hurt it within GrudgeTime</returns>
		private bool HoldsGrudge(Character target)
		{
			return(target != null && target == grudge && Time.time <= grudgeUntil);
		}

		/// <summary>
		/// Notes an enemy that hurt the viking, so it chases them beyond its leash, and stops it running home from its
		/// leash to do so
		/// </summary>
		/// <param name="attacker">Who hurt it, or null</param>
		public void OnHurt(Character attacker)
		{
			if (life != null)
			{
				life.OnHurt(attacker);
			}
			if (attacker == null || attacker.IsDead() || !BaseAI.IsEnemy(humanoid, attacker))
			{
				return;
			}
			grudge = attacker;
			grudgeUntil = Time.time + GrudgeTime;
			if (returningSince >= 0f && !returningFromGrudge)
			{
				returningSince = -1f;
				ai.m_targetCreature = attacker;
				ai.m_lastKnownTargetPos = attacker.transform.position;
				ai.m_beenAtLastPos = false;
				ai.m_targetStatic = null;
				ai.SetAlerted(true);
			}
		}

		/// <summary>
		/// Forgets an enemy the viking has picked that's too far from its home to go after - beyond its leash, or beyond
		/// GrudgeRange for an enemy that has just hurt it - or, for a PC viking in its village, too far from the
		/// village
		/// </summary>
		public void LeashTarget()
		{
			if (life != null && life.IsActive())
			{
				life.LeashTarget();
				return;
			}
			if (ai == null || !hasHome)
			{
				return;
			}
			Character target = ai.m_targetCreature;
			if (target != null && !NearHome(target.transform.position, (HoldsGrudge(target) ? GrudgeRange : LeashRange)))
			{
				ai.m_targetCreature = null;
			}
			if (ai.m_targetStatic != null && !NearHome(ai.m_targetStatic.transform.position, LeashRange))
			{
				ai.m_targetStatic = null;
			}
		}

		/// <summary>
		/// Runs the viking home when it has gone too far from it - beyond its leash, or beyond GrudgeRange while it chases
		/// an enemy that hurt it - ignoring everything but being hurt again from its leash until it's back, and puts it
		/// there, standing still, when it's stuck
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's running home, so the AI's own update is skipped</returns>
		public bool UpdateReturn(float dt)
		{
			if (ai == null || !hasHome || !nview.IsOwner() || (ai is VikingAI vikingAI && vikingAI.Held()))
			{
				return(false);
			}
			float distance = Utils.DistanceXZ(transform.position, home);
			if (returningSince < 0f)
			{
				float limit = (HoldsGrudge(ai.m_targetCreature) ? GrudgeRange : LeashRange);
				if (distance <= limit)
				{
					return(false);
				}
				returningSince = Time.time;
				returningFromGrudge = (distance > GrudgeRange);
				if (returningFromGrudge)
				{
					grudge = null;
				}
				ai.m_targetCreature = null;
				ai.m_targetStatic = null;
				ai.SetAlerted(false);
			}
			if (distance <= HomeRange)
			{
				returningSince = -1f;
				return(false);
			}
			if (Time.time - returningSince > ReturnTimeout)
			{
				returningSince = -1f;
				ai.StopMoving();
				Vector3 landing = home + Vector3.up * 0.5f;
				humanoid.m_body.position = landing;
				humanoid.m_body.linearVelocity = Vector3.zero;
				humanoid.m_body.angularVelocity = Vector3.zero;
				transform.position = landing;
				// From where it lands, so the move home isn't taken for a fall
				humanoid.m_maxAirAltitude = landing.y;
				return(true);
			}
			ai.MoveTo(dt, home, 0f, true);
			return(true);
		}

		/// <summary>
		/// Runs a PC viking's life in its village while it has nothing to fight, or keeps a viking with a home by it
		/// - walking straight back when it has strayed, and otherwise standing still, with a short stroll now and then
		/// </summary>
		/// <remarks>
		/// The game's wandering runs from spot to spot without pausing while a creature is alerted, which the viking is
		/// as it rises and for a while after each fight
		/// </remarks>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True when it's handled, so the game's wandering is skipped</returns>
		public bool UpdateIdle(float dt)
		{
			if (life != null && life.Idle(dt))
			{
				return(true);
			}
			if (ai == null || !hasHome)
			{
				return(false);
			}
			if (Utils.DistanceXZ(transform.position, home) > HomeRange)
			{
				hasStrollTarget = false;
				ai.MoveTo(dt, home, 0f, false);
				return(true);
			}
			if (hasStrollTarget)
			{
				if (ai.MoveTo(dt, strollTarget, StrollReach, false))
				{
					hasStrollTarget = false;
					nextStroll = Time.time + Random.Range(StrollIntervalMin, StrollIntervalMax);
				}
				return(true);
			}
			ai.StopMoving();
			if (nextStroll == 0f)
			{
				nextStroll = Time.time + Random.Range(StrollIntervalMin, StrollIntervalMax);
			}
			else if (Time.time >= nextStroll)
			{
				Vector2 offset = Random.insideUnitCircle * GuardRange;
				strollTarget = home + new Vector3(offset.x, 0f, offset.y);
				hasStrollTarget = true;
			}
			return(true);
		}
	}
}
