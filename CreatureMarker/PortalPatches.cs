using HarmonyLib;

namespace CreatureMarker
{
	/// <summary>
	/// Adds each portal to the portal list
	/// </summary>
	[HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Awake))]
	public static class TeleportWorldAwakePatch
	{
		/// <summary>
		/// Adds the portal to the portal list
		/// </summary>
		/// <param name="__instance">The portal that was just created</param>
		private static void Postfix(TeleportWorld __instance)
		{
			PortalList.Add(__instance);
		}
	}
}
