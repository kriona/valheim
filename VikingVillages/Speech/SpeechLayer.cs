using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Draws what a viking says, and its name over its health bar while it speaks, above other mods' markers - each piece
	/// in a canvas of its own, sorted above them - and behind the big map and the black loading screen while either
	/// shows
	/// </summary>
	internal static class SpeechLayer
	{
		/// <summary>
		/// Above CreatureMarker's markers (450) and the game's health bars it sorts over them (455), and below the top
		/// left messages (500), the inventory and every other screen opened over the game
		/// </summary>
		private const int SortingOrder = 460;

		/// <summary>
		/// Just below the game's HUD (400), whose canvas holds the big map and the black loading screen shown while
		/// teleporting, sleeping or dead - where the speech sorts while either shows, so it's covered
		/// </summary>
		private const int CoveredOrder = 399;

		/// <summary>
		/// Raises a piece of the HUD above the markers - or while the big map or the loading screen shows, sorts it just
		/// behind them - or puts it back in its own canvas's order. Call it every frame for a raised piece, so it follows
		/// the map and the loading screen as they open and close
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
			if (raised)
			{
				Sort(canvas, SortingOrder);
			}
		}

		/// <summary>
		/// Sorts a canvas of its own drawn over the game's HUD at its order - or while the big map or the loading screen
		/// shows, just behind them. Call it every frame, so it follows the map and the loading screen as they open and
		/// close
		/// </summary>
		/// <param name="canvas">The canvas, with its sorting overridden</param>
		/// <param name="order">Its order while neither shows</param>
		public static void Sort(Canvas canvas, int order)
		{
			int wanted = (Covered() ? CoveredOrder : order);
			if (canvas != null && canvas.sortingOrder != wanted)
			{
				canvas.sortingOrder = wanted;
			}
		}

		/// <summary>
		/// Checks whether the big map or the black loading screen shows - the loading screen even while it fades in or out
		/// </summary>
		/// <returns>True when either does</returns>
		private static bool Covered()
		{
			Hud hud = Hud.instance;
			bool loading = (hud != null && hud.m_loadingScreen != null && hud.m_loadingScreen.gameObject.activeSelf);
			return(loading || Minimap.IsOpen());
		}
	}
}
