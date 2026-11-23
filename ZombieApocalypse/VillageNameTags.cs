using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Shows a village's name over its bonfire or campfire while the player is within ShowRange of it, the way the
	/// game shows a creature's name over it - in the same font, beside the health bars, so it hides along with them
	/// </summary>
	/// <remarks>
	/// It's placed each frame once the camera has moved (see VillageNameTagPatch), so it keeps up with it
	/// </remarks>
	internal static class VillageNameTags
	{
		/// <summary>
		/// How near the fire the player has to be to see the name, and how far above the top of the fire's pieces it
		/// shows, over the flames, in meters
		/// </summary>
		private const float ShowRange = 20f;
		private const float Lift = 1.5f;

		/// <summary>
		/// The name's size and color - the village's name's color in the fire's hover text
		/// </summary>
		private const float FontSize = 20f;
		private static readonly Color NameColor = new Color(1f, 0.65f, 0f, 1f);

		/// <summary>
		/// A village's name shown over its fire - the label, and the fire and name it was last set for
		/// </summary>
		private class Tag
		{
			public TextMeshProUGUI Label;
			public Fireplace Fire;
			public string Name;
			public float Top;
		}

		/// <summary>
		/// The name over each village's fire, by its generator's ZDO, and the health bars' root they're shown in
		/// </summary>
		private static readonly Dictionary<ZDOID, Tag> tags = new Dictionary<ZDOID, Tag>();
		private static GameObject root;

		/// <summary>
		/// Reused for the villages whose names are no longer shown
		/// </summary>
		private static readonly List<ZDOID> gone = new List<ZDOID>();
		private static readonly HashSet<ZDOID> shown = new HashSet<ZDOID>();

		/// <summary>
		/// Shows each named village's name over its fire while the player is near and it's on screen, and hides the rest
		/// - call it once the camera has moved for the frame
		/// </summary>
		public static void Place()
		{
			EnemyHud hud = EnemyHud.instance;
			Camera camera = Utils.GetMainCamera();
			Player player = Player.m_localPlayer;
			if (hud == null || hud.m_hudRoot == null)
			{
				return;
			}
			if (root != hud.m_hudRoot)
			{
				// The labels were destroyed along with the health bars' previous root
				root = hud.m_hudRoot;
				tags.Clear();
			}
			shown.Clear();
			if (camera != null && player != null && ServerCheck.Active)
			{
				foreach (Village village in Village.Loaded())
				{
					Fireplace fire = village.Fire();
					string name = VillageNames.Get(village);
					if (fire == null || name.Length == 0 || village.Zdo == null || Village.FlatDistance(fire.transform.position, player.transform.position) > ShowRange)
					{
						continue;
					}
					Tag tag = GetTag(village.Zdo.m_uid, hud);
					if (tag.Fire != fire)
					{
						tag.Fire = fire;
						tag.Top = FireTop(fire);
					}
					Vector3 screen = camera.WorldToScreenPointScaled(new Vector3(fire.transform.position.x, tag.Top + Lift, fire.transform.position.z));
					if (screen.z <= 0f)
					{
						continue;
					}
					if (tag.Name != name)
					{
						tag.Name = name;
						tag.Label.text = name;
						tag.Label.rectTransform.sizeDelta = tag.Label.GetPreferredValues();
					}
					tag.Label.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
					if (!tag.Label.gameObject.activeSelf)
					{
						tag.Label.gameObject.SetActive(true);
					}
					shown.Add(village.Zdo.m_uid);
				}
			}
			foreach (KeyValuePair<ZDOID, Tag> entry in tags)
			{
				if (entry.Value.Label == null)
				{
					gone.Add(entry.Key);
				}
				else if (!shown.Contains(entry.Key) && entry.Value.Label.gameObject.activeSelf)
				{
					entry.Value.Label.gameObject.SetActive(false);
				}
			}
			foreach (ZDOID id in gone)
			{
				tags.Remove(id);
			}
			gone.Clear();
		}

		/// <summary>
		/// Finds how high the top of a fire's pieces is - its logs and stones, below the flames
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>The height, in meters</returns>
		private static float FireTop(Fireplace fire)
		{
			float top = fire.transform.position.y;
			foreach (Collider collider in fire.GetComponentsInChildren<Collider>())
			{
				if (collider != null && collider.enabled && !collider.isTrigger)
				{
					top = Mathf.Max(top, collider.bounds.max.y);
				}
			}
			return(top);
		}

		/// <summary>
		/// Gets the name over a village's fire, making its label when there isn't one yet, in the health bars' root and
		/// styled like their creatures' names
		/// </summary>
		/// <param name="id">The village's generator's ZDO</param>
		/// <param name="hud">The game's health bars</param>
		/// <returns>The name</returns>
		private static Tag GetTag(ZDOID id, EnemyHud hud)
		{
			if (tags.TryGetValue(id, out Tag tag) && tag.Label != null)
			{
				return(tag);
			}
			GameObject go = new GameObject("VillageNameTag", typeof(RectTransform));
			go.transform.SetParent(hud.m_hudRoot.transform, false);
			TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
			Transform nameTemplate = ((hud.m_baseHud != null) ? hud.m_baseHud.transform.Find("Name") : null);
			if (nameTemplate != null && nameTemplate.TryGetComponent(out TMP_Text template))
			{
				label.font = template.font;
				label.fontSharedMaterial = template.fontSharedMaterial;
			}
			label.fontSize = FontSize;
			label.color = NameColor;
			label.alignment = TextAlignmentOptions.Center;
			label.textWrappingMode = TextWrappingModes.NoWrap;
			label.raycastTarget = false;
			label.rectTransform.pivot = new Vector2(0.5f, 0f);
			tag = new Tag { Label = label };
			tags[id] = tag;
			return(tag);
		}
	}
}
