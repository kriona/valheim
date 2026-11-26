using System.Collections.Generic;
using HarmonyLib;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Tames the animals an abandoned farm's pens spawn, as the living vikings' livestock, while a living viking is
	/// still there to keep them - one near the pen, or any in the village the pen stands in
	/// </summary>
	/// <remarks>
	/// A tamed animal and a living viking leave each other alone unless the viking has been provoked, and a tamed
	/// animal fights zombies and monsters
	/// </remarks>
	[HarmonyPatch(typeof(CreatureSpawner), nameof(CreatureSpawner.Spawn))]
	public static class VikingFarmAnimalsPatch
	{
		/// <summary>
		/// Tames an animal spawned inside an abandoned farm
		/// </summary>
		/// <param name="__instance">The spawner</param>
		/// <param name="__result">The creature spawned, or null</param>
		private static void Postfix(CreatureSpawner __instance, ZNetView __result)
		{
			Vector3 position = __instance.transform.position;
			if (__result == null || !FarmSpawns.InFarm(position) || (!FarmSpawns.LivingVikingNear(position) && !VillageLivestock.InKeptVillage(position)))
			{
				return;
			}
			Character animal = __result.GetComponent<Character>();
			if (animal != null && animal.GetComponent<Tameable>() != null && !ZombieState.IsZombie(animal))
			{
				__result.GetZDO().Set(FarmSpawns.LivestockKey, true);
				animal.SetTamed(true);
			}
		}
	}

	/// <summary>
	/// Keeps an abandoned farm's animal spawners refilling their pens, so the livestock the vikings slaughter comes back
	/// (see VillageLife.GatherFor) - letting them spawn inside the village's player base areas, its fire's and its
	/// beds', which the game keeps spawners out of, and giving one that only ever spawns once a respawn time of
	/// LivestockRespawnMinutes
	/// </summary>
	[HarmonyPatch(typeof(CreatureSpawner), nameof(CreatureSpawner.UpdateSpawner))]
	public static class VikingFarmRespawnPatch
	{
		/// <summary>
		/// The respawn time given to a farm's animal spawner that has none, in minutes, the game's own default
		/// </summary>
		private const float LivestockRespawnMinutes = 20f;

		/// <summary>
		/// Lets a farm's animal spawner spawn inside a player base area and respawn its animal
		/// </summary>
		/// <param name="__instance">The spawner</param>
		private static void Prefix(CreatureSpawner __instance)
		{
			if ((__instance.m_spawnInPlayerBase && __instance.m_respawnTimeMinuts > 0f) || __instance.m_creaturePrefab == null || __instance.m_creaturePrefab.GetComponent<Tameable>() == null)
			{
				return;
			}
			if (FarmSpawns.InFarm(__instance.transform.position))
			{
				__instance.m_spawnInPlayerBase = true;
				if (__instance.m_respawnTimeMinuts <= 0f)
				{
					__instance.m_respawnTimeMinuts = LivestockRespawnMinutes;
				}
			}
		}
	}

	/// <summary>
	/// Shows a farm's livestock as its village's, without the prompts to pet or rename it
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
	public static class VikingLivestockHoverPatch
	{
		/// <summary>
		/// Replaces the hover text of the vikings' livestock with its name and its village's
		/// </summary>
		/// <param name="__instance">The animal's Tameable</param>
		/// <param name="__result">Its hover text</param>
		private static void Postfix(Tameable __instance, ref string __result)
		{
			if (FarmSpawns.IsLivestock(__instance))
			{
				__result = FarmSpawns.LivestockHover(__instance.GetName(), VillageNames.Get(Village.Find(__instance.transform.position)));
			}
		}
	}

	/// <summary>
	/// Keeps players from petting or renaming a farm's livestock, since it's the vikings'
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
	public static class VikingLivestockInteractPatch
	{
		/// <summary>
		/// Skips the interaction for the vikings' livestock
		/// </summary>
		/// <param name="__instance">The animal's Tameable</param>
		/// <param name="__result">Whether the interaction did anything, set to false for livestock</param>
		/// <returns>False for livestock, to skip the interaction</returns>
		private static bool Prefix(Tameable __instance, ref bool __result)
		{
			if (!FarmSpawns.IsLivestock(__instance))
			{
				return(true);
			}
			__result = false;
			return(false);
		}
	}

	/// <summary>
	/// Keeps a farm's livestock from breeding, so players can't feed it for tamed young of their own
	/// </summary>
	[HarmonyPatch(typeof(Procreation), nameof(Procreation.Procreate))]
	public static class VikingLivestockBreedPatch
	{
		/// <summary>
		/// Skips breeding for the vikings' livestock
		/// </summary>
		/// <param name="__instance">The animal's Procreation</param>
		/// <returns>False for livestock, to skip breeding</returns>
		private static bool Prefix(Procreation __instance)
		{
			return(!FarmSpawns.IsLivestock(__instance));
		}
	}

	/// <summary>
	/// Puts back the building pieces an abandoned farm's rooms leave out by chance, and takes out its wild beehives, as
	/// it's generated
	/// </summary>
	[HarmonyPatch(typeof(RandomSpawn), nameof(RandomSpawn.Randomize))]
	public static class VikingFarmPiecesPatch
	{
		/// <summary>
		/// Puts back a farm's building piece, or takes out its wild beehive, once its chance has been rolled
		/// </summary>
		/// <param name="__instance">The piece's random spawn</param>
		/// <param name="pos">Where the piece stands in the world</param>
		private static void Postfix(RandomSpawn __instance, Vector3 pos)
		{
			FarmSpawns.OnRandomized(__instance, pos);
		}
	}

	/// <summary>
	/// Keeps an abandoned farm's pieces whole as it's generated
	/// </summary>
	/// <remarks>
	/// The game turns on random damage while it places a location that asks for it, and each piece reads it as it
	/// wakes
	/// </remarks>
	[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Awake))]
	public static class VikingFarmRepairPatch
	{
		/// <summary>
		/// Turns off the random damage for a farm's piece
		/// </summary>
		private static void Prefix()
		{
			FarmSpawns.OnPieceAwake();
		}
	}

	/// <summary>
	/// Makes the treasure chests an abandoned farm, or a house its vikings take in, is generated with the village's own
	/// </summary>
	[HarmonyPatch(typeof(Container), nameof(Container.Awake))]
	public static class VikingFarmChestPatch
	{
		/// <summary>
		/// Marks a farm's treasure chest as the village's before it fills itself with loot
		/// </summary>
		/// <param name="__instance">The chest</param>
		private static void Prefix(Container __instance)
		{
			FarmSpawns.OnChestAwake(__instance);
		}
	}

	/// <summary>
	/// Notes an abandoned farm's farmhouses as its rooms are generated
	/// </summary>
	/// <remarks>
	/// The generator saves its rooms right after placing them, while it still has them listed - a ghost placement
	/// removes them right after, and the list is cleared. It only lists rooms placed on the server or in single player,
	/// so a client showing a farm that's already there notes none
	/// </remarks>
	[HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.Save))]
	public static class VikingFarmPatch
	{
		/// <summary>
		/// Notes the farmhouses among the rooms placed
		/// </summary>
		private static void Postfix()
		{
			FarmSpawns.OnRoomsGenerated(DungeonGenerator.m_placedRooms);
		}
	}

	/// <summary>
	/// Readies abandoned farms and the empty houses near them for living vikings as they're generated
	/// </summary>
	[HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.SpawnLocation))]
	public static class VikingFarmLocationPatch
	{
		/// <summary>
		/// Forgets the farmhouses of any location placed before, and notes whether this one is a farm or a house its
		/// vikings take in
		/// </summary>
		/// <param name="location">The location being placed</param>
		/// <param name="pos">Where it's being placed</param>
		/// <param name="mode">How it's being placed</param>
		private static void Prefix(ZoneSystem.ZoneLocation location, Vector3 pos, ZoneSystem.SpawnMode mode)
		{
			FarmSpawns.OnLocationStarting(location, pos, mode);
		}

		/// <summary>
		/// Notes the farm's farmhouses, or fixes up the house, once it's placed
		/// </summary>
		/// <param name="location">The location placed</param>
		/// <param name="pos">Where it stands</param>
		/// <param name="mode">How it was placed</param>
		/// <param name="spawnedGhostObjects">The objects of a ghost placement</param>
		private static void Postfix(ZoneSystem.ZoneLocation location, Vector3 pos, ZoneSystem.SpawnMode mode, List<GameObject> spawnedGhostObjects)
		{
			FarmSpawns.OnLocationSpawned(location, pos, mode, spawnedGhostObjects);
		}

		/// <summary>
		/// Undoes what the prefix noted when placing the location threw, since the postfix doesn't run then
		/// </summary>
		/// <param name="__exception">The exception thrown, or null</param>
		/// <param name="mode">How it was being placed</param>
		/// <returns>The exception, so it's still thrown</returns>
		private static System.Exception Finalizer(System.Exception __exception, ZoneSystem.SpawnMode mode)
		{
			if (__exception != null)
			{
				FarmSpawns.OnLocationFailed(mode);
			}
			return(__exception);
		}
	}
}
