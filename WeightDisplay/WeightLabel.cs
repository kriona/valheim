using TMPro;
using UnityEngine;

namespace WeightDisplay
{
	/// <summary>
	/// HUD text showing the local player's current / max carry weight
	/// </summary>
	/// <remarks>
	/// The text lives under the HUD root, so it hides along with the rest of the HUD
	/// </remarks>
	public class WeightLabel
	{
		private TextMeshProUGUI text;
		private int shownWeight = -1;
		private int shownMax = -1;
		private int shownUsedSlots = -1;
		private int shownSlots = -1;

		/// <summary>
		/// Creates the label once the HUD exists, hides it while the inventory or large map is open and otherwise
		/// refreshes its text and position
		/// </summary>
		/// <param name="fontSize">Size of the weight text</param>
		/// <param name="margin">Gap between the minimap or screen corner and the text</param>
		/// <param name="showWeight">Whether to show the weight</param>
		/// <param name="showSlots">Whether to show the slot count</param>
		/// <param name="warnings">When the weight and slot count change color and which colors they take</param>
		public void Update(int fontSize, int margin, bool showWeight, bool showSlots, WarningSettings warnings)
		{
			Player player = Player.m_localPlayer;
			Hud hud = Hud.instance;
			if (player == null || hud == null)
			{
				return;
			}
			if (text == null)
			{
				Create(hud);
			}
			bool show = (showWeight || showSlots) && !InventoryGui.IsVisible() && !Minimap.IsOpen();
			if (text.gameObject.activeSelf != show)
			{
				text.gameObject.SetActive(show);
			}
			if (!show)
			{
				return;
			}
			text.fontSize = fontSize;
			SetText(player, showWeight, showSlots, warnings);
			Position(margin);
		}

		/// <summary>
		/// Makes the next update rebuild the text, for when a setting it depends on changes
		/// </summary>
		public void Refresh()
		{
			shownWeight = -1;
			shownMax = -1;
			shownUsedSlots = -1;
			shownSlots = -1;
		}

		/// <summary>
		/// Removes the label
		/// </summary>
		public void Destroy()
		{
			if (text != null)
			{
				Object.Destroy(text.gameObject);
			}
		}

		/// <summary>
		/// Creates the text under the HUD root, anchored to the top-right corner and using the health text's font
		/// </summary>
		/// <param name="hud">The player HUD</param>
		private void Create(Hud hud)
		{
			GameObject go = new GameObject("WeightDisplay", typeof(RectTransform));
			go.transform.SetParent(hud.m_rootObject.transform, false);
			text = go.AddComponent<TextMeshProUGUI>();
			text.font = hud.m_healthText.font;
			text.fontSharedMaterial = hud.m_healthText.fontSharedMaterial;
			text.alignment = TextAlignmentOptions.TopRight;
			text.textWrappingMode = TextWrappingModes.NoWrap;
			text.raycastTarget = false;
			RectTransform rect = text.rectTransform;
			rect.anchorMin = new Vector2(1f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(1f, 1f);
			rect.sizeDelta = new Vector2(200f, 30f);
			Refresh();
		}

		/// <summary>
		/// Shows the weight and/or used inventory slots, rounding the weight the same way as the inventory screen and only
		/// rebuilding the string when a number changes
		/// </summary>
		/// <param name="player">The local player</param>
		/// <param name="showWeight">Whether to show the weight</param>
		/// <param name="showSlots">Whether to show the slot count</param>
		/// <param name="warnings">When the weight and slot count change color and which colors they take</param>
		private void SetText(Player player, bool showWeight, bool showSlots, WarningSettings warnings)
		{
			Inventory inventory = player.GetInventory();
			int weight = Mathf.CeilToInt(inventory.GetTotalWeight());
			int max = Mathf.CeilToInt(player.GetMaxCarryWeight());
			int usedSlots = inventory.NrOfItems();
			int slots = inventory.GetWidth() * inventory.GetHeight();
			if (weight == shownWeight && max == shownMax && usedSlots == shownUsedSlots && slots == shownSlots)
			{
				return;
			}
			shownWeight = weight;
			shownMax = max;
			shownUsedSlots = usedSlots;
			shownSlots = slots;
			string weightText = "Weight " + ColorWeight(weight, max, warnings) + "/" + max;
			string slotsText = "Slots " + ColorSlots(usedSlots, slots, warnings) + "/" + slots;
			if (showWeight && showSlots)
			{
				text.text = weightText + "  " + slotsText;
			}
			else
			{
				text.text = (showWeight ? weightText : slotsText);
			}
		}

		/// <summary>
		/// Colors the weight when overburdened, or when at or above the warning percentage of the maximum
		/// </summary>
		/// <param name="weight">Rounded current weight</param>
		/// <param name="max">Rounded maximum carry weight</param>
		/// <param name="warnings">When the weight changes color and which colors it takes</param>
		/// <returns>The weight, wrapped in a color tag when near or over the maximum</returns>
		private static string ColorWeight(int weight, int max, WarningSettings warnings)
		{
			if (weight > max)
			{
				return(Colorize(weight, warnings.OverburdenedColor.Value));
			}
			else if (weight * 100 >= max * warnings.WeightWarningPercent.Value)
			{
				return(Colorize(weight, warnings.WeightWarningColor.Value));
			}
			return(weight.ToString());
		}

		/// <summary>
		/// Colors the used slot count when every slot is in use, or when the warning number of open slots or fewer are left
		/// </summary>
		/// <param name="usedSlots">Number of slots holding an item</param>
		/// <param name="slots">Total number of inventory slots</param>
		/// <param name="warnings">When the slot count changes color and which colors it takes</param>
		/// <returns>The used slot count, wrapped in a color tag when near or at the total</returns>
		private static string ColorSlots(int usedSlots, int slots, WarningSettings warnings)
		{
			if (usedSlots >= slots)
			{
				return(Colorize(usedSlots, warnings.FullSlotsColor.Value));
			}
			else if (slots - usedSlots <= warnings.SlotWarningOpenSlots.Value)
			{
				return(Colorize(usedSlots, warnings.SlotWarningColor.Value));
			}
			return(usedSlots.ToString());
		}

		/// <summary>
		/// Wraps a number in a color tag, or leaves it uncolored when no color is set
		/// </summary>
		/// <param name="number">The number to color</param>
		/// <param name="color">Color name or #RRGGBB value</param>
		/// <returns>The number, wrapped in a color tag when a color is set</returns>
		private static string Colorize(int number, string color)
		{
			if (string.IsNullOrWhiteSpace(color))
			{
				return(number.ToString());
			}
			return("<color=" + color.Trim() + ">" + number + "</color>");
		}

		/// <summary>
		/// Places the text under the small minimap, or in the top-right corner when the minimap is hidden
		/// </summary>
		/// <remarks>
		/// The minimap is on its own canvas, so its corner is converted through screen space into the HUD's coordinates
		/// </remarks>
		/// <param name="margin">Gap between the minimap or screen corner and the text</param>
		private void Position(int margin)
		{
			RectTransform rect = text.rectTransform;
			Minimap minimap = Minimap.instance;
			if (minimap == null || !minimap.m_smallRoot.activeInHierarchy)
			{
				rect.anchoredPosition = new Vector2(-margin, -margin);
				return;
			}
			Vector3[] corners = new Vector3[4];
			RectTransform mapRect = minimap.m_mapImageSmall.rectTransform;
			mapRect.GetWorldCorners(corners);
			Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(GetCamera(mapRect), corners[3]);
			RectTransform parent = (RectTransform)rect.parent;
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, GetCamera(parent), out Vector2 local))
			{
				rect.localPosition = new Vector3(local.x, local.y - margin, 0f);
			}
		}

		/// <summary>
		/// The camera a UI element renders through, or null for an overlay canvas
		/// </summary>
		/// <param name="element">The UI element</param>
		/// <returns>The canvas camera, or null</returns>
		private static Camera GetCamera(Transform element)
		{
			Canvas canvas = element.GetComponentInParent<Canvas>();
			if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				return(null);
			}
			return(canvas.worldCamera);
		}
	}
}
