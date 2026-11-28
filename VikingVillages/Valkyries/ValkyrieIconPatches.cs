using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Shows a valkyrie's own icon in the mount panel while she's ridden (see ValkyrieIcon) - or none at all when it
	/// couldn't be taken, never another creature's
	/// </summary>
	[HarmonyPatch(typeof(Hud), nameof(Hud.UpdateMount))]
	internal static class ValkyrieMountIconPatch
	{
		/// <summary>
		/// Puts her icon in the mount panel, hiding the icon when she has none, and shows it again for any other mount
		/// </summary>
		/// <param name="__instance">The HUD</param>
		/// <param name="player">The local player</param>
		private static void Postfix(Hud __instance, Player player)
		{
			if (__instance.m_mountIcon == null || player == null || !(player.GetDoodadController() is Sadle saddle) || saddle.GetCharacter() == null)
			{
				return;
			}
			if (saddle.GetCharacter().GetComponent<ValkyrieFlight>() == null)
			{
				__instance.m_mountIcon.enabled = true;
				return;
			}
			UnityEngine.Sprite icon = ValkyrieIcon.Get();
			__instance.m_mountIcon.overrideSprite = icon;
			__instance.m_mountIcon.enabled = icon != null;
		}
	}
}
