using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a viking keeps clear of while it works - roots, which it calls an archer to, flames and poison clouds
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Keeps a viking out of a root's reach, stepping back out when it's within RootKeepAway, even in a fight - one
		/// fighting hand to hand calls an archer over when a root is within RootCallRange, every ArcherCallInterval
		/// seconds, and an archer keeps the root as its target as it backs off, so it shoots it from out of its reach
		/// </summary>
		/// <returns>True while it's stepping away, so the AI's own update is skipped</returns>
		private bool UpdateRoots()
		{
			bool archer = HasBow(humanoid);
			Character root = NearestRoot(archer ? RootKeepAway : RootCallRange);
			if (root == null)
			{
				return(false);
			}
			if (!archer)
			{
				CallArcher(root);
			}
			Vector3 away = transform.position - root.transform.position;
			away.y = 0f;
			if (away.magnitude > RootKeepAway)
			{
				return(false);
			}
			if (away.sqrMagnitude < 0.01f)
			{
				away = -transform.forward;
			}
			if (!archer && ai.m_targetCreature == root)
			{
				ai.m_targetCreature = null;
			}
			Interrupt("keeping clear of a root");
			mover = (archer ? "backing off from a root to shoot it" : "keeping clear of a root");
			ai.MoveTowards(away.normalized, true);
			return(true);
		}

		/// <summary>
		/// Finds the nearest root hostile to the viking within a range of it
		/// </summary>
		/// <param name="range">The range, in meters</param>
		/// <returns>The root, or null when there's none</returns>
		private Character NearestRoot(float range)
		{
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(transform.position, range, near);
			Character nearest = null;
			float best = float.MaxValue;
			foreach (Character other in near)
			{
				if (other == humanoid || other.IsDead() || !MonsterThreat.IsRoot(other) || !BaseAI.IsEnemy(other, humanoid))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(other.transform.position, transform.position);
				if (distance < best)
				{
					nearest = other;
					best = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Calls the village's nearest archer this client runs over to shoot a root, at most every ArcherCallInterval
		/// seconds - giving it the root as its target - with a shout, or a warning to keep clear when there's no archer
		/// </summary>
		/// <param name="root">The root</param>
		private void CallArcher(Character root)
		{
			if (Time.time - lastArcherCall < ArcherCallInterval)
			{
				return;
			}
			lastArcherCall = Time.time;
			VillageLife archer = null;
			float best = float.MaxValue;
			foreach (VillageLife other in All)
			{
				if (other == null || other == this || other.village != village || !other.active || other.humanoid.IsDead() || !HasBow(other.humanoid))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(other.transform.position, root.transform.position);
				if (distance < best)
				{
					archer = other;
					best = distance;
				}
			}
			string[] lines = ((archer != null) ? ArcherCalls : RootWarnings);
			VikingSpeech.Chatter(humanoid, lines[Random.Range(0, lines.Length)], VikingSpeech.TalkTime);
			if (archer != null && archer.ai.m_targetCreature != root)
			{
				archer.ai.m_targetCreature = root;
				archer.ai.SetAlerted(true);
			}
		}

		/// <summary>
		/// Has the viking run out of a fire's flames when its body gets within FlameStep of them, straight away from the
		/// fire's middle, even in a fight, until it's FlameClear clear of them
		/// </summary>
		/// <returns>True while it's running out, so the AI's own update is skipped</returns>
		private bool UpdateFlames()
		{
			if (attachPoint != null)
			{
				escapingFlames = false;
				return(false);
			}
			if (!escapingFlames && Time.time < nextFlameCheck)
			{
				return(false);
			}
			nextFlameCheck = Time.time + FlameCheckInterval;
			float radius = humanoid.GetRadius() + (escapingFlames ? FlameClear : FlameStep);
			if (!FireSafety.FlamesNear(humanoid.GetCenterPoint(), radius, out Vector3 middle, out float _))
			{
				escapingFlames = false;
				return(false);
			}
			Vector3 away = transform.position - middle;
			away.y = 0f;
			if (away.sqrMagnitude < 0.01f)
			{
				away = -transform.forward;
			}
			escapingFlames = true;
			Interrupt("flames");
			mover = "running out of the flames";
			ai.MoveTowards(away.normalized, true);
			return(true);
		}

		/// <summary>
		/// Has the viking step away from a poison cloud or a zombie's corpse that might leave one when it's too near,
		/// running straight away from it, even in a fight. It keeps its job, picking it up again once it's clear, unless
		/// it's sitting, lying or on a seat, which it has to get up from
		/// </summary>
		/// <returns>True while it's stepping away, so the AI's own update is skipped</returns>
		private bool UpdatePoison()
		{
			if (!VillageHazards.NearestPoison(transform.position, PoisonAvoid, out Vector3 poison))
			{
				steppingFromPoison = false;
				return(false);
			}
			Vector3 away = transform.position - poison;
			away.y = 0f;
			if (away.sqrMagnitude < 0.01f)
			{
				away = -transform.forward;
			}
			if (attachPoint != null || VikingPoses.SettledDown(humanoid, out bool _))
			{
				Interrupt("poison");
			}
			if (!steppingFromPoison)
			{
				steppingFromPoison = true;
				TestLog.Count("Stepped away from poison, by job", ((job != null) ? LogJobName() : "none"));
			}
			mover = "stepping away from poison";
			ai.MoveTowards(away.normalized, true);
			return(true);
		}
	}
}
