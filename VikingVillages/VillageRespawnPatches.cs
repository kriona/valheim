using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Notes the zones this client generates in full - placing everything in them at once, rather than sending them on
	/// as a dedicated server does - so a village in them is set up without waiting for objects to arrive (see
	/// VillageRespawn.ZoneSettled)
	/// </summary>
	[HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.SpawnZone))]
	public static class VillageZoneGeneratedPatch
	{
		/// <summary>
		/// Notes whether the zone is about to be generated here, rather than loaded
		/// </summary>
		/// <param name="__instance">The zone system</param>
		/// <param name="zoneID">The zone</param>
		/// <param name="mode">How it's placed</param>
		/// <param name="__state">Set to whether it's being generated in full</param>
		private static void Prefix(ZoneSystem __instance, Vector2s zoneID, ZoneSystem.SpawnMode mode, out bool __state)
		{
			__state = (mode == ZoneSystem.SpawnMode.Full && !__instance.IsZoneGenerated(zoneID));
		}

		/// <summary>
		/// Notes the zone once it was generated
		/// </summary>
		/// <param name="zoneID">The zone</param>
		/// <param name="__result">Whether it was spawned</param>
		/// <param name="__state">Whether it was being generated in full</param>
		private static void Postfix(Vector2s zoneID, bool __result, bool __state)
		{
			if (__state && __result)
			{
				VillageRespawn.NoteGenerated(zoneID);
			}
		}
	}

	/// <summary>
	/// Listens for living vikings' deaths reported to the owners of their villages' generators, in each world
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	public static class VillageDeathRpcPatch
	{
		/// <summary>
		/// Registers the routed RPC once the world's network is up
		/// </summary>
		private static void Postfix()
		{
			VillageRespawn.RegisterRpc();
		}
	}

	/// <summary>
	/// Reports a living viking's death as it dies on its owner, so its village can bring it back and remember its name,
	/// and so a viking who sees one keeping watch die can take its place
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
	public static class VillageDeathPatch
	{
		/// <summary>
		/// Reports the death before the viking is removed, while its ZDO still notes its village
		/// </summary>
		/// <param name="__instance">The creature dying</param>
		private static void Prefix(Character __instance)
		{
			VillageRespawn.OnVikingDeath(__instance);
			VillageWatch.OnVikingDeath(__instance);
			VillageFallen.OnVikingDeath(__instance);
		}
	}
}
