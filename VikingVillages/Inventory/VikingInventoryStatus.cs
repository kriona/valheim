using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VikingVillages
{
	/// <summary>
	/// Shows status effects under each inventory while the window shows a viking's, the way the HUD shows the player's -
	/// each effect's icon, name and time left - the viking's under its inventory, including resting and rested, which
	/// the mod keeps for a viking in place of the game's own effects (see VikingRested), and the player's under theirs
	/// </summary>
	/// <remarks>
	/// The icons are copies of the HUD's own, laid out in rows under each inventory from its left edge, and move along
	/// with it as the window animates
	/// </remarks>
	internal static class VikingInventoryStatus
	{
		/// <summary>
		/// The space between an inventory and the icons under it, in the window's units
		/// </summary>
		private const float Gap = 8f;

		/// <summary>
		/// A row of icons under an inventory - what holds them, and the icons in it
		/// </summary>
		private class Row
		{
			public RectTransform Holder;
			public readonly List<RectTransform> Icons = new List<RectTransform>();
		}

		/// <summary>
		/// The rows under the viking's inventory and the player's, or null while none show
		/// </summary>
		private static Row vikingRow;
		private static Row playerRow;

		/// <summary>
		/// An effect as an icon shows it
		/// </summary>
		private struct Shown
		{
			public Sprite Icon;
			public string Name;
			public string Time;
			public bool Flash;
			public bool Cooldown;
		}

		private static readonly List<Shown> shown = new List<Shown>();
		private static readonly List<StatusEffect> effects = new List<StatusEffect>();

		/// <summary>
		/// Starts showing the effects under the viking's inventory and the player's
		/// </summary>
		/// <param name="container">The viking's inventory panel</param>
		/// <param name="player">The player's inventory panel</param>
		public static void Show(RectTransform container, RectTransform player)
		{
			Hide();
			if (Hud.instance == null || Hud.instance.m_statusEffectTemplate == null)
			{
				return;
			}
			vikingRow = MakeRow(container, "VikingStatusEffects");
			playerRow = MakeRow(player, "PlayerStatusEffects");
		}

		/// <summary>
		/// Keeps the icons showing the viking's effects and the player's as they are now - call it every frame while the
		/// window shows the viking's inventory
		/// </summary>
		/// <param name="viking">The viking</param>
		public static void Update(Character viking)
		{
			if (vikingRow != null && viking != null)
			{
				shown.Clear();
				if (viking is LivingVikingBody living)
				{
					Add(VikingRested.RestingEffect(), living.Rest.RestingLeft());
					Add(VikingRested.RestedEffect(), living.Rest.RestedLeft());
				}
				AddEffects(viking);
				Fill(vikingRow);
			}
			if (playerRow != null && Player.m_localPlayer != null)
			{
				shown.Clear();
				AddEffects(Player.m_localPlayer);
				Fill(playerRow);
			}
		}

		/// <summary>
		/// Takes the icons away
		/// </summary>
		public static void Hide()
		{
			foreach (Row row in new[] { vikingRow, playerRow })
			{
				if (row != null && row.Holder != null)
				{
					Object.Destroy(row.Holder.gameObject);
				}
			}
			vikingRow = null;
			playerRow = null;
		}

		/// <summary>
		/// Makes a row to hold icons under an inventory, from its left edge across its width
		/// </summary>
		/// <param name="panel">The inventory panel</param>
		/// <param name="name">The row's name</param>
		/// <returns>The row, or null without a panel</returns>
		private static Row MakeRow(RectTransform panel, string name)
		{
			if (panel == null)
			{
				return(null);
			}
			GameObject made = new GameObject(name, typeof(RectTransform));
			RectTransform holder = made.transform as RectTransform;
			holder.SetParent(panel, false);
			// The panel's own layout would place it among the panel's parts
			made.AddComponent<LayoutElement>().ignoreLayout = true;
			holder.anchorMin = Vector2.zero;
			holder.anchorMax = new Vector2(1f, 0f);
			holder.pivot = new Vector2(0f, 1f);
			holder.sizeDelta = Vector2.zero;
			holder.anchoredPosition = new Vector2(0f, -Gap);
			return(new Row { Holder = holder });
		}

		/// <summary>
		/// Adds the game's effects on a character that the HUD would show to those shown
		/// </summary>
		/// <param name="character">The character</param>
		private static void AddEffects(Character character)
		{
			effects.Clear();
			if (character.GetSEMan() != null)
			{
				character.GetSEMan().GetHUDStatusEffects(effects);
			}
			foreach (StatusEffect effect in effects)
			{
				shown.Add(new Shown { Icon = effect.m_icon, Name = effect.m_name, Time = effect.GetIconText(), Flash = effect.m_flashIcon, Cooldown = effect.m_cooldownIcon });
			}
		}

		/// <summary>
		/// Adds one of the effects the mod keeps for a viking to those shown, while the viking has it
		/// </summary>
		/// <param name="effect">The game's effect it stands for, for its icon and name, or null</param>
		/// <param name="left">The seconds it has left, or -1 when the viking doesn't have it</param>
		private static void Add(StatusEffect effect, float left)
		{
			if (effect == null || effect.m_icon == null || left < 0f)
			{
				return;
			}
			shown.Add(new Shown { Icon = effect.m_icon, Name = effect.m_name, Time = StatusEffect.GetTimeString(left) });
		}

		/// <summary>
		/// Shows the effects shown on a row's icons, making the icons again when their number changes
		/// </summary>
		/// <param name="row">The row</param>
		private static void Fill(Row row)
		{
			if (row.Holder == null)
			{
				return;
			}
			if (row.Icons.Count != shown.Count)
			{
				MakeIcons(row);
			}
			for (int i = 0; i < shown.Count && i < row.Icons.Count; i++)
			{
				Fill(row.Icons[i], shown[i]);
			}
		}

		/// <summary>
		/// Makes an icon on a row for each effect shown, in rows from the inventory's left edge, as many to a row as fit
		/// across it at the HUD's spacing
		/// </summary>
		/// <param name="row">The row</param>
		private static void MakeIcons(Row row)
		{
			foreach (RectTransform icon in row.Icons)
			{
				if (icon != null)
				{
					Object.Destroy(icon.gameObject);
				}
			}
			row.Icons.Clear();
			Hud hud = Hud.instance;
			if (hud == null || hud.m_statusEffectTemplate == null)
			{
				return;
			}
			float spacing = hud.m_statusEffectSpacing;
			int perRow = Mathf.Max(1, Mathf.FloorToInt(row.Holder.rect.width / spacing));
			for (int i = 0; i < shown.Count; i++)
			{
				RectTransform icon = Object.Instantiate(hud.m_statusEffectTemplate, row.Holder);
				icon.gameObject.SetActive(true);
				// Placed by its top left corner, whatever its pivot
				icon.anchorMin = new Vector2(0f, 1f);
				icon.anchorMax = new Vector2(0f, 1f);
				Vector2 size = icon.rect.size;
				int line = i / perRow;
				int column = i - line * perRow;
				icon.anchoredPosition = new Vector2(column * spacing + size.x * icon.pivot.x, -line * spacing - size.y * (1f - icon.pivot.y));
				// It only shows the effect, so it lets the pointer through
				foreach (Graphic graphic in icon.GetComponentsInChildren<Graphic>(true))
				{
					graphic.raycastTarget = false;
				}
				row.Icons.Add(icon);
			}
		}

		/// <summary>
		/// Shows an effect on an icon, the way the HUD does - its picture, flashing when it asks to, its name and its time
		/// left
		/// </summary>
		/// <param name="icon">The icon</param>
		/// <param name="effect">The effect</param>
		private static void Fill(RectTransform icon, Shown effect)
		{
			Transform picture = icon.Find("Icon");
			if (picture != null && picture.TryGetComponent(out Image image))
			{
				image.sprite = effect.Icon;
				image.color = ((effect.Flash && Mathf.Sin(Time.time * 10f) > 0f) ? Hud.s_colorRedish : Color.white);
			}
			Transform cooldown = icon.Find("Cooldown");
			if (cooldown != null)
			{
				cooldown.gameObject.SetActive(effect.Cooldown);
			}
			TMP_Text name = icon.GetComponentInChildren<TMP_Text>();
			if (name != null)
			{
				name.text = Localization.instance.Localize(effect.Name);
			}
			Transform time = icon.Find("TimeText");
			if (time != null && time.TryGetComponent(out TMP_Text timeText))
			{
				bool hasTime = !string.IsNullOrEmpty(effect.Time);
				time.gameObject.SetActive(hasTime);
				timeText.text = (hasTime ? effect.Time : "");
			}
		}
	}
}
