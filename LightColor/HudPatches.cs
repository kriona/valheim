using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// Adds the color key to the hover text of pieces with lights
	/// </summary>
	[HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
	public static class HudUpdateCrosshairPatch
	{
		/// <summary>
		/// Appends the color line to the hover text, starting with the piece's name when it has no hover text of its own
		/// </summary>
		/// <param name="__instance">The HUD</param>
		/// <param name="player">The local player</param>
		private static void Postfix(Hud __instance, Player player)
		{
			KeyboardShortcut shortcut = Plugin.ColorKey.Value;
			if (shortcut.MainKey == KeyCode.None || TextViewer.instance.IsVisible())
			{
				return;
			}
			GameObject hover = player.GetHoverObject();
			if (hover == null)
			{
				return;
			}
			LightTint tint = hover.GetComponentInParent<LightTint>();
			if (tint == null || !tint.HasEffects)
			{
				return;
			}
			string text = __instance.m_hoverName.text;
			if (text.Length == 0)
			{
				Piece piece = tint.GetComponent<Piece>();
				text = ((piece != null) ? piece.m_name : "");
			}
			__instance.m_hoverName.text = Localization.instance.Localize(text + "\n[<color=yellow><b>" + shortcut + "</b></color>] Set light color");
			__instance.m_crosshair.color = Color.yellow;
		}
	}
}
