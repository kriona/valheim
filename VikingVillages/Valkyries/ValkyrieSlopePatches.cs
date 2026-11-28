using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Keeps a valkyrie from sliding down a slope she's standing on - the game slides a ridden creature down anything
	/// steeper than 45 degrees (see Character.ApplySlide), and her body's friction lets her slip down a steep roof
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.ApplySlide))]
	internal static class ValkyrieSlidePatch
	{
		/// <summary>
		/// Skips sliding a valkyrie
		/// </summary>
		/// <param name="__instance">The creature</param>
		/// <returns>False for a valkyrie, to skip sliding her</returns>
		private static bool Prefix(Character __instance)
		{
			if (__instance.GetComponent<ValkyrieFlight>() == null)
			{
				return(true);
			}
			__instance.m_slippage = 0f;
			return(false);
		}
	}

	/// <summary>
	/// Grips a valkyrie standing still on the ground firmly enough not to slip down even a steep roof (see
	/// ValkyrieSlidePatch)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.UpdateBodyFriction))]
	internal static class ValkyrieGripPatch
	{
		/// <summary>
		/// How firmly she grips while she stands still - the game gives a creature 0.8, which holds on slopes up to about
		/// 39 degrees, and this up to about 72
		/// </summary>
		private const float Grip = 3f;

		/// <summary>
		/// Raises a valkyrie's friction while she stands still on the ground
		/// </summary>
		/// <param name="__instance">The creature</param>
		private static void Postfix(Character __instance)
		{
			if (__instance.m_collider == null || __instance.IsDead() || __instance.IsFlying() || __instance.IsSwimming() || !__instance.IsOnGround() || __instance.m_moveDir.magnitude >= 0.1f || __instance.GetComponent<ValkyrieFlight>() == null)
			{
				return;
			}
			__instance.m_collider.material.staticFriction = Grip;
			__instance.m_collider.material.dynamicFriction = Grip;
			__instance.m_collider.material.frictionCombine = UnityEngine.PhysicsMaterialCombine.Maximum;
		}
	}
}
