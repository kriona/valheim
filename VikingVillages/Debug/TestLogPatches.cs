using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Notes the hits PC vikings take for the test log, so a viking's death line lists its last ones (see TestLog)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
	public static class TestLogHitPatch
	{
		/// <summary>
		/// Keeps the creature's health before the hit
		/// </summary>
		/// <param name="__instance">The creature hit</param>
		/// <param name="__state">Set to its health</param>
		private static void Prefix(Character __instance, out float __state)
		{
			__state = __instance.GetHealth();
		}

		/// <summary>
		/// Notes the hit when it took some of the creature's health
		/// </summary>
		/// <param name="__instance">The creature hit</param>
		/// <param name="hit">The hit, after its modifiers</param>
		/// <param name="__state">Its health before the hit</param>
		private static void Postfix(Character __instance, HitData hit, float __state)
		{
			if (TestLog.On && __instance.GetHealth() < __state)
			{
				TestLog.OnHit(__instance, hit, __state);
			}
			if (hit.m_hitType == HitData.HitType.Burning && __instance.TryGetComponent(out VillageLife life))
			{
				life.LogBurn("a tick of its burning", hit.GetTotalDamage());
			}
		}
	}

	/// <summary>
	/// Writes a fire's flames, or anything else dealing fire damage, setting a village viking burning (see
	/// VillageLife.LogBurn)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.AddFireDamage))]
	public static class TestLogFirePatch
	{
		/// <summary>
		/// Writes the fire damage the creature takes, when it's a village viking
		/// </summary>
		/// <param name="__instance">The creature hit</param>
		/// <param name="damage">The fire damage, after its resistances and armor</param>
		private static void Prefix(Character __instance, float damage)
		{
			if (damage > 0f && __instance.TryGetComponent(out VillageLife life))
			{
				life.LogBurn("a fire hit", damage);
			}
		}
	}

	/// <summary>
	/// Writes the deaths of PC vikings, and of the creatures in and around villages, to the test log
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
	public static class TestLogDeathPatch
	{
		/// <summary>
		/// Writes the death while the creature is still whole
		/// </summary>
		/// <param name="__instance">The creature dying</param>
		private static void Prefix(Character __instance)
		{
			TestLog.OnDeath(__instance);
		}
	}

	/// <summary>
	/// Writes loot the vikings could have put away that despawns, the way ItemDrop.TimedDestruction judges it, to the
	/// test log
	/// </summary>
	[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.TimedDestruction))]
	public static class TestLogDespawnPatch
	{
		/// <summary>
		/// How long an item lies before it may despawn, and how near a player keeps it, as TimedDestruction has them
		/// </summary>
		private const double DespawnTime = 3600.0;
		private const float PlayerKeeps = 25f;

		/// <summary>
		/// Works out why the vikings left the item, when it's loot near a village that's about to despawn
		/// </summary>
		/// <param name="__instance">The item</param>
		/// <param name="__state">Set to the report, or null when there's nothing to write</param>
		private static void Prefix(ItemDrop __instance, out VillageLife.LootLeft __state)
		{
			__state = null;
			if (!TestLog.On || __instance.GetTimeSinceSpawned() < DespawnTime || __instance.IsInsideBase() || Player.IsPlayerInRange(__instance.transform.position, PlayerKeeps) || __instance.InTar() || __instance.IsPiece())
			{
				return;
			}
			__state = VillageLife.LootLeftReport(__instance);
		}

		/// <summary>
		/// Writes the report once the item is gone
		/// </summary>
		/// <param name="__instance">The item</param>
		/// <param name="__state">The report, or null</param>
		private static void Postfix(ItemDrop __instance, VillageLife.LootLeft __state)
		{
			if (__state != null && (__instance.m_nview == null || !__instance.m_nview.IsValid()))
			{
				TestLog.OnDespawn(__state);
			}
		}
	}
}
