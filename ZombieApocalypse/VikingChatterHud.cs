using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Shows what a viking says to another viking under its health bar, rather than over its head - the same size as
	/// its name, and only where its health bar shows, close by and on screen
	/// </summary>
	/// <remarks>
	/// A creature's health bar shows only for a while after the player has looked at it, so a viking's is kept showing
	/// while it speaks, and goes back to the game's own showing once it has said its line
	/// </remarks>
	internal static class VikingChatterHud
	{
		/// <summary>
		/// How the line shows - how much wider than the name it can run before it wraps, how far under the health bar it
		/// sits, in pixels, and its color. It's the name's size, so it reads as easily
		/// </summary>
		private const float WidthScale = 1.6f;
		private const float Gap = 2f;
		private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.88f, 1f);

		/// <summary>
		/// How long before a health bar would hide it's held at while its viking speaks, in seconds
		/// </summary>
		private const float HoldMargin = 1f;

		/// <summary>
		/// The line under each health bar the game shows, by the bar's object
		/// </summary>
		private static readonly Dictionary<GameObject, TextMeshProUGUI> labels = new Dictionary<GameObject, TextMeshProUGUI>();

		private static readonly Vector3[] corners = new Vector3[4];

		/// <summary>
		/// Keeps the health bar of each viking that's speaking showing, before the game decides which bars show
		/// </summary>
		/// <param name="hud">The game's health bars</param>
		public static void BeforeUpdate(EnemyHud hud)
		{
			float hold = hud.m_hoverShowDuration - HoldMargin;
			foreach (KeyValuePair<Character, EnemyHud.HudData> entry in hud.m_huds)
			{
				if (entry.Value.m_hoverTimer > hold && VikingSpeech.Chattering(entry.Key, out string _))
				{
					entry.Value.m_hoverTimer = hold;
				}
			}
		}

		/// <summary>
		/// Shows each speaking viking's line under its health bar, and hides the lines of the rest
		/// </summary>
		/// <param name="hud">The game's health bars</param>
		public static void AfterUpdate(EnemyHud hud)
		{
			foreach (KeyValuePair<Character, EnemyHud.HudData> entry in hud.m_huds)
			{
				EnemyHud.HudData data = entry.Value;
				if (data.m_gui == null)
				{
					continue;
				}
				bool speaking = VikingSpeech.Chattering(entry.Key, out string text);
				labels.TryGetValue(data.m_gui, out TextMeshProUGUI label);
				if (!speaking || !data.m_gui.activeSelf)
				{
					if (label != null)
					{
						label.gameObject.SetActive(false);
					}
					continue;
				}
				if (label == null)
				{
					label = MakeLabel(data);
					if (label == null)
					{
						continue;
					}
				}
				label.gameObject.SetActive(true);
				label.text = text;
				Place(data, label);
			}
			CleanUp();
		}

		/// <summary>
		/// Makes the line for a health bar, from its name - wrapping and hanging down from where it's placed
		/// </summary>
		/// <param name="data">The health bar</param>
		/// <returns>The line, or null when the bar has no name to make it from</returns>
		private static TextMeshProUGUI MakeLabel(EnemyHud.HudData data)
		{
			if (data.m_name == null)
			{
				return(null);
			}
			GameObject copy = Object.Instantiate(data.m_name.gameObject, data.m_name.transform.parent);
			copy.name = "VikingChatter";
			TextMeshProUGUI label = copy.GetComponent<TextMeshProUGUI>();
			label.fontSize = data.m_name.fontSize;
			label.fontSizeMin = label.fontSize;
			label.fontSizeMax = label.fontSize;
			label.enableAutoSizing = false;
			label.textWrappingMode = TextWrappingModes.Normal;
			label.alignment = TextAlignmentOptions.Top;
			label.color = TextColor;
			RectTransform rect = label.rectTransform;
			rect.pivot = new Vector2(0.5f, 1f);
			rect.sizeDelta = new Vector2(data.m_name.rectTransform.rect.width * WidthScale, rect.sizeDelta.y);
			labels[data.m_gui] = label;
			return(label);
		}

		/// <summary>
		/// Puts a line just under its health bar, centered on it
		/// </summary>
		/// <param name="data">The health bar</param>
		/// <param name="label">The line</param>
		private static void Place(EnemyHud.HudData data, TextMeshProUGUI label)
		{
			RectTransform bar = data.m_gui.transform.Find("Health") as RectTransform;
			if (bar == null)
			{
				bar = data.m_name.rectTransform;
			}
			bar.GetWorldCorners(corners);
			float middle = (corners[0].x + corners[3].x) / 2f;
			label.rectTransform.position = new Vector3(middle, corners[0].y - Gap, corners[0].z);
		}

		/// <summary>
		/// Forgets the lines of health bars the game has taken down
		/// </summary>
		private static void CleanUp()
		{
			if (labels.Count == 0)
			{
				return;
			}
			List<GameObject> gone = null;
			foreach (KeyValuePair<GameObject, TextMeshProUGUI> entry in labels)
			{
				if (entry.Key == null)
				{
					if (gone == null)
					{
						gone = new List<GameObject>();
					}
					gone.Add(entry.Key);
				}
			}
			if (gone != null)
			{
				foreach (GameObject key in gone)
				{
					labels.Remove(key);
				}
			}
		}
	}
}
