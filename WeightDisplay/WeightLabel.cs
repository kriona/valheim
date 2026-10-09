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

		/// <summary>
		/// Creates the label once the HUD exists, hides it while the inventory or large map is open and otherwise
		/// refreshes its text and position
		/// </summary>
		/// <param name="fontSize">Size of the weight text</param>
		/// <param name="margin">Gap between the minimap or screen corner and the text</param>
		public void Update(int fontSize, int margin)
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
			bool show = !InventoryGui.IsVisible() && !Minimap.IsOpen();
			if (text.gameObject.activeSelf != show)
			{
				text.gameObject.SetActive(show);
			}
			if (!show)
			{
				return;
			}
			text.fontSize = fontSize;
			SetText(player);
			Position(margin);
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
			shownWeight = -1;
			shownMax = -1;
		}

		/// <summary>
		/// Rounds the same way as the inventory screen and only rebuilds the string when a number changes
		/// </summary>
		/// <param name="player">The local player</param>
		private void SetText(Player player)
		{
			int weight = Mathf.CeilToInt(player.GetInventory().GetTotalWeight());
			int max = Mathf.CeilToInt(player.GetMaxCarryWeight());
			if (weight == shownWeight && max == shownMax)
			{
				return;
			}
			shownWeight = weight;
			shownMax = max;
			string weightText = ((weight > max) ? "<color=red>" + weight + "</color>" : weight.ToString());
			text.text = "Weight " + weightText + "/" + max;
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
