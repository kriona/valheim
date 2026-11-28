using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Wakes a sleeping monster - like an abomination lying disguised in the Swamp, or a stone golem standing still as
	/// rock in the Mountains - when a PC viking it would fight comes within its wake-up range, or makes enough noise near
	/// one that wakes to noise, the way the game wakes it for a player, so it goes for the viking and the viking, which
	/// leaves a sleeping monster alone, sees it to fight it
	/// </summary>
	/// <remarks>
	/// Each sleeping monster looks around for vikings every CheckInterval seconds rather than every update, on the client
	/// that owns it, which runs its AI. A viking's noise is in its ZDO, so it's heard on any client
	/// </remarks>
	internal static class MonsterWake
	{
		/// <summary>
		/// Seconds between a sleeping monster's looks for PC vikings near it
		/// </summary>
		private const float CheckInterval = 0.5f;

		/// <summary>
		/// When each sleeping monster next looks, by the monster
		/// </summary>
		private static readonly Dictionary<MonsterAI, float> nextCheck = new Dictionary<MonsterAI, float>();

		/// <summary>
		/// Checks whether a sleeping monster wakes for someone near it, the way the game decides for a player - within
		/// its wake-up range, or for one that wakes to noise, within as far as their noise carries, up to the farthest it
		/// hears
		/// </summary>
		/// <param name="distance">How far off they are, in meters</param>
		/// <param name="wakeupRange">The monster's wake-up range, in meters, or 0 when it has none</param>
		/// <param name="noiseWakeup">Whether it wakes to noise</param>
		/// <param name="noise">How far their noise carries, in meters</param>
		/// <param name="maxNoiseRange">The farthest the monster hears noise from, in meters</param>
		/// <returns>True when it wakes</returns>
		internal static bool WakesFor(float distance, float wakeupRange, bool noiseWakeup, float noise, float maxNoiseRange)
		{
			if (wakeupRange > 0f && distance < wakeupRange)
			{
				return(true);
			}
			return(noiseWakeup && distance < Mathf.Min(noise, maxNoiseRange));
		}

		/// <summary>
		/// Wakes a sleeping monster when a PC viking it would fight is within its wake-up range, or makes enough noise near
		/// one that wakes to noise (see WakesFor)
		/// </summary>
		/// <param name="ai">The monster's AI</param>
		public static void Check(MonsterAI ai)
		{
			if (ai == null || !ai.IsSleeping() || (ai.m_wakeupRange <= 0f && !ai.m_noiseWakeup) || !ServerCheck.Active)
			{
				return;
			}
			if (nextCheck.TryGetValue(ai, out float at) && Time.time < at)
			{
				return;
			}
			if (nextCheck.Count > 256)
			{
				nextCheck.Clear();
			}
			nextCheck[ai] = Time.time + CheckInterval;
			Character monster = ai.m_character;
			if (monster == null)
			{
				return;
			}
			Vector3 position = monster.transform.position;
			float reach = Mathf.Max(ai.m_wakeupRange, (ai.m_noiseWakeup ? ai.m_maxNoiseWakeupRange : 0f));
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(position, reach, near);
			foreach (Character other in near)
			{
				if (other == monster || other.IsDead() || !VikingCore.IsPlayerLikeViking(other) || !BaseAI.IsEnemy(monster, other))
				{
					continue;
				}
				if (WakesFor(Vector3.Distance(other.transform.position, position), ai.m_wakeupRange, ai.m_noiseWakeup, other.GetNoiseRange(), ai.m_maxNoiseWakeupRange))
				{
					ai.Wakeup();
					nextCheck.Remove(ai);
					return;
				}
			}
		}
	}
}
