using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SkillProgress
{
	/// <summary>
	/// One skill's row: a thin progress bar with the skill's icon to its left and its name, level and percentage to
	/// its right
	/// </summary>
	public class SkillRow
	{
		public const float Height = 20f;

		private const float BarWidth = 100f;
		private const float BarHeight = 5f;
		private const float IconSize = 18f;
		private const float IconGap = 4f;
		private const float TextGap = 6f;
		private const float FontSize = 14f;

		private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.6f);
		private static readonly Color FillColor = new Color(0.45f, 0.75f, 1f, 1f);

		private readonly GameObject root;
		private readonly CanvasGroup group;
		private readonly RectTransform fill;
		private readonly Image icon;
		private readonly TextMeshProUGUI text;
		private Skills.SkillType shownType = Skills.SkillType.None;
		private string shownName;
		private int shownLevel = -1;
		private int shownTenths = -1;

		/// <summary>
		/// Creates the row's bar, icon and text, hidden until it's given a skill
		/// </summary>
		/// <param name="parent">Transform to put the row under, whose pivot is the top center of the first row</param>
		/// <param name="hud">The player HUD, whose health text font the row uses</param>
		public SkillRow(Transform parent, Hud hud)
		{
			root = new GameObject("SkillRow", typeof(RectTransform));
			root.transform.SetParent(parent, false);
			root.SetActive(false);
			group = root.AddComponent<CanvasGroup>();
			group.blocksRaycasts = false;
			group.interactable = false;
			RectTransform rootRect = (RectTransform)root.transform;
			rootRect.anchorMin = new Vector2(0.5f, 0.5f);
			rootRect.anchorMax = new Vector2(0.5f, 0.5f);
			rootRect.pivot = new Vector2(0.5f, 1f);
			rootRect.sizeDelta = new Vector2(BarWidth, Height);

			Image background = CreateImage("Background", root.transform);
			background.color = BackgroundColor;
			RectTransform bar = background.rectTransform;
			bar.anchorMin = new Vector2(0f, 0.5f);
			bar.anchorMax = new Vector2(1f, 0.5f);
			bar.pivot = new Vector2(0.5f, 0.5f);
			bar.sizeDelta = new Vector2(0f, BarHeight);

			Image fillImage = CreateImage("Fill", bar);
			fillImage.color = FillColor;
			fill = fillImage.rectTransform;
			fill.anchorMin = Vector2.zero;
			fill.anchorMax = new Vector2(0f, 1f);
			fill.offsetMin = Vector2.zero;
			fill.offsetMax = Vector2.zero;

			icon = CreateImage("Icon", root.transform);
			icon.preserveAspect = true;
			RectTransform iconRect = icon.rectTransform;
			iconRect.anchorMin = new Vector2(0f, 0.5f);
			iconRect.anchorMax = new Vector2(0f, 0.5f);
			iconRect.pivot = new Vector2(1f, 0.5f);
			iconRect.sizeDelta = new Vector2(IconSize, IconSize);
			iconRect.anchoredPosition = new Vector2(-IconGap, 0f);

			GameObject textObject = new GameObject("Text", typeof(RectTransform));
			textObject.transform.SetParent(root.transform, false);
			text = textObject.AddComponent<TextMeshProUGUI>();
			text.font = hud.m_healthText.font;
			text.fontSharedMaterial = hud.m_healthText.fontSharedMaterial;
			text.fontSize = FontSize;
			text.alignment = TextAlignmentOptions.MidlineLeft;
			text.textWrappingMode = TextWrappingModes.NoWrap;
			text.raycastTarget = false;
			RectTransform textRect = text.rectTransform;
			textRect.anchorMin = new Vector2(1f, 0.5f);
			textRect.anchorMax = new Vector2(1f, 0.5f);
			textRect.pivot = new Vector2(0f, 0.5f);
			textRect.sizeDelta = new Vector2(300f, Height);
			textRect.anchoredPosition = new Vector2(TextGap, 0f);
		}

		/// <summary>
		/// Shows a skill's progress in this row
		/// </summary>
		/// <param name="skill">The skill to show</param>
		/// <param name="alpha">The row's opacity, from 0 to 1</param>
		/// <param name="index">The row's place in the stack, 0 being the top</param>
		public void Show(Skills.Skill skill, float alpha, int index)
		{
			if (!root.activeSelf)
			{
				root.SetActive(true);
			}
			group.alpha = alpha;
			root.transform.localPosition = new Vector3(0f, -index * Height, 0f);
			SetProgress(skill);
		}

		/// <summary>
		/// Hides the row
		/// </summary>
		public void Hide()
		{
			if (root.activeSelf)
			{
				root.SetActive(false);
			}
		}

		/// <summary>
		/// Creates an image that ignores the mouse
		/// </summary>
		/// <param name="name">Name of the image's game object</param>
		/// <param name="parent">Transform to put the image under</param>
		/// <returns>The new image</returns>
		private static Image CreateImage(string name, Transform parent)
		{
			GameObject go = new GameObject(name, typeof(RectTransform));
			go.transform.SetParent(parent, false);
			Image image = go.AddComponent<Image>();
			image.raycastTarget = false;
			return(image);
		}

		/// <summary>
		/// Fills the bar to the skill's progress toward its next level and shows its icon, name, level and percentage,
		/// only rebuilding the text when one of them changes
		/// </summary>
		/// <param name="skill">The skill to show</param>
		private void SetProgress(Skills.Skill skill)
		{
			float progress = skill.GetLevelPercentage();
			fill.anchorMax = new Vector2(progress, 1f);
			Skills.SkillType type = skill.m_info.m_skill;
			if (type != shownType)
			{
				shownType = type;
				shownName = Localization.instance.Localize("$skill_" + type.ToString().ToLower());
				icon.sprite = skill.m_info.m_icon;
				icon.enabled = (icon.sprite != null);
				shownLevel = -1;
			}
			int level = (int)skill.m_level;
			int tenths = Mathf.FloorToInt(progress * 1000f);
			if (level == shownLevel && tenths == shownTenths)
			{
				return;
			}
			shownLevel = level;
			shownTenths = tenths;
			text.text = shownName + " " + level + "  " + (tenths / 10f).ToString("0.0") + "%";
		}
	}
}
