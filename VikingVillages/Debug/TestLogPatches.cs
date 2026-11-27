using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Times the game's AI updates for the test log - the whole list, and each AI in it (see TestLogSlow)
	/// </summary>
	/// <remarks>
	/// Each AI is timed where the list calls it rather than by patching BaseAI.UpdateAI, which the AIs override, a
	/// viking's through two classes, so a patch there would time only part of some updates. A list that throws isn't
	/// timed, since its postfix doesn't run - MonoUpdaterAIClearPatch's finalizer logs the exception
	/// </remarks>
	[HarmonyPatch(typeof(MonoUpdatersExtra), nameof(MonoUpdatersExtra.UpdateAI))]
	public static class TestLogAIListPatch
	{
		/// <summary>
		/// Notes when the list starts
		/// </summary>
		/// <param name="__state">Set to the start, or 0 when no run is going</param>
		private static void Prefix(out long __state)
		{
			__state = TestLogSlow.Start();
		}

		/// <summary>
		/// Writes the list when it was slow
		/// </summary>
		/// <param name="source">The AIs updated</param>
		/// <param name="profileScope">The list's name</param>
		/// <param name="__state">When it started</param>
		private static void Postfix(List<IUpdateAI> source, string profileScope, long __state)
		{
			if (__state != 0L)
			{
				TestLogSlow.EndList(profileScope, source.Count, __state);
			}
		}

		/// <summary>
		/// Swaps the call to each AI's update for TestLogSlow.UpdateAI, leaving the method as it is unless the call is
		/// there once
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <returns>The instructions with the call swapped</returns>
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return(SwapCall(instructions, AccessTools.Method(typeof(IUpdateAI), nameof(IUpdateAI.UpdateAI)), AccessTools.Method(typeof(TestLogSlow), nameof(TestLogSlow.UpdateAI)), "MonoUpdatersExtra.UpdateAI"));
		}

		/// <summary>
		/// Swaps an interface call in a list's loop for a static method that takes the object and the call's arguments
		/// and makes the call, when the call is there once
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <param name="call">The interface method called</param>
		/// <param name="timed">The static method to call in its place</param>
		/// <param name="method">The method's name, for the warning when the call isn't there once</param>
		/// <returns>The instructions, with the call swapped or as they were</returns>
		internal static List<CodeInstruction> SwapCall(IEnumerable<CodeInstruction> instructions, MethodInfo call, MethodInfo timed, string method)
		{
			List<CodeInstruction> result = new List<CodeInstruction>(instructions);
			List<CodeInstruction> found = ((call != null) ? result.FindAll((CodeInstruction instruction) => instruction.Calls(call)) : new List<CodeInstruction>());
			if (found.Count != 1 || timed == null)
			{
				Plugin.Log.LogWarning("Expected one " + ((call != null) ? call.Name : "update") + " call in " + method + " but found " + found.Count + " - the test log won't time single objects there");
				return(result);
			}
			found[0].opcode = OpCodes.Call;
			found[0].operand = timed;
			return(result);
		}
	}

	/// <summary>
	/// Times the game's fixed updates for the test log - each list, and each object in one, like a creature's
	/// Character.CustomFixedUpdate (see TestLogSlow)
	/// </summary>
	/// <remarks>
	/// Each object is timed where the list calls it, as TestLogAIListPatch does for the AIs
	/// </remarks>
	[HarmonyPatch(typeof(MonoUpdatersExtra), nameof(MonoUpdatersExtra.CustomFixedUpdate))]
	public static class TestLogFixedListPatch
	{
		/// <summary>
		/// Notes when the list starts
		/// </summary>
		/// <param name="__state">Set to the start, or 0 when no run is going</param>
		private static void Prefix(out long __state)
		{
			__state = TestLogSlow.Start();
		}

		/// <summary>
		/// Writes the list when it was slow
		/// </summary>
		/// <param name="source">The objects updated</param>
		/// <param name="profileScope">The list's name</param>
		/// <param name="__state">When it started</param>
		private static void Postfix(List<IMonoUpdater> source, string profileScope, long __state)
		{
			if (__state != 0L)
			{
				TestLogSlow.EndList(profileScope, source.Count, __state);
			}
		}

		/// <summary>
		/// Swaps the call to each object's fixed update for TestLogSlow.FixedUpdate
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <returns>The instructions with the call swapped</returns>
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return(TestLogAIListPatch.SwapCall(instructions, AccessTools.Method(typeof(IMonoUpdater), nameof(IMonoUpdater.CustomFixedUpdate)), AccessTools.Method(typeof(TestLogSlow), nameof(TestLogSlow.FixedUpdate)), "MonoUpdatersExtra.CustomFixedUpdate"));
		}
	}

	/// <summary>
	/// Times each list of the game's updates for the test log (see TestLogSlow)
	/// </summary>
	[HarmonyPatch(typeof(MonoUpdatersExtra), nameof(MonoUpdatersExtra.CustomUpdate))]
	public static class TestLogUpdateListPatch
	{
		/// <summary>
		/// Notes when the list starts
		/// </summary>
		/// <param name="__state">Set to the start, or 0 when no run is going</param>
		private static void Prefix(out long __state)
		{
			__state = TestLogSlow.Start();
		}

		/// <summary>
		/// Writes the list when it was slow
		/// </summary>
		/// <param name="source">The objects updated</param>
		/// <param name="profileScope">The list's name</param>
		/// <param name="__state">When it started</param>
		private static void Postfix(List<IMonoUpdater> source, string profileScope, long __state)
		{
			if (__state != 0L)
			{
				TestLogSlow.EndList(profileScope, source.Count, __state);
			}
		}
	}

	/// <summary>
	/// Times each list of the game's late updates for the test log (see TestLogSlow)
	/// </summary>
	[HarmonyPatch(typeof(MonoUpdatersExtra), nameof(MonoUpdatersExtra.CustomLateUpdate))]
	public static class TestLogLateListPatch
	{
		/// <summary>
		/// Notes when the list starts
		/// </summary>
		/// <param name="__state">Set to the start, or 0 when no run is going</param>
		private static void Prefix(out long __state)
		{
			__state = TestLogSlow.Start();
		}

		/// <summary>
		/// Writes the list when it was slow
		/// </summary>
		/// <param name="source">The objects updated</param>
		/// <param name="profileScope">The list's name</param>
		/// <param name="__state">When it started</param>
		private static void Postfix(List<IMonoUpdater> source, string profileScope, long __state)
		{
			if (__state != 0L)
			{
				TestLogSlow.EndList(profileScope, source.Count, __state);
			}
		}
	}

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
