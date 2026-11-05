using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureMarker
{
	/// <summary>
	/// Colored arrows above marked creatures, pinned to the screen edge and pointing at them when they are off-screen,
	/// with an optional name label behind each arrow
	/// </summary>
	/// <remarks>
	/// The markers live under the enemy HUD root, so they share the health bars' canvas and hide along with them
	/// </remarks>
	public class MarkerHud
	{
		private const float HeadOffset = 0.4f;
		private const float EdgeMargin = 40f;
		private const float BobSpeed = 5f;
		private const float LabelGap = 2f;
		private const float LabelFontSize = 16f;
		private const float GameHudGap = 4f;
		private static readonly Vector2 ArrowSize = new Vector2(24f, 32f);
		internal static readonly Color OutlineColor = new Color(0.1f, 0.1f, 0.1f, 1f);

		/// <summary>
		/// One creature's, fish's or bird's arrow and name label
		/// </summary>
		/// <remarks>
		/// The label is a sibling of the arrow so it stays upright when the arrow rotates at the screen edge
		/// </remarks>
		private class Marker
		{
			public Image arrow;
			public TextMeshProUGUI label;
			public Object source;

			/// <summary>
			/// The target's localized name, looked up when the marker is given a new target
			/// </summary>
			public string name;

			/// <summary>
			/// The name, level, star color and distance in the label's text, so the text is only rebuilt when one of
			/// them changes
			/// </summary>
			/// <remarks>
			/// The name is null when names are hidden, the level is 0 when levels are hidden and the distance is -1
			/// when distances are hidden, so a distance of -2 means the text hasn't been set
			/// </remarks>
			public string shownName;
			public int shownLevel;
			public bool shownFish;
			public Color shownColor;
			public int shownDistance = -2;
		}

		private readonly List<Marker> markers = new List<Marker>();
		private GameObject root;
		private Sprite sprite;

		/// <summary>
		/// The star shown for each of a creature's stars, set up when the first label is created
		/// </summary>
		private readonly LevelStars stars = new LevelStars();

		/// <summary>
		/// Reused when measuring the game's own HUD above a creature
		/// </summary>
		private readonly List<Graphic> graphics = new List<Graphic>();
		private readonly Vector3[] corners = new Vector3[4];

		/// <summary>
		/// Shows an arrow for each marked creature, fish and bird when Show On Screen is on and hides the spare ones
		/// </summary>
		/// <param name="marked">The living marked creatures and the marked fish and birds within range</param>
		/// <param name="player">The local player</param>
		public void Update(List<Target> marked, Player player)
		{
			EnemyHud enemyHud = EnemyHud.instance;
			Camera camera = Utils.GetMainCamera();
			if (enemyHud == null || camera == null)
			{
				return;
			}
			if (root != enemyHud.m_hudRoot)
			{
				// The markers were destroyed along with the previous HUD root
				root = enemyHud.m_hudRoot;
				markers.Clear();
			}
			int used = 0;
			if (CreatureConfig.ShowOnScreen)
			{
				bool showName = CreatureConfig.ShowName;
				bool showDistance = CreatureConfig.ShowDistance;
				bool showLevel = CreatureConfig.ShowLevel;
				bool showLabel = (showName || showDistance || showLevel);
				float farDistanceSquared = CreatureConfig.FarDistance * CreatureConfig.FarDistance;
				Vector3 farScale = Vector3.one * CreatureConfig.FarScale;
				Vector3 playerPosition = player.transform.position;
				foreach (Target target in marked)
				{
					Marker marker = GetMarker(used, enemyHud);
					used++;
					float distanceSquared = Vector3.SqrMagnitude(target.position - playerPosition);
					Vector3 scale = ((distanceSquared > farDistanceSquared) ? farScale : Vector3.one);
					// The label is scaled along with the arrow, so placing it can go by the arrow's scale alone
					marker.arrow.rectTransform.localScale = scale;
					marker.label.rectTransform.localScale = scale;
					marker.arrow.color = target.color;
					// While the game's HUD shows the name and level above an on-screen creature, the arrow sits above it and
					// the label leaves them out - an arrow pinned to the screen edge is away from the HUD, so it keeps them
					float gameHudHeight = GetGameHudHeight(enemyHud, target.source);
					Vector2 pointing;
					bool onScreen;
					if (gameHudHeight >= 0f)
					{
						pointing = Place(marker.arrow.rectTransform, camera, target.top, gameHudHeight + GameHudGap, scale.x, out onScreen);
					}
					else
					{
						pointing = Place(marker.arrow.rectTransform, camera, target.top + Vector3.up * HeadOffset, 0f, scale.x, out onScreen);
					}
					bool underGameHud = (gameHudHeight >= 0f && onScreen);
					bool showText = (underGameHud ? showDistance : showLabel);
					SetActive(marker.label.gameObject, showText);
					if (showText)
					{
						if (marker.source != target.source)
						{
							marker.source = target.source;
							marker.name = Localization.instance.Localize(target.nameToken);
						}
						int distance = (showDistance ? Mathf.RoundToInt(Mathf.Sqrt(distanceSquared)) : -1);
						string name = ((showName && !underGameHud) ? marker.name : null);
						int level = ((showLevel && !underGameHud) ? target.level : 0);
						SetLabelText(marker, name, level, target.fish, target.color, distance);
						marker.label.color = target.color;
						PlaceLabel(marker, pointing);
					}
				}
			}
			for (int i = used; i < markers.Count; i++)
			{
				markers[i].source = null;
				markers[i].shownName = null;
				markers[i].shownDistance = -2;
				SetActive(markers[i].arrow.gameObject, false);
				SetActive(markers[i].label.gameObject, false);
			}
		}

		/// <summary>
		/// Removes the markers, the generated sprite and the star sprite asset
		/// </summary>
		public void Destroy()
		{
			stars.Destroy();
			foreach (Marker marker in markers)
			{
				if (marker.arrow != null)
				{
					Object.Destroy(marker.arrow.gameObject);
				}
				if (marker.label != null)
				{
					Object.Destroy(marker.label.gameObject);
				}
			}
			markers.Clear();
			if (sprite != null)
			{
				Object.Destroy(sprite.texture);
				Object.Destroy(sprite);
			}
		}

		/// <summary>
		/// Puts the arrow's tip just above the target, or on the screen edge in the target's direction when it is off-screen
		/// </summary>
		/// <param name="arrow">The arrow to move</param>
		/// <param name="camera">The main camera</param>
		/// <param name="target">World point the arrow points at</param>
		/// <param name="lift">How many screen pixels above the target an on-screen arrow's tip sits</param>
		/// <param name="size">The marker's scale, which shrinks the bob along with the arrow</param>
		/// <param name="onScreen">Set to whether the target is on screen, rather than the arrow pinned to the edge</param>
		/// <returns>The screen direction the arrow points in, as a unit vector</returns>
		private static Vector2 Place(RectTransform arrow, Camera camera, Vector3 target, float lift, float size, out bool onScreen)
		{
			Vector3 screen = camera.WorldToScreenPointScaled(target);
			onScreen = (screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height);
			if (onScreen)
			{
				float bob = Mathf.Abs(Mathf.Sin(Time.time * BobSpeed)) * CreatureConfig.BobHeight * size;
				arrow.position = new Vector3(screen.x, screen.y + lift + bob, 0f);
				arrow.localRotation = Quaternion.identity;
				return(Vector2.down);
			}
			Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
			Vector2 direction = new Vector2(screen.x, screen.y) - center;
			if (screen.z < 0f)
			{
				// Points behind the camera project mirrored through the screen center
				direction = -direction;
			}
			if (direction.sqrMagnitude < 0.01f)
			{
				direction = Vector2.down;
			}
			float halfWidth = center.x - EdgeMargin;
			float halfHeight = center.y - EdgeMargin;
			float scale = Mathf.Min(halfWidth / Mathf.Max(Mathf.Abs(direction.x), 0.001f), halfHeight / Mathf.Max(Mathf.Abs(direction.y), 0.001f));
			Vector2 edge = center + direction * scale;
			arrow.position = new Vector3(edge.x, edge.y, 0f);
			// The arrow graphic points down, so rotate down onto the direction
			float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
			arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
			return(direction.normalized);
		}

		/// <summary>
		/// How far the top of the game's own name, stars and health bar sits above the creature's top point, in screen
		/// pixels, while the game shows them above the creature
		/// </summary>
		/// <remarks>
		/// The game places its HUD at the creature's top point, so measuring from the HUD's own position gives a height
		/// that holds whether the game has moved it yet this frame or not. Bosses are left out since their bar sits at
		/// the top of the screen
		/// </remarks>
		/// <param name="enemyHud">The enemy HUD</param>
		/// <param name="source">The marked Character, Trader, Petable, Fish or RandomFlyingBird</param>
		/// <returns>The height, or -1 when the game isn't showing a name above the target</returns>
		private float GetGameHudHeight(EnemyHud enemyHud, Object source)
		{
			Character character = source as Character;
			if (character == null || character.IsBoss() || !enemyHud.m_huds.TryGetValue(character, out EnemyHud.HudData data))
			{
				return(-1f);
			}
			if (data.m_isMount || data.m_gui == null || !data.m_gui.activeSelf || data.m_name == null || !data.m_name.gameObject.activeInHierarchy)
			{
				return(-1f);
			}
			float bottom = data.m_gui.transform.position.y;
			float top = bottom;
			data.m_gui.GetComponentsInChildren(false, graphics);
			foreach (Graphic graphic in graphics)
			{
				if (graphic.color.a <= 0f)
				{
					continue;
				}
				if (graphic is TMP_Text text)
				{
					// The text's rect can be taller than the letters, so measure the letters themselves
					if (!string.IsNullOrEmpty(text.text))
					{
						top = Mathf.Max(top, text.rectTransform.TransformPoint(text.textBounds.max).y);
					}
				}
				else
				{
					graphic.rectTransform.GetWorldCorners(corners);
					top = Mathf.Max(top, corners[1].y);
				}
			}
			graphics.Clear();
			return(top - bottom);
		}

		/// <summary>
		/// Sets the label to the name, then the level, then the distance in brackets when there is text before it -
		/// like "Greydwarf ⭐⭐ (5m)", "Perch[3]" or "5m" - rebuilding the text only when one of them has changed
		/// </summary>
		/// <param name="marker">The marker whose label to set</param>
		/// <param name="name">The name to show, or null to leave it out</param>
		/// <param name="level">The level to show, or 0 to leave it out</param>
		/// <param name="fish">Whether the level is a fish's, shown as a number instead of stars</param>
		/// <param name="color">The marker color, which fills the stars</param>
		/// <param name="distance">The distance in meters to show, or -1 to leave it out</param>
		private void SetLabelText(Marker marker, string name, int level, bool fish, Color color, int distance)
		{
			if (name == marker.shownName && level == marker.shownLevel && fish == marker.shownFish && color == marker.shownColor && distance == marker.shownDistance)
			{
				return;
			}
			marker.shownName = name;
			marker.shownLevel = level;
			marker.shownFish = fish;
			marker.shownColor = color;
			marker.shownDistance = distance;
			string text = (name ?? "");
			if (level > 1)
			{
				if (fish)
				{
					// The game shows a fish's level the same way when it lies on the ground
					text += "[" + level + "]";
				}
				else
				{
					if (text.Length > 0)
					{
						text += " ";
					}
					string star = stars.GetStar(color);
					for (int i = 1; i < level; i++)
					{
						text += star;
					}
				}
			}
			if (distance >= 0)
			{
				text = ((text.Length > 0) ? text + " (" + distance + "m)" : distance + "m");
			}
			marker.label.text = text;
		}

		/// <summary>
		/// Centers the label just behind the arrow's tail, kept inside the screen
		/// </summary>
		/// <param name="marker">The marker whose arrow has already been placed</param>
		/// <param name="pointing">The screen direction the arrow points in, as a unit vector</param>
		private static void PlaceLabel(Marker marker, Vector2 pointing)
		{
			RectTransform arrow = marker.arrow.rectTransform;
			RectTransform label = marker.label.rectTransform;
			// Sizes are in canvas units, positions in screen pixels
			float scale = arrow.lossyScale.x;
			Vector2 size = marker.label.GetPreferredValues();
			label.sizeDelta = size;
			Vector2 halfSize = size * scale * 0.5f;
			// How far the label's center sits from its edge facing the arrow, along the pointing direction
			float halfExtent = Mathf.Abs(pointing.x) * halfSize.x + Mathf.Abs(pointing.y) * halfSize.y;
			Vector2 tip = arrow.position;
			Vector2 center = tip - pointing * ((ArrowSize.y + LabelGap) * scale + halfExtent);
			center.x = Mathf.Clamp(center.x, halfSize.x, Mathf.Max(halfSize.x, Screen.width - halfSize.x));
			center.y = Mathf.Clamp(center.y, halfSize.y, Mathf.Max(halfSize.y, Screen.height - halfSize.y));
			label.position = new Vector3(center.x, center.y, 0f);
		}

		/// <summary>
		/// Returns the pooled marker at the index, creating it if needed, with its arrow active
		/// </summary>
		/// <param name="index">Pool index</param>
		/// <param name="enemyHud">The enemy HUD, for the label font</param>
		/// <returns>The marker</returns>
		private Marker GetMarker(int index, EnemyHud enemyHud)
		{
			if (index >= markers.Count)
			{
				Marker created = new Marker();
				created.arrow = CreateArrow();
				created.label = CreateLabel(enemyHud);
				markers.Add(created);
			}
			Marker marker = markers[index];
			SetActive(marker.arrow.gameObject, true);
			return(marker);
		}

		/// <summary>
		/// Sets a GameObject's active state when it differs
		/// </summary>
		/// <param name="go">The object</param>
		/// <param name="active">Whether it should be active</param>
		internal static void SetActive(GameObject go, bool active)
		{
			if (go.activeSelf != active)
			{
				go.SetActive(active);
			}
		}

		/// <summary>
		/// Creates a centered name label under the enemy HUD root, styled like the health bars' names
		/// </summary>
		/// <param name="enemyHud">The enemy HUD whose name text supplies the font</param>
		/// <returns>The label</returns>
		private TextMeshProUGUI CreateLabel(EnemyHud enemyHud)
		{
			GameObject go = new GameObject("CreatureMarkerLabel", typeof(RectTransform));
			go.transform.SetParent(root.transform, false);
			go.SetActive(false);
			TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
			TMP_Text template = GetNameTemplate(enemyHud);
			if (template != null)
			{
				label.font = template.font;
				label.fontSharedMaterial = template.fontSharedMaterial;
			}
			stars.Create(enemyHud, label.font, LabelFontSize);
			if (stars.Asset != null)
			{
				label.spriteAsset = stars.Asset;
			}
			label.fontSize = LabelFontSize;
			label.alignment = TextAlignmentOptions.Center;
			label.textWrappingMode = TextWrappingModes.NoWrap;
			label.raycastTarget = false;
			label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
			return(label);
		}

		/// <summary>
		/// The name text on the enemy HUD's health bar template, or the player HUD's health text when it can't be found
		/// </summary>
		/// <param name="enemyHud">The enemy HUD</param>
		/// <returns>The text to copy the font from, or null</returns>
		private static TMP_Text GetNameTemplate(EnemyHud enemyHud)
		{
			Transform name = enemyHud.m_baseHud.transform.Find("Name");
			if (name != null && name.TryGetComponent(out TMP_Text text))
			{
				return(text);
			}
			if (Hud.instance != null)
			{
				return(Hud.instance.m_healthText);
			}
			return(null);
		}

		/// <summary>
		/// Creates an arrow image under the enemy HUD root with its pivot on the tip
		/// </summary>
		/// <returns>The arrow's image</returns>
		private Image CreateArrow()
		{
			GameObject go = new GameObject("CreatureMarkerArrow", typeof(RectTransform));
			go.transform.SetParent(root.transform, false);
			Image image = go.AddComponent<Image>();
			image.sprite = GetSprite();
			image.raycastTarget = false;
			RectTransform rect = image.rectTransform;
			rect.pivot = new Vector2(0.5f, 0f);
			rect.sizeDelta = ArrowSize;
			return(image);
		}

		/// <summary>
		/// Draws a downward-pointing outlined arrow into a texture the first time it is needed
		/// </summary>
		/// <remarks>
		/// The fill is white so the image color tints it, while the dark outline stays dark under any tint
		/// </remarks>
		/// <returns>The arrow sprite</returns>
		private Sprite GetSprite()
		{
			if (sprite != null)
			{
				return(sprite);
			}
			const int width = 48;
			const int height = 64;
			const float outline = 3f;
			Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = FilterMode.Bilinear;
			Color[] pixels = new Color[width * height];
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					float px = Mathf.Abs(x + 0.5f - width * 0.5f);
					float py = y + 0.5f;
					Color color = Color.clear;
					if (InArrow(px, py, outline, width, height))
					{
						color = Color.white;
					}
					else if (InArrow(px, py, 0f, width, height))
					{
						color = OutlineColor;
					}
					pixels[y * width + x] = color;
				}
			}
			texture.SetPixels(pixels);
			texture.Apply();
			sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f));
			return(sprite);
		}

		/// <summary>
		/// Whether a point is inside the arrow shape shrunk by an inset
		/// </summary>
		/// <remarks>
		/// The arrow is a triangular head with its tip at y = 0 and a shaft above it
		/// </remarks>
		/// <param name="px">Horizontal distance from the arrow's center line</param>
		/// <param name="py">Height above the bottom of the texture</param>
		/// <param name="inset">How far to shrink the shape on every side</param>
		/// <param name="width">Texture width</param>
		/// <param name="height">Texture height</param>
		/// <returns>True when the point is inside</returns>
		private static bool InArrow(float px, float py, float inset, int width, int height)
		{
			float headHeight = height * 0.55f;
			float halfHead = width * 0.5f;
			float halfShaft = width * 0.2f;
			float slope = halfHead / headHeight;
			// Horizontal distance that matches a perpendicular inset on the slanted edges
			float slantInset = inset * Mathf.Sqrt(1f + slope * slope);
			bool inHead = (py <= headHeight - inset && px <= py * slope - slantInset);
			bool inShaft = (py >= headHeight * 0.5f && py <= height - inset && px <= halfShaft - inset);
			return(inHead || inShaft);
		}
	}
}
