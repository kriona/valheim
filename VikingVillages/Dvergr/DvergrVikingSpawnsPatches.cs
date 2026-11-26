using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Has some of the world's dvergr patrols spawn with a dvergr viking along (see DvergrVikingSpawns.OnSpawned)
	/// </summary>
	[HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.Spawn))]
	public static class DvergrPatrolSpawnPatch
	{
		/// <summary>
		/// Rolls for a viking once a dvergr has spawned
		/// </summary>
		/// <param name="critter">The spawn</param>
		/// <param name="spawnPoint">Where it spawned</param>
		/// <param name="eventSpawner">Whether an event spawned it</param>
		private static void Postfix(SpawnSystem.SpawnData critter, Vector3 spawnPoint, bool eventSpawner)
		{
			DvergrVikingSpawns.OnSpawned(critter, spawnPoint, eventSpawner);
		}
	}
}
