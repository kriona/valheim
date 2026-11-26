using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Draws what a viking says, and its name over its health bar while it speaks, above other mods' markers - each piece
	/// in a canvas of its own, sorted above them
	/// </summary>
	internal static class SpeechLayer
	{
		/// <summary>
		/// Above CreatureMarker's markers (450) and the game's health bars it sorts over them (455), and below the top
		/// left messages (500), the inventory and every other screen opened over the game
		/// </summary>
		private const int SortingOrder = 460;

		/// <summary>
		/// Raises a piece of the HUD above the markers, or puts it back in its own canvas's order
		/// </summary>
		/// <remarks>
		/// Putting it back turns its canvas's sorting off rather than the canvas itself, since a disabled canvas draws
		/// nothing under it
		/// </remarks>
		/// <param name="go">The piece</param>
		/// <param name="raised">Whether it's drawn above the markers</param>
		public static void Set(GameObject go, bool raised)
		{
			if (go == null)
			{
				return;
			}
			Canvas canvas = go.GetComponent<Canvas>();
			if (canvas == null)
			{
				if (!raised)
				{
					return;
				}
				canvas = go.AddComponent<Canvas>();
			}
			if (canvas.overrideSorting != raised)
			{
				canvas.overrideSorting = raised;
			}
			if (raised && canvas.sortingOrder != SortingOrder)
			{
				canvas.sortingOrder = SortingOrder;
			}
		}
	}
}
