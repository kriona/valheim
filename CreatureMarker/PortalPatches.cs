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
		/// Adds the portal to the portal list, leaving out a portal with no ZDO, such as the ghost of one being placed
		/// </summary>
		/// <param name="__instance">The portal that was just created</param>
		private static void Postfix(TeleportWorld __instance)
		{
			if (__instance.m_nview == null || __instance.m_nview.GetZDO() == null)
			{
				return;
			}
			PortalList.Add(__instance);
		}
	}
}
