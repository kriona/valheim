using System.Collections.Generic;
using UnityEngine;

namespace SkillProgress
{
	/// <summary>
	/// Stack of skill rows under the stamina bar, one for each skill the local player has trained in the last few
	/// seconds
	/// </summary>
	/// <remarks>
	/// The stack lives under the HUD root, so it hides along with the rest of the HUD. Each row stays for a few seconds
	/// after its skill's last gain and then fades out, so a skill trained once (a jump) stays up beside one trained
	/// continuously (running). Rows keep the order their skills started showing in
	/// </remarks>
	public class SkillBars
	{
		private const float Gap = 4f;
		private const float ShowSeconds = 3f;
		private const float FadeSeconds = 0.5f;

		private static readonly Dictionary<Skills.SkillType, float> trainedTimes = new Dictionary<Skills.SkillType, float>();
		private static Skills trainedSkills;

		private readonly List<Skills.SkillType> shown = new List<Skills.SkillType>();
		private readonly List<SkillRow> rows = new List<SkillRow>();
		private readonly List<Skills.SkillType> expired = new List<Skills.SkillType>();
		private GameObject root;

		/// <summary>
		/// Records that the local player trained a skill, so its row shows
		/// </summary>
		/// <param name="skills">The local player's skills</param>
		/// <param name="skillType">The skill that was trained</param>
		public static void Trained(Skills skills, Skills.SkillType skillType)
		{
			if (skills != trainedSkills)
			{
				trainedSkills = skills;
				trainedTimes.Clear();
			}
			trainedTimes[skillType] = Time.time;
		}

		/// <summary>
		/// Removes a skill's row because the skill can't level up any further
		/// </summary>
		/// <param name="skillType">The skill that was trained without gaining</param>
		public static void Maxed(Skills.SkillType skillType)
		{
			trainedTimes.Remove(skillType);
		}

		/// <summary>
		/// Creates the stack once the HUD exists, then shows a row for each recently trained skill below the stamina bar
		/// </summary>
		public void Update()
		{
			Player player = Player.m_localPlayer;
			Hud hud = Hud.instance;
			if (player == null || hud == null)
			{
				return;
			}
			if (root == null)
			{
				Create(hud);
			}
			UpdateShown(player);
			bool show = (shown.Count > 0);
			if (root.activeSelf != show)
			{
				root.SetActive(show);
			}
			if (!show)
			{
				return;
			}
			while (rows.Count < shown.Count)
			{
				rows.Add(new SkillRow(root.transform, hud));
			}
			for (int i = 0; i < rows.Count; i++)
			{
				if (i < shown.Count)
				{
					Skills.SkillType type = shown[i];
					rows[i].Show(trainedSkills.GetSkill(type), GetAlpha(trainedTimes[type]), i);
				}
				else
				{
					rows[i].Hide();
				}
			}
			Position(hud, player);
		}

		/// <summary>
		/// Removes the stack
		/// </summary>
		public void Destroy()
		{
			if (root != null)
			{
				Object.Destroy(root);
			}
		}

		/// <summary>
		/// How visible a skill's row is - fully shown for a few seconds after the skill's last gain, then fading out
		/// </summary>
		/// <param name="trainedTime">When the skill was last trained</param>
		/// <returns>The row's opacity, from 0 to 1</returns>
		private static float GetAlpha(float trainedTime)
		{
			float elapsed = Time.time - trainedTime;
			if (elapsed < ShowSeconds)
			{
				return(1f);
			}
			return(Mathf.Clamp01(1f - (elapsed - ShowSeconds) / FadeSeconds));
		}

		/// <summary>
		/// Drops skills that have faded out or can't level up any further, and adds newly trained ones to the end of the
		/// stack
		/// </summary>
		/// <param name="player">The local player</param>
		private void UpdateShown(Player player)
		{
			if (trainedSkills == null || trainedSkills.m_player != player)
			{
				shown.Clear();
				return;
			}
			expired.Clear();
			foreach (KeyValuePair<Skills.SkillType, float> trained in trainedTimes)
			{
				if (GetAlpha(trained.Value) <= 0f)
				{
					expired.Add(trained.Key);
				}
				else if (!shown.Contains(trained.Key))
				{
					shown.Add(trained.Key);
				}
			}
			foreach (Skills.SkillType type in expired)
			{
				trainedTimes.Remove(type);
			}
			shown.RemoveAll(type => !trainedTimes.ContainsKey(type));
		}

		/// <summary>
		/// Creates the stack's root under the HUD root, with its pivot at the top center of the first row - the rows of a
		/// previous HUD were destroyed along with it
		/// </summary>
		/// <param name="hud">The player HUD</param>
		private void Create(Hud hud)
		{
			rows.Clear();
			root = new GameObject("SkillProgress", typeof(RectTransform));
			root.transform.SetParent(hud.m_rootObject.transform, false);
			RectTransform rect = (RectTransform)root.transform;
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.sizeDelta = Vector2.zero;
		}

		/// <summary>
		/// Centers the stack just below the stamina bar, or below the eitr bar when that sits under the stamina bar
		/// </summary>
		/// <remarks>
		/// The bars' own rects are used rather than their world corners, so the stamina bar's show and hide animation
		/// doesn't move the stack
		/// </remarks>
		/// <param name="hud">The player HUD</param>
		/// <param name="player">The local player</param>
		private void Position(Hud hud, Player player)
		{
			Transform parent = root.transform.parent;
			Vector3 point = GetBottomCenter(hud.m_staminaBar2Root, parent);
			if (player.GetMaxEitr() > 0f && hud.m_eitrBarRoot.gameObject.activeInHierarchy)
			{
				Vector3 eitrPoint = GetBottomCenter(hud.m_eitrBarRoot, parent);
				float staminaHalfWidth = hud.m_staminaBar2Root.rect.width * 0.5f;
				if (eitrPoint.y < point.y && Mathf.Abs(eitrPoint.x - point.x) < staminaHalfWidth)
				{
					point.y = eitrPoint.y;
				}
			}
			root.transform.localPosition = new Vector3(point.x, point.y - Gap, 0f);
		}

		/// <summary>
		/// The bottom center of a bar in another transform's local space, ignoring the bar's own scale
		/// </summary>
		/// <param name="bar">The bar's root</param>
		/// <param name="space">Transform whose local space the point is returned in</param>
		/// <returns>The bar's bottom center</returns>
		private static Vector3 GetBottomCenter(RectTransform bar, Transform space)
		{
			Rect rect = bar.rect;
			Vector3 local = bar.localPosition + new Vector3(rect.center.x, rect.yMin, 0f);
			Vector3 world = bar.parent.TransformPoint(local);
			return(space.InverseTransformPoint(world));
		}
	}
}
