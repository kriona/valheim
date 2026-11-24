using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VikingVillages
{
	/// <summary>
	/// Shows the debug summary of the village the player is in (see VillageDebug) in a sidebar down the left of the
	/// screen, between the hotbar and the food the player has eaten, in place of the fire's hover text - turned on and
	/// off with PanelKey at a village's fire in debug mode, and shown whenever the player is inside a village while it's
	/// on
	/// </summary>
	/// <remarks>
	/// The sidebar is the first child of the canvas the game's HUD is drawn on, so everything else on that canvas - the
	/// HUD, the menus and the windows - is drawn over it, whichever way the canvas is rendered. It's fitted between the
	/// hotbar and the food as it's shown again, so it follows them when the screen or the GUI's scale changes. It's taken
	/// away with the canvas when the player leaves the world, and made again the next time it's wanted. Whether it's on
	/// lasts until the game closes
	/// </remarks>
	internal static class VillageDebugPanel
	{
		/// <summary>
		/// The key that turns the sidebar on and off, while looking at a village's fire with debug mode on
		/// </summary>
		public const KeyCode PanelKey = KeyCode.D;

		/// <summary>
		/// How often the village the player is in is looked for again and its summary shown again, in seconds - the
		/// summary itself is worked out at most once a second (see VillageDebug.Summary)
		/// </summary>
		private const float LookInterval = 0.5f;

		/// <summary>
		/// How much of the screen's width the sidebar covers, how far its text stands in from its edges and the smallest
		/// size the text shrinks to so it all fits, in the canvas's units
		/// </summary>
		private const float Width = 0.35f;
		private const float Padding = 16f;
		private const float MinFontSize = 10f;

		/// <summary>
		/// How far the sidebar stands clear of the hotbar above it and the food below it, and the least height it keeps
		/// between them before it covers the screen's full height instead, in the canvas's units
		/// </summary>
		private const float Gap = 8f;
		private const float MinHeight = 120f;

		/// <summary>
		/// The text's size when the hover text's can't be read
		/// </summary>
		private const float FallbackFontSize = 18f;

		/// <summary>
		/// The sidebar's background, a see-through black like the console's
		/// </summary>
		private static readonly Color Background = new Color(0f, 0f, 0f, 0.7f);

		/// <summary>
		/// Whether the sidebar is turned on
		/// </summary>
		private static bool on;

		/// <summary>
		/// The sidebar and its text, or null until they're made
		/// </summary>
		private static GameObject panel;
		private static TextMeshProUGUI text;

		/// <summary>
		/// The game's hotbar, found the first time the sidebar is fitted in a world, and the corners of what's measured,
		/// reused
		/// </summary>
		private static HotkeyBar hotbar;
		private static readonly Vector3[] corners = new Vector3[4];

		/// <summary>
		/// The generator's or anchor's ZDO of the village whose summary the sidebar shows, while it shows one, and when
		/// the village is next looked for
		/// </summary>
		private static ZDOID shown = ZDOID.None;
		private static float nextLook;

		/// <summary>
		/// Checks whether the sidebar is turned on, for the key's line in the fire's hover text
		/// </summary>
		/// <returns>True when it is</returns>
		public static bool IsOn()
		{
			return(on);
		}

		/// <summary>
		/// Checks whether the sidebar shows a village's summary right now, so the fire's hover text leaves it out
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when it does</returns>
		public static bool Shows(Village village)
		{
			return(village != null && !shown.IsNone() && panel != null && panel.activeSelf && village.Zdo.m_uid == shown);
		}

		/// <summary>
		/// Turns the sidebar on or off when the key is pressed at a village's fire in debug mode, and shows the summary of
		/// the village the player is in while it's on, debug mode is on and the HUD shows - call it from the plugin's
		/// Update
		/// </summary>
		public static void Update()
		{
			Player player = Player.m_localPlayer;
			if (KeyPressed(player))
			{
				on = !on;
				nextLook = 0f;
			}
			Hud hud = Hud.instance;
			if (!on || !Player.m_debugMode || player == null || hud == null || !hud.IsVisible())
			{
				Hide();
				return;
			}
			if (Time.time < nextLook)
			{
				return;
			}
			nextLook = Time.time + LookInterval;
			Village village = Find(player.transform.position);
			if (village == null || !Ensure(hud))
			{
				Hide();
				return;
			}
			string name = VillageNames.Get(village);
			string summary = ((name.Length > 0) ? "<color=orange>" + name + "</color>\n" : "") + VillageDebug.Summary(village);
			if (text.text != summary)
			{
				text.text = summary;
			}
			Fit(hud);
			if (panel.transform.GetSiblingIndex() != 0)
			{
				panel.transform.SetAsFirstSibling();
			}
			if (!panel.activeSelf)
			{
				panel.SetActive(true);
			}
			shown = village.Zdo.m_uid;
		}

		/// <summary>
		/// Checks whether the key was just pressed while debug mode is on and the player takes input and looks at a
		/// village's own fire
		/// </summary>
		/// <param name="player">The local player, or null</param>
		/// <returns>True when it was</returns>
		private static bool KeyPressed(Player player)
		{
			if (!Player.m_debugMode || player == null || !ZInput.GetKeyDown(PanelKey, false) || !player.TakeInput())
			{
				return(false);
			}
			GameObject hover = player.GetHoverObject();
			Fireplace fire = ((hover != null) ? hover.GetComponentInParent<Fireplace>() : null);
			return(fire != null && VillageDebug.Of(fire) != null);
		}

		/// <summary>
		/// Finds the loaded village a point is inside
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The village, or null when it's in none</returns>
		private static Village Find(Vector3 point)
		{
			foreach (Village village in Village.Loaded())
			{
				if (village.Contains(point))
				{
					return(village);
				}
			}
			return(null);
		}

		/// <summary>
		/// Fits the sidebar between the bottom of the hotbar and the top of the food the player has eaten (see Between)
		/// </summary>
		/// <param name="hud">The game's HUD</param>
		private static void Fit(Hud hud)
		{
			RectTransform rect = (RectTransform)panel.transform;
			RectTransform parent = rect.parent as RectTransform;
			if (parent == null)
			{
				return;
			}
			if (hotbar == null)
			{
				hotbar = parent.GetComponentInChildren<HotkeyBar>(true);
			}
			Rect area = parent.rect;
			Vector2 span = Between(area.yMin, area.yMax, FoodTop(parent, hud), HotbarBottom(parent, hotbar));
			Vector2 low = new Vector2(0f, span.x - area.yMin);
			Vector2 high = new Vector2(0f, span.y - area.yMax);
			if (rect.offsetMin != low)
			{
				rect.offsetMin = low;
			}
			if (rect.offsetMax != high)
			{
				rect.offsetMax = high;
			}
		}

		/// <summary>
		/// Works out where the sidebar's bottom and top go - Gap above the food and Gap below the hotbar, an edge at the
		/// canvas's own when what it goes by isn't found, and both at the canvas's own when that leaves less than
		/// MinHeight between them
		/// </summary>
		/// <param name="floor">The bottom of the canvas</param>
		/// <param name="ceiling">The top of the canvas</param>
		/// <param name="foodTop">The top of the food, or null when it isn't found</param>
		/// <param name="hotbarBottom">The bottom of the hotbar, or null when it isn't found or has no slots</param>
		/// <returns>The sidebar's bottom as x and its top as y, in the canvas's units</returns>
		internal static Vector2 Between(float floor, float ceiling, float? foodTop, float? hotbarBottom)
		{
			float bottom = (foodTop.HasValue ? Mathf.Max(floor, foodTop.Value + Gap) : floor);
			float top = (hotbarBottom.HasValue ? Mathf.Min(ceiling, hotbarBottom.Value - Gap) : ceiling);
			if (top - bottom < MinHeight)
			{
				return(new Vector2(floor, ceiling));
			}
			return(new Vector2(bottom, top));
		}

		/// <summary>
		/// Finds the top of the food the player has eaten - the icons and the time each has left, shown or not, so the
		/// sidebar stays put as food runs out
		/// </summary>
		/// <param name="parent">The canvas the sidebar is on</param>
		/// <param name="hud">The game's HUD</param>
		/// <returns>The top, in the canvas's units, or null when there's no food on the HUD</returns>
		private static float? FoodTop(RectTransform parent, Hud hud)
		{
			List<RectTransform> parts = new List<RectTransform>();
			if (hud.m_foodIcons != null)
			{
				foreach (Image icon in hud.m_foodIcons)
				{
					if (icon != null)
					{
						parts.Add(icon.rectTransform);
					}
				}
			}
			if (hud.m_foodTime != null)
			{
				foreach (TMP_Text time in hud.m_foodTime)
				{
					if (time != null)
					{
						parts.Add(time.rectTransform);
					}
				}
			}
			float? top = null;
			foreach (RectTransform part in parts)
			{
				part.GetWorldCorners(corners);
				foreach (Vector3 corner in corners)
				{
					float y = parent.InverseTransformPoint(corner).y;
					top = (top.HasValue ? Mathf.Max(top.Value, y) : y);
				}
			}
			return(top);
		}

		/// <summary>
		/// Finds the bottom of the hotbar - its slots and everything on them, shown or not, like the stack sizes under
		/// them, so the sidebar stays put as they come and go
		/// </summary>
		/// <param name="parent">The canvas the sidebar is on</param>
		/// <param name="bar">The hotbar, or null</param>
		/// <returns>The bottom, in the canvas's units, or null when there's no hotbar or it has no slots</returns>
		private static float? HotbarBottom(RectTransform parent, HotkeyBar bar)
		{
			if (bar == null)
			{
				return(null);
			}
			float? bottom = null;
			foreach (Transform slot in bar.transform)
			{
				foreach (RectTransform part in slot.GetComponentsInChildren<RectTransform>(true))
				{
					part.GetWorldCorners(corners);
					foreach (Vector3 corner in corners)
					{
						float y = parent.InverseTransformPoint(corner).y;
						bottom = (bottom.HasValue ? Mathf.Min(bottom.Value, y) : y);
					}
				}
			}
			return(bottom);
		}

		/// <summary>
		/// Makes the sidebar the first time it's wanted in a world - behind everything else on the canvas the HUD is
		/// drawn on, down the left of the screen, its text in the hover text's font, shrinking to fit when it's long
		/// </summary>
		/// <param name="hud">The game's HUD</param>
		/// <returns>False when the HUD's canvas can't be found</returns>
		private static bool Ensure(Hud hud)
		{
			if (panel != null && text != null)
			{
				return(true);
			}
			Canvas canvas = ((hud.m_rootObject != null) ? hud.m_rootObject.GetComponentInParent<Canvas>(true) : null);
			if (canvas == null)
			{
				return(false);
			}
			panel = new GameObject("VillageDebugPanel", typeof(RectTransform));
			panel.transform.SetParent(canvas.rootCanvas.transform, false);
			panel.transform.SetAsFirstSibling();
			RectTransform rect = (RectTransform)panel.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = new Vector2(Width, 1f);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			Image background = panel.AddComponent<Image>();
			background.color = Background;
			background.raycastTarget = false;
			GameObject textObject = new GameObject("VillageDebugText", typeof(RectTransform));
			textObject.transform.SetParent(panel.transform, false);
			text = textObject.AddComponent<TextMeshProUGUI>();
			TextMeshProUGUI template = hud.m_hoverName;
			if (template != null)
			{
				text.font = template.font;
				text.fontSharedMaterial = template.fontSharedMaterial;
			}
			text.enableAutoSizing = true;
			text.fontSizeMin = MinFontSize;
			text.fontSizeMax = ((template != null && template.fontSize > MinFontSize) ? template.fontSize : FallbackFontSize);
			text.alignment = TextAlignmentOptions.TopLeft;
			text.textWrappingMode = TextWrappingModes.Normal;
			text.overflowMode = TextOverflowModes.Truncate;
			text.richText = true;
			text.raycastTarget = false;
			RectTransform textRect = text.rectTransform;
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = new Vector2(Padding, Padding);
			textRect.offsetMax = new Vector2(-Padding, -Padding);
			panel.SetActive(false);
			return(true);
		}

		/// <summary>
		/// Hides the sidebar, so the fire's hover text shows the summary again
		/// </summary>
		private static void Hide()
		{
			shown = ZDOID.None;
			if (panel != null && panel.activeSelf)
			{
				panel.SetActive(false);
			}
		}
	}
}
