using System.Collections.Generic;
using HarmonyLib;
using Kriona.Vikings;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Adds the viking's spawns to the world's as its spawn systems wake
	/// </summary>
	[HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.Awake))]
	public static class VikingSpawnListPatch
	{
		/// <summary>
		/// Adds the spawns, once per list
		/// </summary>
		/// <param name="__instance">The spawn system</param>
		private static void Postfix(SpawnSystem __instance)
		{
			VikingSpawns.AddToSpawnLists(__instance);
		}
	}

	/// <summary>
	/// Keeps the world's spawns and raids from spawning zombie vikings on a server that doesn't have the mod (see
	/// ServerCheck)
	/// </summary>
	/// <remarks>
	/// The spawns are added to the spawn lists as they wake, before the server has answered, and the client that owns
	/// an area runs its spawns, so they're held back here instead
	/// </remarks>
	[HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.Spawn))]
	public static class VikingSpawnServerPatch
	{
		/// <summary>
		/// Skips spawning a viking until the server has the mod
		/// </summary>
		/// <param name="critter">The spawn</param>
		/// <returns>False to skip it</returns>
		private static bool Prefix(SpawnSystem.SpawnData critter)
		{
			return(!VikingSpawns.IsVikingPrefab(critter.m_prefab) || ServerCheck.Active);
		}
	}

	/// <summary>
	/// Keeps body piles from spawning anything while they would pick zombie vikings on a server that doesn't have the
	/// mod (see ServerCheck)
	/// </summary>
	[HarmonyPatch(typeof(SpawnArea), nameof(SpawnArea.SpawnOne))]
	public static class VikingSpawnAreaServerPatch
	{
		/// <summary>
		/// Skips a body pile's spawn while it would pick from a list with vikings in, until the server has the mod
		/// </summary>
		/// <param name="__instance">The body pile</param>
		/// <param name="__result">Whether it spawned, set to false when skipped</param>
		/// <returns>False to skip it</returns>
		private static bool Prefix(SpawnArea __instance, ref bool __result)
		{
			if (ServerCheck.Active || !__instance.m_prefabs.Exists((SpawnArea.SpawnData spawn) => VikingSpawns.IsVikingPrefab(spawn.m_prefab)))
			{
				return(true);
			}
			__result = false;
			return(false);
		}
	}

	/// <summary>
	/// Puts zombie vikings in abandoned houses as they're generated
	/// </summary>
	[HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.SpawnLocation))]
	public static class VikingHousePatch
	{
		/// <summary>
		/// Spawns the vikings once the house is placed
		/// </summary>
		/// <param name="location">The location placed</param>
		/// <param name="pos">Where it stands</param>
		/// <param name="mode">How it was placed</param>
		/// <param name="spawnedGhostObjects">The objects of a ghost placement</param>
		private static void Postfix(ZoneSystem.ZoneLocation location, Vector3 pos, ZoneSystem.SpawnMode mode, List<GameObject> spawnedGhostObjects)
		{
			VikingSpawns.OnLocationSpawned(location, pos, mode, spawnedGhostObjects);
		}
	}
}
