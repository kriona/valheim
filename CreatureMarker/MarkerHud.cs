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
		private const float BobHeight = 6f;
		private const float BobSpeed = 5f;
		private const float LabelGap = 2f;
		private const float LabelFontSize = 16f;
		private static readonly Vector2 ArrowSize = new Vector2(24f, 32f);
		internal static readonly Color OutlineColor = new Color(0.1f, 0.1f, 0.1f, 1f);

		/// <summary>
		/// One creature's arrow and name label
		/// </summary>
		/// <remarks>
		/// The label is a sibling of the arrow so it stays upright when the arrow rotates at the screen edge
		/// </remarks>
		private class Marker
		{
			public Image arrow;
			public TextMeshProUGUI label;
			public Character character;

			/// <summary>
			/// The creature's localized name, looked up when the marker is given a new creature
			/// </summary>
			public string name;

			/// <summary>
			/// The name and distance in the label's text, so the text is only rebuilt when one of them changes
			/// </summary>
			/// <remarks>
			/// The name is null when names are hidden and the distance is -1 when distances are hidden, so -2 means
			/// the text hasn't been set
			/// </remarks>
			public string shownName;
			public int shownDistance = -2;
		}

		private readonly List<Marker> markers = new List<Marker>();
		private GameObject root;
		private Sprite sprite;

		/// <summary>
		/// Shows an arrow for each marked creature when Show On Screen is on and hides the spare ones
		/// </summary>
		/// <param name="marked">The living marked creatures within range</param>
		/// <param name="player">The local player</param>
		public void Update(List<Character> marked, Player player)
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
				bool showLabel = (showName || showDistance);
				Vector3 playerPosition = player.transform.position;
				foreach (Character character in marked)
				{
					Marker marker = GetMarker(used, enemyHud);
					used++;
					Color color = CreatureConfig.GetColor(character, player);
					marker.arrow.color = color;
					Vector2 pointing = Place(marker.arrow.rectTransform, camera, character.GetTopPoint() + Vector3.up * HeadOffset);
					SetActive(marker.label.gameObject, showLabel);
					if (showLabel)
					{
						if (marker.character != character)
						{
							marker.character = character;
							marker.name = Localization.instance.Localize(character.GetHoverName());
						}
						int distance = (showDistance ? Mathf.RoundToInt(Vector3.Distance(character.transform.position, playerPosition)) : -1);
						SetLabelText(marker, (showName ? marker.name : null), distance);
						marker.label.color = color;
						PlaceLabel(marker, pointing);
					}
				}
			}
			for (int i = used; i < markers.Count; i++)
			{
				markers[i].character = null;
				markers[i].shownName = null;
				markers[i].shownDistance = -2;
				SetActive(markers[i].arrow.gameObject, false);
				SetActive(markers[i].label.gameObject, false);
			}
		}

		/// <summary>
		/// Removes the markers and the generated sprite
		/// </summary>
		public void Destroy()
		{
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
		/// <returns>The screen direction the arrow points in, as a unit vector</returns>
		private static Vector2 Place(RectTransform arrow, Camera camera, Vector3 target)
		{
			Vector3 screen = camera.WorldToScreenPointScaled(target);
			bool onScreen = (screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height);
			if (onScreen)
			{
				float bob = Mathf.Abs(Mathf.Sin(Time.time * BobSpeed)) * BobHeight;
				arrow.position = new Vector3(screen.x, screen.y + bob, 0f);
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
		/// Sets the label to "Name (5m)", "Name" or "5m", rebuilding the text only when the name or distance has changed
		/// </summary>
		/// <param name="marker">The marker whose label to set</param>
		/// <param name="name">The name to show, or null to leave it out</param>
		/// <param name="distance">The distance in meters to show, or -1 to leave it out</param>
		private static void SetLabelText(Marker marker, string name, int distance)
		{
			if (name == marker.shownName && distance == marker.shownDistance)
			{
				return;
			}
			marker.shownName = name;
			marker.shownDistance = distance;
			if (distance < 0)
			{
				marker.label.text = (name ?? "");
			}
			else if (name == null)
			{
				marker.label.text = distance + "m";
			}
			else
			{
				marker.label.text = name + " (" + distance + "m)";
			}
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
