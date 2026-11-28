using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Lets every creature and player be grabbed and dropped by a valkyrie, on the client that owns it (see
	/// ValkyrieCarried)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.Awake))]
	internal static class ValkyrieGrabRPCPatch
	{
		/// <summary>
		/// Listens for a valkyrie grabbing and dropping the creature or player
		/// </summary>
		/// <param name="__instance">The creature or player</param>
		private static void Postfix(Character __instance)
		{
			ZNetView view = __instance.m_nview;
			if (view == null || !view.IsValid())
			{
				return;
			}
			view.Register<ZDOID>(ValkyrieGrab.GrabRPC, (long sender, ZDOID valkyrie) => ValkyrieCarried.Grab(__instance, valkyrie));
			view.Register(ValkyrieGrab.DropRPC, (long sender) => ValkyrieCarried.Drop(__instance));
		}
	}

	/// <summary>
	/// Has a valkyrie catch a rider who gets off her in the air over a dangerous fall (see ValkyrieCatch)
	/// </summary>
	[HarmonyPatch(typeof(Sadle), nameof(Sadle.OnUseStop))]
	internal static class ValkyrieCatchPatch
	{
		/// <summary>
		/// Catches the rider once they're off her saddle
		/// </summary>
		/// <param name="__instance">The saddle</param>
		/// <param name="player">The rider getting off</param>
		private static void Postfix(Sadle __instance, Player player)
		{
			ValkyrieCatch.AfterDismount(__instance, player);
		}
	}

	/// <summary>
	/// Has a valkyrie's grab take hold of the enemy it went for when its strike lands (see ValkyrieGrab.Struck)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
	internal static class ValkyrieGrabHitPatch
	{
		/// <summary>
		/// Asks a creature or player hit by a valkyrie's grab to be held, on the client that owns her, where her
		/// strikes land - it takes the strike's damage as usual
		/// </summary>
		/// <param name="__instance">The creature or player hit</param>
		/// <param name="hit">The hit</param>
		private static void Prefix(Character __instance, HitData hit)
		{
			if (hit == null || hit.m_attacker.IsNone())
			{
				return;
			}
			Character attacker = hit.GetAttacker();
			if (attacker != null && attacker.TryGetComponent(out ValkyrieGrab grab) && grab.Grabbing())
			{
				grab.Struck(__instance);
			}
		}
	}
}
