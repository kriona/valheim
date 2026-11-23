using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Lays out a long hover text in debug mode so it stays on screen at the same size - wider, so fewer of its lines
	/// wrap, and centered on the crosshair rather than running down from it - putting the game's own layout back for
	/// the rest
	/// </summary>
	[HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
	public static class DebugHoverLayoutPatch
	{
		/// <summary>
		/// How many lines a hover text has to have to be laid out this way, and how much wider it's made
		/// </summary>
		private const int LongLines = 12;
		private const float WideScale = 2f;

		/// <summary>
		/// The hover text's own size and alignment, read the first time, and whether it's laid out wide now
		/// </summary>
		private static bool saved;
		private static Vector2 size;
		private static TextAlignmentOptions alignment;
		private static bool wide;

		/// <summary>
		/// Widens and centers the hover text while it's long in debug mode, and puts it back otherwise
		/// </summary>
		/// <param name="__instance">The HUD</param>
		private static void Postfix(Hud __instance)
		{
			TextMeshProUGUI text = __instance.m_hoverName;
			if (text == null)
			{
				return;
			}
			RectTransform rect = text.rectTransform;
			if (!saved)
			{
				size = rect.sizeDelta;
				alignment = text.alignment;
				saved = true;
			}
			bool tall = (Player.m_debugMode && Lines(text.text) > LongLines);
			if (tall == wide)
			{
				return;
			}
			wide = tall;
			rect.sizeDelta = size;
			text.alignment = alignment;
			if (tall)
			{
				rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rect.rect.width * WideScale);
				text.alignment = TextAlignmentOptions.Left;
			}
		}

		/// <summary>
		/// Counts the lines of a text
		/// </summary>
		/// <param name="value">The text</param>
		/// <returns>How many</returns>
		private static int Lines(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return(0);
			}
			int lines = 1;
			foreach (char character in value)
			{
				if (character == '\n')
				{
					lines++;
				}
			}
			return(lines);
		}
	}
}
