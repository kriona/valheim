using System.Text;
using TMPro;
using UnityEngine;

namespace CraftMore
{
	/// <summary>
	/// A line of text under the Craft button listing the game's own multi-craft key and each configured key with how
	/// many items holding it makes, e.g. "Shift: x5 (100)   Ctrl: Stack (100)"
	/// </summary>
	/// <remarks>
	/// The text is a child of the Craft button, so it hides along with the button while an item is being crafted. It
	/// doesn't wrap, so a line wider than the button spreads out evenly on both sides
	/// </remarks>
	internal static class CraftHint
	{
		/// <summary>
		/// Size of the text compared to the Craft button's own text
		/// </summary>
		private const float FontScale = 0.55f;

		/// <summary>
		/// Goes between the keys, which share one line
		/// </summary>
		private const string Separator = "   ";

		/// <summary>
		/// Gap between the bottom of the Craft button and the text
		/// </summary>
		private const float Gap = 4f;

		/// <summary>
		/// The game's multi-craft button
		/// </summary>
		private const string GameButton = "AltPlace";

		private static TextMeshProUGUI label;
		private static readonly StringBuilder builder = new StringBuilder();

		/// <summary>
		/// Shows the keys for the selected recipe, or hides the text when there's nothing to explain - no recipe, an
		/// upgrade, no keys, the hints turned off or a gamepad in use
		/// </summary>
		/// <param name="gui">The inventory GUI</param>
		/// <param name="player">The player crafting</param>
		public static void Update(InventoryGui gui, Player player)
		{
			Recipe recipe = gui.m_selectedRecipe.Recipe;
			bool show = (CraftConfig.ShowHints && recipe != null && gui.m_selectedRecipe.ItemData == null && CraftConfig.Keys.Count > 0 && !ZInput.IsGamepadActive());
			if (!show)
			{
				if (label != null)
				{
					label.gameObject.SetActive(false);
				}
				return;
			}
			if (label == null)
			{
				label = CreateLabel(gui);
				if (label == null)
				{
					return;
				}
			}
			builder.Clear();
			string gameKeyName = GetGameKeyName();
			bool showGameKey = (gameKeyName != null && Craft.DefaultAmount > 0);
			if (showGameKey)
			{
				int items = Craft.DefaultAmount * Mathf.Max(recipe.m_amount, 1);
				builder.Append(gameKeyName).Append(": x").Append(Craft.DefaultAmount).Append(" (").Append(items).Append(')');
			}
			int gameCount = (showGameKey ? Craft.DefaultAmount : -1);
			int stackCount = -1;
			foreach (CraftKey craftKey in CraftConfig.Keys)
			{
				if (craftKey.Mode == CraftMode.Stack)
				{
					stackCount = Craft.GetStackAmount(recipe);
					break;
				}
			}
			foreach (CraftKey craftKey in CraftConfig.Keys)
			{
				int count = Craft.GetCraftCount(craftKey, recipe, player);
				// A stack or Craft All that makes the same as the game's own key or a stack would only repeat that line,
				// and a stack of one is the same as pressing Craft
				if (craftKey.Mode == CraftMode.Stack && (count == gameCount || count <= 1))
				{
					continue;
				}
				if (craftKey.Mode == CraftMode.All && (count == gameCount || count == stackCount))
				{
					continue;
				}
				if (builder.Length > 0)
				{
					builder.Append(Separator);
				}
				int items = count * Mathf.Max(recipe.m_amount, 1);
				builder.Append(GetKeyName(craftKey.Key)).Append(": ").Append(GetModeName(craftKey)).Append(" (").Append(items).Append(')');
			}
			string text = builder.ToString();
			if (label.text != text)
			{
				label.text = text;
			}
			label.gameObject.SetActive(true);
		}

		/// <summary>
		/// Creates the text under the Craft button, styled from the button's own text
		/// </summary>
		/// <param name="gui">The inventory GUI</param>
		/// <returns>The text, or null when the Craft button has no text to copy</returns>
		private static TextMeshProUGUI CreateLabel(InventoryGui gui)
		{
			TMP_Text buttonText = gui.m_craftButton.GetComponentInChildren<TMP_Text>(true);
			if (buttonText == null)
			{
				return(null);
			}
			GameObject labelObject = new GameObject("CraftMoreHint", typeof(RectTransform));
			labelObject.transform.SetParent(gui.m_craftButton.transform, false);
			RectTransform rect = (RectTransform)labelObject.transform;
			rect.anchorMin = new Vector2(0f, 0f);
			rect.anchorMax = new Vector2(1f, 0f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, -Gap);
			rect.sizeDelta = new Vector2(0f, 30f);
			TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
			text.font = buttonText.font;
			text.fontSharedMaterial = buttonText.fontSharedMaterial;
			text.fontSize = ((buttonText.enableAutoSizing ? buttonText.fontSizeMax : buttonText.fontSize) * FontScale);
			text.color = Color.white;
			text.alignment = TextAlignmentOptions.Top;
			text.textWrappingMode = TextWrappingModes.NoWrap;
			text.overflowMode = TextOverflowModes.Overflow;
			text.raycastTarget = false;
			return(text);
		}

		/// <summary>
		/// The name of the key bound to the game's own multi-craft button (AltPlace, Shift by default)
		/// </summary>
		/// <returns>The key's display name, or null when the button isn't bound to a key or a configured key is the same
		/// key and takes it over</returns>
		private static string GetGameKeyName()
		{
			ZInput.ButtonDef button = (ZInput.instance != null ? ZInput.instance.GetButtonDef(GameButton) : null);
			if (button == null || button.Source == ZInput.InputSource.Gamepad || button.ButtonAction.bindings.Count == 0)
			{
				return(null);
			}
			string path = button.ButtonAction.bindings[0].effectivePath;
			if (string.IsNullOrEmpty(path))
			{
				return(null);
			}
			string key = NormalizeKey(path.Substring(path.LastIndexOf('/') + 1));
			foreach (CraftKey craftKey in CraftConfig.Keys)
			{
				if (NormalizeKey(craftKey.Key.ToString()) == key)
				{
					return(null);
				}
			}
			switch (key)
			{
				case "control":
					return("Ctrl");
				case "shift":
					return("Shift");
				case "alt":
					return("Alt");
				case "command":
					return("Cmd");
				default:
					return(Localization.instance.Localize(ZInput.instance.GetBoundKeyString(GameButton, true)));
			}
		}

		/// <summary>
		/// A key name reduced to a form that matches between a Unity KeyCode and an Input System path - lowercase,
		/// without the side, since either side of a modifier counts
		/// </summary>
		/// <param name="name">A KeyCode name like LeftControl, or the end of a path like leftCtrl</param>
		/// <returns>The reduced name, e.g. control</returns>
		private static string NormalizeKey(string name)
		{
			string key = name.ToLowerInvariant().Replace("left", "").Replace("right", "");
			switch (key)
			{
				case "ctrl":
					return("control");
				case "meta":
					return("command");
				default:
					return(key);
			}
		}

		/// <summary>
		/// What a key crafts, as shown in the text
		/// </summary>
		/// <param name="craftKey">The key's binding</param>
		/// <returns>"Stack", "All" or "x" and the number</returns>
		private static string GetModeName(CraftKey craftKey)
		{
			switch (craftKey.Mode)
			{
				case CraftMode.Stack:
					return("Stack");
				case CraftMode.All:
					return("All");
				default:
					return("x" + craftKey.Count);
			}
		}

		/// <summary>
		/// The name of a key as shown in the text, with left and right modifiers shortened to one name
		/// </summary>
		/// <param name="key">The key</param>
		/// <returns>The key's display name</returns>
		private static string GetKeyName(KeyCode key)
		{
			switch (key)
			{
				case KeyCode.LeftControl:
				case KeyCode.RightControl:
					return("Ctrl");
				case KeyCode.LeftShift:
				case KeyCode.RightShift:
					return("Shift");
				case KeyCode.LeftAlt:
				case KeyCode.RightAlt:
					return("Alt");
				case KeyCode.LeftCommand:
				case KeyCode.RightCommand:
					return("Cmd");
				default:
					return(key.ToString());
			}
		}
	}
}
