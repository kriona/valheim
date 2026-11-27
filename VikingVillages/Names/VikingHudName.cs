using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps a PC viking's name over its health bar clear of the bar while it runs to more than one line, as it does
	/// with its job under it in debug mode (see LivingVikingBody.GetHoverName) - lifting the name so its last line sits
	/// where a name of one line would
	/// </summary>
	/// <remarks>
	/// The game centers the name in a box over the bar, so each line past the first would push the text half a line
	/// down onto the bar. The lift is worked out again only when the name's text changes
	/// </remarks>
	internal static class VikingHudName
	{
		/// <summary>
		/// A health bar's name - where the game put it, the text the lift was worked out for, and the lift
		/// </summary>
		private class Lifted
		{
			public Vector2 Home;
			public string Text = "";
			public float Lift;
		}

		/// <summary>
		/// The names of the PC vikings' health bars the game has shown, by the bar's object
		/// </summary>
		private static readonly Dictionary<GameObject, Lifted> names = new Dictionary<GameObject, Lifted>();

		/// <summary>
		/// Lifts the name over each PC viking's health bar by as much as its lines past the first take, and puts it
		/// back where the game put it once it's one line again - call it after the game updates its health bars
		/// </summary>
		/// <param name="hud">The game's health bars</param>
		public static void AfterUpdate(EnemyHud hud)
		{
			foreach (KeyValuePair<Character, EnemyHud.HudData> entry in hud.m_huds)
			{
				EnemyHud.HudData data = entry.Value;
				if (!(entry.Key is LivingVikingBody) || data.m_gui == null || !data.m_gui.activeSelf || data.m_name == null)
				{
					continue;
				}
				TextMeshProUGUI name = data.m_name;
				if (!names.TryGetValue(data.m_gui, out Lifted lifted))
				{
					lifted = new Lifted { Home = name.rectTransform.anchoredPosition };
					names[data.m_gui] = lifted;
				}
				string text = (name.text ?? "");
				if (text != lifted.Text)
				{
					lifted.Text = text;
					int lineEnd = text.IndexOf('\n');
					float width = name.rectTransform.rect.width;
					lifted.Lift = ((lineEnd < 0) ? 0f : Lift(name.GetPreferredValues(text, width, Mathf.Infinity).y, name.GetPreferredValues(text.Substring(0, lineEnd), width, Mathf.Infinity).y));
				}
				Vector2 position = lifted.Home + new Vector2(0f, lifted.Lift);
				if (name.rectTransform.anchoredPosition != position)
				{
					name.rectTransform.anchoredPosition = position;
				}
			}
			CleanUp();
		}

		/// <summary>
		/// Works out how far to lift a name centered in its box so its last line sits where its first would on its own
		/// </summary>
		/// <param name="height">The whole name's height</param>
		/// <param name="firstLine">Its first line's height on its own</param>
		/// <returns>The lift, in the name's units, never below 0</returns>
		internal static float Lift(float height, float firstLine)
		{
			return(Mathf.Max(0f, (height - firstLine) * 0.5f));
		}

		/// <summary>
		/// Forgets the names of health bars the game has taken down
		/// </summary>
		private static void CleanUp()
		{
			List<GameObject> gone = null;
			foreach (GameObject gui in names.Keys)
			{
				if (gui == null)
				{
					gone = (gone ?? new List<GameObject>());
					gone.Add(gui);
				}
			}
			if (gone == null)
			{
				return;
			}
			foreach (GameObject gui in gone)
			{
				names.Remove(gui);
			}
		}
	}
}
