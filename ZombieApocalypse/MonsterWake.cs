using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Wakes a sleeping monster - like an abomination lying disguised in the Swamp - when a living viking it would fight
	/// comes within its wake-up range, the way the game wakes it for a player, so it goes for the viking and the viking,
	/// which leaves a sleeping monster alone, sees it to fight it
	/// </summary>
	/// <remarks>
	/// Each sleeping monster looks around for vikings every CheckInterval seconds rather than every update, on the client
	/// that owns it, which runs its AI
	/// </remarks>
	internal static class MonsterWake
	{
		/// <summary>
		/// Seconds between a sleeping monster's looks for living vikings near it
		/// </summary>
		private const float CheckInterval = 0.5f;

		/// <summary>
		/// When each sleeping monster next looks, by the monster
		/// </summary>
		private static readonly Dictionary<MonsterAI, float> nextCheck = new Dictionary<MonsterAI, float>();

		/// <summary>
		/// Wakes a sleeping monster when a living viking it would fight is within its wake-up range
		/// </summary>
		/// <param name="ai">The monster's AI</param>
		public static void Check(MonsterAI ai)
		{
			if (ai == null || !ai.IsSleeping() || ai.m_wakeupRange <= 0f || !ServerCheck.Active || !Plugin.VikingVillages.Value)
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
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(monster.transform.position, ai.m_wakeupRange, near);
			foreach (Character other in near)
			{
				if (other != monster && !other.IsDead() && VikingCore.IsLivingViking(other) && BaseAI.IsEnemy(monster, other))
				{
					ai.Wakeup();
					nextCheck.Remove(ai);
					return;
				}
			}
		}
	}
}
