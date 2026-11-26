using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// How a viking walks, runs and climbs, and the run skill it raises as it goes
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Has a living or dvergr viking walk while there's no danger and it's at home, and move at its full pace once
		/// there's danger - when it's alerted, has a target, or was hurt in the last few seconds - or it's away from its
		/// village's core or on the road to a village, or it's climbing a slope or taking a run-up to a fence, so it
		/// makes it over
		/// </summary>
		/// <remarks>
		/// A creature moves at its jog whenever it isn't running, which for a viking built from the player is a quick
		/// trot. Walking beats both, and the AI never changes it, so it's set here every update
		/// </remarks>
		private void UpdateWalk()
		{
			if (ai == null || !IsLivingOrDvergr(humanoid))
			{
				return;
			}
			bool away = (life != null && life.AwayFromHome());
			bool hopping = (life != null && life.Hopping());
			humanoid.SetWalk(!InDanger() && !away && !hopping && !Climbing());
		}

		/// <summary>
		/// Raises a living or dvergr viking's Run and Swim skills for every RunSkillInterval seconds it spends running or
		/// swimming, the way a player's rise, and speeds its swimming by its Swim skill (see SwimSkillBonus)
		/// </summary>
		private void UpdateRunSkill()
		{
			if (!IsLivingOrDvergr(humanoid))
			{
				return;
			}
			if (baseSwimSpeed < 0f)
			{
				baseSwimSpeed = humanoid.m_swimSpeed;
			}
			float swim = SkillLevel(Skills.SkillType.Swim);
			humanoid.m_swimSpeed = baseSwimSpeed * SkillBoost(swim, SwimSkillBonus);
			bool moving = (humanoid.m_moveDir.sqrMagnitude >= 0.01f);
			if (moving && humanoid.IsSwimming())
			{
				swimSkillTimer += Time.deltaTime;
				if (swimSkillTimer > RunSkillInterval)
				{
					swimSkillTimer = 0f;
					RaiseSkill(Skills.SkillType.Swim, 1f);
				}
			}
			else if (moving && humanoid.IsRunning())
			{
				runSkillTimer += Time.deltaTime;
				if (runSkillTimer > RunSkillInterval)
				{
					runSkillTimer = 0f;
					RaiseSkill(Skills.SkillType.Run, 1f);
				}
			}
		}

		/// <summary>
		/// Checks whether a living viking is climbing a slope - on ground sloping at least ClimbAngle and heading up it -
		/// or was within the last ClimbHold seconds
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Climbing()
		{
			if (IsClimbStep(humanoid.m_lastGroundNormal, humanoid.m_moveDir, humanoid.IsOnGround()))
			{
				climbUntil = Time.time + ClimbHold;
			}
			return(Time.time < climbUntil);
		}

		/// <summary>
		/// Checks whether a step is a climb - on the ground, which slopes at least ClimbAngle, heading up it at least
		/// ClimbHeading of the way
		/// </summary>
		/// <param name="normal">The ground's normal</param>
		/// <param name="moveDir">Which way the viking is moving, where only its way across the ground counts</param>
		/// <param name="onGround">Whether it's on the ground</param>
		/// <returns>True when it is</returns>
		internal static bool IsClimbStep(Vector3 normal, Vector3 moveDir, bool onGround)
		{
			Vector3 heading = moveDir;
			heading.y = 0f;
			Vector3 downhill = new Vector3(normal.x, 0f, normal.z);
			bool sloped = (onGround && Vector3.Angle(normal, Vector3.up) >= ClimbAngle);
			return(sloped && heading.sqrMagnitude > 0.01f && downhill.sqrMagnitude > 0.0001f && Vector3.Dot(heading.normalized, downhill.normalized) <= -ClimbHeading);
		}

		/// <summary>
		/// Checks whether a living viking is in danger - alerted, with a target, hurt in the last few seconds, waiting
		/// for a corpse to rise, or with a deathsquito after it
		/// </summary>
		/// <returns>True when it is</returns>
		private bool InDanger()
		{
			if ((life != null && life.IsWary()) || diver != null)
			{
				return(true);
			}
			return(ai != null && (ai.IsAlerted() || ai.GetTargetCreature() != null || ai.m_timeSinceHurt < DangerHurtTime));
		}
	}
}
